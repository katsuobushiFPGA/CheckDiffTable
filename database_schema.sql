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

-- サンプルデータの挿入（大量トランザクションのシミュレーション）
INSERT INTO transaction_table (entity_id, name, description, status, amount, transaction_type, created_at, updated_at) VALUES
-- Entity 1のトランザクション
(1, '商品A', '商品Aの説明', 'active', 1000.00, 'sale', '2025-01-01 10:00:00', '2025-01-01 10:00:00'),
(1, '商品A更新', '商品Aの更新された説明', 'active', 1500.00, 'sale', '2025-01-02 11:00:00', '2025-01-02 11:00:00'),
(1, '商品A再更新', '商品Aの再更新された説明', 'active', 1200.00, 'return', '2025-01-03 09:00:00', '2025-01-03 09:00:00'),
-- Entity 2のトランザクション
(2, '商品B', '商品Bの説明', 'active', 2000.00, 'sale', '2025-01-01 12:00:00', '2025-01-01 12:00:00'),
(2, '商品B更新', '商品Bの更新説明', 'inactive', 2500.00, 'sale', '2025-01-02 13:00:00', '2025-01-02 13:00:00'),
-- Entity 3のトランザクション
(3, '商品C', '商品Cの説明', 'inactive', 500.00, 'purchase', '2025-01-01 13:00:00', '2025-01-01 13:00:00'),
(3, '商品C更新', '商品Cの更新された説明', 'active', 800.00, 'sale', '2025-01-03 14:00:00', '2025-01-03 14:00:00'),
(3, '商品C最新', '商品Cの最新説明', 'active', 750.00, 'sale', '2025-01-04 15:00:00', '2025-01-04 15:00:00'),
-- Entity 4のトランザクション（新しいエンティティ）
(4, '商品D', '商品Dの説明', 'active', 3000.00, 'sale', '2025-01-02 16:00:00', '2025-01-02 16:00:00'),
(4, '商品D更新', '商品Dの更新説明', 'active', 3200.00, 'sale', '2025-01-03 17:00:00', '2025-01-03 17:00:00');

-- 既存データの確認用
-- SELECT * FROM transaction_table ORDER BY entity_id, created_at;
-- SELECT * FROM latest_data_table ORDER BY entity_id;

-- 最新トランザクション取得用クエリ例
-- SELECT entity_id, MAX(created_at) as latest_created_at 
-- FROM transaction_table 
-- GROUP BY entity_id;
