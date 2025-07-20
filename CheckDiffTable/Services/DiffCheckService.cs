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
        /// <summary>
        /// 全エンティティをバッチ処理で効率的に差分チェック・更新を行う
        /// </summary>
        /// <param name="batchSize">バッチサイズ。未指定の場合は設定値またはデフォルト値を使用</param>
        /// <returns>処理結果（成功/失敗、件数、詳細情報を含む）</returns>
        Task<BatchProcessResult> ProcessAllEntitiesBatchAsync(int? batchSize = null);
    }

    /// <summary>
    /// 個別エンティティの処理結果を格納するクラス
    /// </summary>
    public class ProcessResult
    {
        /// <summary>エンティティID</summary>
        public int EntityId { get; set; }
        
        /// <summary>実行されたアクション（新規登録、更新、スキップ、エラー）</summary>
        public ProcessAction Action { get; set; }
        
        /// <summary>処理の成功/失敗フラグ</summary>
        public bool Success { get; set; }
        
        /// <summary>処理結果の詳細メッセージ</summary>
        public string? Message { get; set; }
        
        /// <summary>関連するトランザクションID</summary>
        public int? TransactionId { get; set; }
    }

    /// <summary>
    /// バッチ処理全体の結果を格納するクラス
    /// </summary>
    public class BatchProcessResult
    {
        /// <summary>処理対象の総エンティティ数</summary>
        public int TotalEntities { get; set; }
        
        /// <summary>新規登録された件数</summary>
        public int InsertCount { get; set; }
        
        /// <summary>更新された件数</summary>
        public int UpdateCount { get; set; }
        
        /// <summary>差分なしでスキップされた件数</summary>
        public int SkipCount { get; set; }
        
        /// <summary>エラーが発生した件数</summary>
        public int ErrorCount { get; set; }
        
        /// <summary>削除された未処理トランザクション件数</summary>
        public int DeletedCount { get; set; }
        
        /// <summary>バッチ処理全体の成功/失敗フラグ</summary>
        public bool Success { get; set; }
        
        /// <summary>処理結果のサマリーメッセージ</summary>
        public string? Message { get; set; }
        
        /// <summary>各エンティティの詳細処理結果リスト</summary>
        public List<ProcessResult> Details { get; set; } = new();
        
        /// <summary>処理にかかった時間</summary>
        public TimeSpan ProcessingTime { get; set; }
    }

    /// <summary>
    /// エンティティに対して実行されたアクションの種類を表す列挙型
    /// </summary>
    public enum ProcessAction
    {
        /// <summary>処理なし（データに差分がないためスキップ）</summary>
        None,
        
        /// <summary>新規登録（既存データが存在しない）</summary>
        Insert,
        
        /// <summary>更新（既存データと差分があるため更新）</summary>
        Update,
        
        /// <summary>エラー（処理中に例外が発生）</summary>
        Error
    }

    /// <summary>
    /// 差分チェック・更新サービスの実装クラス
    /// トランザクションテーブルと最新データテーブル間の差分をチェックし、効率的な一括処理を提供
    /// </summary>
    public class DiffCheckService : IDiffCheckService
    {
        /// <summary>トランザクションデータアクセス用リポジトリ</summary>
        private readonly ITransactionRepository _transactionRepository;
        
        /// <summary>最新データテーブルアクセス用リポジトリ</summary>
        private readonly ILatestDataRepository _latestDataRepository;
        
        /// <summary>ログ出力用インスタンス</summary>
        private readonly ILogger<DiffCheckService> _logger;
        
        /// <summary>バッチ処理設定オプション</summary>
        private readonly BatchProcessingOptions _batchOptions;

        /// <summary>
        /// DiffCheckServiceのコンストラクタ
        /// 依存関係の注入により、リポジトリ、ログ、設定オプションを受け取る
        /// </summary>
        /// <param name="transactionRepository">トランザクションデータアクセス用リポジトリ</param>
        /// <param name="latestDataRepository">最新データテーブルアクセス用リポジトリ</param>
        /// <param name="logger">ログ出力用インスタンス</param>
        /// <param name="batchOptions">バッチ処理設定オプション</param>
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

        /// <summary>
        /// 全エンティティをバッチ処理で効率的に差分チェック・更新を行う
        /// トランザクションテーブルから最新データを取得し、最新データテーブルと比較して差分があるものを一括更新する
        /// </summary>
        /// <param name="batchSize">バッチサイズ。未指定の場合は設定値またはデフォルト値を使用</param>
        /// <returns>処理結果（成功/失敗、件数、詳細情報を含む）</returns>
        public async Task<BatchProcessResult> ProcessAllEntitiesBatchAsync(int? batchSize = null)
        {
            var startTime = DateTime.UtcNow;
            var result = new BatchProcessResult { Success = true };

            try
            {
                // バッチサイズの決定（パラメータ優先、設定値、デフォルト値の順）
                var effectiveBatchSize = batchSize ?? _batchOptions.GetValidatedBatchSize();
                _logger.LogInformation("Starting batch processing with batch size: {BatchSize}", effectiveBatchSize);

                // 1. トランザクションテーブルから全てのトランザクションを一括取得（効率化）
                // N+1問題を回避するため、個別のクエリではなく一括取得を行う
                var latestTransactions = await _transactionRepository.GetAllTransactionsAsync();
                if (!latestTransactions.Any())
                {
                    _logger.LogInformation("No transactions found");
                    result.Message = "処理対象のトランザクションがありません";
                    result.ProcessingTime = DateTime.UtcNow - startTime;
                    return result;
                }

                result.TotalEntities = latestTransactions.Count;
                _logger.LogInformation("Found {Count} entities with transactions", result.TotalEntities);

                // データサイズに応じて処理方法を選択
                // N+1問題回避：バッチ単位でデータベースアクセスを制御
                // 小さなデータセットの場合は一括処理、大きなデータセットはバッチ分割処理
                if (latestTransactions.Count <= effectiveBatchSize)
                {
                    // 全件一括処理（最も効率的）
                    // メモリ使用量が許容範囲内の場合、最も高速な処理を実行
                    await ProcessTransactionBatch(latestTransactions, result);
                }
                else
                {
                    // バッチ分割処理（メモリ効率重視）
                    // 大量データの場合、メモリ使用量を抑えるためバッチ単位で処理
                    await ProcessTransactionsBatched(latestTransactions, effectiveBatchSize, result);
                }

                // 6. 未処理トランザクションの削除
                // 処理されたトランザクションキーを収集
                var processedTransactionKeys = latestTransactions
                    .Where(t => result.Details.Any(d => d.TransactionId == t.Id && d.EntityId == t.EntityId))
                    .Select(t => (t.Id, t.EntityId))
                    .ToList();

                var deletedCount = await _transactionRepository.DeleteUnprocessedTransactionsAsync(processedTransactionKeys);
                result.DeletedCount = deletedCount;
                _logger.LogInformation("Cleaned up {DeletedCount} unprocessed transactions from transaction table", deletedCount);

                // 処理完了：結果サマリーの生成とログ出力
                result.ProcessingTime = DateTime.UtcNow - startTime;
                result.Message = $"一括処理完了: {result.TotalEntities}エンティティ処理 " +
                               $"(新規:{result.InsertCount}, 更新:{result.UpdateCount}, " +
                               $"スキップ:{result.SkipCount}, エラー:{result.ErrorCount}) " +
                               $"削除:{deletedCount}件 " +
                               $"処理時間:{result.ProcessingTime.TotalMilliseconds:F0}ms";

                _logger.LogInformation(result.Message);
                return result;
            }
            catch (Exception ex)
            {
                // 予期しないエラーが発生した場合の処理
                result.Success = false;
                result.Message = $"一括処理中にエラーが発生しました: {ex.Message}";
                result.ProcessingTime = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "Batch processing failed");
                return result;
            }
        }

        /// <summary>
        /// トランザクションをバッチ単位で処理する
        /// 大量データを効率的に処理するため、指定されたバッチサイズごとに分割して処理を行う
        /// </summary>
        /// <param name="transactions">処理対象のトランザクションリスト</param>
        /// <param name="batchSize">1回の処理で扱うトランザクション数</param>
        /// <param name="result">処理結果を蓄積するオブジェクト</param>
        /// <returns>非同期処理タスク</returns>
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
        /// 1つのバッチ内のトランザクションを処理する
        /// 既存データとの差分チェックを行い、新規登録・更新・スキップの判定を行う
        /// データベースアクセスを最小限に抑えるため、バッチ内の全てのデータを一括取得・一括更新する
        /// </summary>
        /// <param name="batchTransactions">処理対象のトランザクションバッチ</param>
        /// <param name="result">処理結果を蓄積するオブジェクト</param>
        /// <returns>非同期処理タスク</returns>
        private async Task ProcessTransactionBatch(List<TransactionEntity> batchTransactions, BatchProcessResult result)
        {
            // 2. 関連する最新データを一括取得（効率化）
            // バッチ内の全（id, entity_id）複合主キーに対する既存データを一括取得し、N+1問題を回避
            var transactionKeys = batchTransactions.Select(t => (t.Id, t.EntityId)).ToList();
            var existingLatestData = await _latestDataRepository.GetByTransactionKeysAsync(transactionKeys);
            // 高速検索のため複合主キーでDictionary形式に変換
            var existingLatestDataDict = existingLatestData.ToDictionary(e => (e.Id, e.EntityId));

            // 3. 差分チェックと処理データ準備
            // 各トランザクションに対して既存データとの差分をチェックし、
            // 新規登録・更新・スキップの分類を行う
            var toInsert = new List<LatestDataEntity>();
            var toUpdate = new List<LatestDataEntity>();

            foreach (var transaction in batchTransactions)
            {
                // 各トランザクションの処理結果を記録するオブジェクトを作成
                var processResult = new ProcessResult
                {
                    EntityId = transaction.EntityId,
                    TransactionId = transaction.Id,
                    Success = true
                };

                try
                {
                    // トランザクションデータを最新データエンティティ形式に変換
                    var newLatestData = LatestDataEntity.FromTransaction(transaction);

                    if (existingLatestDataDict.TryGetValue((transaction.Id, transaction.EntityId), out var existingData))
                    {
                        // 既存データありの場合：差分チェック
                        if (existingData.HasDifference(newLatestData))
                        {
                            // 差分あり：更新対象リストに追加
                            newLatestData.CreatedAt = existingData.CreatedAt; // 作成日時は保持
                            newLatestData.UpdatedAt = DateTime.UtcNow;
                            toUpdate.Add(newLatestData);

                            processResult.Action = ProcessAction.Update;
                            processResult.Message = "データ更新";
                            result.UpdateCount++;
                        }
                        else
                        {
                            // 差分なし：処理をスキップ
                            processResult.Action = ProcessAction.None;
                            processResult.Message = "差分なし - スキップ";
                            result.SkipCount++;
                        }
                    }
                    else
                    {
                        // 既存データなし：新規登録対象リストに追加
                        toInsert.Add(newLatestData);

                        processResult.Action = ProcessAction.Insert;
                        processResult.Message = "新規データ登録";
                        result.InsertCount++;
                    }
                }
                catch (Exception ex)
                {
                    // 個別トランザクション処理でのエラーをログに記録
                    _logger.LogError(ex, "Error processing transaction {TransactionId} for entity {EntityId}",
                        transaction.Id, transaction.EntityId);

                    processResult.Action = ProcessAction.Error;
                    processResult.Success = false;
                    processResult.Message = $"処理エラー: {ex.Message}";
                    result.ErrorCount++;
                }

                // 各トランザクションの処理結果を詳細リストに追加
                result.Details.Add(processResult);
            }

            // 4. 一括データベース操作（効率化）
            // 新規登録・更新対象データがある場合、一括でデータベースに反映
            // 個別のINSERT/UPDATEではなく、UPSERT操作で効率化
            if (toInsert.Any() || toUpdate.Any())
            {
                var allToUpsert = toInsert.Concat(toUpdate).ToList();
                await _latestDataRepository.BulkUpsertAsync(allToUpsert);
                _logger.LogInformation("Bulk upserted {Count} records ({Insert} inserts, {Update} updates)",
                    allToUpsert.Count, toInsert.Count, toUpdate.Count);
            }
        }
    }
}
