using CheckDiffTable.Models;
using CheckDiffTable.Repositories;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Data;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace CheckDiffTable.Repositories
{
    /// <summary>
    /// トランザクションテーブルリポジトリの実装（毎回トランケートされる前提）
    /// </summary>
    public class TransactionRepository : ITransactionRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<TransactionRepository> _logger;

        public TransactionRepository(IConfiguration configuration, ILogger<TransactionRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new ArgumentNullException("Connection string is required");
            _logger = logger;
        }

        public async Task<List<TransactionEntity>> GetLatestTransactionsByEntityAsync()
        {
            // 効率化：各エンティティの最新トランザクションのみを取得
            const string sql = @"
                WITH latest_transactions AS (
                    SELECT entity_id, MAX(created_at) as latest_created_at
                    FROM transaction_table 
                    GROUP BY entity_id
                )
                SELECT t.id, t.entity_id, t.name, t.description, t.status, 
                       t.amount, t.transaction_type, t.created_at, t.updated_at
                FROM transaction_table t
                INNER JOIN latest_transactions lt ON t.entity_id = lt.entity_id 
                    AND t.created_at = lt.latest_created_at
                ORDER BY t.entity_id";

            var transactions = new List<TransactionEntity>();

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();
                
                using var command = new NpgsqlCommand(sql, connection);
                using var reader = await command.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    transactions.Add(new TransactionEntity
                    {
                        Id = reader.GetInt32("id"),
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
                
                _logger.LogInformation("Retrieved {Count} latest transactions", transactions.Count);
                return transactions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting latest transactions");
                throw;
            }
        }

        public async Task<TransactionEntity?> GetLatestByEntityIdAsync(int entityId)
        {
            const string sql = @"
                SELECT id, entity_id, name, description, status, amount, transaction_type, 
                       created_at, updated_at
                FROM transaction_table 
                WHERE entity_id = @entityId 
                ORDER BY created_at DESC 
                LIMIT 1";

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();
                
                using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue("@entityId", entityId);
                
                using var reader = await command.ExecuteReaderAsync();
                
                if (await reader.ReadAsync())
                {
                    return new TransactionEntity
                    {
                        Id = reader.GetInt32("id"),
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
                _logger.LogError(ex, "Error getting latest transaction for entity {EntityId}", entityId);
                throw;
            }
        }

        public async Task<List<TransactionEntity>> GetAllAsync()
        {
            const string sql = @"
                SELECT id, entity_id, name, description, status, amount, transaction_type,
                       created_at, updated_at
                FROM transaction_table 
                ORDER BY entity_id, created_at DESC";

            var transactions = new List<TransactionEntity>();

            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();
                
                using var command = new NpgsqlCommand(sql, connection);
                using var reader = await command.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    transactions.Add(new TransactionEntity
                    {
                        Id = reader.GetInt32("id"),
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
                
                return transactions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all transactions");
                throw;
            }
        }
    }
}
