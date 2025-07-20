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
    /// PostgreSQLデータベースからトランザクションデータを読み取る機能を提供
    /// </summary>
    public class TransactionRepository : ITransactionRepository
    {
        /// <summary>データベース接続文字列</summary>
        private readonly string _connectionString;
        
        /// <summary>ログ出力用インスタンス</summary>
        private readonly ILogger<TransactionRepository> _logger;

        /// <summary>
        /// TransactionRepositoryのコンストラクタ
        /// 依存関係の注入により設定とログを受け取る
        /// </summary>
        /// <param name="configuration">アプリケーション設定</param>
        /// <param name="logger">ログ出力用インスタンス</param>
        /// <exception cref="ArgumentNullException">接続文字列が設定されていない場合</exception>
        public TransactionRepository(IConfiguration configuration, ILogger<TransactionRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new ArgumentNullException("Connection string is required");
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
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();
                
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
                
                _logger.LogInformation("Retrieved {Count} transactions", transactions.Count);
                return transactions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transactions");
                throw;
            }
        }

        /// <summary>
        /// 指定されたトランザクションキーリストに該当しないトランザクションを削除する
        /// 処理されなかった（更新がなかった）トランザクションのクリーンアップを行う
        /// </summary>
        /// <param name="processedTransactionKeys">処理済みのトランザクションキー（ID, EntityID）リスト</param>
        /// <returns>削除された件数</returns>
        public async Task<int> DeleteUnprocessedTransactionsAsync(List<(int Id, int EntityId)> processedTransactionKeys)
        {
            if (!processedTransactionKeys.Any())
            {
                // 処理済みトランザクションがない場合は、全件削除
                var deleteAllSql = $"DELETE FROM {TransactionTable.TableName}";
                
                try
                {
                    using var connection = new NpgsqlConnection(_connectionString);
                    await connection.OpenAsync();
                    
                    using var command = new NpgsqlCommand(deleteAllSql, connection);
                    var deletedCount = await command.ExecuteNonQueryAsync();
                    
                    _logger.LogInformation("Deleted all {Count} unprocessed transactions", deletedCount);
                    return deletedCount;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting all unprocessed transactions");
                    throw;
                }
            }

            // 処理済みトランザクションキー以外を削除
            var sql = $@"
                DELETE FROM {TransactionTable.TableName} 
                WHERE ({TransactionTable.Id}, {TransactionTable.EntityId}) NOT IN (
                    SELECT unnest(@ids), unnest(@entityIds)
                )";

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();
                
                using var command = new NpgsqlCommand(sql, connection);
                
                // パラメータ配列を準備
                var ids = processedTransactionKeys.Select(k => k.Id).ToArray();
                var entityIds = processedTransactionKeys.Select(k => k.EntityId).ToArray();
                
                command.Parameters.AddWithValue("@ids", ids);
                command.Parameters.AddWithValue("@entityIds", entityIds);
                
                var deletedCount = await command.ExecuteNonQueryAsync();
                
                _logger.LogInformation("Deleted {DeletedCount} unprocessed transactions (kept {ProcessedCount} processed transactions)", 
                    deletedCount, processedTransactionKeys.Count);
                
                return deletedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting unprocessed transactions");
                throw;
            }
        }
    }
}
