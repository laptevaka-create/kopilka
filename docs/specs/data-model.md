# Спецификация модели данных

Нормативный документ: описывает хранилище «Копилки» так, как оно
реализовано в коде. При расхождении приоритет: `readme.md` → этот документ
→ код.

- Хранилище: SQLite, файл `%LOCALAPPDATA%\Kopilka\kopilka.db`
  (демо-режим — `kopilka.demo.db`).
- Доступ: `Microsoft.Data.Sqlite`, без Entity Framework; SQL-запросы —
  в `Kopilka.Core.Data`, параметры всегда именованные (`@имя`).
- Режим: `PRAGMA journal_mode = WAL` (целостность при сбое питания),
  `PRAGMA foreign_keys = ON` (проверка ссылок включается на каждом
  соединении).
- Соединение: одно на всё время работы приложения (`SqliteDatabase`).

## 1. Суммы, даты и месяцы

| Понятие | Хранение | Причина |
| --- | --- | --- |
| Сумма | целые копейки, `INTEGER` (C# — `long`) | нет ошибок плавающей точки при суммировании; рубли считаются только на выводе |
| Дата | текст `ГГГГ-ММ-ДД` (C# — `DateOnly`) | сортируется как текст, читается глазами, не зависит от форматов дат |
| Месяц бюджета | текст `ГГГГ-ММ` | как дата, но без дня; уникален вместе с категорией |
| Булевы признаки | `0 / 1` с `CHECK` | SQLite не имеет отдельного булева типа |

## 2. Таблицы

### accounts — справочник счетов

| Поле | Тип | Ограничения |
| --- | --- | --- |
| id | INTEGER | PRIMARY KEY |
| name | TEXT | NOT NULL UNIQUE |
| sort_order | INTEGER | NOT NULL DEFAULT 0 |

Счёт по умолчанию для новых транзакций — **не** поле таблицы, а настройка
`settings.default_account_id`: один факт хранится в одном месте.

Предустановлено (миграция V001): «Наличные» (id 1), «Карта» (id 2).

### categories — справочник категорий

| Поле | Тип | Ограничения |
| --- | --- | --- |
| id | INTEGER | PRIMARY KEY |
| name | TEXT | NOT NULL |
| kind | TEXT | NOT NULL CHECK (IN ('income', 'expense')) |
| has_limit | INTEGER | NOT NULL DEFAULT 0 CHECK (IN (0, 1)) |
| sort_order | INTEGER | NOT NULL DEFAULT 0 |

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

`UNIQUE (category_id, month)`: бюджет на категорию в месяце один.

### recurring — шаблоны регулярных платежей

| Поле | Тип | Ограничения |
| --- | --- | --- |
| id | INTEGER | PRIMARY KEY |
| category_id | INTEGER | NOT NULL REFERENCES categories(id) |
| account_id | INTEGER | NOT NULL REFERENCES accounts(id) |
| amount_kopecks | INTEGER | NOT NULL CHECK (> 0) |
| day_of_month | INTEGER | NOT NULL CHECK (BETWEEN 1 AND 31) |
| comment | TEXT | NULL разрешён |
| is_active | INTEGER | NOT NULL DEFAULT 1 CHECK (IN (0, 1)) |
| last_applied | TEXT | дата последнего начисления, ГГГГ-ММ-ДД |

`last_applied` — защита от повторных начислений (ФТ-6.3): движок
генерирует даты только после неё.

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

- Версия — `PRAGMA user_version`, хранится в самом файле базы.
- Миграции — SQL-файлы `Data/Migrations/V###__имя.sql`, встроенные
  в сборку Core как ресурсы (`EmbeddedResource`).
- `DatabaseMigrator` применяет миграции с номером больше текущей
  версии, по порядку; каждая — в отдельной транзакции: сбой откатывает
  миграцию, но не файл базы.
- Повторный запуск ничего не делает (идемпотентность) — проверено тестом.

## 5. Отображение в коде

| Таблица | Запись домена | Репозиторий |
| --- | --- | --- |
| accounts | `Domain.Account` | `SqliteAccountRepository` : `IAccountRepository` |
| categories | `Domain.Category` | `SqliteCategoryRepository` : `ICategoryRepository` |
| transactions | `Domain.Transaction` | `SqliteTransactionRepository` : `ITransactionRepository` |
| budgets | `Domain.Budget` | шаг 4 |
| recurring | `Domain.RecurringPayment` | шаг 6 |
| settings | `Domain.Setting` | шаг 7 |

Удаление используемых справочников запрещено на уровне репозитория
(`DeleteResult.InUse`, ФТ-1.3): история транзакций неприкосновенна.
