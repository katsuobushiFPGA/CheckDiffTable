# トランザクション差分チェック・更新システム

## 概要

このシステムは、大量のトランザクションデータが継続的に流入するトランザクションテーブルから最新データのみを保持する専用テーブルに対して、効率的なバッチ処理で差分チェックを行い必要に応じて更新を行うC#アプリケーションです。複合主キー設計により、トランザクションと最新データの関係性を明確化し、差分がないトランザクションの自動削除機能を提供します。

## プロジェクト構成

```
CheckDiffTable/
├── CheckDiffTable.csproj          # プロジェクトファイル
├── Program.cs                     # メインエントリーポイント（NpgsqlDataSource設定含む）
├── appsettings.json              # 設定ファイル（データベース接続・タイムアウト設定）
├── database_schema.sql           # データベーススキーマ
├── Configuration/
│   └── BatchProcessingOptions.cs # バッチ処理設定オプション
├── Models/
│   ├── TransactionEntity.cs      # トランザクションエンティティ
│   ├── LatestDataEntity.cs       # 最新データエンティティ
│   ├── DatabaseConstants.cs     # データベース定数（JST時刻対応）
│   └── Results/                  # 処理結果クラス群
│       ├── ProcessAction.cs     # 処理アクション列挙型
│       ├── ProcessResult.cs     # 個別処理結果クラス
│       └── BatchProcessResult.cs # バッチ処理結果クラス
├── Repositories/
│   ├── ITransactionRepository.cs # トランザクションリポジトリインターフェース
│   ├── ILatestDataRepository.cs  # 最新データリポジトリインターフェース
│   ├── TransactionRepository.cs  # トランザクションテーブルリポジトリ
│   └── LatestDataRepository.cs   # 最新データテーブルリポジトリ
├── Services/
│   ├── IDiffCheckService.cs      # 差分チェックサービスインターフェース
│   └── DiffCheckService.cs       # 差分チェック・更新サービス
```

## 主な機能

### 1. 複合主キー設計
- **トランザクション関係性の明確化**: (id, entity_id) 複合主キーによる正確なデータ対応
- **データ整合性の向上**: 同一トランザクションに対する正確な管理
- **PostgreSQL ROW構文**: 複合主キーでの効率的なバッチ検索を実装

### 2. 効率的なバッチ処理
- **全トランザクション一括取得**: transaction_tableから全データを一括取得
- **関連データ一括取得**: 複合主キーを使用した効率的なデータ取得
- **バッチサイズ制御**: 設定可能なバッチサイズによる処理量制御
- **一括UPSERT**: PostgreSQLのON CONFLICT構文を使用した高速更新

### 3. 差分チェック機能
- トランザクションテーブルの最新データと最新データテーブルの既存データを比較
- フィールド単位での差分判定（name, description, status, amount, transaction_type）

### 4. データ更新ロジック
- **新規登録**: 最新データテーブルにデータが存在しない場合
- **更新**: データが存在し、差分がある場合
- **スキップ後削除**: データが存在し、差分がない場合はトランザクションを削除

### 5. 自動クリーンアップ機能
- **差分なしトランザクション削除**: バッチ処理内で差分がなかったトランザクションを自動削除
- **バッチ単位削除**: 効率的な一括削除処理

### 6. データベース接続管理
- **NpgsqlDataSource使用**: 接続プールによる効率的なデータベース接続管理
- **接続タイムアウト制御**: appsettings.jsonでのタイムアウト設定
- **包括的エラーハンドリング**: データベース接続、タイムアウト、一般エラーの個別対応
- **リソース自動解放**: プログラム終了時の適切なリソース解放

### 7. データベーストランザクション管理
- **ACID特性保証**: バッチ処理単位でのトランザクション制御によるデータ整合性確保
- **ロールバック機能**: 処理失敗時の自動ロールバックによる部分更新防止
- **アトミック操作**: UPSERT処理と削除処理を単一トランザクションで実行
- **エラー分離**: バッチ単位での例外処理と詳細なエラーログ出力

