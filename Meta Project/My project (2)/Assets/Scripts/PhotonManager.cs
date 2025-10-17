using System.Collections;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using System.Linq;

public class PhotonManager : MonoBehaviourPunCallbacks
{
    public static PhotonManager Instance { get; private set; }

    [Header("Connection Settings")] public bool autoConnectOnStart = true;
    public string roomName = "TestRoom";
    public int maxPlayers = 4;

    [Header("Room Lock Settings")] public int minPlayersToLock = 2;
    public int turnsToLock = 3;
    public bool allowRejoiningLockedRoom = true; // THÊM: Cho phép rejoining

    [Header("Debug")] public bool enableDetailedLogging = true;

    // ID của người chơi hiện tại
    private string playerID;

    private string lastAttemptedRoom = "";

    // THÊM: Delegate để thông báo sự kiện join room thất bại
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

        if (autoConnectOnStart)
        {
            Debug.Log("Attempting to connect to Photon...");
            PhotonNetwork.ConnectUsingSettings();
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

        // Đặt nickname với ID (rút ngắn để dễ nhìn)
        PhotonNetwork.NickName = $"Player_{playerID}";

        Debug.Log($"Player Nickname: {PhotonNetwork.NickName}");

        // SỬA: KHÔNG tự động thử join room cũ nữa
        // Chỉ kết nối và để người dùng chọn thủ công
        Debug.Log("Đã kết nối thành công. Vui lòng chọn phòng thủ công từ menu.");

        // Có thể thêm sự kiện để UI cập nhật trạng thái
        // UI sẽ tự động cập nhật rejoin button thông qua UpdateRejoinButton()
    }

    private bool TryJoinPreviousRoom()
    {
        string lastRoom = PlayerPrefs.GetString($"LastRoom_{playerID}", "");
        if (!string.IsNullOrEmpty(lastRoom))
        {
            Debug.Log($"Attempting to rejoin previous room: {lastRoom}");
            lastAttemptedRoom = lastRoom;

            // THÊM: Kiểm tra xem room có tồn tại không trước khi join
            // Sử dụng JoinRoom thay vì các phương thức khác để Photon tự xử lý
            PhotonNetwork.JoinRoom(lastRoom);
            return true;
        }

        return false;
    }

    private void CreateOrJoinRoom()
    {
        RoomOptions roomOptions = new RoomOptions();
        roomOptions.MaxPlayers = (byte)maxPlayers;

        // SỬA QUAN TRỌNG: Luôn để room mở cho đến khi đủ điều kiện khóa
        // Điều này cho phép rejoining hoạt động
        roomOptions.IsVisible = true;
        roomOptions.IsOpen = true; // QUAN TRỌNG: Luôn mở khi tạo room

        roomOptions.EmptyRoomTtl = 15000;
        roomOptions.PlayerTtl = 1000;

        // Thêm custom properties
        roomOptions.CustomRoomProperties = new ExitGames.Client.Photon.Hashtable
        {
            { "PlayerIDs", "" },
            { "IsLocked", false },
            { "TurnCount", 0 },
            { "AllowedPlayers", "" } // Danh sách player được phép vào khi room locked
        };
        roomOptions.CustomRoomPropertiesForLobby =
            new string[] { "PlayerIDs", "IsLocked", "TurnCount", "AllowedPlayers" };

        Debug.Log($"Attempting to join or create room '{roomName}'...");
        lastAttemptedRoom = roomName;
        PhotonNetwork.JoinOrCreateRoom(roomName, roomOptions, TypedLobby.Default);
    }

