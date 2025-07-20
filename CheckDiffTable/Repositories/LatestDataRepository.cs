using CheckDiffTable.Models;
using CheckDiffTable.Repositories;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using static CheckDiffTable.Models.DatabaseConstants;

namespace CheckDiffTable.Repositories
{
    /// <summary>
    /// 最新データテーブルリポジトリの実装クラス
    /// PostgreSQLデータベースでの最新データの管理（取得、登録、更新、一括操作）を提供
    /// </summary>
    public class LatestDataRepository : ILatestDataRepository
    {
        /// <summary>データベース接続文字列</summary>
        private readonly string _connectionString;
        
        /// <summary>ログ出力用インスタンス</summary>
        private readonly ILogger<LatestDataRepository> _logger;

        /// <summary>
        /// LatestDataRepositoryのコンストラクタ
        /// 依存関係の注入により設定とログを受け取る
        /// </summary>
        /// <param name="configuration">アプリケーション設定</param>
        /// <param name="logger">ログ出力用インスタンス</param>
        /// <exception cref="ArgumentNullException">接続文字列が設定されていない場合</exception>
        public LatestDataRepository(IConfiguration configuration, ILogger<LatestDataRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new ArgumentNullException("Connection string is required");
            _logger = logger;
        }

        /// <summary>
        /// 複合主キー（id, entity_id）のリストに対応する最新データを一括取得する
        /// N+1問題を回避するため、PostgreSQLのROW構文を使用して一度のクエリで取得
        /// </summary>
        /// <param name="transactionKeys">取得対象の複合主キー（id, entity_id）のリスト</param>
        /// <returns>該当する最新データのリスト</returns>
        public async Task<List<LatestDataEntity>> GetByTransactionKeysAsync(List<(int Id, int EntityId)> transactionKeys)
        {
            if (!transactionKeys.Any()) return new List<LatestDataEntity>();

            // ROW構文とIN句を使った複合主キー検索（PostgreSQL標準の方法）
            var rowValues = string.Join(", ", transactionKeys.Select((_, index) => $"(@id{index}, @entityId{index})"));
            
            var sql = $@"
                SELECT {LatestDataTable.Id}, 
                       {LatestDataTable.EntityId}, 
                       {LatestDataTable.Name}, 
                       {LatestDataTable.Description}, 
                       {LatestDataTable.Status}, 
                       {LatestDataTable.Amount}, 
                       {LatestDataTable.TransactionType},
                       {LatestDataTable.CreatedAt}, 
                       {LatestDataTable.UpdatedAt}
                FROM {LatestDataTable.TableName} 
                WHERE ({LatestDataTable.Id}, {LatestDataTable.EntityId}) IN ({rowValues})";

            var entities = new List<LatestDataEntity>();

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();
                
                using var command = new NpgsqlCommand(sql, connection);
                
                // 個別パラメータで型安全に渡す
                for (int i = 0; i < transactionKeys.Count; i++)
                {
                    var key = transactionKeys[i];
                    command.Parameters.AddWithValue($"@id{i}", key.Id);
                    command.Parameters.AddWithValue($"@entityId{i}", key.EntityId);
                }
                
                using var reader = await command.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    entities.Add(new LatestDataEntity
                    {
                        Id = reader.GetInt32(DatabaseConstants.LatestDataTable.Id),
                        EntityId = reader.GetInt32(DatabaseConstants.LatestDataTable.EntityId),
                        Name = reader.GetString(DatabaseConstants.LatestDataTable.Name),
                        Description = reader.IsDBNull(DatabaseConstants.LatestDataTable.Description) 
                            ? null : reader.GetString(DatabaseConstants.LatestDataTable.Description),
                        Status = reader.GetString(DatabaseConstants.LatestDataTable.Status),
                        Amount = reader.GetDecimal(DatabaseConstants.LatestDataTable.Amount),
                        TransactionType = reader.GetString(DatabaseConstants.LatestDataTable.TransactionType),
                        CreatedAt = reader.GetDateTime(DatabaseConstants.LatestDataTable.CreatedAt),
                        UpdatedAt = reader.GetDateTime(DatabaseConstants.LatestDataTable.UpdatedAt)
                    });
                }
                
