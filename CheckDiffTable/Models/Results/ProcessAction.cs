namespace CheckDiffTable.Models.Results
{
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
