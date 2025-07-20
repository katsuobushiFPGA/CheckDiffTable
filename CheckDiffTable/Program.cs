using CheckDiffTable.Configuration;
using CheckDiffTable.Models;
using CheckDiffTable.Models.Results;
using CheckDiffTable.Repositories;
using CheckDiffTable.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using Npgsql;

namespace CheckDiffTable
{
    /// <summary>
    /// アプリケーションのメインクラス
    /// トランザクション差分チェック・更新システムのエントリーポイント
    /// </summary>
    class Program
    {
        /// <summary>
        /// アプリケーションのメインメソッド
        /// 依存関係注入の設定、サービスの初期化、バッチ処理の実行を行う
        /// </summary>
        /// <returns>非同期処理タスク</returns>
        static async Task Main()
        {
            // ホストビルダーの設定：アプリケーションの構成管理、依存関係注入、ログ設定
            var host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    // アプリケーション設定ファイルの読み込み
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                })
                .ConfigureServices((context, services) =>
                {
                    // データベース接続の設定（appsettings.jsonから読み込み）
                    var connectionString = context.Configuration.GetConnectionString("DefaultConnection")
                        ?? throw new InvalidOperationException("DefaultConnection is required");
                    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
                    var dataSource = dataSourceBuilder.Build();
                    services.AddSingleton(dataSource);

                    // 設定オプションの登録
                    services.Configure<BatchProcessingOptions>(
                        context.Configuration.GetSection(BatchProcessingOptions.SectionName));

                    // データアクセス層の依存関係注入
                    // リポジトリの登録
                    services.AddScoped<ITransactionRepository, TransactionRepository>();
                    services.AddScoped<ILatestDataRepository, LatestDataRepository>();
                    
                    // サービスの登録
                    services.AddScoped<IDiffCheckService, DiffCheckService>();
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                })
                .Build();

            try
            {
                using var scope = host.Services.CreateScope();
                var diffCheckService = scope.ServiceProvider.GetRequiredService<IDiffCheckService>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

                logger.LogInformation("=== トランザクション差分チェック・更新システム開始 ===");

                // appsettings.jsonで設定されたデフォルトバッチサイズを使用
                logger.LogInformation("一括処理モード - デフォルトバッチサイズ");
                
                var batchResult = await diffCheckService.ProcessAllEntitiesBatchAsync();
                DisplayBatchResult(batchResult, logger);

                logger.LogInformation("=== トランザクション差分チェック・更新システム終了 ===");
            }
            catch (NpgsqlException npgsqlEx)
            {
                var logger = host.Services.GetService<ILogger<Program>>();
                logger?.LogError(npgsqlEx, "データベース接続エラーが発生しました: {Message}", npgsqlEx.Message);
                
                if (npgsqlEx.IsTransient || npgsqlEx.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                {
                    logger?.LogError("データベース接続タイムアウトのため、アプリケーションを終了します");
                    Environment.Exit(1);
                }
                else
                {
                    logger?.LogError("データベースエラーのため、アプリケーションを終了します");
                    Environment.Exit(1);
                }
            }
            catch (TimeoutException timeoutEx)
            {
                var logger = host.Services.GetService<ILogger<Program>>();
                logger?.LogError(timeoutEx, "処理タイムアウトが発生しました");
                Environment.Exit(1); 
            }
            catch (Exception ex)
            {
                var logger = host.Services.GetService<ILogger<Program>>();
                logger?.LogError(ex, "アプリケーション実行中にエラーが発生しました");
                Environment.Exit(1);
            }
            finally
            {
                // データベースリソースの解放
                var dataSource = host.Services.GetService<NpgsqlDataSource>();
                if (dataSource != null)
                {
                    await dataSource.DisposeAsync();
                }
            }
        }

        /// <summary>
        /// バッチ処理結果をコンソールに出力する
        /// 処理結果のサマリー情報と詳細情報を適切なログレベルで表示
        /// </summary>
        /// <param name="result">バッチ処理結果</param>
        /// <param name="logger">ログ出力用インスタンス</param>
        private static void DisplayBatchResult(BatchProcessResult result, ILogger logger)
        {
            var logLevel = result.Success ? LogLevel.Information : LogLevel.Error;
            
            logger.Log(logLevel, "=== 一括処理結果サマリー ===");
            logger.Log(logLevel, "処理成功: {Success}", result.Success);
            logger.Log(logLevel, "総エンティティ数: {Total}", result.TotalEntities);
            logger.Log(logLevel, "新規登録: {Insert}件", result.InsertCount);
            logger.Log(logLevel, "更新: {Update}件", result.UpdateCount);
            logger.Log(logLevel, "スキップ: {Skip}件", result.SkipCount);
            logger.Log(logLevel, "削除: {Delete}件", result.DeletedCount);
            logger.Log(logLevel, "エラー: {Error}件", result.ErrorCount);
            logger.Log(logLevel, "処理時間: {ProcessingTime}ms", result.ProcessingTime.TotalMilliseconds);
            logger.Log(logLevel, "メッセージ: {Message}", result.Message);

            // 詳細結果がある場合のみ表示
            if (result.Details.Any())
            {
                logger.LogInformation("=== 詳細結果 ===");
                foreach (var detail in result.Details)
                {
                    var detailLogLevel = detail.Success ? LogLevel.Information : LogLevel.Error;
                    logger.Log(detailLogLevel, 
                        "EntityID: {EntityId}, Action: {Action}, Success: {Success}, Message: {Message}, TransactionID: {TransactionId}",
                        detail.EntityId, detail.Action, detail.Success, detail.Message, detail.TransactionId);
                }
            }
        }
    }
}
