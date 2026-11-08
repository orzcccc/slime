using System;
using System.Collections.Generic;

namespace SlimeBlindBox.Backend.Local
{
    /// <summary>
    /// 本地存档结构。字段划分与未来 PlayFab 玩家数据对应：
    /// 计时与待领取数量对应 User Data，盲盒与史莱姆对应 Inventory，图鉴对应 Statistics/User Data。
    /// 时间统一使用 UTC Unix 毫秒。
    /// </summary>
    [Serializable]
    public sealed class PlayerSaveData
    {
        /// <summary>v2：新增累计开箱次数 totalBoxesOpened。</summary>
        public const int CurrentSchemaVersion = 2;

        public int schemaVersion = CurrentSchemaVersion;
        public string playerId;
        public string nickname;
        public long createdAtUnixMs;
        public long lastLoginAtUnixMs;
        public int pendingBoxCount;
        /// <summary>待领取盲盒的品质键，产出时即确定；数量与 pendingBoxCount 一致，旧存档加载时自动补齐。</summary>
        public List<string> pendingBoxRarities = new List<string>();
        public int boxCapacity;
        public long lastBoxAtUnixMs;
        public int totalBoxesOpened;
        public List<BoxStackSave> boxInventory = new List<BoxStackSave>();
        public List<SlimeSave> slimes = new List<SlimeSave>();

        public PlayerSaveData Clone()
        {
            var copy = (PlayerSaveData)MemberwiseClone();
            copy.boxInventory = new List<BoxStackSave>();
            if (boxInventory != null) foreach (var stack in boxInventory) if (stack != null) copy.boxInventory.Add(stack.Clone());
            copy.slimes = new List<SlimeSave>();
            if (slimes != null) foreach (var slime in slimes) if (slime != null) copy.slimes.Add(slime.Clone());
            copy.pendingBoxRarities = pendingBoxRarities != null ? new List<string>(pendingBoxRarities) : new List<string>();
            return copy;
        }
    }

    [Serializable]
    public sealed class BoxStackSave
    {
        public string key;
        public int quantity;

        public BoxStackSave Clone() => (BoxStackSave)MemberwiseClone();
    }

    /// <summary>史莱姆记录。记录存在即代表图鉴已解锁，数量可以为 0。</summary>
    [Serializable]
    public sealed class SlimeSave
    {
        public int slimeId;
        public int quantity;
        public int level = 1;
        public int experience;
        public long firstObtainedAtUnixMs;

        public SlimeSave Clone() => (SlimeSave)MemberwiseClone();
    }
}
