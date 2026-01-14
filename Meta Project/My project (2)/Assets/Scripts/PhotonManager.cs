using System.Collections;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using System.Linq;

public class PhotonManager : MonoBehaviourPunCallbacks
{
    public static PhotonManager Instance { get; private set; }

    public event System.Action<PlayerColor> OnLocalPlayerColorAssigned;

    public PlayerColor LocalPlayerColor { get; private set; } = PlayerColor.None;

    private readonly Dictionary<string, PlayerColor> cachedPlayerColors = new Dictionary<string, PlayerColor>();

    [Header("Connection Settings")] public bool autoConnectOnStart = true;
    public string roomName = "TestRoom";
    public int maxPlayers = 4;

    [Header("Network Overrides")]
    [Tooltip("Check this to keep Photon in OfflineMode so you can test without disabling your internet connection.")]
    public bool forceOfflineMode = false;

    [Header("Room Lock Settings")] public int minPlayersToLock = 2;
    public int turnsToLock = 3;
    public bool allowRejoiningLockedRoom = true; // THÊM: Cho phép rejoining

    [Header("Debug")] public bool enableDetailedLogging = true;

    // ID c?a ngu?i choi hi?n t?i
    private string playerID;

    private string lastAttemptedRoom = "";

    // THÊM: Delegate d? thông báo s? ki?n join room th?t b?i
    public System.Action<short, string> OnJoinRoomFailedEvent;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            playerID = GetOrCreatePlayerID();
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        Debug.Log("=== PHOTON MANAGER STARTED ===");
        Debug.Log($"Player ID: {playerID}");

        PhotonNetwork.SendRate = 60;
        PhotonNetwork.SerializationRate = 10;

        if (enableDetailedLogging)
        {
            Debug.Log($"Photon App Version: {PhotonNetwork.AppVersion}");
            Debug.Log($"Game Version: {PhotonNetwork.GameVersion}");
            Debug.Log($"Is Connected: {PhotonNetwork.IsConnected}");
            Debug.Log($"Connection State: {PhotonNetwork.NetworkingClient.State}");
        }

        if (forceOfflineMode)
        {
            OfflineBootstrap.EnsureOfflineMode("ForceOfflineMode toggle enabled. Starting in OfflineMode.");
            return;
        }

        if (autoConnectOnStart)
        {
            // N?u không có m?ng, ch?y ? ch? d? Offline (không c?n k?t n?i)
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                OfflineBootstrap.EnsureOfflineMode("No internet detected. Starting in OfflineMode.");
            }
            else
            {
                Debug.Log("Attempting to connect to Photon...");
                PhotonNetwork.ConnectUsingSettings();
            }
        }
    }

    private string GetOrCreatePlayerID()
    {
        string id = PlayerPrefs.GetString("PlayerID", "");
        if (string.IsNullOrEmpty(id))
        {
            id = System.Guid.NewGuid().ToString();
            PlayerPrefs.SetString("PlayerID", id);
            PlayerPrefs.Save();
            Debug.Log($"Created new Player ID: {id}");
        }
        else
        {
            Debug.Log($"Loaded existing Player ID: {id}");
        }

        return id;
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("=== CONNECTED TO MASTER SERVER ===");

        // Ð?t nickname v?i ID (rút ng?n d? d? nhìn)
        PhotonNetwork.NickName = $"Player_{playerID}";

        Debug.Log($"Player Nickname: {PhotonNetwork.NickName}");

        // S?A: KHÔNG t? d?ng th? join room cu n?a
        // Ch? k?t n?i và d? ngu?i dùng ch?n th? công
        Debug.Log("Ðã k?t n?i thành công. Vui lòng ch?n phòng th? công t? menu.");

        // Có th? thêm s? ki?n d? UI c?p nh?t tr?ng thái
        // UI s? t? d?ng c?p nh?t rejoin button thông qua UpdateRejoinButton()
    }

    private bool TryJoinPreviousRoom()
    {
        string lastRoom = PlayerPrefs.GetString($"LastRoom_{playerID}", "");
        if (!string.IsNullOrEmpty(lastRoom))
        {
            Debug.Log($"Attempting to rejoin previous room: {lastRoom}");
            lastAttemptedRoom = lastRoom;

            // THÊM: Ki?m tra xem room có t?n t?i không tru?c khi join
            // S? d?ng JoinRoom thay vì các phuong th?c khác d? Photon t? x? lý
            PhotonNetwork.JoinRoom(lastRoom);
            return true;
        }

        return false;
    }

    private void CreateOrJoinRoom()
    {
        RoomOptions roomOptions = new RoomOptions();
        roomOptions.MaxPlayers = (byte)maxPlayers;

        // S?A QUAN TR?NG: Luôn d? room m? cho d?n khi d? di?u ki?n khóa
        // Ði?u này cho phép rejoining ho?t d?ng
        roomOptions.IsVisible = true;
        roomOptions.IsOpen = true; // QUAN TR?NG: Luôn m? khi t?o room

        roomOptions.EmptyRoomTtl = 15000;
        roomOptions.PlayerTtl = 1000;

        // Thêm custom properties
        roomOptions.CustomRoomProperties = new ExitGames.Client.Photon.Hashtable
        {
            { "PlayerIDs", "" },
            { "IsLocked", false },
            { "TurnCount", 0 },
            { "AllowedPlayers", "" } // Danh sách player du?c phép vào khi room locked
        };
        roomOptions.CustomRoomPropertiesForLobby =
            new string[] { "PlayerIDs", "IsLocked", "TurnCount", "AllowedPlayers" };

        Debug.Log($"Attempting to join or create room '{roomName}'...");
        lastAttemptedRoom = roomName;
        PhotonNetwork.JoinOrCreateRoom(roomName, roomOptions, TypedLobby.Default);
    }

    // THÊM: L?y danh sách màu quân c? du?c ch?n ng?u nhiên
    public List<PlayerColor> GetRandomPlayerColors(int playerCount)
    {
        List<PlayerColor> allColors = new List<PlayerColor>
        {
            PlayerColor.Red,
            PlayerColor.Blue,
            PlayerColor.Yellow,
            PlayerColor.Green
        };

        // Xáo tr?n danh sách màu
        System.Random rng = new System.Random();
        List<PlayerColor> shuffledColors = allColors.OrderBy(x => rng.Next()).ToList();

        // L?y s? lu?ng màu tuong ?ng v?i s? ngu?i choi
        return shuffledColors.Take(playerCount).ToList();
    }

