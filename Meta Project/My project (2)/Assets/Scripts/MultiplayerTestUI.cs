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
    
    [Header("Room Creation Panel")]
    public GameObject roomCreationPanel;
    public TMP_InputField maxPlayersInput;
    public TMP_InputField piecesPerPlayerInput;
    public Button confirmCreateRoomButton;
    public Button cancelCreateRoomButton;
    /*public TextMeshProUGUI generatedRoomIdText;*/
    
    
    [Header("Room Join Panel")]
    public GameObject roomJoinPanel;
    public TMP_InputField roomIdInput;
    public Button confirmJoinRoomButton;
    public Button cancelJoinRoomButton;
    
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
        
        // THÊM: Button cho tạo phòng mới
        if (confirmCreateRoomButton != null)
            confirmCreateRoomButton.onClick.AddListener(ConfirmCreateRoom);
        
        if (cancelCreateRoomButton != null)
            cancelCreateRoomButton.onClick.AddListener(CancelCreateRoom);
        
        // THÊM: Button cho join room panel
        if (confirmJoinRoomButton != null)
            confirmJoinRoomButton.onClick.AddListener(ConfirmJoinRoom);
        
        if (cancelJoinRoomButton != null)
            cancelJoinRoomButton.onClick.AddListener(CancelJoinRoom);
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
    
    // SỬA: Cập nhật ShowMainMenu để ẩn tất cả panel
    private void ShowMainMenu()
    {
        SetPanelActive(mainMenuPanel, true);
        SetPanelActive(roomCreationPanel, false);
        SetPanelActive(roomJoinPanel, false); // THÊM: Ẩn panel join room
        SetPanelActive(roomPanel, false);
        SetPanelActive(loadingPanel, false);
        ClearError();
    }
    
    // SỬA: Cập nhật ShowRoomPanel để ẩn các panel khác
    private void ShowRoomPanel()
    {
        SetPanelActive(mainMenuPanel, false);
        SetPanelActive(roomCreationPanel, false);
        SetPanelActive(roomJoinPanel, false); // THÊM: Ẩn panel join room
        SetPanelActive(roomPanel, true);
        SetPanelActive(loadingPanel, false);
        ClearError();
    }
    
    // SỬA: Cập nhật ShowLoadingPanel để ẩn các panel khác
    private void ShowLoadingPanel(string message = "Đang tải...")
    {
        SetPanelActive(mainMenuPanel, false);
        SetPanelActive(roomCreationPanel, false);
        SetPanelActive(roomJoinPanel, false); // THÊM: Ẩn panel join room
        SetPanelActive(roomPanel, false);
        SetPanelActive(loadingPanel, true);
    
        if (loadingText != null)
        {
            loadingText.text = message;
        }
        ClearError();
    }
    
    // SỬA: Thêm roomCreationPanel vào SetPanelActive
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
    
    // SỬA: Phương thức CreateRoom cũ thành hiển thị panel cấu hình
    public void CreateRoom()
    {
        // Hiển thị panel cấu hình phòng thay vì tạo phòng ngay
        ShowRoomCreationPanel();
    }
    // THÊM: Hiển thị panel tạo phòng
    // SỬA: Cập nhật ShowRoomCreationPanel để ẩn các panel khác
    private void ShowRoomCreationPanel()
    {
        SetPanelActive(mainMenuPanel, false);
        SetPanelActive(roomCreationPanel, true);
        SetPanelActive(roomJoinPanel, false); // THÊM: Ẩn panel join room
        SetPanelActive(roomPanel, false);
        SetPanelActive(loadingPanel, false);

        // Đặt giá trị mặc định
        if (maxPlayersInput != null) maxPlayersInput.text = "4";
        if (piecesPerPlayerInput != null) piecesPerPlayerInput.text = "4";
    }

// THÊM: Xác nhận tạo phòng
    public void ConfirmCreateRoom()
    {
        if (PhotonNetwork.IsConnected && !PhotonNetwork.InRoom)
        {
            int maxPlayers = 4;
            int piecesPerPlayer = 4;
        
            // Lấy giá trị từ input
            if (!string.IsNullOrEmpty(maxPlayersInput.text))
                int.TryParse(maxPlayersInput.text, out maxPlayers);
            
            if (!string.IsNullOrEmpty(piecesPerPlayerInput.text))
                int.TryParse(piecesPerPlayerInput.text, out piecesPerPlayer);
        
            // Giới hạn giá trị hợp lệ
            maxPlayers = Mathf.Clamp(maxPlayers, 2, 8);
            piecesPerPlayer = Mathf.Clamp(piecesPerPlayer, 1, 4);
        
            ShowLoadingPanel("Đang tạo phòng...");
        
            // Gọi PhotonManager để tạo phòng ngẫu nhiên
            if (PhotonManager.Instance != null)
            {
                PhotonManager.Instance.CreateRandomRoom(maxPlayers, piecesPerPlayer);
            }
        }
    }

// THÊM: Hủy tạo phòng
    public void CancelCreateRoom()
    {
        ShowMainMenu();
    }

