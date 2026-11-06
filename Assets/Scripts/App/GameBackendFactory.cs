using System.IO;
using SlimeBlindBox.Backend;
using SlimeBlindBox.Backend.Local;
using SlimeBlindBox.UI;
using UnityEngine;

namespace SlimeBlindBox.App
{
    /// <summary>
    /// 创建当前使用的游戏后端。开发阶段使用客户端本地后端；
    /// 接入 PlayFab + Steam 后只需在此返回新的 IGameBackend 实现，其余代码不变。
    /// </summary>
    public static class GameBackendFactory
    {
        private const string LocalPlayerId = "local-player";
        private const string SaveFolder = "save";
        private const string SaveFileName = "player.json";

        public static IGameBackend Create()
        {
            var savePath = Path.Combine(Application.persistentDataPath, SaveFolder, SaveFileName);
            return new LocalGameBackend(
                new JsonFilePlayerDataStore(savePath),
                new SystemClock(),
                new CryptoRandomSource(),
                Debug.isDebugBuild,
                LocalPlayerId,
                MainUiText.LocalPlayerName,
                Debug.LogException);
        }
    }
}