// THÊM: L?y màu c?a player hi?n t?i
    public PlayerColor GetCurrentPlayerColor()
    {
        if (LocalPlayerColor != PlayerColor.None)
        {
            return LocalPlayerColor;
        }

        if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("PlayerColors", out object rawValue) && rawValue is string playerColorsData)
        {
            UpdateColorCacheAndNotify(playerColorsData);

            if (LocalPlayerColor != PlayerColor.None)
            {
                return LocalPlayerColor;
            }
        }

        return PlayerColor.None;
    }

// THÊM: L?y t?t c? màu dang du?c s? d?ng trong room
    public List<PlayerColor> GetRoomPlayerColors()
    {
        if (cachedPlayerColors.Count > 0)
        {
            return cachedPlayerColors.Values.Distinct().ToList();
        }

        if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("PlayerColors", out object rawValue) && rawValue is string playerColorsData)
        {
            UpdateColorCacheAndNotify(playerColorsData);

            if (cachedPlayerColors.Count > 0)
            {
                return cachedPlayerColors.Values.Distinct().ToList();
            }
        }

        return new List<PlayerColor>();
    }

    public bool TryGetPlayerColorByActorNumber(int actorNumber, out PlayerColor color)
    {
        color = PlayerColor.None;

        if (PhotonNetwork.CurrentRoom == null || PhotonNetwork.CurrentRoom.Players == null)
        {
            return false;
        }

        if (!PhotonNetwork.CurrentRoom.Players.TryGetValue(actorNumber, out Player targetPlayer))
        {
            return false;
        }

        string playerId = GetPlayerIDFromNickname(targetPlayer.NickName);
        if (string.IsNullOrEmpty(playerId))
        {
            return false;
        }

        if (cachedPlayerColors.TryGetValue(playerId, out color))
        {
            return true;
        }

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("PlayerColors", out object rawValue) &&
            rawValue is string playerColorsData)
        {
            UpdateColorCacheAndNotify(playerColorsData);
            if (cachedPlayerColors.TryGetValue(playerId, out color))
            {
                return true;
            }
        }

        return false;
    }

// THÊM: Phân ph?i màu cho ngu?i choi khi t?o phòng

// THÊM: L?y màu dã du?c gán tru?c dó cho player
    private PlayerColor GetPreviouslyAssignedColor(string playerId)
    {
        string key = $"AssignedColor_{playerId}";
        if (PlayerPrefs.HasKey(key))
        {
            return (PlayerColor)PlayerPrefs.GetInt(key, 0);
        }

        return PlayerColor.None;
    }

// THÊM: Luu màu dã gán cho player
    private void SaveAssignedColor(string playerId, PlayerColor color)
    {
        string key = $"AssignedColor_{playerId}";
        PlayerPrefs.SetInt(key, (int)color);
        PlayerPrefs.Save();
        Debug.Log($"Saved color {color} for player {playerId}");

        cachedPlayerColors[playerId] = color;
        if (playerId == playerID)
        {
            SetLocalPlayerColor(color);
        }
    }

