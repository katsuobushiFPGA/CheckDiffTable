using CheckDiffTable.Models.Results;
using System.Threading.Tasks;

namespace CheckDiffTable.Services
{
    /// <summary>
    /// 差分チェック・更新サービスのインターフェース
    /// </summary>
    public interface IDiffCheckService
    {
        /// <summary>
        /// 全エンティティをバッチ処理で差分チェック・更新を行う
        /// </summary>
        /// <returns>処理結果（成功/失敗、件数、詳細情報を含む）</returns>
        Task<BatchProcessResult> ProcessAllEntitiesBatchAsync();
    }
}