    // THÊM: Lấy danh sách màu quân cờ được chọn ngẫu nhiên
    public List<PlayerColor> GetRandomPlayerColors(int playerCount)
    {
        List<PlayerColor> allColors = new List<PlayerColor>
        {
            PlayerColor.Red,
            PlayerColor.Blue,
            PlayerColor.Yellow,
            PlayerColor.Green
        };

        // Xáo trộn danh sách màu
        System.Random rng = new System.Random();
        List<PlayerColor> shuffledColors = allColors.OrderBy(x => rng.Next()).ToList();

        // Lấy số lượng màu tương ứng với số người chơi
        return shuffledColors.Take(playerCount).ToList();
    }

// THÊM: Lấy màu của player hiện tại
    public PlayerColor GetCurrentPlayerColor()
    {
        if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("PlayerColors"))
        {
            string playerColorsData = (string)PhotonNetwork.CurrentRoom.CustomProperties["PlayerColors"];
            if (!string.IsNullOrEmpty(playerColorsData))
            {
                string[] entries = playerColorsData.Split(';');
                foreach (string entry in entries)
                {
                    string[] parts = entry.Split(':');
                    if (parts.Length == 2 && parts[0] == playerID)
                    {
                        return (PlayerColor)System.Enum.Parse(typeof(PlayerColor), parts[1]);
                    }
                }
            }
        }

        return PlayerColor.None;
    }

// THÊM: Lấy tất cả màu đang được sử dụng trong room
    public List<PlayerColor> GetRoomPlayerColors()
    {
        List<PlayerColor> colors = new List<PlayerColor>();

        if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("PlayerColors"))
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
                        colors.Add((PlayerColor)System.Enum.Parse(typeof(PlayerColor), parts[1]));
                    }
                }
            }
        }

        return colors;
    }

// THÊM: Phân phối màu cho người chơi khi tạo phòng

// THÊM: Lấy màu đã được gán trước đó cho player
    private PlayerColor GetPreviouslyAssignedColor(string playerId)
    {
        string key = $"AssignedColor_{playerId}";
        if (PlayerPrefs.HasKey(key))
        {
            return (PlayerColor)PlayerPrefs.GetInt(key, 0);
        }

        return PlayerColor.None;
    }

// THÊM: Lưu màu đã gán cho player
    private void SaveAssignedColor(string playerId, PlayerColor color)
    {
        string key = $"AssignedColor_{playerId}";
        PlayerPrefs.SetInt(key, (int)color);
        PlayerPrefs.Save();
        Debug.Log($"Saved color {color} for player {playerId}");
    }

// THÊM: Xóa màu đã gán (khi player rời phòng hoàn toàn)
    private void ClearAssignedColor(string playerId)
    {
        string key = $"AssignedColor_{playerId}";
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
        Debug.Log($"Cleared color for player {playerId}");
    }

// Trong PhotonManager.cs, SỬA LẠI HOÀN TOÀN phương thức AssignPlayerColors:

// SỬA: Phương thức AssignPlayerColors để xử lý màu trùng thông minh hơn
    private void AssignPlayerColors()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        Debug.Log("=== ASSIGNING PLAYER COLORS ===");

        // Tạo dictionary để theo dõi màu đã gán
        Dictionary<string, PlayerColor> assignedColors = new Dictionary<string, PlayerColor>();
        List<PlayerColor> usedColors = new List<PlayerColor>();

        Player[] players = PhotonNetwork.PlayerList;

        // BƯỚC 1: Gán màu cũ CHO TỪNG PLAYER và kiểm tra trùng
        foreach (Player player in players)
        {
            string playerId = GetPlayerIDFromNickname(player.NickName);
            PlayerColor previousColor = GetPreviouslyAssignedColor(playerId);

            if (previousColor != PlayerColor.None)
            {
                // KIỂM TRA TRÙNG: Nếu màu cũ đã được player khác sử dụng
                if (usedColors.Contains(previousColor))
                {
                    Debug.LogWarning($"Color conflict: {previousColor} is already used by another player");
                    // KHÔNG gán màu cũ trong trường hợp này, sẽ gán màu mới ở bước sau
                    continue;
                }

                assignedColors[playerId] = previousColor;
                usedColors.Add(previousColor);
                Debug.Log($"Reassigned previous color {previousColor} to rejoining player {playerId}");
            }
        }

        // BƯỚC 2: Gán màu mới cho player chưa có màu hoặc bị trùng màu
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
                // Tìm màu chưa được sử dụng
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

        // Cập nhật room properties
        UpdatePlayerColorsInRoom(assignedColors);
    }