// THÊM: Xóa màu dã gán (khi player r?i phòng hoàn toàn)
    private void ClearAssignedColor(string playerId)
    {
        string key = $"AssignedColor_{playerId}";
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
        Debug.Log($"Cleared color for player {playerId}");

        if (cachedPlayerColors.Remove(playerId) && playerId == playerID)
        {
            SetLocalPlayerColor(PlayerColor.None);
        }
    }

    private void SetLocalPlayerColor(PlayerColor color)
    {
        if (LocalPlayerColor == color) return;

        LocalPlayerColor = color;
        Debug.Log($"Local player color updated to {LocalPlayerColor}");
        OnLocalPlayerColorAssigned?.Invoke(LocalPlayerColor);
    }

    private void UpdateColorCacheAndNotify(string playerColorsData)
    {
        cachedPlayerColors.Clear();

        if (string.IsNullOrEmpty(playerColorsData))
        {
            SetLocalPlayerColor(PlayerColor.None);
            return;
        }

        string[] entries = playerColorsData.Split(';');
        foreach (string entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry)) continue;

            string[] parts = entry.Split(':');
            if (parts.Length != 2) continue;

            string targetPlayerId = parts[0];
            if (string.IsNullOrEmpty(targetPlayerId)) continue;

            if (System.Enum.TryParse(parts[1], out PlayerColor color))
            {
                cachedPlayerColors[targetPlayerId] = color;
            }
        }

        if (!string.IsNullOrEmpty(playerID) && cachedPlayerColors.TryGetValue(playerID, out PlayerColor localColor))
        {
            SetLocalPlayerColor(localColor);
        }
    }

// Trong PhotonManager.cs, S?A L?I HOÀN TOÀN phuong th?c AssignPlayerColors:

// S?A: Phuong th?c AssignPlayerColors d? x? lý màu trùng thông minh hon
    private void AssignPlayerColors()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // In OfflineMode, keep the offline 4-color setup prepared elsewhere
        if (PhotonNetwork.OfflineMode)
        {
            Debug.Log("OfflineMode detected. Skipping online color assignment to keep all 4 colors active.");
            return;
        }

        Debug.Log("=== ASSIGNING PLAYER COLORS ===");

        // T?o dictionary d? theo dõi màu dã gán
        Dictionary<string, PlayerColor> assignedColors = new Dictionary<string, PlayerColor>();
        List<PlayerColor> usedColors = new List<PlayerColor>();

        Player[] players = PhotonNetwork.PlayerList;

        // BU?C 1: Gán màu cu CHO T?NG PLAYER và ki?m tra trùng
        foreach (Player player in players)
        {
            string playerId = GetPlayerIDFromNickname(player.NickName);
            PlayerColor previousColor = GetPreviouslyAssignedColor(playerId);

            if (previousColor != PlayerColor.None)
            {
                // KI?M TRA TRÙNG: N?u màu cu dã du?c player khác s? d?ng
                if (usedColors.Contains(previousColor))
                {
                    Debug.LogWarning($"Color conflict: {previousColor} is already used by another player");
                    // KHÔNG gán màu cu trong tru?ng h?p này, s? gán màu m?i ? bu?c sau
                    continue;
                }

                assignedColors[playerId] = previousColor;
                usedColors.Add(previousColor);
                Debug.Log($"Reassigned previous color {previousColor} to rejoining player {playerId}");
            }
        }

        // BU?C 2: Gán màu m?i cho player chua có màu ho?c b? trùng màu
        List<PlayerColor> allAvailableColors = new List<PlayerColor>
        {
            PlayerColor.Red,
            PlayerColor.Blue,
            PlayerColor.Yellow,
            PlayerColor.Green
        };

        foreach (Player player in players)
        {
            string playerId = GetPlayerIDFromNickname(player.NickName);

            if (!assignedColors.ContainsKey(playerId))
            {
                // Tìm màu chua du?c s? d?ng
                PlayerColor availableColor = allAvailableColors.FirstOrDefault(color => !usedColors.Contains(color));

                if (availableColor != PlayerColor.None)
                {
                    assignedColors[playerId] = availableColor;
                    usedColors.Add(availableColor);
                    SaveAssignedColor(playerId, availableColor);
                    Debug.Log($"Assigned new color {availableColor} to player {playerId}");
                }
                else
                {
                    Debug.LogError($"No available colors for player {playerId}!");
                }
            }
        }

        // C?p nh?t room properties
        UpdatePlayerColorsInRoom(assignedColors);
    }

// THÊM: Phuong th?c c?p nh?t màu trong room
    private void UpdatePlayerColorsInRoom(Dictionary<string, PlayerColor> assignedColors)
    {
        List<string> playerColorAssignments = new List<string>();
        foreach (var assignment in assignedColors)
        {
            playerColorAssignments.Add($"{assignment.Key}:{assignment.Value}");
        }

        string playerColorsData = string.Join(";", playerColorAssignments);

        PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable
        {
            { "PlayerColors", playerColorsData }
        });

        Debug.Log($"Final color assignments: {playerColorsData}");

        UpdateColorCacheAndNotify(playerColorsData);
    }

// THÊM: Phuong th?c x? lý khi player rejoins
    private void HandlePlayerRejoining(Player rejoiningPlayer)
    {
        string playerId = GetPlayerIDFromNickname(rejoiningPlayer.NickName);
        PlayerColor previousColor = GetPreviouslyAssignedColor(playerId);

        if (previousColor != PlayerColor.None)
        {
            Debug.Log($"Player {playerId} rejoined with previous color: {previousColor}");

            // Ð?m b?o màu này du?c gi? nguyên trong room properties
            AssignPlayerColors(); // G?i l?i d? c?p nh?t
        }
    }

// THÊM: L?y PlayerID t? nickname
    private string GetPlayerIDFromNickname(string nickname)
    {
        if (nickname.StartsWith("Player_") && nickname.Length > 7)
        {
            return nickname.Substring(7);
        }

        return "";
    }

