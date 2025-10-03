using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class PhotonManager : MonoBehaviourPunCallbacks
{
    public static PhotonManager Instance { get; private set; }

    [Header("Connection Settings")]
    public bool autoConnectOnStart = true;
    public string roomName = "TestRoom";
    public int maxPlayers = 4;
    
    [Header("Debug")]
    public bool enableDetailedLogging = true;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
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

        // Cấu hình Photon để đồng bộ tốt hơn - THỐNG NHẤT CẤU HÌNH
        PhotonNetwork.SendRate = 60; // Giảm để tránh spam
        PhotonNetwork.SerializationRate = 10; // Giảm để tránh spam

        // Debug connection info
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


    public override void OnJoinedRoom()
    {
        Debug.Log($"Joined room successfully as {PhotonNetwork.NickName}. Total players: {PhotonNetwork.CurrentRoom.PlayerCount}");
        Debug.Log($"Is Master Client: {PhotonNetwork.IsMasterClient}");

        // Không spawn object nữa - sử dụng object có sẵn trong scene
        Debug.Log("Ready to interact with existing scene objects");

        // Tìm tất cả PhotonView trong scene và log thông tin ownership
        PhotonView[] scenePhotonViews = FindObjectsOfType<PhotonView>();
        foreach (PhotonView pv in scenePhotonViews)
        {
            Debug.Log($"Scene PhotonView found: {pv.gameObject.name}, Owner: {pv.Owner?.NickName ?? "None"}, IsMine: {pv.IsMine}");
        }
    }


    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"Player {newPlayer.NickName} joined the room. Total players: {PhotonNetwork.CurrentRoom.PlayerCount}");
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"Player {otherPlayer.NickName} left the room. Total players: {PhotonNetwork.CurrentRoom.PlayerCount}");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"Disconnected from Photon: {cause}");
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("=== CONNECTED TO MASTER SERVER ===");
        if (enableDetailedLogging)
        {
            Debug.Log($"Server Address: {PhotonNetwork.ServerAddress}");
            Debug.Log($"Cloud Region: {PhotonNetwork.CloudRegion}");
        }

        // Đặt nickname cho player - ĐẢM BẢO NICKNAME DUY NHẤT
        if (string.IsNullOrEmpty(PhotonNetwork.NickName))
        {
            PhotonNetwork.NickName = "Player" + System.DateTime.Now.Millisecond + Random.Range(100, 999);
        }

        Debug.Log($"Player Nickname: {PhotonNetwork.NickName}");

        // Tạo room options với cấu hình rõ ràng
        RoomOptions roomOptions = new RoomOptions();
        roomOptions.MaxPlayers = maxPlayers;
        roomOptions.IsVisible = true;
        roomOptions.IsOpen = true;
        roomOptions.EmptyRoomTtl = 30000; // 30 giây
        roomOptions.PlayerTtl = 30000; // 30 giây

        Debug.Log($"Attempting to join or create room '{roomName}'...");
        PhotonNetwork.JoinOrCreateRoom(roomName, roomOptions, TypedLobby.Default);
    }

    // Thêm method để connect manually
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

    // Thêm method để join room cụ thể
    public void JoinRoom(string roomName)
    {
        if (PhotonNetwork.IsConnectedAndReady)
        {
            this.roomName = roomName;
            RoomOptions roomOptions = new RoomOptions();
            roomOptions.MaxPlayers = maxPlayers;
            roomOptions.IsVisible = true;
            roomOptions.IsOpen = true;
            
            PhotonNetwork.JoinOrCreateRoom(roomName, roomOptions, TypedLobby.Default);
        }
        else
        {
            Debug.LogWarning("Not connected to Photon yet. Please wait for connection.");
        }
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Failed to join room '{roomName}': {returnCode} - {message}");
        
        // Thử tạo room mới nếu join thất bại
        if (returnCode == ErrorCode.GameDoesNotExist)
        {
            Debug.Log("Room doesn't exist, creating new room...");
            RoomOptions roomOptions = new RoomOptions();
            roomOptions.MaxPlayers = maxPlayers;
            roomOptions.IsVisible = true;
            roomOptions.IsOpen = true;
            
            PhotonNetwork.CreateRoom(roomName, roomOptions, TypedLobby.Default);
        }
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Failed to create room '{roomName}': {returnCode} - {message}");
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.LogError($"Failed to join random room: {returnCode} - {message}");
    }

    // Thêm callback để debug connection state
    
    public override void OnRegionListReceived(RegionHandler regionHandler)
    {
        if (enableDetailedLogging)
        {
            Debug.Log("Available regions:");
            foreach (var region in regionHandler.EnabledRegions)
            {
                Debug.Log($"- {region.Code}: {region.HostAndPort}");
            }
        }
    }
}