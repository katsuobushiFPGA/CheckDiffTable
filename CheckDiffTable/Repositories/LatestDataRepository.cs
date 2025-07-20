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

namespace CheckDiffTable.Repositories
{
    /// <summary>
    /// 最新データテーブルリポジトリの実装
    /// </summary>
    public class LatestDataRepository : ILatestDataRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<LatestDataRepository> _logger;

        public LatestDataRepository(IConfiguration configuration, ILogger<LatestDataRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new ArgumentNullException("Connection string is required");
            _logger = logger;
        }

        public async Task<LatestDataEntity?> GetByEntityIdAsync(int entityId)
        {
            const string sql = @"
                SELECT entity_id, name, description, status, amount, transaction_type, 
                       created_at, updated_at
                FROM latest_data_table 
                WHERE entity_id = @entityId";

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();
                
                using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue("@entityId", entityId);
                
                using var reader = await command.ExecuteReaderAsync();
                
                if (await reader.ReadAsync())
                {
                    return new LatestDataEntity
                    {
                        EntityId = reader.GetInt32("entity_id"),
                        Name = reader.GetString("name"),
                        Description = reader.IsDBNull("description") ? null : reader.GetString("description"),
                        Status = reader.GetString("status"),
                        Amount = reader.GetDecimal("amount"),
                        TransactionType = reader.GetString("transaction_type"),
                        CreatedAt = reader.GetDateTime("created_at"),
                        UpdatedAt = reader.GetDateTime("updated_at")
                    };
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting latest data for entity {EntityId}", entityId);
                throw;
            }
        }

        public async Task<List<LatestDataEntity>> GetByEntityIdsAsync(List<int> entityIds)
        {
            if (!entityIds.Any()) return new List<LatestDataEntity>();

            const string sql = @"
                SELECT entity_id, name, description, status, amount, transaction_type,
                       created_at, updated_at
                FROM latest_data_table 
                WHERE entity_id = ANY(@entityIds)";

            var entities = new List<LatestDataEntity>();

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();
                
                using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue("@entityIds", entityIds.ToArray());
                
                using var reader = await command.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    entities.Add(new LatestDataEntity
                    {
                        EntityId = reader.GetInt32("entity_id"),
                        Name = reader.GetString("name"),
                        Description = reader.IsDBNull("description") ? null : reader.GetString("description"),
                        Status = reader.GetString("status"),
                        Amount = reader.GetDecimal("amount"),
                        TransactionType = reader.GetString("transaction_type"),
                        CreatedAt = reader.GetDateTime("created_at"),
                        UpdatedAt = reader.GetDateTime("updated_at")
                    });
                }
                
                return entities;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting latest data for entity IDs");
                throw;
            }
        }

        public async Task InsertAsync(LatestDataEntity entity)
        {
            const string sql = @"
                INSERT INTO latest_data_table 
                (entity_id, name, description, status, amount, transaction_type, 
                 created_at, updated_at)
                VALUES (@entityId, @name, @description, @status, @amount, @transactionType, 
                        @createdAt, @updatedAt)";

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();
                
                using var command = new NpgsqlCommand(sql, connection);
                AddParameters(command, entity);
                
                await command.ExecuteNonQueryAsync();
                _logger.LogInformation("Inserted new latest data for entity {EntityId}", entity.EntityId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting latest data for entity {EntityId}", entity.EntityId);
                throw;
            }
        }

        public async Task UpdateAsync(LatestDataEntity entity)
        {
            const string sql = @"
                UPDATE latest_data_table 
                SET name = @name, 
                    description = @description, 
                    status = @status,
                    amount = @amount,
                    transaction_type = @transactionType,
                    updated_at = @updatedAt
                WHERE entity_id = @entityId";

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();
                
                using var command = new NpgsqlCommand(sql, connection);
                AddParameters(command, entity);
                
                var rowsAffected = await command.ExecuteNonQueryAsync();
                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Updated latest data for entity {EntityId}", entity.EntityId);
                }
                else
                {
                    _logger.LogWarning("No rows updated for entity {EntityId}", entity.EntityId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating latest data for entity {EntityId}", entity.EntityId);
                throw;
            }
        }

        public async Task BulkUpsertAsync(List<LatestDataEntity> entities)
        {
            if (!entities.Any()) return;

            // PostgreSQLのUPSERT構文を使用した一括処理
            var sql = new StringBuilder(@"
                INSERT INTO latest_data_table 
                (entity_id, name, description, status, amount, transaction_type, 
                 created_at, updated_at) 
                VALUES ");

            var parameters = new List<NpgsqlParameter>();
            var valuesClauses = new List<string>();

            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                
                valuesClauses.Add($"(@entityId{i}, @name{i}, @description{i}, @status{i}, @amount{i}, @transactionType{i}, @createdAt{i}, @updatedAt{i})");
                
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
            sql.Append(@"
                ON CONFLICT (entity_id) 
                DO UPDATE SET 
                    name = EXCLUDED.name,
                    description = EXCLUDED.description,
                    status = EXCLUDED.status,
                    amount = EXCLUDED.amount,
                    transaction_type = EXCLUDED.transaction_type,
                    updated_at = EXCLUDED.updated_at");

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

        public async Task<List<LatestDataEntity>> GetAllAsync()
        {
            const string sql = @"
                SELECT entity_id, name, description, status, amount, transaction_type,
                       created_at, updated_at
                FROM latest_data_table 
                ORDER BY entity_id";

            var entities = new List<LatestDataEntity>();

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();
                
                using var command = new NpgsqlCommand(sql, connection);
                using var reader = await command.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    entities.Add(new LatestDataEntity
                    {
                        EntityId = reader.GetInt32("entity_id"),
                        Name = reader.GetString("name"),
                        Description = reader.IsDBNull("description") ? null : reader.GetString("description"),
                        Status = reader.GetString("status"),
                        Amount = reader.GetDecimal("amount"),
                        TransactionType = reader.GetString("transaction_type"),
                        CreatedAt = reader.GetDateTime("created_at"),
                        UpdatedAt = reader.GetDateTime("updated_at")
                    });
                }
                
                return entities;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all latest data entities");
                throw;
            }
        }

        private static void AddParameters(NpgsqlCommand command, LatestDataEntity entity)
        {
            command.Parameters.AddWithValue("@entityId", entity.EntityId);
            command.Parameters.AddWithValue("@name", entity.Name);
            command.Parameters.AddWithValue("@description", (object?)entity.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("@status", entity.Status);
            command.Parameters.AddWithValue("@amount", entity.Amount);
            command.Parameters.AddWithValue("@transactionType", entity.TransactionType);
            command.Parameters.AddWithValue("@createdAt", entity.CreatedAt);
            command.Parameters.AddWithValue("@updatedAt", entity.UpdatedAt);
        }
    }
}