// THÊM: Phuong th?c d? ki?m tra và fix màu b? sai
    public void ValidateAndFixPlayerColors()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        Debug.Log("Validating player color assignments...");

        bool needsFix = false;
        Dictionary<string, PlayerColor> currentAssignments = new Dictionary<string, PlayerColor>();

        // L?y assignments hi?n t?i t? room properties
        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("PlayerColors"))
        {
            string playerColorsData = (string)PhotonNetwork.CurrentRoom.CustomProperties["PlayerColors"];
            if (!string.IsNullOrEmpty(playerColorsData))
            {
                string[] entries = playerColorsData.Split(';');
                foreach (string entry in entries)
                {
                    string[] parts = entry.Split(':');
                    if (parts.Length == 2)
                    {
                        currentAssignments[parts[0]] = (PlayerColor)System.Enum.Parse(typeof(PlayerColor), parts[1]);
                    }
                }
            }
        }

        // Ki?m tra t?ng player hi?n t?i
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            string playerId = GetPlayerIDFromNickname(player.NickName);
            PlayerColor savedColor = GetPreviouslyAssignedColor(playerId);

            if (savedColor != PlayerColor.None)
            {
                // Ki?m tra xem màu saved có kh?p v?i màu trong room không
                if (!currentAssignments.ContainsKey(playerId) || currentAssignments[playerId] != savedColor)
                {
                    Debug.LogWarning(
                        $"Color mismatch for player {playerId}. Saved: {savedColor}, In room: {(currentAssignments.ContainsKey(playerId) ? currentAssignments[playerId].ToString() : "None")}");
                    needsFix = true;
                }
            }
        }

        if (needsFix)
        {
            Debug.Log("Color assignments need fixing, reassigning...");
            AssignPlayerColors();
        }
        else
        {
            Debug.Log("Player color assignments are valid");
        }
    }


// THÊM vào OnJoinedRoom()
    public override void OnJoinedRoom()
    {
        Debug.Log(
            $"Joined room successfully as {PhotonNetwork.NickName}. Total players: {PhotonNetwork.CurrentRoom.PlayerCount}");
        Debug.Log($"Is Master Client: {PhotonNetwork.IsMasterClient}");

        string currentRoomName = PhotonNetwork.CurrentRoom.Name;
        string lastRoom = PlayerPrefs.GetString($"LastRoom_{playerID}", "");

        bool isRejoining = (currentRoomName == lastRoom);
        Debug.Log($"Join type: {(isRejoining ? "REJOINING previous room" : "JOINING new room")}");

        // LUÔN LUU ROOM HI?N T?I
        PlayerPrefs.SetString($"LastRoom_{playerID}", currentRoomName);
        PlayerPrefs.Save();

        // T?I L?I TR?NG THÁI GAME N?U CÓ
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.LoadGameStateFromRoomProperties();

            // In debug info
            GameStateManager.Instance.PrintGameState();
        }

        if (PhotonNetwork.IsMasterClient)
        {
            UpdateRoomPlayerIDs();
            if (!PhotonNetwork.OfflineMode)
            {
                AssignPlayerColors();
            }
            else
            {
                Debug.Log("OfflineMode: Not reassigning colors here. OfflineInitRunner prepares 4-player colors.");
            }
        }
        else if (isRejoining)
        {
            Debug.Log($"Player {playerID} is rejoining, should keep previous color");
        }

        // Kích ho?t quân c? sau khi join room
        StartCoroutine(ActivatePiecesAfterDelay());
    }

// THÊM: Coroutine d? kích ho?t quân c? sau m?t kho?ng delay


    private void UpdateRoomPlayerIDs()
    {
        var currentIDs = GetCurrentPlayerIDs();
        string allowedPlayers = string.Join(",", currentIDs);

        PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable
        {
            { "PlayerIDs", allowedPlayers },
            { "AllowedPlayers", allowedPlayers } // C?p nh?t danh sách allowed players
        });
    }

    private List<string> GetCurrentPlayerIDs()
    {
        var playerIDs = new List<string>();
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            string nickname = player.NickName;
            if (nickname.StartsWith("Player_") && nickname.Length > 7)
            {
                playerIDs.Add(nickname.Substring(7));
            }
        }

        return playerIDs;
    }

    // THÊM: Ki?m tra xem player hi?n t?i có trong danh sách allowed không
    // S?A: Ki?m tra xem player hi?n t?i có trong danh sách allowed không
    public bool IsPlayerAllowedInRoom(string roomName)
    {
        // N?u chua có thông tin room, m?c d?nh cho phép
        if (string.IsNullOrEmpty(roomName)) return true;

        // Ki?m tra n?u dây là room cu c?a player - LUÔN CHO PHÉP VÀO L?I PHÒNG CU
        string lastRoom = PlayerPrefs.GetString($"LastRoom_{playerID}", "");
        if (roomName == lastRoom)
        {
            Debug.Log($"Player {playerID} is allowed to rejoin previous room: {lastRoom}");
            return true;
        }

        // N?u không ph?i room cu, ki?m tra thêm di?u ki?n khác
        // (gi? nguyên logic ki?m tra khác n?u có)
        return true; // T?m th?i luôn cho phép, có th? di?u ch?nh sau
    }

    // Trong PhotonManager.cs, THÊM các phuong th?c sau:

