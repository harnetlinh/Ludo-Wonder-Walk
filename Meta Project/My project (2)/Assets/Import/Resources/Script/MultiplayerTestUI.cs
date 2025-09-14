using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;

// UI phòng đơn giản để test multiplayer và tương tác vật thể
public class MultiplayerTestUI : MonoBehaviourPunCallbacks
{
    [Header("UI Elements")]
    public GameObject mainMenuPanel;
    public GameObject roomPanel;
    public GameObject loadingPanel;
    public Button connectButton;
    public Button createRoomButton;
    public Button joinRoomButton;
    public Button leaveRoomButton;
    public Button spawnCubeButton;
    public Button spawnSphereButton;
    public InputField roomNameInput;
    public Text connectionStatusText;
    public Text roomInfoText;
    public Text playerCountText;
    public Text loadingText;
    
    [Header("Spawn Objects")]
    public GameObject cubePrefab;
    public GameObject spherePrefab;
    public Transform spawnPoint;
    
    private void Start()
    {
        // UI này nên hoạt động cho mọi client cục bộ để quản lý kết nối
        // Không cần kiểm tra photonView.IsMine cho UI quản lý phòng
        SetupUI();
        ShowMainMenu(); // Hiển thị main menu đầu tiên
    }
    
    private void SetupUI()
    {
        // Gán các button events
        if (connectButton != null)
            connectButton.onClick.AddListener(ConnectToPhoton);
            
        if (createRoomButton != null)
            createRoomButton.onClick.AddListener(CreateRoom);
            
        if (joinRoomButton != null)
            joinRoomButton.onClick.AddListener(JoinRoom);
            
        if (leaveRoomButton != null)
            leaveRoomButton.onClick.AddListener(LeaveRoom);
            
        if (spawnCubeButton != null)
            spawnCubeButton.onClick.AddListener(SpawnCube);
            
        if (spawnSphereButton != null)
            spawnSphereButton.onClick.AddListener(SpawnSphere);
    }
    
    private void Update()
    {
        // UI này nên hoạt động cho mọi client cục bộ để quản lý kết nối
        // Không cần kiểm tra photonView.IsMine cho UI quản lý phòng
        
        // Chỉ update UI nếu đã setup xong
        if (mainMenuPanel != null || roomPanel != null || loadingPanel != null)
        {
            UpdateUI();
        }
    }
    
    private void UpdateUI()
    {
        // Cập nhật trạng thái kết nối
        if (connectionStatusText != null)
        {
            try
            {
                if (PhotonNetwork.IsConnected)
                {
                    if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null)
                    {
                        connectionStatusText.text = "Đã kết nối - Trong phòng";
                        connectionStatusText.color = Color.green;
                    }
                    else
                    {
                        connectionStatusText.text = "Đã kết nối - Chưa vào phòng";
                        connectionStatusText.color = Color.yellow;
                    }
                }
                else
                {
                    connectionStatusText.text = "Chưa kết nối";
                    connectionStatusText.color = Color.red;
                }
            }
            catch (System.Exception e)
            {
                connectionStatusText.text = "Lỗi kết nối";
                connectionStatusText.color = Color.red;
                Debug.LogError($"UI Update Error: {e.Message}");
            }
        }
        
