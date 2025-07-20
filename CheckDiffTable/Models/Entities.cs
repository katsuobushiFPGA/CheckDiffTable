using System;

namespace CheckDiffTable.Models
{
    /// <summary>
    /// トランザクションテーブルのエンティティ（毎回トランケートされる）
    /// </summary>
    public class TransactionEntity
    {
        public int Id { get; set; }
        public int EntityId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// 最新データテーブルのエンティティ
    /// </summary>
    public class LatestDataEntity
    {
        public int EntityId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// 他のエンティティとの差分をチェック
        /// </summary>
        /// <param name="other">比較対象</param>
        /// <returns>差分がある場合true</returns>
        public bool HasDifference(LatestDataEntity other)
        {
            return Name != other.Name ||
                   Description != other.Description ||
                   Status != other.Status ||
                   Amount != other.Amount ||
                   TransactionType != other.TransactionType;
        }

        /// <summary>
        /// TransactionEntityから変換
        /// </summary>
        /// <param name="transaction">トランザクションエンティティ</param>
        /// <returns>最新データエンティティ</returns>
        public static LatestDataEntity FromTransaction(TransactionEntity transaction)
        {
            return new LatestDataEntity
            {
                EntityId = transaction.EntityId,
                Name = transaction.Name,
                Description = transaction.Description,
                Status = transaction.Status,
                Amount = transaction.Amount,
                TransactionType = transaction.TransactionType,
                CreatedAt = transaction.CreatedAt,
                UpdatedAt = transaction.UpdatedAt
            };
        }
    }

    /// <summary>
    /// 一括処理用のエンティティ情報
    /// </summary>
    public class EntityBatchInfo
    {
        public int EntityId { get; set; }
        public TransactionEntity LatestTransaction { get; set; } = new();
        public LatestDataEntity? ExistingLatestData { get; set; }
    }
}
