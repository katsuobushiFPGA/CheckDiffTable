using System;

namespace CheckDiffTable.Models
{
    /// <summary>
    /// トランザクションテーブルのエンティティクラス
    /// トランザクションデータの一時的な格納と処理に使用される
    /// </summary>
    public class TransactionEntity
    {
        /// <summary>トランザクションの一意識別子</summary>
        public int Id { get; set; }
        
        /// <summary>エンティティの識別子（業務キー）</summary>
        public int EntityId { get; set; }
        
        /// <summary>エンティティ名</summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>エンティティの説明（任意項目）</summary>
        public string? Description { get; set; }
        
        /// <summary>エンティティの状態</summary>
        public string Status { get; set; } = string.Empty;
        
        /// <summary>金額</summary>
        public decimal Amount { get; set; }
        
        /// <summary>トランザクションの種別</summary>
        public string TransactionType { get; set; } = string.Empty;
        
        /// <summary>作成日時</summary>
        public DateTime CreatedAt { get; set; }
        
        /// <summary>更新日時</summary>
        public DateTime UpdatedAt { get; set; }
    }
}
