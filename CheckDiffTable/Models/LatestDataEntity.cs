using System;

namespace CheckDiffTable.Models
{
    /// <summary>
    /// 最新データテーブルのエンティティクラス
    /// 各エンティティの最新状態を永続化するために使用される
    /// </summary>
    public class LatestDataEntity
    {
        /// <summary>ID（自動採番、複合主キーの一部）</summary>
        public int Id { get; set; }
        
        /// <summary>エンティティの識別子（業務キー、複合主キーの一部）</summary>
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

        /// <summary>
        /// 他のLatestDataEntityとの差分をチェックする
        /// 業務データ項目（Name, Description, Status, Amount, TransactionType）を比較
        /// </summary>
        /// <param name="other">比較対象のLatestDataEntity</param>
        /// <returns>差分がある場合true、差分がない場合false</returns>
        public bool HasDifference(LatestDataEntity other)
        {
            return Name != other.Name ||
                   Description != other.Description ||
                   Status != other.Status ||
                   Amount != other.Amount ||
                   TransactionType != other.TransactionType;
        }

        /// <summary>
        /// TransactionEntityからLatestDataEntityに変換する（新規作成用）
        /// トランザクションデータを最新データ形式に変換し、差分チェック・更新処理で使用
        /// </summary>
        /// <param name="transaction">変換元のトランザクションエンティティ</param>
        /// <returns>変換後の最新データエンティティ</returns>
        public static LatestDataEntity FromTransaction(TransactionEntity transaction)
        {
            var now = DateTime.UtcNow;
            return new LatestDataEntity
            {
                Id = transaction.Id,
                EntityId = transaction.EntityId,
                Name = transaction.Name,
                Description = transaction.Description,
                Status = transaction.Status,
                Amount = transaction.Amount,
                TransactionType = transaction.TransactionType,
                CreatedAt = now,  // 新規作成時は現在時刻
                UpdatedAt = now   // 更新時刻も現在時刻
            };
        }

        /// <summary>
        /// TransactionEntityからLatestDataEntityに変換する（更新用）
        /// 既存の作成日時を保持して更新用のエンティティを作成
        /// </summary>
        /// <param name="transaction">変換元のトランザクションエンティティ</param>
        /// <param name="existingCreatedAt">既存データの作成日時</param>
        /// <returns>変換後の最新データエンティティ</returns>
        public static LatestDataEntity FromTransactionForUpdate(TransactionEntity transaction, DateTime existingCreatedAt)
        {
            return new LatestDataEntity
            {
                Id = transaction.Id,
                EntityId = transaction.EntityId,
                Name = transaction.Name,
                Description = transaction.Description,
                Status = transaction.Status,
                Amount = transaction.Amount,
                TransactionType = transaction.TransactionType,
                CreatedAt = existingCreatedAt,  // 既存の作成日時を保持
                UpdatedAt = DateTime.UtcNow     // 更新時刻は現在時刻
            };
        }
    }
}