// THÊM: Phương thức cập nhật màu trong room
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
    }

// THÊM: Phương thức xử lý khi player rejoins
    private void HandlePlayerRejoining(Player rejoiningPlayer)
    {
        string playerId = GetPlayerIDFromNickname(rejoiningPlayer.NickName);
        PlayerColor previousColor = GetPreviouslyAssignedColor(playerId);

        if (previousColor != PlayerColor.None)
        {
            Debug.Log($"Player {playerId} rejoined with previous color: {previousColor}");

            // Đảm bảo màu này được giữ nguyên trong room properties
            AssignPlayerColors(); // Gọi lại để cập nhật
        }
    }

// THÊM: Lấy PlayerID từ nickname
    private string GetPlayerIDFromNickname(string nickname)
    {
        if (nickname.StartsWith("Player_") && nickname.Length > 7)
        {
            return nickname.Substring(7);
        }

        return "";
    }

// THÊM: Phương thức để kiểm tra và fix màu bị sai
    public void ValidateAndFixPlayerColors()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        Debug.Log("Validating player color assignments...");

        bool needsFix = false;
        Dictionary<string, PlayerColor> currentAssignments = new Dictionary<string, PlayerColor>();

        // Lấy assignments hiện tại từ room properties
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

        // Kiểm tra từng player hiện tại
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            string playerId = GetPlayerIDFromNickname(player.NickName);
            PlayerColor savedColor = GetPreviouslyAssignedColor(playerId);

            if (savedColor != PlayerColor.None)
            {
                // Kiểm tra xem màu saved có khớp với màu trong room không
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

        // LUÔN LƯU ROOM HIỆN TẠI
        PlayerPrefs.SetString($"LastRoom_{playerID}", currentRoomName);
        PlayerPrefs.Save();

        // TẢI LẠI TRẠNG THÁI GAME NẾU CÓ
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.LoadGameStateFromRoomProperties();

            // In debug info
            GameStateManager.Instance.PrintGameState();
        }

        if (PhotonNetwork.IsMasterClient)
        {
            UpdateRoomPlayerIDs();
            AssignPlayerColors();
        }
        else if (isRejoining)
        {
            Debug.Log($"Player {playerID} is rejoining, should keep previous color");
        }

        // Kích hoạt quân cờ sau khi join room
        StartCoroutine(ActivatePiecesAfterDelay());
    }

