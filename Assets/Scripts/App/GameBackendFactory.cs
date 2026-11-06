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
        /// <summary>
        /// 是否提供开发者工具（GM）。按用户要求，开发阶段正式打包的游戏也显示 GM；
        /// 对外发售前必须改为 false（已列入上线阻塞项）。
        /// </summary>
        public const bool DeveloperToolsEnabled = true;

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
                DeveloperToolsEnabled,
                LocalPlayerId,
                MainUiText.LocalPlayerName,
                Debug.LogException);
        }
    }
}
