using System;

namespace SlimeBlindBox.Backend
{
    /// <summary>
    /// 游戏后端统一入口。界面和控制器只通过本接口读取与修改玩家数据，不直接改存档。
    /// 开发阶段由 <see cref="Local.LocalGameBackend"/> 在客户端本地实现；
    /// 正式版替换为 PlayFab + Steam 实现，接口与返回结构保持不变。
    /// 所有方法都以回调返回结果，回调可能同步也可能异步触发，调用方不得假设时序。
    /// </summary>
    public interface IGameBackend
    {
        /// <summary>当前后端是否提供开发者工具（GM）。正式版后端必须返回 false。</summary>
        bool SupportsDebugTools { get; }

        void Login(Action<LoginResponse> onSuccess, Action<BackendError> onError);

        void GetGameState(Action<GameStateDto> onSuccess, Action<BackendError> onError);

        void ClaimBoxes(Action<ClaimBoxesResponse> onSuccess, Action<BackendError> onError);

        void OpenBox(string boxRarityKey, Action<OpenBoxResponse> onSuccess, Action<BackendError> onError);

        void GetInventory(Action<InventoryResponse> onSuccess, Action<BackendError> onError);

        void GetDictionary(Action<DictionaryResponse> onSuccess, Action<BackendError> onError);

        void DebugModifyBoxes(DebugBoxAction action, int amount, Action<GameStateDto> onSuccess, Action<BackendError> onError);
    }
}