// THÊM: Coroutine để kích hoạt quân cờ sau một khoảng delay


    private void UpdateRoomPlayerIDs()
    {
        var currentIDs = GetCurrentPlayerIDs();
        string allowedPlayers = string.Join(",", currentIDs);

        PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable
        {
            { "PlayerIDs", allowedPlayers },
            { "AllowedPlayers", allowedPlayers } // Cập nhật danh sách allowed players
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

    // THÊM: Kiểm tra xem player hiện tại có trong danh sách allowed không
    // SỬA: Kiểm tra xem player hiện tại có trong danh sách allowed không
    public bool IsPlayerAllowedInRoom(string roomName)
    {
        // Nếu chưa có thông tin room, mặc định cho phép
        if (string.IsNullOrEmpty(roomName)) return true;

        // Kiểm tra nếu đây là room cũ của player - LUÔN CHO PHÉP VÀO LẠI PHÒNG CŨ
        string lastRoom = PlayerPrefs.GetString($"LastRoom_{playerID}", "");
        if (roomName == lastRoom)
        {
            Debug.Log($"Player {playerID} is allowed to rejoin previous room: {lastRoom}");
            return true;
        }

        // Nếu không phải room cũ, kiểm tra thêm điều kiện khác
        // (giữ nguyên logic kiểm tra khác nếu có)
        return true; // Tạm thời luôn cho phép, có thể điều chỉnh sau
    }

    // Trong PhotonManager.cs, THÊM các phương thức sau:

// THÊM: Callback khi player properties thay đổi (để phát hiện rejoining)
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        // Phát hiện khi player rejoining bằng cách theo dõi sự thay đổi trạng thái
        if (changedProps.ContainsKey("IsInactive"))
        {
            bool isInactive = (bool)changedProps["IsInactive"];
            if (!isInactive)
            {
                // Player đã trở lại active -> rejoining
                Debug.Log($"Player {targetPlayer.NickName} became active (rejoining)");
                HandlePlayerRejoining(targetPlayer);
            }
        }
    }

// SỬA: OnPlayerEnteredRoom để xử lý rejoining
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log(
            $"Player {newPlayer.NickName} entered the room. Total players: {PhotonNetwork.CurrentRoom.PlayerCount}");

        // KIỂM TRA: Đây có phải là player rejoining không?
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
            AssignPlayerColors(); // LUÔN gán màu khi có player mới

            // THÊM: Kiểm tra và bắt đầu game nếu phòng full
            CheckAndStartGame();

            // Kích hoạt lại quân cờ khi có người mới
            StartCoroutine(ActivatePiecesAfterDelay());
        }
        else
        {
            // Client cũng cần cập nhật khi có player mới
            StartCoroutine(ActivatePiecesAfterDelay());
        }
    }

// THÊM: Phương thức force reassign màu khi cần
    public void ForceColorReassignment()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("Forcing color reassignment for all players");
            AssignPlayerColors();
        }
    }

    // SỬA: OnPlayerLeftRoom để không xóa màu ngay lập tức
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log(
            $"Player {otherPlayer.NickName} left the room. Total players: {PhotonNetwork.CurrentRoom.PlayerCount}");

        string leftPlayerId = GetPlayerIDFromNickname(otherPlayer.NickName);

        // QUAN TRỌNG: KHÔNG xóa màu ngay lập tức khi player rời
        // Chỉ đánh dấu player là inactive, giữ màu để cho phép rejoining
        Debug.Log($"Player {leftPlayerId} left, but keeping assigned color for potential rejoining");

        if (PhotonNetwork.IsMasterClient)
        {
            UpdateRoomPlayerIDs();
            // KHÔNG gán lại màu ngay lập tức, chờ xem player có rejoining không
            // Chỉ gán lại sau một khoảng thời gian nếu cần
            StartCoroutine(DelayedColorCleanup(leftPlayerId, 180f)); // Chờ 10 giây
        }
    }