// THÊM: Callback khi player properties thay d?i (d? phát hi?n rejoining)
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        // Phát hi?n khi player rejoining b?ng cách theo dõi s? thay d?i tr?ng thái
        if (changedProps.ContainsKey("IsInactive"))
        {
            bool isInactive = (bool)changedProps["IsInactive"];
            if (!isInactive)
            {
                // Player dã tr? l?i active -> rejoining
                Debug.Log($"Player {targetPlayer.NickName} became active (rejoining)");
                HandlePlayerRejoining(targetPlayer);
            }
        }
    }

// S?A: OnPlayerEnteredRoom d? x? lý rejoining
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log(
            $"Player {newPlayer.NickName} entered the room. Total players: {PhotonNetwork.CurrentRoom.PlayerCount}");

        // In OfflineMode there are no remote joins; avoid reassigning colors.
        if (PhotonNetwork.OfflineMode)
        {
            StartCoroutine(ActivatePiecesAfterDelay());
            return;
        }

        // KI?M TRA: Ðây có ph?i là player rejoining không?
        string playerId = GetPlayerIDFromNickname(newPlayer.NickName);
        PlayerColor previousColor = GetPreviouslyAssignedColor(playerId);

        if (previousColor != PlayerColor.None)
        {
            Debug.Log($"This is a REJOINING player with previous color: {previousColor}");
            HandlePlayerRejoining(newPlayer);
        }

        if (PhotonNetwork.IsMasterClient)
        {
            CheckAndLockRoom();
            UpdateRoomPlayerIDs();
            AssignPlayerColors(); // LUÔN gán màu khi có player m?i

            // THÊM: Ki?m tra và b?t d?u game n?u phòng full
            CheckAndStartGame();

            // Kích ho?t l?i quân c? khi có ngu?i m?i
            StartCoroutine(ActivatePiecesAfterDelay());
        }
        else
        {
            // Client cung c?n c?p nh?t khi có player m?i
            StartCoroutine(ActivatePiecesAfterDelay());
        }
    }

// THÊM: Phuong th?c force reassign màu khi c?n
    public void ForceColorReassignment()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("Forcing color reassignment for all players");
            AssignPlayerColors();
        }
    }

    // S?A: OnPlayerLeftRoom d? không xóa màu ngay l?p t?c
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log(
            $"Player {otherPlayer.NickName} left the room. Total players: {PhotonNetwork.CurrentRoom.PlayerCount}");

        if (PhotonNetwork.OfflineMode)
        {
            // Nothing to do in offline mode
            return;
        }

        string leftPlayerId = GetPlayerIDFromNickname(otherPlayer.NickName);

        // QUAN TR?NG: KHÔNG xóa màu ngay l?p t?c khi player r?i
        // Ch? dánh d?u player là inactive, gi? màu d? cho phép rejoining
        Debug.Log($"Player {leftPlayerId} left, but keeping assigned color for potential rejoining");

        if (PhotonNetwork.IsMasterClient)
        {
            UpdateRoomPlayerIDs();
            // KHÔNG gán l?i màu ngay l?p t?c, ch? xem player có rejoining không
            // Ch? gán l?i sau m?t kho?ng th?i gian n?u c?n
            StartCoroutine(DelayedColorCleanup(leftPlayerId, 180f)); // Ch? 10 giây
        }
    }