### 8. 時刻管理機能
- **JST対応**: DatabaseConstants.DateTimeHelperによる日本標準時サポート
- **タイムゾーン設定**: appsettings.jsonでの設定可能なタイムゾーン
- **統一時刻管理**: 全データの作成・更新時刻をJSTで統一

### 9. 監視・ログ機能
- **完全日本語対応**: 全てのシステムログが日本語で出力され、運用時の可読性を向上
- **構造化ログ**: パラメータ付きログメッセージによる詳細な情報記録
- **処理時間測定**: バッチ処理全体および個別処理の時間測定とログ出力
- **処理件数サマリー**: 新規登録/更新/スキップ/削除/エラー件数の詳細表示
- **バッチ処理進捗**: リアルタイムでのバッチ処理進捗状況の可視化
- **詳細エラー情報**: 例外の詳細情報と発生箇所の特定が容易なログ出力

### 10. コード最適化・保守性
- **不要メソッド削除**: データベーストランザクション管理の導入により、トランザクション未対応の旧メソッドを削除
- **統一インターフェース**: 全データベース操作をトランザクション対応メソッドに統一
- **コード重複排除**: 機能統合による保守性向上とコードベースの簡素化
- **一貫性保証**: 全操作でACID特性を保証する統一的なアプローチ

## 使用技術

- **C# .NET 8.0**: 高性能なクロスプラットフォーム開発環境
- **Npgsql**: PostgreSQL用データアクセスライブラリ（NpgsqlDataSource使用）
- **Microsoft.Extensions**: DI、Configuration、Logging（ホストビルダー使用）
- **PostgreSQL**: 高性能データベース（ROW構文、複合主キー対応）

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
       "DefaultConnection": "Host=localhost;Database=checkdiff_db;Username=postgres;Password=your_password;Command Timeout=30;Timeout=15;Timezone=Asia/Tokyo"
     },
     "BatchProcessing": {
       "BatchSize": 1000
     }
   }
   ```

## 実行方法

### バッチ処理実行
```bash
dotnet run              # appsettings.jsonで設定されたバッチサイズで実行
```

### バッチサイズ設定
appsettings.jsonで設定可能：
```json
{
  "BatchProcessing": {
    "BatchSize": 1000      # デフォルトバッチサイズ（0以下の場合は1000が適用される）
  }
}
```

## 実行例

### 初回実行時（一括処理）
```
=== トランザクション差分チェック・更新システム開始 ===
Starting batch processing with batch size: 1000
Found 7 entities with transactions
Processing batch 1/1 (7 entities)
Bulk upserted 7 records (7 inserts, 0 updates)
All batches completed including cleanup of transactions with no differences

=== 一括処理結果サマリー ===
処理成功: True
総エンティティ数: 7
新規登録: 7件
更新: 0件
スキップ: 0件
削除: 0件
エラー: 0件
処理時間: 125ms
メッセージ: 一括処理完了: 7エンティティ処理 (新規:7, 更新:0, スキップ:0, エラー:0) 削除:0件 処理時間:125ms
```

### 2回目実行時（同じデータ - 差分なし）
```
=== トランザクション差分チェック・更新システム開始 ===
Starting batch processing with batch size: 1000
Found 7 entities with transactions
Processing batch 1/1 (7 entities)
Deleted 7 transactions with no differences from transaction table
All batches completed including cleanup of transactions with no differences

