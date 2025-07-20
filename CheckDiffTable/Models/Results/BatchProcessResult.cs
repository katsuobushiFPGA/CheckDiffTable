using System;
using System.Collections.Generic;

namespace CheckDiffTable.Models.Results
{
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
}