// THÊM: Coroutine d? cleanup màu sau delay
    private IEnumerator DelayedColorCleanup(string playerId, float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);

        // Ki?m tra xem player có rejoined chua
        bool playerRejoined = PhotonNetwork.PlayerList.Any(p => GetPlayerIDFromNickname(p.NickName) == playerId);

        if (!playerRejoined)
        {
            // N?u sau delay mà player không rejoined, thì xóa màu
            ClearAssignedColor(playerId);
            Debug.Log($"Player {playerId} did not rejoin after {delaySeconds} seconds, cleared assigned color");

            // Gán l?i màu cho các player còn l?i
            AssignPlayerColors();
        }
        else
        {
            Debug.Log($"Player {playerId} rejoined within {delaySeconds} seconds, keeping assigned color");
        }
    }

    private void CheckAndLockRoom()
    {
        bool shouldLock = PhotonNetwork.CurrentRoom.PlayerCount >= maxPlayers ||
                          (PhotonNetwork.CurrentRoom.PlayerCount >= minPlayersToLock &&
                           GetTurnCount() >= turnsToLock);

        if (shouldLock && !IsRoomLocked())
        {
            LockRoom();
        }
    }

    public void LockRoom()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // S?A QUAN TR?NG: KHÔNG khóa room hoàn toàn, ch? dánh d?u là locked
            // Gi? room m? d? cho phép rejoining, nhung dánh d?u custom property
            PhotonNetwork.CurrentRoom.IsOpen = true; // V?N M? d? cho phép rejoining

            PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable
            {
                { "IsLocked", true }
            });
            Debug.Log("Room is marked as LOCKED - New players discouraged but rejoining allowed");
        }
    }

    public void UnlockRoom()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.CurrentRoom.IsOpen = true;
            PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable
            {
                { "IsLocked", false }
            });
            Debug.Log("Room is now UNLOCKED - New players can join");
        }
    }

    public bool IsRoomLocked()
    {
        if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("IsLocked"))
        {
            return (bool)PhotonNetwork.CurrentRoom.CustomProperties["IsLocked"];
        }

        return false;
    }

    public int GetTurnCount()
    {
        if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("TurnCount"))
        {
            return (int)PhotonNetwork.CurrentRoom.CustomProperties["TurnCount"];
        }

        return 0;
    }

    public void IncrementTurnCount()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            int currentCount = GetTurnCount();
            PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable
            {
                { "TurnCount", currentCount + 1 }
            });

            CheckAndLockRoom();
        }
    }

    // S?A: X? lý l?i join room t?t hon
    // S?A: X? lý l?i join room t?t hon
    // S?A: X? lý l?i join room - KHÔNG t? d?ng t?o phòng
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Failed to join room '{lastAttemptedRoom}': {returnCode} - {message}");

        // THÊM: G?i s? ki?n d? UI x? lý
        OnJoinRoomFailedEvent?.Invoke(returnCode, message);

        // KHÔNG t? d?ng t?o phòng n?a - d? UI quy?t d?nh hành d?ng ti?p theo
        switch (returnCode)
        {
            case ErrorCode.GameDoesNotExist:
                Debug.Log($"Room '{lastAttemptedRoom}' doesn't exist.");
                break;

            case ErrorCode.GameClosed:
                Debug.LogWarning($"Room '{lastAttemptedRoom}' is closed/locked.");
                break;

            case ErrorCode.GameFull:
                Debug.LogWarning($"Room '{lastAttemptedRoom}' is full.");
                break;

            default:
                Debug.LogWarning($"Unknown error joining room: {returnCode} - {message}");
                break;
        }
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Failed to create room '{roomName}': {returnCode} - {message}");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"Disconnected from Photon: {cause}");
    }

    // Method d? connect manually
    public void ConnectToPhoton()
    {
        if (forceOfflineMode)
        {
            Debug.Log("ForceOfflineMode is enabled; staying in OfflineMode instead of connecting.");
            OfflineBootstrap.EnsureOfflineMode("ForceOfflineMode prevented an online connection attempt.");
            return;
        }

        if (PhotonNetwork.IsConnected || PhotonNetwork.OfflineMode)
        {
            Debug.Log("Already connected or in OfflineMode");
            return;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            OfflineBootstrap.EnsureOfflineMode("No internet. Switching to OfflineMode.");
            return;
        }

        Debug.Log("Connecting to Photon...");
        PhotonNetwork.ConnectUsingSettings();
    }

    // S?A: Method d? join room c? th? v?i ki?m tra
    // S?A: Method d? join room c? th? v?i ki?m tra
    // S?A: Method d? join room c? th? v?i ki?m tra
    // S?A: Method d? join room c? th? v?i ki?m tra màu cu
    public void JoinRoom(string roomName)
    {
        if (PhotonNetwork.IsConnectedAndReady)
        {
            this.roomName = roomName;
            lastAttemptedRoom = roomName;

            // THÊM: Ki?m tra và xóa màu cu n?u không ph?i rejoin
            CheckAndClearPreviousColorIfNotRejoining(roomName);

            Debug.Log($"Attempting to join room: {roomName}");

            // S? d?ng JoinRoom thay vì CreateOrJoinRoom d? ch? join room có s?n
            PhotonNetwork.JoinRoom(roomName);
        }
        else
        {
            Debug.LogWarning("Not connected to Photon yet. Please wait for connection.");
        }
    }

    // L?y Player ID hi?n t?i
    public string GetCurrentPlayerID()
    {
        return playerID;
    }

    // THÊM: L?y room name dã attempt cu?i cùng
    public string GetLastAttemptedRoom()
    {
        return lastAttemptedRoom;
    }

    // THÊM: Reset room cu c?a player (dùng khi player mu?n join room m?i)
    public void ResetPreviousRoom()
    {
        PlayerPrefs.DeleteKey($"LastRoom_{playerID}");
        PlayerPrefs.Save();
        Debug.Log("Previous room record has been reset");
    }

