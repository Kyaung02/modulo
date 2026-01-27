using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Network
{
    public class LobbyManager : MonoBehaviour
    {
        private static LobbyManager _instance;
        public static LobbyManager Instance 
        { 
            get 
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<LobbyManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("LobbyManager");
                        _instance = go.AddComponent<LobbyManager>();
                    }
                }
                return _instance;
            }
        }

        private string currentSessionId;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public async Task Authenticate()
        {
            if (UnityServices.State == ServicesInitializationState.Initialized) return;

            InitializationOptions options = new InitializationOptions();
        
#if UNITY_EDITOR
            options.SetProfile("Player_" + Random.Range(0, 10000));
#endif

            await UnityServices.InitializeAsync(options);

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            
            Debug.Log($"[LobbyManager] Signed in as {AuthenticationService.Instance.PlayerId}");
        }

        // 호스트: 세션 생성
        public async Task<bool> CreateLobby(string sessionName, int maxPlayers)
        {
            try
            {
                // Session Options setup
                var options = new SessionOptions
                {
                    MaxPlayers = maxPlayers,
                    IsPrivate = false,
                    Name = sessionName
                }.WithRelayNetwork(); // Automatically handle Relay for NGO

                var session = await MultiplayerService.Instance.CreateSessionAsync(options);
                currentSessionId = session.Id;
                
                Debug.Log($"[LobbyManager] Created Session: {session.Name} / ID: {session.Id}");

                // Assuming Multiplayer Service + NGO auto-hooks transport via widgets or we just start host.
                // However, without Widgets, we might need to verify if transport is set.
                // For now, simpler path: standard StartHost. 
                // Note: If using 'com.unity.services.multiplayer', it often requires 'MultiplayerService.Instance.JoinSession' flow separately?
                // Actually, CreateSessionAsync puts you in the session.
                
                if (NetworkManager.Singleton.IsListening)
                {
                    return true;
                }
                return NetworkManager.Singleton.StartHost();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        // 클라이언트: 세션 목록 조회
        public async Task<IList<ISessionInfo>> GetLobbyList()
        {
            try
            {
                var options = new QuerySessionsOptions
                {
                    Count = 25
                };

                // Add filters if needed, e.g. AvailableSlots > 0 is common but let's just query first
                // options.WithFilter(...)

                var results = await MultiplayerService.Instance.QuerySessionsAsync(options);
                return results.Sessions;
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                return new List<ISessionInfo>();
            }
        }

        // 클라이언트: 세션 참가
        public async Task<bool> JoinLobby(ISessionInfo sessionInfo)
        {
            try
            {
                await MultiplayerService.Instance.JoinSessionByIdAsync(sessionInfo.Id);
                
                // After joining session, start client.
                // The Multiplayer package should configure the transport automatically if 'WithNetworkOption(NetworkOptions.Relay)' was used by host.
                
                if (NetworkManager.Singleton.IsListening)
                {
                    return true;
                }
                return NetworkManager.Singleton.StartClient();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        public async Task<bool> QuickJoinLobby()
        {
             try
            {
                var options = new QuerySessionsOptions
                {
                    Count = 10
                };

                var results = await MultiplayerService.Instance.QuerySessionsAsync(options);
                
                if (results.Sessions.Count > 0)
                {
                    // Simple logic: Join the first one. 
                    // In a real app, you'd filter for AvailableSlots > 0, etc.
                    // Assuming Query returns open sessions or we check manually:
                    foreach (var s in results.Sessions)
                    {
                        if (s.AvailableSlots > 0)
                        {
                            await MultiplayerService.Instance.JoinSessionByIdAsync(s.Id);
                            if (NetworkManager.Singleton.IsListening)
                            {
                                return true;
                            }
                            return NetworkManager.Singleton.StartClient();
                        }
                    }
                }

                Debug.LogWarning("[LobbyManager] Quick Join Failed: No available sessions found.");
                return false;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[LobbyManager] Quick Join Failed: {e.Message}");
                return false;
            }
        }
    }
}

