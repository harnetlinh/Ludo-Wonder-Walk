using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using System.Linq;

public class PhotonManager : MonoBehaviourPunCallbacks
{
    public static PhotonManager Instance { get; private set; }

    [Header("Connection Settings")]
    public bool autoConnectOnStart = true;
    public string roomName = "TestRoom";
    public int maxPlayers = 4;
    
    [Header("Room Lock Settings")]
    public int minPlayersToLock = 2;
    public int turnsToLock = 3;
    public bool allowRejoiningLockedRoom = true; // THÊM: Cho phép rejoining
    
    [Header("Debug")]
    public bool enableDetailedLogging = true;

    // ID của người chơi hiện tại
    private string playerID;
    private string lastAttemptedRoom = "";
    

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
        PhotonNetwork.NickName = $"Player_{playerID.Substring(0, 6)}";

        Debug.Log($"Player Nickname: {PhotonNetwork.NickName}");

        // Thử join room cũ trước
        if (TryJoinPreviousRoom())
        {
            return;
        }

        // Nếu không join được room cũ, tạo/join room mới
        CreateOrJoinRoom();
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
    
        roomOptions.EmptyRoomTtl = 30000;
        roomOptions.PlayerTtl = 30000;

        // Thêm custom properties
        roomOptions.CustomRoomProperties = new ExitGames.Client.Photon.Hashtable
        {
            { "PlayerIDs", "" },
            { "IsLocked", false },
            { "TurnCount", 0 },
            { "AllowedPlayers", "" } // Danh sách player được phép vào khi room locked
        };
        roomOptions.CustomRoomPropertiesForLobby = new string[] { "PlayerIDs", "IsLocked", "TurnCount", "AllowedPlayers" };

        Debug.Log($"Attempting to join or create room '{roomName}'...");
        lastAttemptedRoom = roomName;
        PhotonNetwork.JoinOrCreateRoom(roomName, roomOptions, TypedLobby.Default);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"Joined room successfully as {PhotonNetwork.NickName}. Total players: {PhotonNetwork.CurrentRoom.PlayerCount}");
        Debug.Log($"Is Master Client: {PhotonNetwork.IsMasterClient}");

        // LUÔN LƯU ROOM HIỆN TẠI - ĐỂ CÓ THỂ REJOIN SAU NÀY
        PlayerPrefs.SetString($"LastRoom_{playerID}", PhotonNetwork.CurrentRoom.Name);
        PlayerPrefs.Save();

        // Thêm player ID vào room properties (chỉ master client)
        if (PhotonNetwork.IsMasterClient)
        {
            UpdateRoomPlayerIDs();
        }

        // Log thông tin PhotonView
        PhotonView[] scenePhotonViews = FindObjectsOfType<PhotonView>();
        foreach (PhotonView pv in scenePhotonViews)
        {
            Debug.Log($"Scene PhotonView found: {pv.gameObject.name}, Owner: {pv.Owner?.NickName ?? "None"}, IsMine: {pv.IsMine}");
        }
    }

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

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"Player {newPlayer.NickName} joined the room. Total players: {PhotonNetwork.CurrentRoom.PlayerCount}");

        if (PhotonNetwork.IsMasterClient)
        {
            CheckAndLockRoom();
            UpdateRoomPlayerIDs();
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"Player {otherPlayer.NickName} left the room. Total players: {PhotonNetwork.CurrentRoom.PlayerCount}");

        if (PhotonNetwork.IsMasterClient)
        {
            UpdateRoomPlayerIDs();
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
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Failed to join room '{lastAttemptedRoom}': {returnCode} - {message}");
    
        // Phân loại lỗi để xử lý phù hợp
        switch (returnCode)
        {
            case ErrorCode.GameDoesNotExist:
                Debug.Log("Room doesn't exist, creating new room...");
                CreateOrJoinRoom();
                break;
            
            case ErrorCode.GameClosed:
                Debug.LogWarning($"Room '{lastAttemptedRoom}' is closed/locked. Attempting special rejoin...");
            
                // THÊM: Thử rejoining bằng cách tạo room với cùng tên
                // Khi master client tạo room với tên đã tồn tại, nó sẽ trở thành rejoining
                if (IsPlayerAllowedInRoom(lastAttemptedRoom))
                {
                    Debug.Log($"Player is allowed to rejoin locked room '{lastAttemptedRoom}'");
                    roomName = lastAttemptedRoom;
                    CreateOrJoinRoom(); // Thử rejoining
                }
                else
                {
                    Debug.LogWarning($"Player is not allowed to rejoin locked room '{lastAttemptedRoom}'");
                    // Tạo room mới với tên khác
                    roomName = $"{lastAttemptedRoom}_{System.DateTime.Now:HHmmss}";
                    CreateOrJoinRoom();
                }
                break;
            
            case ErrorCode.GameFull:
                Debug.LogWarning($"Room '{lastAttemptedRoom}' is full. Cannot join.");
                // Tạo room mới
                roomName = $"{lastAttemptedRoom}_Full_{System.DateTime.Now:HHmmss}";
                CreateOrJoinRoom();
                break;
            
            default:
                Debug.LogWarning($"Unknown error joining room: {returnCode} - {message}");
                // Thử tạo room mới
                CreateOrJoinRoom();
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
    public void JoinRoom(string roomName)
    {
        if (PhotonNetwork.IsConnectedAndReady)
        {
            this.roomName = roomName;
            lastAttemptedRoom = roomName;
        
            // LUÔN CHO PHÉP JOIN ROOM - BỎ KIỂM TRA HẠN CHẾ
            // Player có thể vào bất kỳ room nào họ muốn, hệ thống Photon sẽ tự động xử lý
            // các trường hợp room đầy, room không tồn tại, etc.
        
            Debug.Log($"Attempting to join room: {roomName}");
            CreateOrJoinRoom();
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
}