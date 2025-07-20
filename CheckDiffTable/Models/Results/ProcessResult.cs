namespace CheckDiffTable.Models.Results
{
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
}
