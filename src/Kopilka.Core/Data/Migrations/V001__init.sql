-- V001: начальная схема и предустановленные справочники.

-- Справочник счетов: «Наличные» и «Карта» создаются сразу,
-- чтобы первый запуск открывался с рабочими справочниками.
CREATE TABLE accounts (
    id          INTEGER PRIMARY KEY,
    name        TEXT NOT NULL UNIQUE,
    sort_order  INTEGER NOT NULL DEFAULT 0
);

-- Справочник категорий. kind: income | expense.
-- has_limit — по категории расхода задаётся месячный бюджет.
CREATE TABLE categories (
    id          INTEGER PRIMARY KEY,
    name        TEXT NOT NULL,
    kind        TEXT NOT NULL CHECK (kind IN ('income', 'expense')),
    has_limit   INTEGER NOT NULL DEFAULT 0 CHECK (has_limit IN (0, 1)),
    sort_order  INTEGER NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX ix_categories_name_kind ON categories (kind, name);

-- Транзакции. Сумма всегда положительная в копейках:
-- направление задаётся типом категории, а не знаком числа.
CREATE TABLE transactions (
    id              INTEGER PRIMARY KEY,
    account_id      INTEGER NOT NULL REFERENCES accounts (id),
    category_id     INTEGER NOT NULL REFERENCES categories (id),
    amount_kopecks  INTEGER NOT NULL CHECK (amount_kopecks > 0),
    date            TEXT NOT NULL,
    comment         TEXT
);
CREATE INDEX ix_transactions_date          ON transactions (date);
CREATE INDEX ix_transactions_category_date ON transactions (category_id, date);
CREATE INDEX ix_transactions_account_date  ON transactions (account_id, date);

-- Месячные бюджеты: одна строка на пару (категория, месяц).
CREATE TABLE budgets (
    id              INTEGER PRIMARY KEY,
    category_id     INTEGER NOT NULL REFERENCES categories (id),
    month           TEXT NOT NULL,
    limit_kopecks   INTEGER NOT NULL CHECK (limit_kopecks > 0),
    UNIQUE (category_id, month)
);

-- Шаблоны регулярных платежей.
CREATE TABLE recurring (
    id              INTEGER PRIMARY KEY,
    category_id     INTEGER NOT NULL REFERENCES categories (id),
    account_id      INTEGER NOT NULL REFERENCES accounts (id),
    amount_kopecks  INTEGER NOT NULL CHECK (amount_kopecks > 0),
    day_of_month    INTEGER NOT NULL CHECK (day_of_month BETWEEN 1 AND 31),
    comment         TEXT,
    is_active       INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1)),
    last_applied    TEXT
);
CREATE INDEX ix_recurring_active ON recurring (is_active);

-- Настройки вида «ключ—значение».
CREATE TABLE settings (
    key     TEXT PRIMARY KEY,
    value   TEXT NOT NULL
);

-- Предустановленные справочники.
INSERT INTO accounts (id, name, sort_order) VALUES
    (1, 'Наличные', 0),
    (2, 'Карта', 1);

INSERT INTO categories (id, name, kind, has_limit, sort_order) VALUES
    (1, 'Зарплата',       'income',  0, 0),
    (2, 'Прочие доходы',  'income',  0, 1),
    (3, 'Продукты',       'expense', 0, 0),
    (4, 'Транспорт',      'expense', 0, 1),
    (5, 'Развлечения',    'expense', 0, 2),
    (6, 'Прочие расходы', 'expense', 0, 3);
