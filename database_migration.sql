-- データベース移行スクリプト: 単一主キーから複合主キーへの変更
-- 実行前に必ずデータのバックアップを取ってください

-- 1. 既存のlatest_data_tableをバックアップ
CREATE TABLE IF NOT EXISTS latest_data_table_backup AS 
SELECT * FROM latest_data_table;

-- 2. 既存テーブルを削除（外部キー制約がある場合は事前に削除）
DROP TABLE IF EXISTS latest_data_table;

-- 3. 新しい複合主キー構造でテーブルを再作成
CREATE TABLE latest_data_table (
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

-- 4. インデックス作成
CREATE INDEX idx_latest_data_composite_key ON latest_data_table(id, entity_id);
CREATE INDEX idx_latest_data_entity_id ON latest_data_table(entity_id);

-- 5. データ移行: 各entity_idに対応するtransaction_tableの最新idを取得して移行
INSERT INTO latest_data_table (id, entity_id, name, description, status, amount, transaction_type, created_at, updated_at)
SELECT 
    t.id,
    b.entity_id,
    b.name,
    b.description,
    b.status,
    b.amount,
    b.transaction_type,
    b.created_at,
    b.updated_at
FROM latest_data_table_backup b
JOIN (
    -- 各entity_idの最新transaction_idを取得
    SELECT 
        entity_id,
        MAX(id) as id
    FROM transaction_table
    GROUP BY entity_id
) t ON b.entity_id = t.entity_id;

-- 6. データ確認用クエリ
-- SELECT 'Migration completed. Verifying data...' as status;
-- SELECT COUNT(*) as backup_count FROM latest_data_table_backup;
-- SELECT COUNT(*) as new_count FROM latest_data_table;
-- SELECT * FROM latest_data_table ORDER BY entity_id, id;

-- 7. バックアップテーブル削除（データ確認後に実行）
-- DROP TABLE latest_data_table_backup;
