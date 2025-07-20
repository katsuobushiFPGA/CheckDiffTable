namespace CheckDiffTable.Models
{
    /// <summary>
    /// バッチ処理の設定オプション
    /// </summary>
    public class BatchProcessingOptions
    {
        public const string SectionName = "BatchProcessing";

        /// <summary>
        /// バッチサイズ（一度に処理する件数）
        /// </summary>
        public int BatchSize { get; set; } = 1000;

        /// <summary>
        /// 最大バッチサイズ（セキュリティ上の上限）
        /// </summary>
        public int MaxBatchSize { get; set; } = 10000;

        /// <summary>
        /// バッチサイズの検証とデフォルト値設定
        /// </summary>
        public int GetValidatedBatchSize()
        {
            if (BatchSize <= 0)
                return 1000;
            
            if (BatchSize > MaxBatchSize)
                return MaxBatchSize;
            
            return BatchSize;
        }
    }
}
