using CheckDiffTable.Models;
using CheckDiffTable.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CheckDiffTable.Services
{
    /// <summary>
    /// 差分チェック・更新サービスのインターフェース
    /// </summary>
    public interface IDiffCheckService
    {
        Task<ProcessResult> ProcessEntityAsync(int entityId);
        Task<BatchProcessResult> ProcessAllEntitiesBatchAsync(int? batchSize = null);
        Task<List<ProcessResult>> ProcessAllEntitiesAsync();
    }

    /// <summary>
    /// 処理結果
    /// </summary>
    public class ProcessResult
    {
        public int EntityId { get; set; }
        public ProcessAction Action { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int? TransactionId { get; set; }
    }

    /// <summary>
    /// 一括処理結果
    /// </summary>
    public class BatchProcessResult
    {
        public int TotalEntities { get; set; }
        public int InsertCount { get; set; }
        public int UpdateCount { get; set; }
        public int SkipCount { get; set; }
        public int ErrorCount { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<ProcessResult> Details { get; set; } = new();
        public TimeSpan ProcessingTime { get; set; }
    }

    /// <summary>
    /// 処理アクション
    /// </summary>
    public enum ProcessAction
    {
        None,       // 処理なし（差分なし）
        Insert,     // 新規登録
        Update,     // 更新
        Error       // エラー
    }

    /// <summary>
    /// 差分チェック・更新サービス（トランザクションテーブルは毎回トランケート前提）
    /// </summary>
    public class DiffCheckService : IDiffCheckService
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly ILatestDataRepository _latestDataRepository;
        private readonly ILogger<DiffCheckService> _logger;
        private readonly BatchProcessingOptions _batchOptions;

        public DiffCheckService(
            ITransactionRepository transactionRepository,
            ILatestDataRepository latestDataRepository,
            ILogger<DiffCheckService> logger,
            IOptions<BatchProcessingOptions> batchOptions)
        {
            _transactionRepository = transactionRepository;
            _latestDataRepository = latestDataRepository;
            _logger = logger;
            _batchOptions = batchOptions.Value;
        }

        public async Task<BatchProcessResult> ProcessAllEntitiesBatchAsync(int? batchSize = null)
        {
            var startTime = DateTime.UtcNow;
            var result = new BatchProcessResult { Success = true };

            try
            {
                // バッチサイズの決定（パラメータ優先、設定値、デフォルト値の順）
                var effectiveBatchSize = batchSize ?? _batchOptions.GetValidatedBatchSize();
                _logger.LogInformation("Starting batch processing with batch size: {BatchSize}", effectiveBatchSize);

                // 1. トランザクションテーブルから最新トランザクションを一括取得（効率化）
                var latestTransactions = await _transactionRepository.GetLatestTransactionsByEntityAsync();
                if (!latestTransactions.Any())
                {
                    _logger.LogInformation("No transactions found");
                    result.Message = "処理対象のトランザクションがありません";
                    result.ProcessingTime = DateTime.UtcNow - startTime;
                    return result;
                }

                result.TotalEntities = latestTransactions.Count;
                _logger.LogInformation("Found {Count} entities with transactions", result.TotalEntities);

                // N+1問題回避：バッチ単位でデータベースアクセスを制御
                // 小さなデータセットの場合は一括処理、大きなデータセットはバッチ分割処理
                if (latestTransactions.Count <= effectiveBatchSize)
                {
                    // 全件一括処理（最も効率的）
                    await ProcessTransactionBatch(latestTransactions, result);
                }
                else
                {
                    // バッチ分割処理（メモリ効率重視）
                    await ProcessTransactionsBatched(latestTransactions, effectiveBatchSize, result);
                }

                result.ProcessingTime = DateTime.UtcNow - startTime;
                result.Message = $"一括処理完了: {result.TotalEntities}エンティティ処理 " +
                               $"(新規:{result.InsertCount}, 更新:{result.UpdateCount}, " +
                               $"スキップ:{result.SkipCount}, エラー:{result.ErrorCount}) " +
                               $"処理時間:{result.ProcessingTime.TotalMilliseconds:F0}ms";

                _logger.LogInformation(result.Message);
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"一括処理中にエラーが発生しました: {ex.Message}";
                result.ProcessingTime = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "Batch processing failed");
                return result;
            }
        }

        /// <summary>
        /// トランザクションをバッチ単位で処理
        /// </summary>
        private async Task ProcessTransactionsBatched(List<TransactionEntity> transactions, int batchSize, BatchProcessResult result)
        {
            for (int i = 0; i < transactions.Count; i += batchSize)
            {
                var batch = transactions.Skip(i).Take(batchSize).ToList();
                _logger.LogInformation("Processing batch {BatchNumber}/{TotalBatches} ({BatchSize} entities)",
                    (i / batchSize) + 1, (int)Math.Ceiling((double)transactions.Count / batchSize), batch.Count);

                await ProcessTransactionBatch(batch, result);
            }
        }

        /// <summary>
        /// 1つのバッチを処理
        /// </summary>
        private async Task ProcessTransactionBatch(List<TransactionEntity> batchTransactions, BatchProcessResult result)
        {
            // 2. 関連する最新データを一括取得（効率化）
            var entityIds = batchTransactions.Select(t => t.EntityId).ToList();
            var existingLatestData = await _latestDataRepository.GetByEntityIdsAsync(entityIds);
            var existingLatestDataDict = existingLatestData.ToDictionary(e => e.EntityId);

            // 3. 差分チェックと処理データ準備
            var toInsert = new List<LatestDataEntity>();
            var toUpdate = new List<LatestDataEntity>();

            foreach (var transaction in batchTransactions)
            {
                var processResult = new ProcessResult
                {
                    EntityId = transaction.EntityId,
                    TransactionId = transaction.Id,
                    Success = true
                };

                try
                {
                    var newLatestData = LatestDataEntity.FromTransaction(transaction);

                    if (existingLatestDataDict.TryGetValue(transaction.EntityId, out var existingData))
                    {
                        // 既存データありの場合：差分チェック
                        if (existingData.HasDifference(newLatestData))
                        {
                            // 差分あり：更新
                            newLatestData.CreatedAt = existingData.CreatedAt; // 作成日時は保持
                            newLatestData.UpdatedAt = DateTime.UtcNow;
                            toUpdate.Add(newLatestData);

                            processResult.Action = ProcessAction.Update;
                            processResult.Message = "データ更新";
                            result.UpdateCount++;
                        }
                        else
                        {
                            // 差分なし：スキップ
                            processResult.Action = ProcessAction.None;
                            processResult.Message = "差分なし - スキップ";
                            result.SkipCount++;
                        }
                    }
                    else
                    {
                        // 既存データなし：新規登録
                        toInsert.Add(newLatestData);

                        processResult.Action = ProcessAction.Insert;
                        processResult.Message = "新規データ登録";
                        result.InsertCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing transaction {TransactionId} for entity {EntityId}",
                        transaction.Id, transaction.EntityId);

                    processResult.Action = ProcessAction.Error;
                    processResult.Success = false;
                    processResult.Message = $"処理エラー: {ex.Message}";
                    result.ErrorCount++;
                }

                result.Details.Add(processResult);
            }

            // 4. 一括データベース操作（効率化）
            if (toInsert.Any() || toUpdate.Any())
            {
                var allToUpsert = toInsert.Concat(toUpdate).ToList();
                await _latestDataRepository.BulkUpsertAsync(allToUpsert);
                _logger.LogInformation("Bulk upserted {Count} records ({Insert} inserts, {Update} updates)",
                    allToUpsert.Count, toInsert.Count, toUpdate.Count);
            }
        }

        public async Task<ProcessResult> ProcessEntityAsync(int entityId)
        {
            try
            {
                _logger.LogInformation("Processing entity {EntityId}", entityId);

                // トランザクションテーブルから最新データを取得
                var latestTransaction = await _transactionRepository.GetLatestByEntityIdAsync(entityId);
                if (latestTransaction == null)
                {
                    _logger.LogWarning("No transaction found for entity {EntityId}", entityId);
                    return new ProcessResult
                    {
                        EntityId = entityId,
                        Action = ProcessAction.None,
                        Success = true,
                        Message = "トランザクションデータが見つかりません"
                    };
                }

                // 最新データテーブルから既存データを取得
                var existingLatest = await _latestDataRepository.GetByEntityIdAsync(entityId);

                // トランザクションデータを最新データエンティティに変換
                var newLatestData = LatestDataEntity.FromTransaction(latestTransaction);

                if (existingLatest == null)
                {
                    // データが存在しない場合：新規登録
                    await _latestDataRepository.InsertAsync(newLatestData);

                    _logger.LogInformation("Inserted new data for entity {EntityId}", entityId);

                    return new ProcessResult
                    {
                        EntityId = entityId,
                        Action = ProcessAction.Insert,
                        Success = true,
                        Message = "新規データを登録しました",
                        TransactionId = latestTransaction.Id
                    };
                }

                // 差分チェック
                if (existingLatest.HasDifference(newLatestData))
                {
                    // 差分がある場合：更新
                    newLatestData.CreatedAt = existingLatest.CreatedAt; // 作成日時は保持
                    newLatestData.UpdatedAt = DateTime.UtcNow; // 更新日時を現在時刻に設定

                    await _latestDataRepository.UpdateAsync(newLatestData);

                    _logger.LogInformation("Updated data for entity {EntityId}", entityId);

                    return new ProcessResult
                    {
                        EntityId = entityId,
                        Action = ProcessAction.Update,
                        Success = true,
                        Message = "データを更新しました",
                        TransactionId = latestTransaction.Id
                    };
                }
                else
                {
                    // 差分がない場合：処理なし
                    _logger.LogInformation("No difference found for entity {EntityId}", entityId);

                    return new ProcessResult
                    {
                        EntityId = entityId,
                        Action = ProcessAction.None,
                        Success = true,
                        Message = "差分なし - 処理をスキップしました",
                        TransactionId = latestTransaction.Id
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing entity {EntityId}", entityId);

                return new ProcessResult
                {
                    EntityId = entityId,
                    Action = ProcessAction.Error,
                    Success = false,
                    Message = $"処理中にエラーが発生しました: {ex.Message}"
                };
            }
        }

        public async Task<List<ProcessResult>> ProcessAllEntitiesAsync()
        {
            var results = new List<ProcessResult>();

            try
            {
                _logger.LogInformation("Starting individual processing of all entities");

                // トランザクションテーブルから最新トランザクションを一括取得
                var latestTransactions = await _transactionRepository.GetLatestTransactionsByEntityAsync();
                if (!latestTransactions.Any())
                {
                    _logger.LogInformation("No transactions found");
                    return results;
                }

                _logger.LogInformation("Found {Count} entities with transactions", latestTransactions.Count);

                // 各エンティティを個別に処理
                foreach (var transaction in latestTransactions)
                {
                    var result = await ProcessEntityAsync(transaction.EntityId);
                    results.Add(result);
                }

                var summary = results.GroupBy(r => r.Action).ToDictionary(g => g.Key, g => g.Count());
                _logger.LogInformation("Individual processing completed. Results: {Summary}",
                    string.Join(", ", summary.Select(kvp => $"{kvp.Key}:{kvp.Value}")));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in individual processing");
            }

            return results;
        }
    }
}
