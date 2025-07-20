using CheckDiffTable.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CheckDiffTable.Repositories
{
    /// <summary>
    /// トランザクションテーブルリポジトリのインターフェース（毎回トランケートされる）
    /// </summary>
    public interface ITransactionRepository
    {
        Task<List<TransactionEntity>> GetLatestTransactionsByEntityAsync();
        Task<TransactionEntity?> GetLatestByEntityIdAsync(int entityId);
        Task<List<TransactionEntity>> GetAllAsync();
    }

    /// <summary>
    /// 最新データテーブルリポジトリのインターフェース
    /// </summary>
    public interface ILatestDataRepository
    {
        Task<LatestDataEntity?> GetByEntityIdAsync(int entityId);
        Task<List<LatestDataEntity>> GetByEntityIdsAsync(List<int> entityIds);
        Task InsertAsync(LatestDataEntity entity);
        Task UpdateAsync(LatestDataEntity entity);
        Task BulkUpsertAsync(List<LatestDataEntity> entities);
        Task<List<LatestDataEntity>> GetAllAsync();
    }
}
