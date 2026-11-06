using System;
using System.IO;
using System.Text;
using SlimeBlindBox.Backend.Local;
using UnityEngine;

namespace SlimeBlindBox.App
{
    /// <summary>
    /// 本地 JSON 存档。写入时先写临时文件再替换，并保留上一份作为备份；
    /// 主存档损坏时自动回退到备份，两者都损坏时把损坏文件改名保留并开始新存档。
    /// </summary>
    public sealed class JsonFilePlayerDataStore : IPlayerDataStore
    {
        private readonly string path;
        private readonly string backupPath;
        private readonly string temporaryPath;

        public JsonFilePlayerDataStore(string path)
        {
            this.path = path;
            backupPath = path + ".bak";
            temporaryPath = path + ".tmp";
        }

        public PlayerSaveData Load()
        {
            if (TryRead(path, out var data)) return data;
            if (TryRead(backupPath, out data))
            {
                Debug.LogWarning("主存档缺失或损坏，已从备份恢复。");
                return data;
            }

            if (File.Exists(path))
            {
                var corruptPath = path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                File.Move(path, corruptPath);
                Debug.LogWarning("存档已损坏且没有可用备份，原文件已保留为 " + Path.GetFileName(corruptPath) + "，将创建新存档。");
            }
            return null;
        }

        public void Save(PlayerSaveData data)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(data, true), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporaryPath, path, backupPath);
            else File.Move(temporaryPath, path);
        }

        private static bool TryRead(string filePath, out PlayerSaveData data)
        {
            data = null;
            if (!File.Exists(filePath)) return false;
            try
            {
                var json = File.ReadAllText(filePath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json)) return false;
                data = JsonUtility.FromJson<PlayerSaveData>(json);
                return data != null;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