                return entities;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting latest data for transaction keys");
                throw;
            }
        }

        /// <summary>
        /// 最新データを一括でUPSERT（新規登録・更新）する
        /// PostgreSQLのON CONFLICT機能を使用して高効率な一括処理を実現
        /// 存在しないものは新規登録、存在するものは更新を行う
        /// </summary>
        /// <param name="entities">処理対象の最新データエンティティリスト</param>
        /// <returns>非同期処理タスク</returns>
        public async Task BulkUpsertAsync(List<LatestDataEntity> entities)
        {
            if (!entities.Any()) return;

            // PostgreSQLのUPSERT構文を使用した一括処理（複合主キー対応）
            var sql = new StringBuilder($@"
                INSERT INTO {DatabaseConstants.LatestDataTable.TableName} 
                ({DatabaseConstants.LatestDataTable.Id}, {DatabaseConstants.LatestDataTable.EntityId}, 
                 {DatabaseConstants.LatestDataTable.Name}, {DatabaseConstants.LatestDataTable.Description}, 
                 {DatabaseConstants.LatestDataTable.Status}, {DatabaseConstants.LatestDataTable.Amount}, 
                 {DatabaseConstants.LatestDataTable.TransactionType}, {DatabaseConstants.LatestDataTable.CreatedAt}, 
                 {DatabaseConstants.LatestDataTable.UpdatedAt}) 
                VALUES ");

            var parameters = new List<NpgsqlParameter>();
            var valuesClauses = new List<string>();

            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                
                valuesClauses.Add($"(@id{i}, @entityId{i}, @name{i}, @description{i}, @status{i}, @amount{i}, @transactionType{i}, @createdAt{i}, @updatedAt{i})");
                
                parameters.Add(new NpgsqlParameter($"@id{i}", entity.Id));
                parameters.Add(new NpgsqlParameter($"@entityId{i}", entity.EntityId));
                parameters.Add(new NpgsqlParameter($"@name{i}", entity.Name));
                parameters.Add(new NpgsqlParameter($"@description{i}", (object?)entity.Description ?? DBNull.Value));
                parameters.Add(new NpgsqlParameter($"@status{i}", entity.Status));
                parameters.Add(new NpgsqlParameter($"@amount{i}", entity.Amount));
                parameters.Add(new NpgsqlParameter($"@transactionType{i}", entity.TransactionType));
                parameters.Add(new NpgsqlParameter($"@createdAt{i}", entity.CreatedAt));
                parameters.Add(new NpgsqlParameter($"@updatedAt{i}", entity.UpdatedAt));
            }

            sql.Append(string.Join(", ", valuesClauses));
            sql.Append($@"
                ON CONFLICT ({DatabaseConstants.LatestDataTable.Id}, {DatabaseConstants.LatestDataTable.EntityId}) 
                DO UPDATE SET 
                    {DatabaseConstants.LatestDataTable.Name} = EXCLUDED.{DatabaseConstants.LatestDataTable.Name},
                    {DatabaseConstants.LatestDataTable.Description} = EXCLUDED.{DatabaseConstants.LatestDataTable.Description},
                    {DatabaseConstants.LatestDataTable.Status} = EXCLUDED.{DatabaseConstants.LatestDataTable.Status},
                    {DatabaseConstants.LatestDataTable.Amount} = EXCLUDED.{DatabaseConstants.LatestDataTable.Amount},
                    {DatabaseConstants.LatestDataTable.TransactionType} = EXCLUDED.{DatabaseConstants.LatestDataTable.TransactionType},
                    {DatabaseConstants.LatestDataTable.UpdatedAt} = EXCLUDED.{DatabaseConstants.LatestDataTable.UpdatedAt}");

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();
                
                using var command = new NpgsqlCommand(sql.ToString(), connection);
                command.Parameters.AddRange(parameters.ToArray());
                
                var rowsAffected = await command.ExecuteNonQueryAsync();
                _logger.LogInformation("Bulk upserted {Count} entities, {RowsAffected} rows affected", entities.Count, rowsAffected);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk upserting {Count} entities", entities.Count);
                throw;
            }
        }
    }
}