// THÊM: L?y room cu c?a player
    public string GetPreviousRoom()
    {
        return PlayerPrefs.GetString($"LastRoom_{playerID}", "");
    }

    // THÊM: Ki?m tra xem player có nên du?c phép rejoining không
    public bool ShouldAllowRejoining(string roomName)
    {
        // Luôn cho phép rejoining room cu
        string lastRoom = PlayerPrefs.GetString($"LastRoom_{playerID}", "");
        if (roomName == lastRoom)
        {
            Debug.Log($"Allowing rejoining for previous room: {lastRoom}");
            return true;
        }

        // Ki?m tra thêm di?u ki?n khác n?u c?n
        return allowRejoiningLockedRoom;
    }


    // THÊM: T?o room v?i ID ng?u nhiên
    public void CreateRandomRoom(int maxPlayers, int piecesPerPlayer)
    {
        if (PhotonNetwork.IsConnectedAndReady)
        {
            // T?o room ID ng?u nhiên
            string randomRoomId = System.Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
            this.roomName = randomRoomId;
            lastAttemptedRoom = randomRoomId;

            RoomOptions roomOptions = new RoomOptions();
            roomOptions.MaxPlayers = (byte)maxPlayers;
            roomOptions.IsVisible = true;
            roomOptions.IsOpen = true;
            roomOptions.EmptyRoomTtl = 15000;
            roomOptions.PlayerTtl = 1000;

            // THÊM: Luu thông tin piecesPerPlayer vào room properties
            roomOptions.CustomRoomProperties = new ExitGames.Client.Photon.Hashtable
            {
                { "PlayerIDs", "" },
                { "IsLocked", false },
                { "TurnCount", 0 },
                { "AllowedPlayers", "" },
                { "PiecesPerPlayer", piecesPerPlayer }, // THÊM: S? quân c? m?i ngu?i
                { "RoomCreator", playerID } // THÊM: Ngu?i t?o phòng
            };
            roomOptions.CustomRoomPropertiesForLobby = new string[]
            {
                "PlayerIDs", "IsLocked", "TurnCount", "AllowedPlayers", "PiecesPerPlayer", "RoomCreator"
            };

            Debug.Log(
                $"Creating random room '{randomRoomId}' with {maxPlayers} players, {piecesPerPlayer} pieces each");
            PhotonNetwork.CreateRoom(randomRoomId, roomOptions);
        }
        else
        {
            Debug.LogWarning("Not connected to Photon yet. Please wait for connection.");
        }
    }

