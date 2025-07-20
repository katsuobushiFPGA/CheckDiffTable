-- 追加の初期化設定（オプション）

-- パフォーマンス用の追加設定
ALTER SYSTEM SET shared_buffers = '256MB';
ALTER SYSTEM SET effective_cache_size = '1GB';
ALTER SYSTEM SET maintenance_work_mem = '64MB';
ALTER SYSTEM SET checkpoint_completion_target = 0.9;
ALTER SYSTEM SET wal_buffers = '16MB';
ALTER SYSTEM SET default_statistics_target = 100;

-- 統計情報の有効化
CREATE EXTENSION IF NOT EXISTS pg_stat_statements;

-- 日本語ロケール設定（必要に応じて）
-- CREATE COLLATION IF NOT EXISTS japanese (provider = icu, locale = 'ja-JP');

-- 開発用のデバッグ設定
SET log_statement = 'all';
SET log_duration = on;
SET log_min_duration_statement = 1000; -- 1秒以上のクエリをログ出力

-- 追加のユーザー作成（必要に応じて）
-- CREATE USER checkdiff_app WITH PASSWORD 'app_password';
-- GRANT ALL PRIVILEGES ON DATABASE checkdiff_db TO checkdiff_app;
-- GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO checkdiff_app;
-- GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO checkdiff_app;
