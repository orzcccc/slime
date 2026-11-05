using System;

namespace SlimeBlindBox.Networking
{
    [Serializable]
    public sealed class LoginRequest
    {
        public string steamTicket;
    }

    [Serializable]
    public sealed class LoginResponse
    {
        public string token;
        public UserDto user;
    }

    [Serializable]
    public sealed class UserDto
    {
        public string steam_id;
        public string nickname;
        public int total_cp;
        public int residue;
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
    public sealed class MergeRequest
    {
        public ConsumeSlimeDto[] consume_slimes;
    }

    [Serializable]
    public sealed class ConsumeSlimeDto
    {
        public int slime_id;
        public int count;
    }

    [Serializable]
    public sealed class MergeResponse
    {
        public bool success;
        public int result_slime_id;
        public string result_name;
        public string result_rarity;
        public string result_rarity_key;
        public string result_rarity_color;
        public bool is_mutation;
        public int residue_gained;
        public int residue;
        public int total_cp;
    }

    [Serializable]
    public sealed class GameStateDto
    {
        public int box_count;
        public int box_capacity;
        public int residue;
        public int total_cp;
        public string server_time;
        public string next_box_at;
        public BoxInventoryItemDto[] box_inventory;
    }

    [Serializable] public sealed class BoxInventoryItemDto { public string key; public string name; public string color; public int quantity; }
    [Serializable] public sealed class OpenBoxRequest { public string box_rarity_key; }
    [Serializable] public sealed class ClaimBoxesResponse { public int claimed; public GameStateDto game_state; }

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

    [Serializable] public sealed class DictionaryResponse { public DictionaryItemDto[] entries; public int discovered; public int total; public float completion; }
    [Serializable] public sealed class DictionaryItemDto { public int slime_id; public string name; public string rarity; public string rarity_key; public string rarity_color; public int base_cp; public int discovered; public string first_obtained_at; }
    [Serializable] public sealed class GuildStateResponse { public bool has_guild; public GuildDto guild; public GuildBossDto boss; public int guild_coins; }
    [Serializable] public sealed class GuildDto { public int id; public string name; public int funds; public int magic_level; public int capacity_level; public int contribution; }
    [Serializable] public sealed class GuildBossDto { public string week_key; public int max_hunger; public int current_hunger; public int completed; public string last_fed_date; }
    [Serializable] public sealed class NameRequest { public string name; }
    [Serializable] public sealed class DonateRequest { public int slime_id; public int count; }
    [Serializable] public sealed class UpgradeRequest { public string type; }
    [Serializable] public sealed class GmBoxesRequest { public string action; public int amount; }
    [Serializable] public sealed class ActionResponse { public bool success; public int damage; public int current_hunger; public bool completed; public int reward_boxes; }

    [Serializable]
    public sealed class LeaderboardResponse
    {
        public LeaderboardItemDto[] leaderboard;
    }

    [Serializable]
    public sealed class LeaderboardItemDto
    {
        public string steam_id;
        public string nickname;
        public int total_cp;
    }

    [Serializable]
    internal sealed class ApiErrorResponse
    {
        public string error;
    }
}
