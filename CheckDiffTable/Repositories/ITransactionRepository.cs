using CheckDiffTable.Models;
using Npgsql;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CheckDiffTable.Repositories
{
    /// <summary>
    /// トランザクションテーブルリポジトリのインターフェース
    /// トランザクションデータの読み取り専用操作
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
        /// 指定されたデータベーストランザクション内で、トランザクションキーリストに該当するトランザクションを削除する
        /// 差分がなかったトランザクションのクリーンアップを行う
        /// </summary>
        /// <param name="transactionKeys">削除対象のトランザクションキー（ID, EntityID）リスト</param>
        /// <param name="transaction">使用するデータベーストランザクション</param>
        /// <returns>削除された件数</returns>
        Task<int> DeleteSpecificTransactionsWithTransactionAsync(List<(int Id, int EntityId)> transactionKeys, NpgsqlTransaction transaction);
    }
}
