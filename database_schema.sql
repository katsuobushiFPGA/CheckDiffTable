-- テーブル差分チェック・更新システム用データベーススキーマ

-- データベース作成（必要に応じて）
-- CREATE DATABASE checkdiff_db;

-- トランザクションテーブル（大量のトランザクションデータが入ってくる、毎回トランケートされる）
CREATE TABLE IF NOT EXISTS transaction_table (
    id SERIAL PRIMARY KEY,
    entity_id INTEGER NOT NULL,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    status VARCHAR(20) NOT NULL,
    amount DECIMAL(10,2),
    transaction_type VARCHAR(20) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 最新データテーブル（各トランザクションに対応する最新状態を保持）
-- 複合主キー（id, entity_id）を使用してトランザクションとの関係性を明確化
CREATE TABLE IF NOT EXISTS latest_data_table (
    id INTEGER NOT NULL,
    entity_id INTEGER NOT NULL,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    status VARCHAR(20) NOT NULL,
    amount DECIMAL(10,2),
    transaction_type VARCHAR(20) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id, entity_id)
);

-- インデックス作成（効率化のため）
CREATE INDEX IF NOT EXISTS idx_transaction_entity_id_created_at ON transaction_table(entity_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_latest_data_composite_key ON latest_data_table(id, entity_id);
CREATE INDEX IF NOT EXISTS idx_latest_data_entity_id ON latest_data_table(entity_id);

-- サンプルデータの挿入（テストケース別）

-- ===== テストケース1: 新規データのみ（latest_data_tableが空の状態） =====
-- transaction_tableに新しいデータを挿入
INSERT INTO transaction_table (id, entity_id, name, description, status, amount, transaction_type, created_at, updated_at) VALUES
(1, 1, '商品A', '商品Aの説明', 'active', 1000.00, 'sale', '2025-01-01 10:00:00', '2025-01-01 10:00:00'),
(2, 2, '商品B', '商品Bの説明', 'active', 2000.00, 'sale', '2025-01-01 12:00:00', '2025-01-01 12:00:00'),
(3, 3, '商品C', '商品Cの説明', 'inactive', 500.00, 'purchase', '2025-01-01 13:00:00', '2025-01-01 13:00:00'),
(4, 4, '商品D', '商品Dの説明', 'active', 3000.00, 'sale', '2025-01-02 16:00:00', '2025-01-02 16:00:00');

-- この時点でプログラム実行 → 全て新規登録される

-- ===== テストケース2: 差分ありデータの更新 =====
-- transaction_tableをクリアして新しいトランザクションを挿入
TRUNCATE TABLE transaction_table;

INSERT INTO transaction_table (id, entity_id, name, description, status, amount, transaction_type, created_at, updated_at) VALUES
(1, 1, '商品A更新版', '商品Aの更新された説明', 'active', 1500.00, 'sale', '2025-01-02 11:00:00', '2025-01-02 11:00:00'),
(2, 2, '商品B更新版', '商品Bの更新説明', 'inactive', 2500.00, 'sale', '2025-01-02 13:00:00', '2025-01-02 13:00:00'),
(5, 5, '商品E', '新しい商品Eの説明', 'active', 4000.00, 'sale', '2025-01-02 14:00:00', '2025-01-02 14:00:00');

-- この時点でプログラム実行 → id=1,2は更新、id=5は新規登録、id=3,4のトランザクションは削除される

-- ===== テストケース3: 差分なしデータ（スキップ＆削除） =====
-- transaction_tableをクリアして同じデータを挿入
TRUNCATE TABLE transaction_table;

INSERT INTO transaction_table (id, entity_id, name, description, status, amount, transaction_type, created_at, updated_at) VALUES
(1, 1, '商品A更新版', '商品Aの更新された説明', 'active', 1500.00, 'sale', '2025-01-03 10:00:00', '2025-01-03 10:00:00'),  -- 差分なし
(2, 2, '商品B最終版', '商品Bの最終説明', 'active', 2800.00, 'return', '2025-01-03 11:00:00', '2025-01-03 11:00:00'),  -- 差分あり
(6, 6, '商品F', '新商品Fの説明', 'active', 5000.00, 'sale', '2025-01-03 12:00:00', '2025-01-03 12:00:00');             -- 新規

-- この時点でプログラム実行 → id=1はスキップ（削除）、id=2は更新、id=6は新規登録、id=5のトランザクションは削除される

-- ===== テストケース4: 大量データテスト =====
-- transaction_tableをクリアして大量データを挿入
TRUNCATE TABLE transaction_table;

INSERT INTO transaction_table (id, entity_id, name, description, status, amount, transaction_type, created_at, updated_at) 
SELECT 
    s as id,
    s as entity_id,
    'テスト商品' || s as name,
    'テスト説明' || s as description,
    CASE WHEN s % 2 = 0 THEN 'active' ELSE 'inactive' END as status,
    (s * 100.0) as amount,
    CASE WHEN s % 3 = 0 THEN 'sale' ELSE 'purchase' END as transaction_type,
    CURRENT_TIMESTAMP as created_at,
    CURRENT_TIMESTAMP as updated_at
FROM generate_series(1, 1000) as s;

-- この時点でプログラム実行 → バッチ処理のパフォーマンステスト

-- 既存データの確認用
-- SELECT * FROM transaction_table ORDER BY entity_id, created_at;
-- SELECT * FROM latest_data_table ORDER BY entity_id;

-- 最新トランザクション取得用クエリ例
-- SELECT entity_id, MAX(created_at) as latest_created_at 
-- FROM transaction_table 
-- GROUP BY entity_id;
