using CheckDiffTable.Models;
using CheckDiffTable.Repositories;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Data;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using static CheckDiffTable.Models.DatabaseConstants;

namespace CheckDiffTable.Repositories
{
    /// <summary>
    /// トランザクションテーブルリポジトリの実装クラス
    /// PostgreSQLデータベースからトランザクションデータを読み取る
    /// </summary>
    public class TransactionRepository : ITransactionRepository
    {
        /// <summary>データベースデータソース（接続プール管理）</summary>
        private readonly NpgsqlDataSource _dataSource;
        
        /// <summary>ログ出力用インスタンス</summary>
        private readonly ILogger<TransactionRepository> _logger;

        /// <summary>
        /// TransactionRepositoryのコンストラクタ
        /// 依存関係の注入によりデータソースとログを受け取る
        /// </summary>
        /// <param name="dataSource">PostgreSQLデータソース</param>
        /// <param name="logger">ログ出力用インスタンス</param>
        public TransactionRepository(NpgsqlDataSource dataSource, ILogger<TransactionRepository> logger)
        {
            _dataSource = dataSource;
            _logger = logger;
        }

        /// <summary>
        /// 全てのトランザクションを取得する
        /// 複合主キー設計により、各(id, entity_id)は一意であるため重複はない
        /// </summary>
        /// <returns>全トランザクションのリスト</returns>
        public async Task<List<TransactionEntity>> GetAllTransactionsAsync()
        {
            var sql = $@"
                SELECT {TransactionTable.Id}, {TransactionTable.EntityId}, {TransactionTable.Name}, 
                       {TransactionTable.Description}, {TransactionTable.Status}, {TransactionTable.Amount}, 
                       {TransactionTable.TransactionType}, {TransactionTable.CreatedAt}, {TransactionTable.UpdatedAt}
                FROM {TransactionTable.TableName} 
                ORDER BY {TransactionTable.EntityId}, {TransactionTable.Id}";

            var transactions = new List<TransactionEntity>();

            try
            {
                using var connection = await _dataSource.OpenConnectionAsync();
                
                using var command = new NpgsqlCommand(sql, connection);
                using var reader = await command.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    // データベースの各行をTransactionEntityオブジェクトに変換
                    transactions.Add(new TransactionEntity
                    {
                        Id = reader.GetInt32(TransactionTable.Id),
                        EntityId = reader.GetInt32(TransactionTable.EntityId),
                        Name = reader.GetString(TransactionTable.Name),
                        Description = reader.IsDBNull(TransactionTable.Description) ? null : reader.GetString(TransactionTable.Description),
                        Status = reader.GetString(TransactionTable.Status),
                        Amount = reader.GetDecimal(TransactionTable.Amount),
                        TransactionType = reader.GetString(TransactionTable.TransactionType),
                        CreatedAt = reader.GetDateTime(TransactionTable.CreatedAt),
                        UpdatedAt = reader.GetDateTime(TransactionTable.UpdatedAt)
                    });
                }
                
                _logger.LogInformation("トランザクション取得完了: {Count}件", transactions.Count);
                return transactions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "トランザクション取得でエラーが発生しました");
                throw;
            }
        }

        /// <summary>
        /// 指定されたデータベーストランザクション内で、トランザクションキーリストに該当するトランザクションを削除する
        /// 差分がなかったトランザクションのクリーンアップを行う
        /// </summary>
        /// <param name="transactionKeys">削除対象のトランザクションキー（ID, EntityID）リスト</param>
        /// <param name="dbTransaction">使用するデータベーストランザクション</param>
        /// <returns>削除された件数</returns>
        public async Task<int> DeleteSpecificTransactionsWithTransactionAsync(List<(int Id, int EntityId)> transactionKeys, NpgsqlTransaction dbTransaction)
        {
            if (!transactionKeys.Any())
            {
                _logger.LogInformation("削除対象がありません");
                return 0;
            }

            // ROW構文とIN句を使った複合主キー削除
            var rowValues = string.Join(", ", transactionKeys.Select((_, index) => $"(@id{index}, @entityId{index})"));
            
            var sql = $@"
                DELETE FROM {TransactionTable.TableName} 
                WHERE ({TransactionTable.Id}, {TransactionTable.EntityId}) IN ({rowValues})";

            try
            {
                using var command = new NpgsqlCommand(sql, dbTransaction.Connection, dbTransaction);
                
                // 個別パラメータで型安全に渡す
                for (int i = 0; i < transactionKeys.Count; i++)
                {
                    var key = transactionKeys[i];
                    command.Parameters.AddWithValue($"@id{i}", key.Id);
                    command.Parameters.AddWithValue($"@entityId{i}", key.EntityId);
                }
                
                var deletedCount = await command.ExecuteNonQueryAsync();
                
                _logger.LogInformation("削除完了: {DeletedCount}件", deletedCount);
                
                return deletedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "削除でエラーが発生しました");
                throw;
            }
        }
    }
}
