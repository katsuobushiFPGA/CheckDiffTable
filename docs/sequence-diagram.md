# シーケンス図 (Sequence Diagram) - バッチ処理フロー

## メインシーケンス（正常処理）

```mermaid
sequenceDiagram
    participant User as システム運用者
    participant Program as Program.cs
    participant DiffService as DiffCheckService
    participant TranRepo as TransactionRepository
    participant LatestRepo as LatestDataRepository
    participant DB as PostgreSQL
    participant Logger as ログシステム

    User->>Program: dotnet run 実行
    Program->>Logger: システム開始ログ
    Program->>DiffService: ProcessAllEntitiesBatchAsync()
    
    Note over DiffService: バッチ処理開始
    DiffService->>Logger: バッチ処理開始ログ
    DiffService->>TranRepo: GetAllTransactionsAsync()
    TranRepo->>DB: SELECT * FROM transaction_table
    DB-->>TranRepo: 全トランザクション
    TranRepo-->>DiffService: List<TransactionEntity>
    
    alt 大量データの場合
        Note over DiffService: バッチ分割処理
        loop バッチごと
            DiffService->>DiffService: ProcessTransactionBatch(batch)
            Note over DiffService: 以下の処理を繰り返し
        end
    else 少量データの場合
        DiffService->>DiffService: ProcessTransactionBatch(全データ)
    end
    
    Note over DiffService,DB: データベーストランザクション開始
    DiffService->>DB: BEGIN TRANSACTION
    
    DiffService->>LatestRepo: GetByTransactionKeysAsync(keys)
    LatestRepo->>DB: SELECT ... WHERE (id, entity_id) IN (...)
    DB-->>LatestRepo: 既存最新データ
    LatestRepo-->>DiffService: List<LatestDataEntity>
    
    Note over DiffService: 差分チェック・分類
    loop 各トランザクション
        alt 既存データなし
            Note over DiffService: 新規登録対象に追加
        else 既存データあり & 差分あり
            Note over DiffService: 更新対象に追加
        else 既存データあり & 差分なし
            Note over DiffService: 削除対象に追加（スキップ）
        end
    end
    
    Note over DiffService,DB: 一括データ操作
    alt 新規・更新データがある場合
        DiffService->>LatestRepo: BulkUpsertWithTransactionAsync()
        LatestRepo->>DB: INSERT ... ON CONFLICT ... DO UPDATE
        DB-->>LatestRepo: UPSERT結果
        LatestRepo->>Logger: UPSERT完了ログ
    end
    
    alt 削除対象がある場合
        DiffService->>TranRepo: DeleteSpecificTransactionsWithTransactionAsync()
        TranRepo->>DB: DELETE FROM transaction_table WHERE ...
        DB-->>TranRepo: 削除件数
        TranRepo->>Logger: 削除完了ログ
    end
    
    DiffService->>DB: COMMIT TRANSACTION
    Note over DiffService,DB: トランザクション完了
    
    DiffService->>Logger: バッチ処理完了ログ
    DiffService-->>Program: BatchProcessResult
    Program->>Logger: 処理結果サマリー表示
    Program->>User: 実行完了
```

## エラー処理シーケンス

```mermaid
sequenceDiagram
    participant DiffService as DiffCheckService
    participant DB as PostgreSQL
    participant Logger as ログシステム

    Note over DiffService,DB: エラー発生ケース
    DiffService->>DB: BEGIN TRANSACTION
    
    DiffService->>DB: データベース操作
    DB-->>DiffService: エラー発生
    
    Note over DiffService: 例外キャッチ
    DiffService->>DB: ROLLBACK TRANSACTION
    DiffService->>Logger: エラーログ（日本語）
    
    Note over DiffService: エラー詳細記録
    loop バッチ内の各トランザクション
        Note over DiffService: ProcessResult にエラー情報設定
    end
    
    DiffService->>DiffService: throw Exception
    Note over DiffService: 上位レイヤーにエラー伝播
```

## 詳細処理シーケンス（差分チェック部分）

```mermaid
sequenceDiagram
    participant DiffService as DiffCheckService
    participant LatestEntity as LatestDataEntity
    participant ProcessResult as ProcessResult

    Note over DiffService: 各トランザクションの処理
    loop batchTransactions
        DiffService->>LatestEntity: FromTransaction()
        Note over LatestEntity: トランザクション→最新データ変換
        
        alt 既存データが存在しない
            DiffService->>ProcessResult: Action = Insert
            Note over DiffService: toInsert リストに追加
        else 既存データが存在する
            DiffService->>LatestEntity: HasDifference(existing)
            alt 差分あり
                DiffService->>ProcessResult: Action = Update
                Note over DiffService: toUpdate リストに追加
            else 差分なし
                DiffService->>ProcessResult: Action = None (Skip)
                Note over DiffService: toDelete リストに追加
            end
        end
        
        DiffService->>ProcessResult: 結果詳細を Details に追加
    end
```

## 設定・依存関係注入シーケンス

```mermaid
sequenceDiagram
    participant Program as Program.cs
    participant Host as HostBuilder
    participant Config as Configuration
    participant DI as DIContainer
    participant Services as サービス群

    Program->>Host: CreateDefaultBuilder()
    Program->>Config: appsettings.json読込み
    Program->>DI: ConfigureServices()
    
    Note over DI: 依存関係の登録
    DI->>DI: AddSingleton<NpgsqlDataSource>()
    DI->>DI: Configure<BatchProcessingOptions>()
    DI->>DI: AddScoped<ITransactionRepository>()
    DI->>DI: AddScoped<ILatestDataRepository>()
    DI->>DI: AddScoped<IDiffCheckService>()
    
    Program->>Host: Build()
    Program->>Services: CreateScope()
    Program->>Services: GetRequiredService<>()
    Note over Services: 依存関係解決とインスタンス生成
```

## 大量データ処理シーケンス

```mermaid
sequenceDiagram
    participant DiffService as DiffCheckService
    participant Logger as ログシステム
    participant BatchProcessor as バッチ処理

    Note over DiffService: 大量データ（例：5500件）
    DiffService->>Logger: Found 5500 entities
    
    Note over DiffService: バッチサイズ1000で分割
    loop 6回（バッチ1〜6）
        DiffService->>Logger: Processing batch X/6
        DiffService->>BatchProcessor: ProcessTransactionBatch(1000件)
        
        Note over BatchProcessor: データベーストランザクション
        BatchProcessor->>BatchProcessor: 差分チェック
        BatchProcessor->>BatchProcessor: 一括UPSERT
        BatchProcessor->>BatchProcessor: 削除処理
        
        BatchProcessor->>Logger: バッチ処理完了ログ
    end
    
    DiffService->>Logger: All batches completed
    Note over DiffService: 処理サマリー（例：新規1200、更新3100、削除1200）
```

## 主要な特徴

### トランザクション制御
- **ACID特性保証**: 各バッチ単位でデータベーストランザクション
- **ロールバック機能**: エラー時の確実な状態復旧
- **アトミック操作**: UPSERT + DELETE の一括実行

### エラーハンドリング
- **階層的例外処理**: 個別エラー + バッチエラー + システムエラー
- **日本語ログ**: 全てのログメッセージが日本語で出力
- **詳細追跡**: 各トランザクションの処理結果を記録

### パフォーマンス最適化
- **バッチ分割処理**: 大量データを効率的に処理
- **一括操作**: N+1問題の回避
- **インメモリ差分チェック**: データベースアクセス最小化
