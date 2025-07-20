namespace CheckDiffTable.Configuration
{
    /// <summary>
    /// バッチ処理の設定オプションクラス
    /// アプリケーション設定からバッチ処理のパフォーマンス調整パラメータを管理
    /// </summary>
    public class BatchProcessingOptions
    {
        /// <summary>設定ファイルでのセクション名</summary>
        public const string SectionName = "BatchProcessing";

        /// <summary>
        /// バッチサイズ（一度に処理する件数）
        /// デフォルト値: 1000件
        /// </summary>
        public int BatchSize { get; set; } = 1000;

        /// <summary>
        /// バッチサイズの検証とデフォルト値設定を行う
        /// 設定値が無効な場合は安全なデフォルト値を返す
        /// </summary>
        /// <returns>検証済みの有効なバッチサイズ</returns>
        public int GetValidatedBatchSize()
        {
            // 無効な値（0以下）の場合はデフォルト値を返す
            if (BatchSize <= 0)
                return 1000;
            
            // 有効な値をそのまま返す
            return BatchSize;
        }
    }
}
