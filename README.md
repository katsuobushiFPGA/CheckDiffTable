# トランザクション差分チェック・更新システム

## 概要

このシステムは、大量のトランザクションデータが継続的に流入するトランザクションテーブルから最新データのみを保持する専用テーブルに対して、効率的な一括処理で差分チェックを行い必要に応じて更新を行うC#アプリケーションです。

## プロジェクト構成

```
CheckDiffTable/
├── CheckDiffTable.csproj          # プロジェクトファイル
├── Program.cs                     # メインエントリーポイント
├── appsettings.json              # 設定ファイル
├── Models/
│   └── Entities.cs               # エンティティクラス
├── Repositories/
│   ├── IRepositories.cs          # リポジトリインターフェース
│   ├── TransactionRepository.cs  # トランザクションテーブルリポジトリ
│   └── LatestDataRepository.cs   # 最新データテーブルリポジトリ
└── Services/
    └── DiffCheckService.cs       # 差分チェック・更新サービス
```

## 主な機能

### 1. 複合主キー設計
- **トランザクション関係性の明確化**: (id, entity_id) 複合主キーによる正確なデータ対応
- **データ整合性の向上**: 単一entity_idに対する複数トランザクションの追跡が可能
- **PostgreSQL ROW構文**: 複合主キーでの効率的なバッチ検索を実装

### 2. 効率的な一括処理
- **最新トランザクション一括取得**: WITH句を使用したエンティティごとの最新データ取得
- **関連データ一括取得**: ANY演算子を使用した効率的なデータ取得
- **一括UPSERT**: PostgreSQLのON CONFLICT構文を使用した高速更新
- **トランケート前提設計**: 毎回トランケートされるトランザクションテーブルに最適化

### 2. 差分チェック機能
- トランザクションテーブルの最新データと最新データテーブルの既存データを比較
- フィールド単位での差分判定（name, description, status, amount, transaction_type）

### 3. データ更新ロジック
- **新規登録**: 最新データテーブルにデータが存在しない場合
- **更新**: データが存在し、差分がある場合
- **スキップ**: データが存在し、差分がない場合

### 4. 処理モード
- **一括処理モード（推奨）**: 最高効率での全件処理
- **個別処理モード**: デバッグ・特定エンティティ処理用

### 5. 監視・ログ機能
- 処理時間の測定とログ出力
- 処理件数のサマリー表示
- 詳細なエラーハンドリングとログ出力

## 使用技術

- **C# .NET 8.0**
- **Npgsql**: PostgreSQL用データアクセスライブラリ
- **Microsoft.Extensions**: DI、Configuration、Logging

## データベース設定

### 前提条件
PostgreSQLサーバーが稼働していること

### セットアップ手順

1. **データベース作成**
   ```sql
   CREATE DATABASE checkdiff_db;
   ```

2. **スキーマ適用**
   ```bash
   psql -U postgres -d checkdiff_db -f database_schema.sql
   ```

3. **接続文字列設定**
   `appsettings.json`の接続文字列を環境に合わせて修正
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Database=checkdiff_db;Username=postgres;Password=your_password"
     }
   }
   ```

## 実行方法

### 一括処理モード
```bash
dotnet run              # appsettings.jsonで設定されたバッチサイズで実行
```

### バッチサイズ設定
appsettings.jsonで設定可能：
```json
{
  "BatchProcessing": {
    "BatchSize": 1000      // デフォルトバッチサイズ
  }
}
```

## 実行例

### 初回実行時（一括処理）
```
=== トランザクション差分チェック・更新システム開始 ===
一括処理モード（デフォルト）
Found 4 entities with transactions
Bulk upserted 4 records (4 inserts, 0 updates)

=== 一括処理結果サマリー ===
処理成功: True
総エンティティ数: 4
新規登録: 4件
更新: 0件
スキップ: 0件
エラー: 0件
処理時間: 89ms
メッセージ: 一括処理完了: 4エンティティ処理 (新規:4, 更新:0, スキップ:0, エラー:0) 処理時間:89ms
```

### 2回目実行時（同じデータ - 差分なし）
```
=== トランザクション差分チェック・更新システム開始 ===
一括処理モード（デフォルト）
Found 4 entities with transactions

