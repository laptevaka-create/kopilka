# Спецификация модели данных

Нормативный документ: описывает хранилище «Копилки» так, как оно
реализовано в коде. При расхождении приоритет: `readme.md` → этот документ
→ код.

- Хранилище: SQLite, файл `%LOCALAPPDATA%\Kopilka\kopilka.db`
  (демо-режим — `kopilka.demo.db`).
- Доступ: Entity Framework Core (`Microsoft.EntityFrameworkCore.Sqlite`),
  контекст `KopilkaDbContext` (EF-модель — в `Kopilka.Core/Data`); запросы —
  LINQ, ручной SQL не используется.
- Режим: `PRAGMA journal_mode = WAL` — целостность при сбое питания
  (включается один раз при старте, режим хранится в самом файле базы);
  внешние ключи EF включает на каждом соединении.
- Контексты: фабрика `IDbContextFactory<KopilkaDbContext>`; репозиторий
  создаёт контекст на операцию — короткие `using`, никаких
  долгоживущих соединений.

## 1. Суммы, даты и месяцы

| Понятие | Хранение | Причина |
| --- | --- | --- |
| Сумма | целые копейки, `INTEGER` (C# — `long`) | нет ошибок плавающей точки при суммировании; рубли считаются только на выводе |
| Дата | текст `ГГГГ-ММ-ДД` (C# — `DateOnly`) | сортируется как текст, читается глазами, не зависит от форматов дат |
| Месяц бюджета | текст `ГГГГ-ММ` | как дата, но без дня; уникален вместе с категорией |
| Булевы признаки | `0 / 1`, в модели — `bool` | SQLite не имеет отдельного булева типа; допустимость значений гарантирует тип на уровне модели, `CHECK` не ставится |

## 2. Таблицы

### accounts — справочник счетов

| Поле | Тип | Ограничения |
| --- | --- | --- |
| id | INTEGER | PRIMARY KEY |
| name | TEXT | NOT NULL; уникальность — уникальный индекс `ix_accounts_name` |
| sort_order | INTEGER | NOT NULL |

Счёт по умолчанию для новых транзакций — **не** поле таблицы, а настройка
`settings.default_account_id`: один факт хранится в одном месте.

Предустановлено (миграция `InitialCreate`): «Наличные» (id 1), «Карта» (id 2).

### categories — справочник категорий

| Поле | Тип | Ограничения |
| --- | --- | --- |
| id | INTEGER | PRIMARY KEY |
| name | TEXT | NOT NULL |
| kind | TEXT | NOT NULL CHECK (IN ('income', 'expense')) |
| has_limit | INTEGER | NOT NULL; 0/1 гарантирует тип `bool` |
| sort_order | INTEGER | NOT NULL |

`UNIQUE INDEX (kind, name)`: имя уникально внутри типа — «Прочие» может
быть и доходом, и расходом.

Предустановлено: «Зарплата», «Прочие доходы» (доходы); «Продукты»,
«Транспорт», «Развлечения», «Прочие расходы» (расходы).

### transactions — журнал

| Поле | Тип | Ограничения |
| --- | --- | --- |
| id | INTEGER | PRIMARY KEY |
| account_id | INTEGER | NOT NULL REFERENCES accounts(id) |
| category_id | INTEGER | NOT NULL REFERENCES categories(id) |
| amount_kopecks | INTEGER | NOT NULL CHECK (> 0) |
| date | TEXT | NOT NULL, формат ГГГГ-ММ-ДД |
| comment | TEXT | NULL разрешён |

Сумма всегда положительная: направление задаёт тип категории, знак
не используется. Индексы: `(date)`, `(category_id, date)`,
`(account_id, date)` — под журнал месяца, фильтр по категории и
сальдо счёта.

### budgets — месячные бюджеты

| Поле | Тип | Ограничения |
| --- | --- | --- |
| id | INTEGER | PRIMARY KEY |
| category_id | INTEGER | NOT NULL REFERENCES categories(id) |
| month | TEXT | NOT NULL, формат ГГГГ-ММ |
| limit_kopecks | INTEGER | NOT NULL CHECK (> 0) |

Уникальность пары — уникальный индекс `ix_budgets_category_month`:
бюджет на категорию в месяце один.

### recurring — шаблоны регулярных платежей

| Поле | Тип | Ограничения |
| --- | --- | --- |
| id | INTEGER | PRIMARY KEY |
| category_id | INTEGER | NOT NULL REFERENCES categories(id) |
| account_id | INTEGER | NOT NULL REFERENCES accounts(id) |
| amount_kopecks | INTEGER | NOT NULL CHECK (> 0) |
| day_of_month | INTEGER | NOT NULL CHECK (BETWEEN 1 AND 31) |
| comment | TEXT | NULL разрешён |
| is_active | INTEGER | NOT NULL; 0/1 гарантирует тип `bool` |
| last_applied | TEXT | дата последнего начисления, ГГГГ-ММ-ДД |

`last_applied` — защита от повторных начислений (ФТ-6.3): движок
генерирует даты только после неё.

Индексы по внешним ключам `IX_recurring_account_id` и
`IX_recurring_category_id` создаёт EF по конвенции провайдера: по ним
база проверяет `Restrict` при удалении счёта или категории.

### settings — настройки

| Поле | Тип | Ограничения |
| --- | --- | --- |
| key | TEXT | PRIMARY KEY |
| value | TEXT | NOT NULL |

Ключи: `default_account_id` и параметры будущих шагов.

## 3. Нормализация

- **1НФ**: все поля атомарны — списков и разделённых строк нет.
- **2НФ/3НФ**: имена счетов и категорий встречаются только в справочниках;
  транзакция ссылается на них по `id`. Переименование категории меняет
  одну строку справочника, история не трогается.
- Суммы денормализуются сознательно только в агрегатах вычислений
  (SUM), но не хранятся.

## 4. Версионирование схемы

- Версионирование — миграциями EF Core; журнал применённых миграций —
  таблица `__EFMigrationsHistory` в самой базе данных.
- Миграции создаются инструментом `dotnet ef` (файлы
  `Data/Migrations/*.cs` в сборке Core), модель — снимок
  `KopilkaDbContextModelSnapshot`.
- Применение — `context.Database.Migrate()` при старте приложения:
  создаются недостающие таблицы, накатываются неприменённые миграции,
  сид справочников входит в миграцию.
- Повторный запуск ничего не делает (идемпотентность) — проверено
  тестом.

## 5. Отображение в коде

| Таблица | Сущность домена | Репозиторий |
| --- | --- | --- |
| accounts | `Domain.Account` | `EfAccountRepository` : `IAccountRepository` |
| categories | `Domain.Category` | `EfCategoryRepository` : `ICategoryRepository` |
| transactions | `Domain.Transaction` | `EfTransactionRepository` : `ITransactionRepository` |
| budgets | `Domain.Budget` | шаг 4 |
| recurring | `Domain.RecurringPayment` | шаг 6 |
| settings | `Domain.Setting` | шаг 7 |

Конфигурация схемы (имена таблиц и столбцов, CHECK, индексы, внешние
ключи `Restrict`, сид через `HasData`) — в
`Data/Configurations/*.cs`; тип категории хранится текстом
`income/expense` через конвертер значений EF.

Удаление используемых справочников запрещено на уровне репозитория
(`DeleteResult.InUse`, ФТ-1.3): история транзакций неприкосновенна.
