using CheckDiffTable.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CheckDiffTable.Repositories
{
    /// <summary>
    /// トランザクションテーブルリポジトリのインターフェース
    /// トランザクションデータの読み取り専用操作を提供
    /// </summary>
    public interface ITransactionRepository
    {
        /// <summary>
        /// 全てのトランザクションを取得する
        /// 複合主キー設計により、各(id, entity_id)は一意であるため重複はない
        /// </summary>
        /// <returns>全トランザクションのリスト</returns>
        Task<List<TransactionEntity>> GetAllTransactionsAsync();
        
        /// <summary>
        /// 指定されたトランザクションキーリストに該当しないトランザクションを削除する
        /// 処理されなかった（更新がなかった）トランザクションのクリーンアップを行う
        /// </summary>
        /// <param name="processedTransactionKeys">処理済みのトランザクションキー（ID, EntityID）リスト</param>
        /// <returns>削除された件数</returns>
        Task<int> DeleteUnprocessedTransactionsAsync(List<(int Id, int EntityId)> processedTransactionKeys);
        
        /// <summary>
        /// 指定されたトランザクションキーリストに該当するトランザクションを削除する
        /// 差分がなかったトランザクションのクリーンアップを行う
        /// </summary>
        /// <param name="transactionKeys">削除対象のトランザクションキー（ID, EntityID）リスト</param>
        /// <returns>削除された件数</returns>
        Task<int> DeleteSpecificTransactionsAsync(List<(int Id, int EntityId)> transactionKeys);
    }

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
        /// 新しい最新データを1件登録する
        /// </summary>
        /// <param name="entity">登録対象の最新データエンティティ</param>
        /// <returns>非同期処理タスク</returns>
        Task InsertAsync(LatestDataEntity entity);
        
        /// <summary>
        /// 既存の最新データを1件更新する
        /// </summary>
        /// <param name="entity">更新対象の最新データエンティティ</param>
        /// <returns>非同期処理タスク</returns>
        Task UpdateAsync(LatestDataEntity entity);
        
        /// <summary>
        /// 最新データを一括でUPSERT（新規登録・更新）する
        /// 存在しないものは新規登録、存在するものは更新を行う
        /// </summary>
        /// <param name="entities">処理対象の最新データエンティティリスト</param>
        /// <returns>非同期処理タスク</returns>
        Task BulkUpsertAsync(List<LatestDataEntity> entities);
        
        /// <summary>
        /// 全ての最新データを取得する
        /// </summary>
        /// <returns>全最新データのリスト</returns>
        Task<List<LatestDataEntity>> GetAllAsync();
    }
}