// THÊM: Cập nhật UI khi tạo phòng thành công
    public override void OnCreatedRoom()
    {
        Debug.Log($"Đã tạo phòng: {PhotonNetwork.CurrentRoom.Name}");
    
        /*// Hiển thị room ID trên UI
        if (generatedRoomIdText != null)
        {
            generatedRoomIdText.text = $"Room ID: {PhotonNetwork.CurrentRoom.Name}";
        }*/
    
        // KHÔNG chuyển sang room panel ngay, vẫn ở loading cho đến khi join hoàn tất
    }

    
    public void JoinRoom()
    {
        // Hiển thị panel nhập ID phòng thay vì join ngay
        ShowRoomJoinPanel();
    }

// THÊM: Hiển thị panel nhập ID phòng
    private void ShowRoomJoinPanel()
    {
        SetPanelActive(mainMenuPanel, false);
        SetPanelActive(roomJoinPanel, true);
        SetPanelActive(roomPanel, false);
        SetPanelActive(loadingPanel, false);
        SetPanelActive(roomCreationPanel, false);
    
        // Reset input field
        if (roomIdInput != null)
        {
            roomIdInput.text = "";
            roomIdInput.placeholder.GetComponent<TextMeshProUGUI>().text = "Nhập Room ID...";
        }
    }

// THÊM: Xác nhận join phòng
    // SỬA: Xác nhận join phòng - sử dụng JoinRoomOnly
    public void ConfirmJoinRoom()
    {
        if (PhotonNetwork.IsConnected && !PhotonNetwork.InRoom)
        {
            string roomId = roomIdInput.text.Trim();
    
            if (string.IsNullOrEmpty(roomId))
            {
                ShowError("Vui lòng nhập Room ID!");
                return;
            }
    
            if (roomId.Length < 4)
            {
                ShowError("Room ID phải có ít nhất 4 ký tự!");
                return;
            }
    
            ShowLoadingPanel($"Đang vào phòng '{roomId}'...");
    
            // SỬA: Sử dụng JoinRoomOnly thay vì JoinRoom
            if (PhotonManager.Instance != null)
            {
                // Đăng ký sự kiện join room thất bại
                PhotonManager.Instance.OnJoinRoomFailedEvent += HandleJoinRoomFailed;
            
                PhotonManager.Instance.JoinRoomOnly(roomId);
            }
        }
    }

// THÊM: Hủy join phòng
    public void CancelJoinRoom()
    {
        ShowMainMenu();
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
    
    // SỬA: Cập nhật khi join phòng thành công để hiển thị thông tin
    public override void OnJoinedRoom()
    {
        Debug.Log($"Đã vào phòng: {PhotonNetwork.CurrentRoom.Name}");
    
        // Hiển thị thông tin phòng
        string roomInfo = $"Phòng: {PhotonNetwork.CurrentRoom.Name}\n";
        roomInfo += $"Số người: {PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers}\n";
    
        if (PhotonManager.Instance != null)
        {
            roomInfo += $"Số quân/người: {PhotonManager.Instance.GetPiecesPerPlayer()}";
        }
    
        if (roomInfoText != null)
        {
            roomInfoText.text = roomInfo;
        }
    
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
    
    // THÊM: Xử lý lỗi tạo phòng
    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Tạo phòng thất bại: {message}");
        ShowMainMenu();
        ShowError($"Tạo phòng thất bại: {message}");
    }
    
    // SỬA: Xử lý lỗi join room với thông báo rõ ràng
    // SỬA: Xử lý lỗi join room với thông báo rõ ràng
    // SỬA: Xử lý lỗi join room với thông báo rõ ràng hơn
    // SỬA: Xử lý lỗi join room - chỉ xử lý các trường hợp đặc biệt
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        // Chỉ xử lý các trường hợp không được xử lý bởi HandleJoinRoomFailed
        // Hoặc có thể bỏ qua vì đã xử lý trong HandleJoinRoomFailed
        Debug.Log($"OnJoinRoomFailed called: {returnCode} - {message}");
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
    
    // THÊM: Xử lý khi join room thất bại
    private void HandleJoinRoomFailed(short returnCode, string message)
    {
        // Hủy đăng ký sự kiện
        if (PhotonManager.Instance != null)
        {
            PhotonManager.Instance.OnJoinRoomFailedEvent -= HandleJoinRoomFailed;
        }

        string roomName = PhotonManager.Instance?.GetLastAttemptedRoom() ?? "unknown";
        string errorMessage = "";
        bool showRetryButton = false;

        switch (returnCode)
        {
            case ErrorCode.GameClosed:
                errorMessage = $"Phòng '{roomName}' đã bị khóa.";
                showRetryButton = true;
                break;
            case ErrorCode.GameFull:
                errorMessage = $"Phòng '{roomName}' đã đầy.";
                break;
            case ErrorCode.GameDoesNotExist:
                errorMessage = $"Phòng '{roomName}' không tồn tại. Vui lòng kiểm tra lại Room ID.";
                showRetryButton = true;
                break;
            default:
                errorMessage = $"Không thể vào phòng '{roomName}': {message}";
                showRetryButton = true;
                break;
        }

        // Quay lại main menu và hiển thị lỗi
        ShowMainMenu();
        ShowError(errorMessage);

        // THÊM: Có thể thêm nút "Thử lại" hoặc "Tạo phòng mới" tùy trường hợp
        if (showRetryButton)
        {
            // Có thể thêm logic hiển thị nút thử lại hoặc tạo phòng ở đây
            Debug.Log($"Có thể cho người dùng tạo phòng '{roomName}' mới hoặc thử lại");
        }
    }
}