// THÊM: L?y thông tin pieces per player t? room
    public int GetPiecesPerPlayer()
    {
        if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("PiecesPerPlayer"))
        {
            return (int)PhotonNetwork.CurrentRoom.CustomProperties["PiecesPerPlayer"];
        }

        return 4; // M?c d?nh 4 quân n?u không có thông tin
    }

    // THÊM: Phuong th?c join room thu?n túy - không t? d?ng t?o phòng khi th?t b?i
    // S?A: Phuong th?c join room thu?n túy - thêm ki?m tra màu cu
    public void JoinRoomOnly(string roomName)
    {
        if (PhotonNetwork.IsConnectedAndReady)
        {
            this.roomName = roomName;
            lastAttemptedRoom = roomName;

            // THÊM: Ki?m tra và xóa màu cu n?u không ph?i rejoin
            CheckAndClearPreviousColorIfNotRejoining(roomName);

            Debug.Log($"Attempting to join room ONLY: {roomName}");

            // Ch? join room, không t?o phòng khi th?t b?i
            PhotonNetwork.JoinRoom(roomName);
        }
        else
        {
            Debug.LogWarning("Not connected to Photon yet. Please wait for connection.");
        }
    }

    // THÊM: Phuong th?c kích ho?t quân c? d?a trên màu du?c phân ph?i
    // THÊM: Phuong th?c kích ho?t quân c? d?a trên màu du?c phân ph?i và s? lu?ng quân m?i ngu?i
    public void ActivatePiecesForPlayers()
    {
        if (!PhotonNetwork.InRoom) return;

        PieceController[] allPieces = FindObjectsOfType<PieceController>(true); // Tìm c? nh?ng cái dang t?t
        List<PlayerColor> activeColors = GetRoomPlayerColors();
        int piecesPerPlayer = GetPiecesPerPlayer();

        Debug.Log(
            $"Activating {piecesPerPlayer} pieces per player for {activeColors.Count} colors: {string.Join(", ", activeColors)}");

        // T?o dictionary d? d?m s? quân dã kích ho?t cho m?i màu
        Dictionary<PlayerColor, int> activatedCount = new Dictionary<PlayerColor, int>();
        foreach (var color in activeColors)
        {
            activatedCount[color] = 0;
        }

        foreach (PieceController piece in allPieces)
        {
            if (activeColors.Contains(piece.playerColor))
            {
                // Ki?m tra xem dã kích ho?t d? s? quân cho màu này chua
                if (activatedCount[piece.playerColor] < piecesPerPlayer)
                {
                    piece.gameObject.SetActive(true);

                    // THÊM: G?i phuong th?c activate n?u có
                    piece.ActivateForPlayer();

                    activatedCount[piece.playerColor]++;
                    Debug.Log(
                        $"Activated piece: {piece.playerColor} ({activatedCount[piece.playerColor]}/{piecesPerPlayer})");
                }
                else
                {
                    piece.gameObject.SetActive(false);
                    Debug.Log($"Deactivated piece (limit reached): {piece.playerColor}");
                }
            }
            else
            {
                piece.gameObject.SetActive(false);
                Debug.Log($"Deactivated piece (color not active): {piece.playerColor}");
            }
        }

        // Log t?ng k?t
        foreach (var color in activeColors)
        {
            Debug.Log($"Final: {color} has {activatedCount[color]}/{piecesPerPlayer} pieces active");
        }
    }

    // THÊM: Phuong th?c d? các script khác ki?m tra s? lu?ng quân t?i da
    public int GetMaxPiecesPerPlayer()
    {
        return GetPiecesPerPlayer();
    }

    // THÊM: Callback khi room properties thay d?i (d? c?p nh?t s? lu?ng quân)
    // THÊM: Callback khi room properties thay d?i (d? bi?t khi game b?t d?u)
    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey("GameStarted"))
        {
            bool gameStarted = (bool)propertiesThatChanged["GameStarted"];
            if (gameStarted)
            {
                Debug.Log("Game dã du?c b?t d?u b?i Master Client");

                // Ð?m b?o client cung kh?i t?o game
                GameTurnManager turnManager = FindObjectOfType<GameTurnManager>();
                if (turnManager != null && !turnManager.isInitialized)
                {
                    turnManager.InitializePlayerOrder(DiceController.Instance);
                }
            }
        }

        // Gi? l?i logic cu cho PiecesPerPlayer
        if (propertiesThatChanged.ContainsKey("PiecesPerPlayer"))
        {
            Debug.Log("PiecesPerPlayer changed, reactivating pieces...");
            ActivatePiecesForPlayers();
        }
        if (propertiesThatChanged.ContainsKey("PlayerColors"))
        {
            Debug.Log("PlayerColors changed, reactivating pieces...");
            if (propertiesThatChanged["PlayerColors"] is string playerColorsData)
            {
                UpdateColorCacheAndNotify(playerColorsData);
            }
            else
            {
                UpdateColorCacheAndNotify(null);
            }
            ActivatePiecesForPlayers();
        }
    }

    // THÊM: Coroutine d? kích ho?t quân c? sau m?t kho?ng delay v?i s? lu?ng chính xác
    private IEnumerator ActivatePiecesAfterDelay()
    {
        // Ð?i m?t frame d? d?m b?o t?t c? component dã kh?i t?o
        yield return new WaitForSeconds(0.5f);
        ActivatePiecesForPlayers(); // S?A: G?i phuong th?c m?i dã c?p nh?t
    }

    // THÊM: Phuong th?c ki?m tra và xóa màu cu khi join phòng không ph?i rejoin
    private void CheckAndClearPreviousColorIfNotRejoining(string roomName)
    {
        string lastRoom = PlayerPrefs.GetString($"LastRoom_{playerID}", "");

        // N?u join phòng KHÔNG PH?I là phòng cu -> xóa màu dã gán tru?c dó
        if (!string.IsNullOrEmpty(lastRoom) && roomName != lastRoom)
        {
            Debug.Log($"Joining new room '{roomName}', clearing previous color assignment from old room '{lastRoom}'");
            ClearAssignedColor(playerID);

            // Ð?ng th?i reset room cu
            ResetPreviousRoom();
        }
    }

    // THÊM: Phuong th?c d? ?n t?t c? quân c? khi r?i phòng
    public void HideAllPiecesOnLeave()
    {
        PieceController[] allPieces = FindObjectsOfType<PieceController>(true); // Tìm c? nh?ng cái dang t?t
        foreach (PieceController piece in allPieces)
        {
            piece.gameObject.SetActive(false);
            Debug.Log($"Ðã ?n quân c? {piece.playerColor} khi r?i phòng");
        }
    }

    // THÊM: Phuong th?c ki?m tra và b?t d?u game khi phòng full - CH? Master Client
    private void CheckAndStartGame()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.Log("Ch? Master Client m?i du?c b?t d?u game");
            return;
        }

        // Ki?m tra n?u phòng dã full và game chua du?c kh?i t?o
        if (PhotonNetwork.CurrentRoom.PlayerCount >= PhotonNetwork.CurrentRoom.MaxPlayers)
        {
            Debug.Log(
                $"Phòng dã d?y ({PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers}), Master Client b?t d?u kh?i t?o game...");

            // Ðánh d?u room dã b?t d?u game
            PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable
            {
                { "GameStarted", true }
            });

            // B?t d?u game sau m?t kho?ng delay ng?n
            StartCoroutine(StartGameAfterDelay(2f));
        }
    }

// THÊM: Coroutine d? b?t d?u game - CH? ch?y trên Master Client
    // THÊM: Coroutine d? b?t d?u game - CH? ch?y trên Master Client
    private IEnumerator StartGameAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Ki?m tra l?i d? ch?c ch?n ch? Master Client th?c hi?n
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.Log("Không ph?i Master Client, không kh?i t?o game");
            yield break;
        }

        // Tìm và kh?i t?o GameTurnManager
        GameTurnManager turnManager = FindObjectOfType<GameTurnManager>();
        if (turnManager != null && !turnManager.isInitialized)
        {
            Debug.Log("Master Client kh?i t?o lu?t choi...");
            turnManager.InitializePlayerOrder(DiceController.Instance);
        }
        else
        {
            Debug.LogWarning("Không tìm th?y GameTurnManager ho?c dã du?c kh?i t?o");
        }
    }


// THÊM: Ki?m tra xem game dã b?t d?u chua
    public bool IsGameStarted()
    {
        if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("GameStarted"))
        {
            return (bool)PhotonNetwork.CurrentRoom.CustomProperties["GameStarted"];
        }

        return false;
    }
}
