# PostgreSQL 16.8 Docker環境

## 概要
このDockerコンテナ環境は、CheckDiffTableプロジェクト用のPostgreSQL 16.8データベースを提供します。

## 構成
- **PostgreSQL 16.8**: メインデータベース
- **pgAdmin 4**: データベース管理UI（オプション）

## セットアップ

### 1. Docker環境の起動
```bash
cd Docker
docker-compose up -d
```

### 2. 接続確認
```bash
# PostgreSQLコンテナの状態確認
docker-compose ps

# ヘルスチェック確認
docker-compose logs postgres
```

### 3. データベース接続情報
- **ホスト**: localhost
- **ポート**: 5432
- **データベース名**: checkdiff_db
- **ユーザー名**: postgres
- **パスワード**: password

### 4. pgAdmin接続（オプション）
- **URL**: http://localhost:8080
- **ユーザー名**: admin@checkdiff.local
- **パスワード**: admin

## 使用方法

### データベースの起動
```bash
cd Docker
docker-compose up -d postgres
```

### pgAdminも含めて起動
```bash
cd Docker
docker-compose up -d
```

### 停止
```bash
cd Docker
docker-compose down
```

### データ永続化の削除（注意: 全データが削除されます）
```bash
cd Docker
docker-compose down -v
```

### ログ確認
```bash
cd Docker
docker-compose logs -f postgres
```

## appsettings.json設定例
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=checkdiff_db;Username=postgres;Password=password"
  }
}
```

## データベース初期化
コンテナ起動時に以下が自動実行されます：
1. `database_schema.sql` - テーブル作成とサンプルデータ投入
2. `init-scripts/` ディレクトリ内の追加スクリプト（オプション）

## トラブルシューティング

### ポート5432が既に使用されている場合
```bash
# 既存のPostgreSQLサービスを停止
sudo systemctl stop postgresql

# または docker-compose.yml で別のポートを使用
ports:
  - "5433:5432"  # ホスト側ポートを5433に変更
```

### データベースリセット
```bash
cd Docker
docker-compose down -v
docker-compose up -d
```

### パフォーマンスチューニング（オプション）
大量データ処理用の設定を追加する場合：
```yaml
environment:
  POSTGRES_INITDB_ARGS: "--encoding=UTF-8 --locale=C"
  # パフォーマンス設定
  POSTGRES_SHARED_PRELOAD_LIBRARIES: "pg_stat_statements"
command: |
  postgres
  -c shared_buffers=256MB
  -c effective_cache_size=1GB
  -c maintenance_work_mem=64MB
  -c checkpoint_completion_target=0.9
  -c wal_buffers=16MB
  -c default_statistics_target=100
```

## セキュリティ注意事項
- 本設定は開発環境用です
- 本番環境では適切なパスワードと設定を使用してください
- ファイアウォール設定を適切に行ってください