// THÊM: Coroutine để cleanup màu sau delay
    private IEnumerator DelayedColorCleanup(string playerId, float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);

        // Kiểm tra xem player có rejoined chưa
        bool playerRejoined = PhotonNetwork.PlayerList.Any(p => GetPlayerIDFromNickname(p.NickName) == playerId);

        if (!playerRejoined)
        {
            // Nếu sau delay mà player không rejoined, thì xóa màu
            ClearAssignedColor(playerId);
            Debug.Log($"Player {playerId} did not rejoin after {delaySeconds} seconds, cleared assigned color");

            // Gán lại màu cho các player còn lại
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
            // SỬA QUAN TRỌNG: KHÔNG khóa room hoàn toàn, chỉ đánh dấu là locked
            // Giữ room mở để cho phép rejoining, nhưng đánh dấu custom property
            PhotonNetwork.CurrentRoom.IsOpen = true; // VẪN MỞ để cho phép rejoining

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

    // SỬA: Xử lý lỗi join room tốt hơn
    // SỬA: Xử lý lỗi join room tốt hơn
    // SỬA: Xử lý lỗi join room - KHÔNG tự động tạo phòng
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Failed to join room '{lastAttemptedRoom}': {returnCode} - {message}");

        // THÊM: Gọi sự kiện để UI xử lý
        OnJoinRoomFailedEvent?.Invoke(returnCode, message);

        // KHÔNG tự động tạo phòng nữa - để UI quyết định hành động tiếp theo
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

    // Method để connect manually
    public void ConnectToPhoton()
    {
        if (!PhotonNetwork.IsConnected)
        {
            Debug.Log("Connecting to Photon...");
            PhotonNetwork.ConnectUsingSettings();
        }
        else
        {
            Debug.Log("Already connected to Photon");
        }
    }

    // SỬA: Method để join room cụ thể với kiểm tra
    // SỬA: Method để join room cụ thể với kiểm tra
    // SỬA: Method để join room cụ thể với kiểm tra
    // SỬA: Method để join room cụ thể với kiểm tra màu cũ
    public void JoinRoom(string roomName)
    {
        if (PhotonNetwork.IsConnectedAndReady)
        {
            this.roomName = roomName;
            lastAttemptedRoom = roomName;

            // THÊM: Kiểm tra và xóa màu cũ nếu không phải rejoin
            CheckAndClearPreviousColorIfNotRejoining(roomName);

            Debug.Log($"Attempting to join room: {roomName}");

            // Sử dụng JoinRoom thay vì CreateOrJoinRoom để chỉ join room có sẵn
            PhotonNetwork.JoinRoom(roomName);
        }
        else
        {
            Debug.LogWarning("Not connected to Photon yet. Please wait for connection.");
        }
    }

    // Lấy Player ID hiện tại
    public string GetCurrentPlayerID()
    {
        return playerID;
    }

    // THÊM: Lấy room name đã attempt cuối cùng
    public string GetLastAttemptedRoom()
    {
        return lastAttemptedRoom;
    }

    // THÊM: Reset room cũ của player (dùng khi player muốn join room mới)
    public void ResetPreviousRoom()
    {
        PlayerPrefs.DeleteKey($"LastRoom_{playerID}");
        PlayerPrefs.Save();
        Debug.Log("Previous room record has been reset");
    }

// THÊM: Lấy room cũ của player
    public string GetPreviousRoom()
    {
        return PlayerPrefs.GetString($"LastRoom_{playerID}", "");
    }

    // THÊM: Kiểm tra xem player có nên được phép rejoining không
    public bool ShouldAllowRejoining(string roomName)
    {
        // Luôn cho phép rejoining room cũ
        string lastRoom = PlayerPrefs.GetString($"LastRoom_{playerID}", "");
        if (roomName == lastRoom)
        {
            Debug.Log($"Allowing rejoining for previous room: {lastRoom}");
            return true;
        }

        // Kiểm tra thêm điều kiện khác nếu cần
        return allowRejoiningLockedRoom;
    }


    // THÊM: Tạo room với ID ngẫu nhiên
    public void CreateRandomRoom(int maxPlayers, int piecesPerPlayer)
    {
        if (PhotonNetwork.IsConnectedAndReady)
        {
            // Tạo room ID ngẫu nhiên
            string randomRoomId = System.Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
            this.roomName = randomRoomId;
            lastAttemptedRoom = randomRoomId;

            RoomOptions roomOptions = new RoomOptions();
            roomOptions.MaxPlayers = (byte)maxPlayers;
            roomOptions.IsVisible = true;
            roomOptions.IsOpen = true;
            roomOptions.EmptyRoomTtl = 15000;
            roomOptions.PlayerTtl = 1000;

            // THÊM: Lưu thông tin piecesPerPlayer vào room properties
            roomOptions.CustomRoomProperties = new ExitGames.Client.Photon.Hashtable
            {
                { "PlayerIDs", "" },
                { "IsLocked", false },
                { "TurnCount", 0 },
                { "AllowedPlayers", "" },
                { "PiecesPerPlayer", piecesPerPlayer }, // THÊM: Số quân cờ mỗi người
                { "RoomCreator", playerID } // THÊM: Người tạo phòng
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

// THÊM: Lấy thông tin pieces per player từ room
    public int GetPiecesPerPlayer()
    {
        if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("PiecesPerPlayer"))
        {
            return (int)PhotonNetwork.CurrentRoom.CustomProperties["PiecesPerPlayer"];
        }

        return 4; // Mặc định 4 quân nếu không có thông tin
    }

    // THÊM: Phương thức join room thuần túy - không tự động tạo phòng khi thất bại
    // SỬA: Phương thức join room thuần túy - thêm kiểm tra màu cũ
    public void JoinRoomOnly(string roomName)
    {
        if (PhotonNetwork.IsConnectedAndReady)
        {
            this.roomName = roomName;
            lastAttemptedRoom = roomName;

            // THÊM: Kiểm tra và xóa màu cũ nếu không phải rejoin
            CheckAndClearPreviousColorIfNotRejoining(roomName);

            Debug.Log($"Attempting to join room ONLY: {roomName}");

            // Chỉ join room, không tạo phòng khi thất bại
            PhotonNetwork.JoinRoom(roomName);
        }
        else
        {
            Debug.LogWarning("Not connected to Photon yet. Please wait for connection.");
        }
    }

    // THÊM: Phương thức kích hoạt quân cờ dựa trên màu được phân phối
    // THÊM: Phương thức kích hoạt quân cờ dựa trên màu được phân phối và số lượng quân mỗi người
    public void ActivatePiecesForPlayers()
    {
        if (!PhotonNetwork.InRoom) return;

        PieceController[] allPieces = FindObjectsOfType<PieceController>(true); // Tìm cả những cái đang tắt
        List<PlayerColor> activeColors = GetRoomPlayerColors();
        int piecesPerPlayer = GetPiecesPerPlayer();

        Debug.Log(
            $"Activating {piecesPerPlayer} pieces per player for {activeColors.Count} colors: {string.Join(", ", activeColors)}");

        // Tạo dictionary để đếm số quân đã kích hoạt cho mỗi màu
        Dictionary<PlayerColor, int> activatedCount = new Dictionary<PlayerColor, int>();
        foreach (var color in activeColors)
        {
            activatedCount[color] = 0;
        }

        foreach (PieceController piece in allPieces)
        {
            if (activeColors.Contains(piece.playerColor))
            {
                // Kiểm tra xem đã kích hoạt đủ số quân cho màu này chưa
                if (activatedCount[piece.playerColor] < piecesPerPlayer)
                {
                    piece.gameObject.SetActive(true);

                    // THÊM: Gọi phương thức activate nếu có
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

        // Log tổng kết
        foreach (var color in activeColors)
        {
            Debug.Log($"Final: {color} has {activatedCount[color]}/{piecesPerPlayer} pieces active");
        }
    }

    // THÊM: Phương thức để các script khác kiểm tra số lượng quân tối đa
    public int GetMaxPiecesPerPlayer()
    {
        return GetPiecesPerPlayer();
    }

    // THÊM: Callback khi room properties thay đổi (để cập nhật số lượng quân)
    // THÊM: Callback khi room properties thay đổi (để biết khi game bắt đầu)
    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey("GameStarted"))
        {
            bool gameStarted = (bool)propertiesThatChanged["GameStarted"];
            if (gameStarted)
            {
                Debug.Log("Game đã được bắt đầu bởi Master Client");

                // Đảm bảo client cũng khởi tạo game
                GameTurnManager turnManager = FindObjectOfType<GameTurnManager>();
                if (turnManager != null && !turnManager.isInitialized)
                {
                    turnManager.InitializePlayerOrder(DiceController.Instance);
                }
            }
        }

        // Giữ lại logic cũ cho PiecesPerPlayer
        if (propertiesThatChanged.ContainsKey("PiecesPerPlayer"))
        {
            Debug.Log("PiecesPerPlayer changed, reactivating pieces...");
            ActivatePiecesForPlayers();
        }
        if (propertiesThatChanged.ContainsKey("PlayerColors"))
        {
            Debug.Log("PlayerColors changed, reactivating pieces...");
            ActivatePiecesForPlayers();
        }
    }

    // THÊM: Coroutine để kích hoạt quân cờ sau một khoảng delay với số lượng chính xác
    private IEnumerator ActivatePiecesAfterDelay()
    {
        // Đợi một frame để đảm bảo tất cả component đã khởi tạo
        yield return new WaitForSeconds(0.5f);
        ActivatePiecesForPlayers(); // SỬA: Gọi phương thức mới đã cập nhật
    }

    // THÊM: Phương thức kiểm tra và xóa màu cũ khi join phòng không phải rejoin
    private void CheckAndClearPreviousColorIfNotRejoining(string roomName)
    {
        string lastRoom = PlayerPrefs.GetString($"LastRoom_{playerID}", "");

        // Nếu join phòng KHÔNG PHẢI là phòng cũ -> xóa màu đã gán trước đó
        if (!string.IsNullOrEmpty(lastRoom) && roomName != lastRoom)
        {
            Debug.Log($"Joining new room '{roomName}', clearing previous color assignment from old room '{lastRoom}'");
            ClearAssignedColor(playerID);

            // Đồng thời reset room cũ
            ResetPreviousRoom();
        }
    }

    // THÊM: Phương thức để ẩn tất cả quân cờ khi rời phòng
    public void HideAllPiecesOnLeave()
    {
        PieceController[] allPieces = FindObjectsOfType<PieceController>(true); // Tìm cả những cái đang tắt
        foreach (PieceController piece in allPieces)
        {
            piece.gameObject.SetActive(false);
            Debug.Log($"Đã ẩn quân cờ {piece.playerColor} khi rời phòng");
        }
    }

    // THÊM: Phương thức kiểm tra và bắt đầu game khi phòng full - CHỈ Master Client
    private void CheckAndStartGame()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.Log("Chỉ Master Client mới được bắt đầu game");
            return;
        }

        // Kiểm tra nếu phòng đã full và game chưa được khởi tạo
        if (PhotonNetwork.CurrentRoom.PlayerCount >= PhotonNetwork.CurrentRoom.MaxPlayers)
        {
            Debug.Log(
                $"Phòng đã đầy ({PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers}), Master Client bắt đầu khởi tạo game...");

            // Đánh dấu room đã bắt đầu game
            PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable
            {
                { "GameStarted", true }
            });

            // Bắt đầu game sau một khoảng delay ngắn
            StartCoroutine(StartGameAfterDelay(2f));
        }
    }

// THÊM: Coroutine để bắt đầu game - CHỈ chạy trên Master Client
    // THÊM: Coroutine để bắt đầu game - CHỈ chạy trên Master Client
    private IEnumerator StartGameAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Kiểm tra lại để chắc chắn chỉ Master Client thực hiện
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.Log("Không phải Master Client, không khởi tạo game");
            yield break;
        }

        // Tìm và khởi tạo GameTurnManager
        GameTurnManager turnManager = FindObjectOfType<GameTurnManager>();
        if (turnManager != null && !turnManager.isInitialized)
        {
            Debug.Log("Master Client khởi tạo lượt chơi...");
            turnManager.InitializePlayerOrder(DiceController.Instance);
        }
        else
        {
            Debug.LogWarning("Không tìm thấy GameTurnManager hoặc đã được khởi tạo");
        }
    }


// THÊM: Kiểm tra xem game đã bắt đầu chưa
    public bool IsGameStarted()
    {
        if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("GameStarted"))
        {
            return (bool)PhotonNetwork.CurrentRoom.CustomProperties["GameStarted"];
        }

        return false;
    }
}
