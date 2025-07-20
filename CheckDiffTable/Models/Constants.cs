namespace CheckDiffTable.Models
{
    /// <summary>
    /// データベーステーブルのカラム名定数
    /// </summary>
    public static class DatabaseConstants
    {
        /// <summary>
        /// 主キーカラム名の定義
        /// トランザクションテーブルと最新データテーブルで共通の主キー構成
        /// </summary>
        public static class PrimaryKeyColumns
        {
            /// <summary>ID（自動採番）</summary>
            public const string Id = "id";
            
            /// <summary>エンティティID（業務キー）</summary>
            public const string EntityId = "entity_id";
        }

        /// <summary>
        /// トランザクションテーブルのカラム名
        /// </summary>
        public static class TransactionTable
        {
            public const string TableName = "transaction_table";
            public const string Id = PrimaryKeyColumns.Id;
            public const string EntityId = PrimaryKeyColumns.EntityId;
            public const string Name = "name";
            public const string Description = "description";
            public const string Status = "status";
            public const string Amount = "amount";
            public const string TransactionType = "transaction_type";
            public const string CreatedAt = "created_at";
            public const string UpdatedAt = "updated_at";
        }

        /// <summary>
        /// 最新データテーブルのカラム名
        /// </summary>
        public static class LatestDataTable
        {
            public const string TableName = "latest_data_table";
            public const string Id = PrimaryKeyColumns.Id;
            public const string EntityId = PrimaryKeyColumns.EntityId;
            public const string Name = "name";
            public const string Description = "description";
            public const string Status = "status";
            public const string Amount = "amount";
            public const string TransactionType = "transaction_type";
            public const string CreatedAt = "created_at";
            public const string UpdatedAt = "updated_at";
        }
    }
}
