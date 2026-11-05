using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace SlimeBlindBox.Networking
{
    public sealed class SlimeApiClient : MonoBehaviour
    {
        [SerializeField] private string baseUrl = "http://localhost:3000";

        public string Token { get; private set; }
        public bool IsLoggedIn => !string.IsNullOrEmpty(Token);

        public IEnumerator Login(string steamTicket, Action<LoginResponse> onSuccess, Action<string> onError)
        {
            var body = new LoginRequest { steamTicket = steamTicket };
            yield return Send("POST", "/api/auth/login", body, false, onSuccess, onError);
        }

        public IEnumerator OpenBox(string boxRarityKey, Action<OpenBoxResponse> onSuccess, Action<string> onError)
        {
            yield return Send("POST", "/api/game/open-box", new OpenBoxRequest { box_rarity_key = boxRarityKey }, true, onSuccess, onError);
        }

        public IEnumerator ClaimBoxes(Action<ClaimBoxesResponse> onSuccess, Action<string> onError)
        {
            yield return Send<object, ClaimBoxesResponse>("POST", "/api/game/claim-boxes", null, true, onSuccess, onError);
        }

        public IEnumerator GetGameState(Action<GameStateDto> onSuccess, Action<string> onError)
        {
            yield return Send<object, GameStateDto>("GET", "/api/game/state", null, true, onSuccess, onError);
        }

        public IEnumerator GetInventory(Action<InventoryResponse> onSuccess, Action<string> onError)
        {
            yield return Send<object, InventoryResponse>("GET", "/api/user/inventory", null, true, onSuccess, onError);
        }

        public IEnumerator GetDictionary(Action<DictionaryResponse> ok, Action<string> error)
        {
            yield return Send<object, DictionaryResponse>("GET", "/api/user/dictionary", null, true, ok, error);
        }

        public IEnumerator GetGuild(Action<GuildStateResponse> ok, Action<string> error)
        {
            yield return Send<object, GuildStateResponse>("GET", "/api/guild/state", null, true, ok, error);
        }

        public IEnumerator CreateGuild(string guildName, Action<ActionResponse> ok, Action<string> error)
        {
            yield return Send("POST", "/api/guild/create", new NameRequest { name = guildName }, true, ok, error);
        }

        public IEnumerator Donate(int slimeId, int count, Action<ActionResponse> ok, Action<string> error)
        {
            yield return Send("POST", "/api/guild/donate", new DonateRequest { slime_id = slimeId, count = count }, true, ok, error);
        }

        public IEnumerator UpgradeGuild(string type, Action<ActionResponse> ok, Action<string> error)
        {
            yield return Send("POST", "/api/guild/upgrade", new UpgradeRequest { type = type }, true, ok, error);
        }

        public IEnumerator FeedBoss(Action<ActionResponse> ok, Action<string> error)
        {
            yield return Send<object, ActionResponse>("POST", "/api/guild/boss/feed", null, true, ok, error);
        }

        public IEnumerator GmBoxes(string action, int amount, Action<GameStateDto> ok, Action<string> error)
        {
            yield return Send("POST", "/api/dev/gm/boxes", new GmBoxesRequest { action = action, amount = amount }, true, ok, error);
        }

        public void Logout()
        {
            Token = null;
        }

        private IEnumerator Send<TRequest, TResponse>(
            string method,
            string path,
            TRequest body,
            bool requiresAuth,
            Action<TResponse> onSuccess,
            Action<string> onError)
        {
            if (requiresAuth && !IsLoggedIn)
            {
                onError?.Invoke("尚未登录");
                yield break;
            }

            var request = new UnityWebRequest(baseUrl.TrimEnd('/') + path, method);
            request.downloadHandler = new DownloadHandlerBuffer();
            if (body != null)
            {
                var json = JsonUtility.ToJson(body);
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.SetRequestHeader("Content-Type", "application/json");
            }

            if (requiresAuth)
            {
                request.SetRequestHeader("Authorization", $"Bearer {Token}");
            }

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(ParseError(request));
                request.Dispose();
                yield break;
            }

            var response = JsonUtility.FromJson<TResponse>(request.downloadHandler.text);
            if (response is LoginResponse loginResponse)
            {
                Token = loginResponse.token;
            }

            onSuccess?.Invoke(response);
            request.Dispose();
        }

        private static string ParseError(UnityWebRequest request)
        {
            var json = request.downloadHandler?.text;
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var response = JsonUtility.FromJson<ApiErrorResponse>(json);
                    if (!string.IsNullOrEmpty(response?.error)) return response.error;
                }
                catch (ArgumentException)
                {
                    // 非 JSON 错误响应，回退到 Unity 提供的错误文本。
                }
            }

            return $"HTTP {request.responseCode}: {request.error}";
        }
    }
}
