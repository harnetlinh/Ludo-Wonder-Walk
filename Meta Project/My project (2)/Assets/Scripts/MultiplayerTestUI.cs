using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using TMPro;

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
    
    [Header("Player Info")]
    public TextMeshProUGUI playerIDText;
    public TextMeshProUGUI roomLockStatusText;
    public TextMeshProUGUI errorText; // THÊM: Hiển thị lỗi
    
    private void Start()
    {
        SetupUI();
        ShowMainMenu();
        ClearError(); // Xóa lỗi khi bắt đầu
    }
    
    private void SetupUI()
    {
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
        
        // Hiển thị Player ID
        if (playerIDText != null && PhotonManager.Instance != null)
        {
            playerIDText.text = $"ID: {PhotonManager.Instance.GetCurrentPlayerID().Substring(0, 8)}...";
        }
    
        // Hiển thị trạng thái khóa phòng
        if (roomLockStatusText != null && PhotonNetwork.InRoom)
        {
            bool isLocked = PhotonManager.Instance != null && PhotonManager.Instance.IsRoomLocked();
            roomLockStatusText.text = isLocked ? "🔒 Đã khóa" : "🔓 Mở";
            roomLockStatusText.color = isLocked ? Color.red : Color.green;
        }
    }
    
    // THÊM: Hiển thị lỗi
    private void ShowError(string message, bool isWarning = false)
    {
        if (errorText != null)
        {
            errorText.text = message;
            errorText.color = isWarning ? Color.yellow : Color.red;
            errorText.gameObject.SetActive(true);
            
            // Tự động ẩn lỗi sau 5 giây
            Invoke("ClearError", 5f);
        }
    }
    
    // THÊM: Xóa lỗi
    private void ClearError()
    {
        if (errorText != null)
        {
            errorText.text = "";
            errorText.gameObject.SetActive(false);
        }
    }
    
    // Hiển thị chỉ 1 panel tại một thời điểm
    private void ShowMainMenu()
    {
        SetPanelActive(mainMenuPanel, true);
        SetPanelActive(roomPanel, false);
        SetPanelActive(loadingPanel, false);
        ClearError();
    }
    
    private void ShowRoomPanel()
    {
        SetPanelActive(mainMenuPanel, false);
        SetPanelActive(roomPanel, true);
        SetPanelActive(loadingPanel, false);
        ClearError();
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
        ClearError();
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
                MaxPlayers = 4, // Sửa thành 4 để phù hợp với maxPlayers
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
        
            // BỎ KIỂM TRA HẠN CHẾ - LUÔN CHO PHÉP THỬ JOIN
            // Photon sẽ tự động xử lý các trường hợp lỗi
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
        ShowMainMenu();
        UpdateUI();
    }
    
    public override void OnJoinedRoom()
    {
        Debug.Log($"Đã vào phòng: {PhotonNetwork.CurrentRoom.Name}");
        ShowRoomPanel();
        UpdateUI();
        ClearError();
    }
    
    public override void OnLeftRoom()
    {
        Debug.Log("Đã rời phòng");
        ShowMainMenu();
        UpdateUI();
    }
    
    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Tạo phòng thất bại: {message}");
        ShowMainMenu();
        ShowError($"Tạo phòng thất bại: {message}");
    }
    
    // SỬA: Xử lý lỗi join room với thông báo rõ ràng
    // SỬA: Xử lý lỗi join room với thông báo rõ ràng
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Vào phòng thất bại: {message}");
    
        string roomName = PhotonManager.Instance?.GetLastAttemptedRoom() ?? "unknown";
        string errorMessage = "";
        bool isWarning = false;
    
        switch (returnCode)
        {
            case ErrorCode.GameClosed:
                errorMessage = $"Phòng '{roomName}' đã bị khóa. Đang thử kết nối lại...";
                isWarning = true;
                break;
            case ErrorCode.GameFull:
                errorMessage = $"Phòng '{roomName}' đã đầy. Đang thử tạo phòng mới...";
                isWarning = true;
                break;
            case ErrorCode.GameDoesNotExist:
                errorMessage = $"Phòng '{roomName}' không tồn tại. Đang thử tạo phòng mới...";
                isWarning = true;
                break;
            default:
                errorMessage = $"Không thể vào phòng '{roomName}': {message}";
                break;
        }
    
        // HIỂN THỊ LỖI TRÊN UI - sửa dòng này
        ShowError(errorMessage, isWarning);
    
        // KHÔNG hiện main menu ngay, để PhotonManager xử lý rejoining
        // ShowMainMenu(); // COMMENT DÒNG NÀY
    
        Debug.Log($"UI: {errorMessage}");
    }
    
    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.Log($"Mất kết nối: {cause}");
        ShowMainMenu();
        ShowError($"Mất kết nối: {cause}");
        UpdateUI();
    }
    
    // THÊM: Hiển thị trạng thái đang thử kết nối lại
    private void ShowReconnectingStatus(string roomName)
    {
        if (loadingText != null)
        {
            loadingText.text = $"Đang thử kết nối lại phòng '{roomName}'...";
        }
    
        if (errorText != null)
        {
            errorText.text = $"Phòng '{roomName}' đã khóa. Đang thử kết nối lại...";
            errorText.color = Color.yellow;
            errorText.gameObject.SetActive(true);
        }
    }
}