=== 一括処理結果サマリー ===
処理成功: True
総エンティティ数: 7
新規登録: 0件
更新: 0件
スキップ: 7件
削除: 7件
エラー: 0件
処理時間: 89ms
メッセージ: 一括処理完了: 7エンティティ処理 (新規:0, 更新:0, スキップ:7, エラー:0) 削除:7件 処理時間:89ms
```

### 大量データのバッチ処理例
```
=== トランザクション差分チェック・更新システム開始 ===
Starting batch processing with batch size: 1000
Found 5500 entities with transactions
Processing batch 1/6 (1000 entities)
Bulk upserted 800 records (200 inserts, 600 updates)
Deleted 200 transactions with no differences from transaction table
Processing batch 2/6 (1000 entities)
...
All batches completed including cleanup of transactions with no differences

=== 一括処理結果サマリー ===
処理成功: True
総エンティティ数: 5500
新規登録: 1200件
更新: 3100件
スキップ: 1200件
削除: 1200件
エラー: 0件
処理時間: 2340ms
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
- **データ追跡性**: 各トランザクションに対する最新状態を一意に特定
- **整合性保証**: 一意性制約により重複データの防止
- **効率的な検索**: ROW構文による高速な複合主キー検索

## パフォーマンス最適化

### データベース最適化
- **効率的なインデックス**: 
  - `idx_transaction_entity_id_created_at`: エンティティID + 作成日時の複合インデックス
  - `idx_latest_data_composite_key`: 複合主キーインデックス
  - `idx_latest_data_entity_id`: エンティティIDインデックス
- **最適化クエリ**: ROW構文を活用した複合主キー一括検索
- **一括処理**: UPSERT構文による高速更新
- **効率的削除**: unnest関数による配列展開での一括削除

### アプリケーション最適化
- **バッチサイズ制御**: 設定可能なバッチサイズによる処理量調整
- **メモリ内処理**: 差分チェックをメモリ内で実行
- **最小限のデータベースアクセス**: 必要なデータのみを一括取得
- **バッチ内完結処理**: 取得→処理→更新→削除をバッチ単位で実行

### データベース接続最適化
- **NpgsqlDataSource使用**: 接続プールによる効率的なリソース管理
- **タイムアウト制御**: 接続・コマンドタイムアウトの適切な設定
- **リソース管理**: 適切なリソース解放と例外処理
- **エラー分離**: データベースエラー種別による適切な処理分岐

### トランザクション処理最適化
- **ACID特性保証**: バッチ単位でのデータベーストランザクション制御
- **ロールバック制御**: 処理失敗時の確実な状態復旧機能
- **アトミック操作**: 複数テーブルへの操作を単一トランザクションで実行
- **デッドロック回避**: 適切なトランザクション範囲とタイムアウト設定

## エラーハンドリング

- **包括的例外処理**: NpgsqlException、TimeoutException、一般Exceptionの個別対応
- **データベース接続エラー**: 接続・認証エラーの詳細分析と適切な終了コード
- **タイムアウトエラー**: 処理・接続タイムアウトの個別検出と対応
- **トランザクション単位のエラー分離**: 一部エラーでも他の処理を継続
- **詳細なエラーログ**: 問題箇所の特定が容易な詳細ログ出力
- **部分的失敗対応**: 処理可能な分のみ更新
- **処理結果詳細追跡**: 各トランザクションの処理結果を詳細に記録
- **リソース解放保証**: finallyブロックによる確実なリソース解放

## 設定管理