=== 一括処理結果サマリー ===
処理成功: True
総エンティティ数: 4
新規登録: 0件
更新: 0件
スキップ: 4件
エラー: 0件
処理時間: 45ms
メッセージ: 一括処理完了: 4エンティティ処理 (新規:0, 更新:0, スキップ:4, エラー:0) 処理時間:45ms
```

### 新しいトランザクションデータ投入後
```sql
-- トランザクションテーブルをトランケートして新しいデータを投入
TRUNCATE TABLE transaction_table RESTART IDENTITY;
INSERT INTO transaction_table (entity_id, name, description, status, amount, transaction_type) 
VALUES (1, '商品A最新版', '商品Aの最新説明', 'active', 1800.00, 'sale');
```

```
=== 一括処理結果サマリー ===
処理成功: True
総エンティティ数: 1
新規登録: 0件
更新: 1件
スキップ: 0件
エラー: 0件
処理時間: 32ms
メッセージ: 一括処理完了: 1エンティティ処理 (新規:0, 更新:1, スキップ:0, エラー:0) 処理時間:32ms
```

## テーブル構造

### transaction_table（トランザクションテーブル）
| カラム名 | 型 | 説明 |
|---------|-----|------|
| id | SERIAL | 主キー（自動採番） |
| entity_id | INTEGER | エンティティID（業務キー） |
| name | VARCHAR(100) | 名前 |
| description | TEXT | 説明（NULL可） |
| status | VARCHAR(20) | ステータス |
| amount | DECIMAL(10,2) | 金額 |
| transaction_type | VARCHAR(20) | トランザクションタイプ |
| created_at | TIMESTAMP | 作成日時 |
| updated_at | TIMESTAMP | 更新日時 |

### latest_data_table（最新データテーブル）
| カラム名 | 型 | 説明 |
|---------|-----|------|
| id | INTEGER | ID（transaction_tableと対応） |
| entity_id | INTEGER | エンティティID（業務キー） |
| name | VARCHAR(100) | 名前 |
| description | TEXT | 説明（NULL可） |
| status | VARCHAR(20) | ステータス |
| amount | DECIMAL(10,2) | 金額 |
| transaction_type | VARCHAR(20) | トランザクションタイプ |
| created_at | TIMESTAMP | 作成日時 |
| updated_at | TIMESTAMP | 更新日時 |
| **主キー** | **(id, entity_id)** | **複合主キー** |

### 複合主キー設計の意義
- **関係性の明確化**: transaction_tableとlatest_data_tableの対応関係を(id, entity_id)で正確に管理
- **データ追跡性**: 同一entity_idでも異なるtransaction_idのデータを区別可能
- **整合性保証**: 一意性制約により重複データの防止
| amount | DECIMAL(10,2) | 金額 |
| transaction_type | VARCHAR(20) | トランザクション種別 |
| created_at | TIMESTAMP | 作成日時 |
| updated_at | TIMESTAMP | 更新日時 |

### latest_data_table（最新データテーブル）
| カラム名 | 型 | 説明 |
|---------|-----|------|
| entity_id | INTEGER | 主キー（エンティティID） |
| name | VARCHAR(100) | 名前 |
| description | TEXT | 説明（NULL可） |
| status | VARCHAR(20) | ステータス |
| amount | DECIMAL(10,2) | 金額 |
| transaction_type | VARCHAR(20) | トランザクション種別 |
| created_at | TIMESTAMP | 作成日時 |
| updated_at | TIMESTAMP | 更新日時 |

## パフォーマンス最適化

### データベース最適化
- **効率的なインデックス**: エンティティID + 作成日時の複合インデックス
- **最適化クエリ**: WITH句を活用した最新データ取得
- **一括処理**: UPSERT構文による高速更新
- **トランケート前提**: 処理済みフラグ不要の簡潔な設計

### アプリケーション最適化
- **最小限のデータベースアクセス**: 必要なデータのみを一括取得
- **メモリ内処理**: 差分チェックをメモリ内で実行
- **効率的な一括更新**: PostgreSQLの最適化機能を活用
- **シンプルな処理フロー**: トランケート前提による簡潔な処理

## エラーハンドリング

- **トランザクション単位のエラー分離**: 一部エラーでも他の処理を継続
- **詳細なエラーログ**: 問題箇所の特定が容易
- **部分的失敗対応**: 処理可能な分のみ更新

## 拡張性

### 新しいフィールドの追加
1. エンティティクラスにプロパティ追加
2. データベーステーブルにカラム追加
3. リポジトリのSQL更新
4. 差分チェックロジック更新

### 大容量対応
- ストリーミング処理への拡張可能
- バッチサイズの調整機能
- 分散処理への対応可能

### 他のデータベースへの対応
- リポジトリ実装の差し替え
- 適切なデータアクセスライブラリへの変更

## テスト実行

### 単体テスト実行
```bash
cd CheckDiffTable.Tests
dotnet test --verbosity normal
```

### 特定のテストクラス実行
```bash
# サービステストのみ実行
dotnet test --filter "FullyQualifiedName~DiffCheckServiceTests" --verbosity normal

# モデルテストのみ実行
dotnet test --filter "FullyQualifiedName~ModelsTests" --verbosity normal
```

### カバレッジレポート生成
```bash
# coverletパッケージを追加（初回のみ）
dotnet add package coverlet.msbuild

# カバレッジ付きでテスト実行
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

### テスト構成
- **Services/DiffCheckServiceTests.cs**: DiffCheckServiceの単体テスト
- **Models/ModelsTests.cs**: エンティティクラスとオプションクラスのテスト
- **Repositories/RepositoryTests.cs**: リポジトリの基本構造テスト
- **Integration/IntegrationTests.cs**: 統合テスト（データベース接続が必要）
