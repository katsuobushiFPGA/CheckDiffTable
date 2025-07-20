# DFD (Data Flow Diagram) - トランザクション差分チェック・更新システム

```mermaid
graph TD
    %% 外部エンティティ
    ExtSystem[外部システム<br/>トランザクション流入] 
    Operator[システム運用者]
    ConfigFile[(appsettings.json<br/>設定ファイル)]
    
    %% プロセス
    P1[1.0<br/>バッチ処理開始<br/>BatchProcessStart]
    P2[2.0<br/>トランザクション<br/>一括取得<br/>GetAllTransactions]
    P3[3.0<br/>最新データ<br/>一括取得<br/>GetLatestData]
    P4[4.0<br/>差分チェック<br/>DifferenceCheck]
    P5[5.0<br/>一括UPSERT<br/>BulkUpsert]
    P6[6.0<br/>削除処理<br/>DeleteTransactions]
    P7[7.0<br/>結果出力<br/>ResultOutput]
    
    %% データストア
    DS1[(D1: transaction_table<br/>トランザクションテーブル)]
    DS2[(D2: latest_data_table<br/>最新データテーブル)]
    DS3[(D3: ProcessResult<br/>処理結果)]
    DS4[(D4: ApplicationLog<br/>アプリケーションログ)]
    
    %% データフロー: 外部からシステムへ
    ExtSystem -->|トランザクションデータ| DS1
    ConfigFile -->|バッチサイズ設定| P1
    Operator -->|実行コマンド| P1
    
    %% データフロー: プロセス間
    P1 -->|処理開始| P2
    P2 -->|トランザクション全件| P3
    DS1 -->|全トランザクション| P2
    
    P3 -->|既存データ| P4
    DS2 -->|最新データ| P3
    P2 -->|トランザクション| P4
    
    P4 -->|新規データ| P5
    P4 -->|更新データ| P5
    P4 -->|削除対象| P6
    P4 -->|処理結果| DS3
    
    P5 -->|UPSERT完了| P7
    P6 -->|削除完了| P7
    DS2 -->|現在データ| P5
    P5 -->|更新後データ| DS2
    
    DS1 -->|削除対象| P6
    P6 -->|削除実行| DS1
    
    DS3 -->|処理サマリー| P7
    P7 -->|実行結果| DS4
    P7 -->|処理結果| Operator
    
    %% スタイリング
    classDef process fill:#e1f5fe,stroke:#01579b,stroke-width:2px
    classDef datastore fill:#f3e5f5,stroke:#4a148c,stroke-width:2px
    classDef external fill:#e8f5e8,stroke:#1b5e20,stroke-width:2px
    
    class P1,P2,P3,P4,P5,P6,P7 process
    class DS1,DS2,DS3,DS4 datastore
    class ExtSystem,Operator,ConfigFile external
```

## データフロー詳細説明

### レベル0（コンテキスト図）
**システム全体の境界と外部エンティティとの関係**

### レベル1（主要プロセス）

#### 1.0 バッチ処理開始
- **入力**: 実行コマンド、設定ファイル
- **出力**: バッチ処理パラメータ
- **処理**: バッチサイズの決定、ログ初期化

#### 2.0 トランザクション一括取得
- **入力**: バッチ処理開始シグナル
- **出力**: 全トランザクションデータ
- **処理**: `SELECT * FROM transaction_table`

#### 3.0 最新データ一括取得
- **入力**: トランザクションキー
- **出力**: 対応する既存最新データ
- **処理**: 複合主キーでの一括検索

#### 4.0 差分チェック
- **入力**: トランザクションデータ、既存最新データ
- **出力**: 新規/更新/削除対象データ
- **処理**: フィールド単位での差分判定

#### 5.0 一括UPSERT
- **入力**: 新規・更新対象データ
- **出力**: UPSERT結果
- **処理**: PostgreSQL ON CONFLICT構文使用

#### 6.0 削除処理
- **入力**: 削除対象トランザクションキー
- **出力**: 削除件数
- **処理**: 差分なしトランザクションの一括削除

#### 7.0 結果出力
- **入力**: 処理結果、ログデータ
- **出力**: 処理サマリー
- **処理**: 統計情報の集計とログ出力

### データストア詳細

#### D1: transaction_table
- **目的**: 一時的なトランザクションデータ格納
- **特徴**: 毎回トランケート、大量データ流入

#### D2: latest_data_table
- **目的**: 各エンティティの最新状態保持
- **特徴**: 複合主キー、永続化データ

#### D3: ProcessResult
- **目的**: 各処理の詳細結果格納
- **特徴**: メモリ内一時データ

#### D4: ApplicationLog
- **目的**: システム動作ログ
- **特徴**: 日本語ログ、構造化データ
