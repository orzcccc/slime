using System;

namespace SlimeBlindBox.Backend
{
    // 后端返回给客户端的数据结构。字段名与未来 PlayFab CloudScript 返回的 JSON 保持一致，切换后端时界面层无需改动。

    [Serializable]
    public sealed class LoginResponse
    {
        public UserDto user;
    }

    [Serializable]
    public sealed class UserDto
    {
        public string player_id;
        public string nickname;
        public int total_cp;
    }

    [Serializable]
    public sealed class GameStateDto
    {
        public int box_count;
        public int box_capacity;
        public int box_interval_seconds;
        public int total_cp;
        public string server_time;
        public string next_box_at;
        public BoxInventoryItemDto[] box_inventory;
    }

    [Serializable]
    public sealed class BoxInventoryItemDto
    {
        public string key;
        public string name;
        public string color;
        public int quantity;
    }

    [Serializable]
    public sealed class ClaimBoxesResponse
    {
        public int claimed;
        public GameStateDto game_state;
    }

    [Serializable]
    public sealed class OpenBoxResponse
    {
        public int slime_id;
        public string name;
        public string rarity;
        public string rarity_key;
        public string rarity_color;
        public string box_rarity_key;
        public string box_rarity;
        public GameStateDto game_state;
    }

    [Serializable]
    public sealed class InventoryResponse
    {
        public InventoryItemDto[] inventory;
    }

    [Serializable]
    public sealed class InventoryItemDto
    {
        public int slime_id;
        public string name;
        public string rarity;
        public string rarity_key;
        public string rarity_color;
        public int base_cp;
        public int quantity;
        public int level;
        public int experience;
        public int next_level_experience;
        public int effective_cp;
        public string first_obtained_at;
    }

    [Serializable]
    public sealed class DictionaryResponse
    {
        public DictionaryItemDto[] entries;
        public int discovered;
        public int total;
        public float completion;
    }

    [Serializable]
    public sealed class DictionaryItemDto
    {
        public int slime_id;
        public string name;
        public string rarity;
        public string rarity_key;
        public string rarity_color;
        public int base_cp;
        public int discovered;
        public string first_obtained_at;
    }

    /// <summary>后端失败结果。code 供程序判断，message 可直接展示给玩家，不含任何内部细节。</summary>
    [Serializable]
    public sealed class BackendError
    {
        public string code;
        public string message;

        public BackendError(string code, string message)
        {
            this.code = code;
            this.message = message;
        }
    }

    /// <summary>开发者工具（GM）对待领取盲盒的操作。</summary>
    public enum DebugBoxAction
    {
        Add,
        Fill,
        Clear,
        AdvanceHours,
    }
}
