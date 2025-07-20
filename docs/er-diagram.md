# ER図 (Entity Relationship Diagram)

```mermaid
erDiagram
    transaction_table {
        int id PK "SERIAL PRIMARY KEY"
        int entity_id "エンティティID（業務キー）"
        varchar name "名前"
        text description "説明（NULL可）"
        varchar status "ステータス"
        decimal amount "金額"
        varchar transaction_type "トランザクションタイプ"
        timestamp created_at "作成日時"
        timestamp updated_at "更新日時"
    }
    
    latest_data_table {
        int id PK "ID（transaction_tableと対応）"
        int entity_id PK "エンティティID（業務キー）"
        varchar name "名前"
        text description "説明（NULL可）"
        varchar status "ステータス"
        decimal amount "金額"
        varchar transaction_type "トランザクションタイプ"
        timestamp created_at "作成日時"
        timestamp updated_at "更新日時"
    }

    %% 関係性の定義
    transaction_table ||--o{ latest_data_table : "差分チェック後に最新データとして格納"
    
    %% インデックス情報（コメントとして記載）
    %% idx_transaction_entity_id_created_at ON transaction_table(entity_id, created_at DESC)
    %% idx_latest_data_composite_key ON latest_data_table(id, entity_id)
    %% idx_latest_data_entity_id ON latest_data_table(entity_id)
```

## 主要な特徴

### 複合主キー設計
- **latest_data_table**: `(id, entity_id)`の複合主キー
- **関係性明確化**: transaction_tableとlatest_data_tableの対応関係を正確に管理

### データフロー
1. **transaction_table**: 大量のトランザクションデータが継続的に流入
2. **差分チェック**: 既存のlatest_data_tableとの比較
3. **UPSERT処理**: 新規登録・更新・スキップの判定
4. **クリーンアップ**: 差分がなかったトランザクションの自動削除

### インデックス最適化
- エンティティID + 作成日時の複合インデックス
- 複合主キーインデックス
- 高速検索のためのエンティティIDインデックス