        // Cập nhật thông tin phòng
        if (roomInfoText != null)
        {
            try
            {
                if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null)
                {
                    roomInfoText.text = $"Phòng: {PhotonNetwork.CurrentRoom.Name}";
                }
                else
                {
                    roomInfoText.text = "Chưa vào phòng";
                }
            }
            catch (System.Exception e)
            {
                roomInfoText.text = "Lỗi thông tin phòng";
                Debug.LogError($"Room Info Error: {e.Message}");
            }
        }
        
        // Cập nhật số người chơi
        if (playerCountText != null)
        {
            try
            {
                if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null)
                {
                    playerCountText.text = $"Người chơi: {PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers}";
                }
                else
                {
                    playerCountText.text = "Người chơi: 0/0";
                }
            }
            catch (System.Exception e)
            {
                playerCountText.text = "Lỗi số người chơi";
                Debug.LogError($"Player Count Error: {e.Message}");
            }
        }
    }
    
    // Hiển thị chỉ 1 panel tại một thời điểm
    private void ShowMainMenu()
    {
        SetPanelActive(mainMenuPanel, true);
        SetPanelActive(roomPanel, false);
        SetPanelActive(loadingPanel, false);
    }
    
    private void ShowRoomPanel()
    {
        SetPanelActive(mainMenuPanel, false);
        SetPanelActive(roomPanel, true);
        SetPanelActive(loadingPanel, false);
    }
    
    private void ShowLoadingPanel(string message = "Đang tải...")
    {
        SetPanelActive(mainMenuPanel, false);
        SetPanelActive(roomPanel, false);
        SetPanelActive(loadingPanel, true);
        
        if (loadingText != null)
        {
            loadingText.text = message;
        }
    }
    
    private void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
        {
            panel.SetActive(active);
        }
    }
    
    public void ConnectToPhoton()
    {
        if (!PhotonNetwork.IsConnected)
        {
            ShowLoadingPanel("Đang kết nối...");
            PhotonNetwork.ConnectUsingSettings();
        }
    }
    
    public void CreateRoom()
    {
        if (PhotonNetwork.IsConnected && !PhotonNetwork.InRoom)
        {
            string roomName = string.IsNullOrEmpty(roomNameInput.text) ? "TestRoom" : roomNameInput.text;
            ShowLoadingPanel($"Đang tạo phòng '{roomName}'...");
            
            RoomOptions roomOptions = new RoomOptions
            {
                MaxPlayers = 2,
                IsVisible = true,
                IsOpen = true
            };
            
            PhotonNetwork.CreateRoom(roomName, roomOptions);
        }
    }
    
    public void JoinRoom()
    {
        if (PhotonNetwork.IsConnected && !PhotonNetwork.InRoom)
        {
            string roomName = string.IsNullOrEmpty(roomNameInput.text) ? "TestRoom" : roomNameInput.text;
            ShowLoadingPanel($"Đang vào phòng '{roomName}'...");
            PhotonNetwork.JoinRoom(roomName);
        }
    }
    
    public void LeaveRoom()
    {
        if (PhotonNetwork.InRoom)
        {
            ShowLoadingPanel("Đang rời phòng...");
            PhotonNetwork.LeaveRoom();
        }
    }
    
    public void SpawnCube()
    {
        if (PhotonNetwork.InRoom)
        {
            Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            PhotonNetwork.Instantiate("TestCube", spawnPos, Quaternion.identity);
        }
    }
    
    public void SpawnSphere()
    {
        if (PhotonNetwork.InRoom)
        {
            Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            PhotonNetwork.Instantiate("TestSphere", spawnPos, Quaternion.identity);
        }
    }
    
    // Photon Callbacks
    public override void OnConnectedToMaster()
    {
        Debug.Log("Đã kết nối tới Master Server");
        ShowMainMenu(); // Quay về main menu sau khi kết nối
        
        // Force update UI ngay lập tức
        UpdateUI();
    }
    
    public override void OnJoinedRoom()
    {
        Debug.Log($"Đã vào phòng: {PhotonNetwork.CurrentRoom.Name}");
        ShowRoomPanel(); // Hiển thị room panel khi vào phòng
        
        // Force update UI ngay lập tức
        UpdateUI();
    }
    
    public override void OnLeftRoom()
    {
        Debug.Log("Đã rời phòng");
        ShowMainMenu(); // Quay về main menu sau khi rời phòng
        
        // Force update UI ngay lập tức
        UpdateUI();
    }
    
    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Tạo phòng thất bại: {message}");
        ShowMainMenu(); // Quay về main menu nếu tạo phòng thất bại
    }
    
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Vào phòng thất bại: {message}");
        ShowMainMenu(); // Quay về main menu nếu vào phòng thất bại
    }
    
    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.Log($"Mất kết nối: {cause}");
        ShowMainMenu(); // Quay về main menu khi mất kết nối
        
        // Force update UI ngay lập tức
        UpdateUI();
    }
}
