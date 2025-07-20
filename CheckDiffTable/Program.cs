using CheckDiffTable.Models;
using CheckDiffTable.Repositories;
using CheckDiffTable.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;

namespace CheckDiffTable
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // ホストビルダーの設定
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                })
                .ConfigureServices((context, services) =>
                {
                    // 設定の登録
                    services.Configure<BatchProcessingOptions>(
                        context.Configuration.GetSection(BatchProcessingOptions.SectionName));

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

                if (args.Length > 0)
                {
                    if (args[0].ToLower() == "batch")
                    {
                        // 一括処理モード（高効率）
                        int? batchSize = null;
                        if (args.Length > 1 && int.TryParse(args[1], out int parsedBatchSize))
                        {
                            batchSize = parsedBatchSize;
                            logger.LogInformation("一括処理モード（高効率版）- バッチサイズ: {BatchSize}", batchSize);
                        }
                        else
                        {
                            logger.LogInformation("一括処理モード（高効率版）- デフォルトバッチサイズ");
                        }
                        
                        var batchResult = await diffCheckService.ProcessAllEntitiesBatchAsync(batchSize);
                        DisplayBatchResult(batchResult, logger);
                    }
                    else if (int.TryParse(args[0], out int entityId))
                    {
                        // 特定のエンティティIDを処理
                        logger.LogInformation("単一エンティティ処理モード: EntityID = {EntityId}", entityId);
                        var result = await diffCheckService.ProcessEntityAsync(entityId);
                        DisplayResult(result, logger);
                    }
                    else
                    {
                        logger.LogError("無効な引数です。使用方法: dotnet run [batch [batchsize]|EntityID]");
                        Environment.Exit(1);
                    }
                }
                else
                {
                    // デフォルト：一括処理モード
                    logger.LogInformation("一括処理モード（デフォルト）");
                    var batchResult = await diffCheckService.ProcessAllEntitiesBatchAsync();
                    DisplayBatchResult(batchResult, logger);
                }

                logger.LogInformation("=== トランザクション差分チェック・更新システム終了 ===");
            }
            catch (Exception ex)
            {
                var logger = host.Services.GetService<ILogger<Program>>();
                logger?.LogError(ex, "アプリケーション実行中にエラーが発生しました");
                Environment.Exit(1);
            }
        }

        private static void DisplayBatchResult(BatchProcessResult result, ILogger logger)
        {
            var logLevel = result.Success ? LogLevel.Information : LogLevel.Error;
            
            logger.Log(logLevel, "=== 一括処理結果サマリー ===");
            logger.Log(logLevel, "処理成功: {Success}", result.Success);
            logger.Log(logLevel, "総エンティティ数: {Total}", result.TotalEntities);
            logger.Log(logLevel, "新規登録: {Insert}件", result.InsertCount);
            logger.Log(logLevel, "更新: {Update}件", result.UpdateCount);
            logger.Log(logLevel, "スキップ: {Skip}件", result.SkipCount);
            logger.Log(logLevel, "エラー: {Error}件", result.ErrorCount);
            logger.Log(logLevel, "処理時間: {ProcessingTime}ms", result.ProcessingTime.TotalMilliseconds);
            logger.Log(logLevel, "メッセージ: {Message}", result.Message);

            if (result.Details.Any())
            {
                logger.LogInformation("=== 詳細結果 ===");
                foreach (var detail in result.Details)
                {
                    DisplayResult(detail, logger);
                }
            }
        }

        private static void DisplayResult(ProcessResult result, ILogger logger)
        {
            var logLevel = result.Success ? LogLevel.Information : LogLevel.Error;
            logger.Log(logLevel, 
                "EntityID: {EntityId}, Action: {Action}, Success: {Success}, Message: {Message}, TransactionID: {TransactionId}",
                result.EntityId, result.Action, result.Success, result.Message, result.TransactionId);
        }
    }
}
