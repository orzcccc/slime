using UnityEngine;

namespace SlimeBlindBox.Networking
{
    /// <summary>
    /// 本地联调用临时入口。挂到场景对象并绑定 UI Button 即可验证 API；正式 Steam 接入后应替换登录方法。
    /// </summary>
    public sealed class DevelopmentApiDemo : MonoBehaviour
    {
        [SerializeField] private SlimeApiClient apiClient;
        [SerializeField] private string developmentPlayerId = "local-player";

        public void LoginForDevelopment()
        {
            StartCoroutine(apiClient.Login(
                $"dev:{developmentPlayerId}",
                response => Debug.Log($"登录成功：{response.user.nickname} ({response.user.steam_id})"),
                error => Debug.LogError($"登录失败：{error}")));
        }

        public void OpenBox()
        {
            StartCoroutine(apiClient.OpenBox("WHITE",
                slime => Debug.Log($"开箱结果：{slime.name}，ID={slime.slime_id}，稀有度={slime.rarity}"),
                error => Debug.LogError($"开箱失败：{error}")));
        }

        public void PrintInventory()
        {
            StartCoroutine(apiClient.GetInventory(
                response =>
                {
                    foreach (var item in response.inventory)
                    {
                        Debug.Log($"库存：{item.name} × {item.quantity}");
                    }
                },
                error => Debug.LogError($"读取库存失败：{error}")));
        }
    }
}
