using CheckDiffTable.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CheckDiffTable.Repositories
{
    /// <summary>
    /// 最新データテーブルリポジトリのインターフェース
    /// 最新データの管理（取得、登録、更新、一括操作）を提供
    /// </summary>
    public interface ILatestDataRepository
    {
        /// <summary>
        /// 指定されたトランザクションキー（ID, EntityID）リストに対応する最新データを一括取得する
        /// 複合主キー（id, entity_id）での検索を行う
        /// </summary>
        /// <param name="transactionKeys">取得対象のトランザクションキー（ID, EntityID）リスト</param>
        /// <returns>該当する最新データのリスト</returns>
        Task<List<LatestDataEntity>> GetByTransactionKeysAsync(List<(int Id, int EntityId)> transactionKeys);
        
        /// <summary>
        /// 最新データを一括でUPSERT（新規登録・更新）する
        /// 存在しないものは新規登録、存在するものは更新を行う
        /// </summary>
        /// <param name="entities">処理対象の最新データエンティティリスト</param>
        /// <returns>非同期処理タスク</returns>
        Task BulkUpsertAsync(List<LatestDataEntity> entities);
    }
}