### appsettings.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=checkdiff_db;Username=postgres;Password=password;Command Timeout=30;Timeout=15;Timezone=Asia/Tokyo"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "BatchProcessing": {
    "BatchSize": 1000
  }
}
```

### 設定項目
- **ConnectionStrings.DefaultConnection**: PostgreSQL接続文字列
  - **Command Timeout**: コマンド実行タイムアウト（秒）
  - **Timeout**: 接続タイムアウト（秒）
  - **Timezone**: データベースのタイムゾーン設定
- **BatchProcessing.BatchSize**: バッチサイズ（デフォルト: 1000）
- **Logging.LogLevel**: ログレベル設定

## 拡張性

### 新しいフィールドの追加
1. エンティティクラスにプロパティ追加
2. データベーステーブルにカラム追加
3. リポジトリのSQL更新
4. 差分チェックロジック更新
5. データベース定数の更新

### 大容量対応
- バッチサイズの動的調整
- ストリーミング処理への拡張可能
- 分散処理への対応可能

### 他のデータベースへの対応
- リポジトリ実装の差し替え
- 適切なデータアクセスライブラリへの変更
- データベース固有機能の抽象化

## アーキテクチャ

### 依存関係注入
- **Repository層**: データアクセスの抽象化
- **Service層**: ビジネスロジックの実装
- **Options Pattern**: 設定値の型安全な管理

### 設計パターン
- **Repository Pattern**: データアクセスの統一
- **Options Pattern**: 設定管理
- **Dependency Injection**: 疎結合設計（ホストビルダー使用）
- **Batch Processing Pattern**: 効率的な大量データ処理
- **Data Source Pattern**: NpgsqlDataSourceによる接続プール管理
- **Transaction Pattern**: データベーストランザクションによるACID特性保証
- **Error Handling Pattern**: 包括的例外処理による堅牢性確保

## トラブルシューティング

### よくある問題と解決方法

1. **接続エラー**
   - PostgreSQLサーバーの起動確認
   - 接続文字列の確認（タイムアウト設定含む）
   - ファイアウォール設定の確認
   - 認証情報の確認

2. **パフォーマンス問題**
   - バッチサイズの調整（大きすぎる場合は小さく）
   - インデックスの確認
   - データベースリソースの監視
   - 接続プールサイズの調整

3. **タイムアウトエラー**
   - Command Timeoutの調整（appsettings.json）
   - 接続Timeoutの調整（appsettings.json）
   - ネットワーク状況の確認
   - クエリ最適化の検討

4. **メモリ不足**
   - バッチサイズを小さく設定
   - 処理対象データ量の確認
   - システムリソースの監視

5. **処理結果が期待と異なる**
   - ログ出力の詳細確認
   - 差分チェックロジックの確認
   - データベース状態の確認
   - JST時刻設定の確認
- 処理時間の測定とログ出力
- 処理件数のサマリー表示
- 詳細なエラーハンドリングとログ出力

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

## パフォーマンス最適化（追加情報）
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

### 日本語ログシステム
- **完全日本語対応**: 全システムログの日本語化により運用効率を向上
- **統一メッセージフォーマット**: 一貫した日本語ログメッセージの提供
- **技術的正確性**: 専門用語の適切な日本語表現による可読性向上

## エラーハンドリング（統合版）

## 拡張性

### 新しいフィールドの追加
- **包括的例外処理**: NpgsqlException、TimeoutException、一般Exceptionの個別対応
- **トランザクション単位エラー分離**: 一部エラーでも他の処理を継続
- **日本語エラーメッセージ**: 全てのログメッセージが日本語で出力され、問題箇所の特定が容易
- **詳細な部分失敗対応**: 処理可能な分のみ更新、各トランザクションの処理結果を詳細に記録
- **リソース解放保証**: データベーストランザクションの確実なコミット/ロールバック制御

## 拡張性・保守性

### コード最適化
1. **不要メソッド削除済み**: トランザクション未対応の旧メソッド（`BulkUpsertAsync`、`DeleteSpecificTransactionsAsync`）を削除
2. **統一インターフェース**: 全データベース操作をトランザクション対応メソッドに統一
3. **エンティティクラスの拡張性**: 新しいフィールドの追加が容易な設計

### 大容量・分散対応
- **動的バッチサイズ調整**: 処理データ量に応じた最適なバッチサイズ設定
- **ストリーミング処理**: 大量データの段階的処理への拡張可能性
- **分散処理対応**: 複数インスタンスでの並列処理への対応可能
- **他データベース対応**: リポジトリ実装の差し替えによる柔軟なデータベース変更

## 開発・テスト環境

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
