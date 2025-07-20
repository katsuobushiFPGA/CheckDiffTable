using CheckDiffTable.Configuration;
using CheckDiffTable.Models;
using CheckDiffTable.Models.Results;
using CheckDiffTable.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CheckDiffTable.Services
{
    /// <summary>
    /// 差分チェック・更新サービスの実装クラス
    /// トランザクションテーブルと最新データテーブル間の差分をチェックし一括処理をする
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
        
        /// <summary>データベースデータソース（トランザクション制御用）</summary>
        private readonly NpgsqlDataSource _dataSource;

        /// <summary>
        /// DiffCheckServiceのコンストラクタ
        /// 依存関係の注入により、リポジトリ、ログ、設定オプション、データソースを受け取る
        /// </summary>
        /// <param name="transactionRepository">トランザクションデータアクセス用リポジトリ</param>
        /// <param name="latestDataRepository">最新データテーブルアクセス用リポジトリ</param>
        /// <param name="logger">ログ出力用インスタンス</param>
        /// <param name="batchOptions">バッチ処理設定オプション</param>
        /// <param name="dataSource">データベースデータソース</param>
        public DiffCheckService(
            ITransactionRepository transactionRepository,
            ILatestDataRepository latestDataRepository,
            ILogger<DiffCheckService> logger,
            IOptions<BatchProcessingOptions> batchOptions,
            NpgsqlDataSource dataSource)
        {
            _transactionRepository = transactionRepository;
            _latestDataRepository = latestDataRepository;
            _logger = logger;
            _batchOptions = batchOptions.Value;
            _dataSource = dataSource;
        }

        /// <summary>
        /// 全エンティティをバッチ処理で効率的に差分チェック・更新を行う
        /// トランザクションテーブルから最新データを取得し、最新データテーブルと比較して差分があるものを一括更新する
        /// </summary>
        /// <returns>処理結果（成功/失敗、件数、詳細情報を含む）</returns>
        public async Task<BatchProcessResult> ProcessAllEntitiesBatchAsync()
        {
            var startTime = DatabaseConstants.DateTimeHelper.GetJstNow();
            var result = new BatchProcessResult { Success = true };

            try
            {
                // バッチサイズの決定（appsettings.jsonの設定値またはデフォルト値を使用）
                var effectiveBatchSize = _batchOptions.GetValidatedBatchSize();
                _logger.LogInformation("バッチ処理開始: バッチサイズ {BatchSize}", effectiveBatchSize);

                // トランザクションテーブルから全てのトランザクションを一括取得
                var transactions = await _transactionRepository.GetAllTransactionsAsync();
                if (!transactions.Any())
                {
                    _logger.LogInformation("処理対象のトランザクションが見つかりませんでした");
                    result.Message = "処理対象のトランザクションがありません";
                    result.ProcessingTime = DatabaseConstants.DateTimeHelper.GetJstNow() - startTime;
                    return result;
                }

                result.TotalEntities = transactions.Count;
                _logger.LogInformation("トランザクション対象エンティティ数: {Count}件", result.TotalEntities);

                // データサイズに応じて処理方法を選択
                // バッチ単位でデータベースアクセスを制御
                // 小さなデータセットの場合は一括処理、大きなデータセットはバッチ分割処理
                if (transactions.Count <= effectiveBatchSize)
                {
                    await ProcessTransactionBatch(transactions, result);
                }
                else
                {
                    await ProcessTransactionsBatched(transactions, effectiveBatchSize, result);
                }

                // 差分がなかったトランザクションの削除は各バッチで実行済み
                _logger.LogInformation("全バッチ処理完了（差分なしトランザクションの削除を含む）");

                // 処理完了：結果サマリーの生成とログ出力
                result.ProcessingTime = DatabaseConstants.DateTimeHelper.GetJstNow() - startTime;
                result.Message = $"一括処理完了: {result.TotalEntities}エンティティ処理 " +
                               $"(新規:{result.InsertCount}, 更新:{result.UpdateCount}, " +
                               $"スキップ:{result.SkipCount}, エラー:{result.ErrorCount}) " +
                               $"削除:{result.DeletedCount}件 " +
                               $"処理時間:{result.ProcessingTime.TotalMilliseconds:F0}ms";

                _logger.LogInformation(result.Message);
                return result;
            }
            catch (Exception ex)
            {
                // 予期しないエラーが発生した場合の処理
                result.Success = false;
                result.Message = $"一括処理中にエラーが発生しました: {ex.Message}";
                result.ProcessingTime = DatabaseConstants.DateTimeHelper.GetJstNow() - startTime;
                _logger.LogError(ex, "バッチ処理が失敗しました");
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
                _logger.LogInformation("バッチ処理中 {BatchNumber}/{TotalBatches} ({BatchSize}エンティティ)",
                    (i / batchSize) + 1, (int)Math.Ceiling((double)transactions.Count / batchSize), batch.Count);

                await ProcessTransactionBatch(batch, result);
            }
        }

        /// <summary>
        /// 1つのバッチ内のトランザクションを処理する（データベーストランザクション使用）
        /// 既存データとの差分チェックを行い、新規登録・更新・スキップの判定を行う
        /// データベースアクセスを最小限に抑えるため、バッチ内の全てのデータを一括取得・一括更新・一括削除する
        /// 全ての操作をデータベーストランザクション内で実行し、失敗時はロールバックを行う
        /// </summary>
        /// <param name="batchTransactions">処理対象のトランザクションバッチ</param>
        /// <param name="result">処理結果を蓄積するオブジェクト</param>
        /// <returns>非同期処理タスク</returns>
        private async Task ProcessTransactionBatch(List<TransactionEntity> batchTransactions, BatchProcessResult result)
        {
            using var connection = await _dataSource.OpenConnectionAsync();
            using var dbTransaction = await connection.BeginTransactionAsync();
            
            try
            {
                _logger.LogDebug("データベーストランザクション開始: バッチ内トランザクション数 {Count}件", batchTransactions.Count);
                
                // 関連する最新データを一括取得（効率化）
                // バッチ内の全（id, entity_id）複合主キーに対する既存データを一括取得し、N+1問題を回避
                var transactionKeys = batchTransactions.Select(t => (t.Id, t.EntityId)).ToList();
                var existingLatestData = await _latestDataRepository.GetByTransactionKeysAsync(transactionKeys);
                // 高速検索のため複合主キーでDictionary形式に変換
                var existingLatestDataDict = existingLatestData.ToDictionary(e => (e.Id, e.EntityId));

                // 差分チェックと処理データ準備
                // 各トランザクションに対して既存データとの差分をチェックし、
                // 新規登録・更新・スキップ・削除の分類を行う
                var toInsert = new List<LatestDataEntity>();
                var toUpdate = new List<LatestDataEntity>();
                var toDelete = new List<(int Id, int EntityId)>();

                foreach (var transactionItem in batchTransactions)
                {
                    // 各トランザクションの処理結果を記録するオブジェクトを作成
                    var processResult = new ProcessResult
                    {
                        EntityId = transactionItem.EntityId,
                        TransactionId = transactionItem.Id,
                        Success = true
                    };

                    try
                    {
                        // トランザクションデータを最新データエンティティ形式に変換
                        var newLatestData = LatestDataEntity.FromTransaction(transactionItem);

                        if (existingLatestDataDict.TryGetValue((transactionItem.Id, transactionItem.EntityId), out var existingData))
                        {
                            // 既存データありの場合：差分チェック
                            if (existingData.HasDifference(newLatestData))
                            {
                                // 差分あり：更新対象リストに追加
                                // PostgreSQLのUPSERTにより、CreatedAtは自動保持、UpdatedAtは新しい値で更新される
                                toUpdate.Add(newLatestData);

                                processResult.Action = ProcessAction.Update;
                                processResult.Message = "データ更新";
                                result.UpdateCount++;
                            }
                            else
                            {
                                // 差分なし：処理をスキップし、削除対象に追加
                                toDelete.Add((transactionItem.Id, transactionItem.EntityId));
                                
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
                        _logger.LogError(ex, "トランザクション処理エラー: ID {TransactionId}, エンティティ {EntityId}",
                            transactionItem.Id, transactionItem.EntityId);

                        processResult.Action = ProcessAction.Error;
                        processResult.Success = false;
                        processResult.Message = $"処理エラー: {ex.Message}";
                        result.ErrorCount++;
                    }

                    // 各トランザクションの処理結果を詳細リストに追加
                    result.Details.Add(processResult);
                }

                // 一括データベース操作（全てトランザクション内で実行）
                // 新規登録・更新・削除対象データがある場合、一括でデータベースに反映
                
                // 1. UPSERT操作（新規登録・更新）
                // → 最新データテーブルに対して一括で新規登録・更新を行う
                if (toInsert.Any() || toUpdate.Any())
                {
                    var allToUpsert = toInsert.Concat(toUpdate).ToList();
                    await _latestDataRepository.BulkUpsertWithTransactionAsync(allToUpsert, dbTransaction);
                    _logger.LogInformation("一括UPSERT完了: {Count}レコード（新規:{Insert}件, 更新:{Update}件）",
                        allToUpsert.Count, toInsert.Count, toUpdate.Count);
                }

                // 2. 削除操作（差分なしトランザクション）
                // → 差分がなかったトランザクションを一括削除
                if (toDelete.Any())
                {
                    var deletedCount = await _transactionRepository.DeleteSpecificTransactionsWithTransactionAsync(toDelete, dbTransaction);
                    result.DeletedCount += deletedCount;
                    _logger.LogInformation("差分なしトランザクション削除完了: {DeletedCount}件", deletedCount);
                }

                // トランザクションをコミット
                await dbTransaction.CommitAsync();
                _logger.LogDebug("データベーストランザクションコミット成功");
            } catch (Exception ex)
            {
                // エラー発生時はロールバック
                await dbTransaction.RollbackAsync();
                _logger.LogError(ex, "バッチ処理エラーによりデータベーストランザクションをロールバックしました");
                
                // バッチ全体を失敗としてマーク
                result.Success = false;
                result.ErrorCount += batchTransactions.Count;
                
                // 詳細結果にエラー情報を追加
                foreach (var transactionItem in batchTransactions)
                {
                    result.Details.Add(new ProcessResult
                    {
                        EntityId = transactionItem.EntityId,
                        TransactionId = transactionItem.Id,
                        Success = false,
                        Action = ProcessAction.Error,
                        Message = $"バッチ処理エラー: {ex.Message}"
                    });
                }
                
                throw;
            }
        }
    }
}
