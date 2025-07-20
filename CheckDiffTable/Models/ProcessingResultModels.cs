using System;
using System.Collections.Generic;

namespace CheckDiffTable.Models
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
}
