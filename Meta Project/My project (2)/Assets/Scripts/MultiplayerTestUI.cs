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
    public Button rejoinButton; // THÊM: Button rejoin
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
    public TextMeshProUGUI errorText;
    
    [Header("Room Creation Panel")]
    public GameObject roomCreationPanel;
    public TMP_InputField maxPlayersInput;
    public TMP_InputField piecesPerPlayerInput;
    public Button confirmCreateRoomButton;
    public Button cancelCreateRoomButton;
    
    [Header("Room Join Panel")]
    public GameObject roomJoinPanel;
    public TMP_InputField roomIdInput;
    public Button confirmJoinRoomButton;
    public Button cancelJoinRoomButton;
    
    private void Start()
    {
        SetupUI();
        ClearError();

        // Nếu đã ở trong phòng (online hoặc offline), hiển thị Room panel ngay
        if ((PhotonNetwork.IsConnected || PhotonNetwork.OfflineMode) && PhotonNetwork.InRoom)
        {
            ShowRoomPanel();
            UpdateUI();
        }
        else
        {
            ShowMainMenu();
        }

        UpdateRejoinButton();
    }
    
    private void SetupUI()
    {
        if (connectButton != null)
            connectButton.onClick.AddListener(ConnectToPhoton);
            
        if (createRoomButton != null)
            createRoomButton.onClick.AddListener(ShowRoomCreationPanel);
            
        if (joinRoomButton != null)
            joinRoomButton.onClick.AddListener(ShowRoomJoinPanel);
            
        // THÊM: Button rejoin
        if (rejoinButton != null)
            rejoinButton.onClick.AddListener(RejoinPreviousRoom);
            
        if (leaveRoomButton != null)
            leaveRoomButton.onClick.AddListener(LeaveRoom);
            
        if (spawnCubeButton != null)
            spawnCubeButton.onClick.AddListener(SpawnCube);
            
        if (spawnSphereButton != null)
            spawnSphereButton.onClick.AddListener(SpawnSphere);
        
        if (confirmCreateRoomButton != null)
            confirmCreateRoomButton.onClick.AddListener(ConfirmCreateRoom);
        
        if (cancelCreateRoomButton != null)
            cancelCreateRoomButton.onClick.AddListener(CancelCreateRoom);
        
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
                if (PhotonNetwork.IsConnected || PhotonNetwork.OfflineMode)
                {
                    if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null)
                    {
                        connectionStatusText.text = "Connected - In Room";
                        connectionStatusText.color = Color.green;
                    }
                    else
                    {
                        connectionStatusText.text = "Connected - Not in room yet";
                        connectionStatusText.color = Color.darkBlue;
                    }
                }
                else
                {
                    connectionStatusText.text = "Not connected";
                    connectionStatusText.color = Color.red;
                }
            }
            catch (System.Exception e)
            {
                connectionStatusText.text = "Connection error";
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
                    roomInfoText.text = $"Room: {PhotonNetwork.CurrentRoom.Name}";
                }
                else
                {
                    roomInfoText.text = "Not in the room yet";
                }
            }
            catch (System.Exception e)
            {
                roomInfoText.text = "Room information error";
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
                    playerCountText.text = $"Player: {PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers}";
                }
                else
                {
                    playerCountText.text = "Player: 0/0";
                }
            }
            catch (System.Exception e)
            {
                playerCountText.text = "Player number error";
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
        
        // THÊM: Cập nhật trạng thái rejoin button
        UpdateRejoinButton();
    }
    
    // THÊM: Cập nhật trạng thái rejoin button
    private void UpdateRejoinButton()
    {
        if (rejoinButton != null)
        {
            bool hasPreviousRoom = PhotonManager.Instance != null && 
                                 !string.IsNullOrEmpty(PhotonManager.Instance.GetPreviousRoom());
            
            rejoinButton.interactable = hasPreviousRoom && PhotonNetwork.IsConnected && !PhotonNetwork.InRoom;
            
            // Cập nhật text hiển thị phòng cũ
            TextMeshProUGUI buttonText = rejoinButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                if (hasPreviousRoom)
                {
                    string previousRoom = PhotonManager.Instance.GetPreviousRoom();
                    buttonText.text = $"Rejoin: {previousRoom}";
                }
                else
                {
                    buttonText.text = "Rejoin: Không có phòng cũ";
                }
            }
        }
    }
    
    // THÊM: Phương thức rejoin phòng cũ
    public void RejoinPreviousRoom()
    {
        if ((PhotonNetwork.IsConnected || PhotonNetwork.OfflineMode) && !PhotonNetwork.InRoom)
        {
            string previousRoom = PhotonManager.Instance?.GetPreviousRoom();
            
            if (string.IsNullOrEmpty(previousRoom))
            {
                ShowError("No old room to rejoin!");
                return;
            }
            
            ShowLoadingPanel($"Rejoining room '{previousRoom}'...");
            
            // Đăng ký sự kiện join room thất bại
            if (PhotonManager.Instance != null)
            {
                PhotonManager.Instance.OnJoinRoomFailedEvent += HandleJoinRoomFailed;
                PhotonManager.Instance.JoinRoomOnly(previousRoom);
            }
        }
        else
        {
            ShowError("Not connected or already in room!");
        }
    }
    
    private void ShowError(string message, bool isWarning = false)
    {
        if (errorText != null)
        {
            errorText.text = message;
            errorText.color = isWarning ? Color.yellow : Color.red;
            errorText.gameObject.SetActive(true);
            
            Invoke("ClearError", 5f);
        }
    }
    
    private void ClearError()
    {
        if (errorText != null)
        {
            errorText.text = "";
            errorText.gameObject.SetActive(false);
        }
    }
    
    private void ShowMainMenu()
    {
        SetPanelActive(mainMenuPanel, true);
        SetPanelActive(roomCreationPanel, false);
        SetPanelActive(roomJoinPanel, false);
        SetPanelActive(roomPanel, false);
        SetPanelActive(loadingPanel, false);
        ClearError();
        UpdateRejoinButton(); // Cập nhật rejoin button khi hiển thị main menu
    }
    
    private void ShowRoomPanel()
    {
        SetPanelActive(mainMenuPanel, false);
        SetPanelActive(roomCreationPanel, false);
        SetPanelActive(roomJoinPanel, false);
        SetPanelActive(roomPanel, true);
        SetPanelActive(loadingPanel, false);
        ClearError();
    }
    
    private void ShowLoadingPanel(string message = "Đang tải...")
    {
        SetPanelActive(mainMenuPanel, false);
        SetPanelActive(roomCreationPanel, false);
        SetPanelActive(roomJoinPanel, false);
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
    
    // SỬA: Xóa logic tự động rejoin trong OnConnectedToMaster
    public override void OnConnectedToMaster()
    {
        Debug.Log("Đã kết nối tới Master Server");
        ShowMainMenu();
        UpdateUI();
        UpdateRejoinButton(); // Chỉ cập nhật UI, không tự động rejoin
    }
    
    // Các phương thức khác giữ nguyên...
    private void ShowRoomCreationPanel()
    {
        SetPanelActive(mainMenuPanel, false);
        SetPanelActive(roomCreationPanel, true);
        SetPanelActive(roomJoinPanel, false);
        SetPanelActive(roomPanel, false);
        SetPanelActive(loadingPanel, false);

        if (maxPlayersInput != null) maxPlayersInput.text = "4";
        if (piecesPerPlayerInput != null) piecesPerPlayerInput.text = "4";
    }

    public void ConfirmCreateRoom()
    {
        if ((PhotonNetwork.IsConnected || PhotonNetwork.OfflineMode) && !PhotonNetwork.InRoom)
        {
            int maxPlayers = 4;
            int piecesPerPlayer = 4;
        
            if (!string.IsNullOrEmpty(maxPlayersInput.text))
                int.TryParse(maxPlayersInput.text, out maxPlayers);
            
            if (!string.IsNullOrEmpty(piecesPerPlayerInput.text))
                int.TryParse(piecesPerPlayerInput.text, out piecesPerPlayer);
        
            maxPlayers = Mathf.Clamp(maxPlayers, 2, 8);
            piecesPerPlayer = Mathf.Clamp(piecesPerPlayer, 1, 4);
        
            ShowLoadingPanel("Đang tạo phòng...");
        
            if (PhotonManager.Instance != null)
            {
                PhotonManager.Instance.CreateRandomRoom(maxPlayers, piecesPerPlayer);
            }
        }
    }

    public void CancelCreateRoom()
    {
        ShowMainMenu();
    }

    public override void OnCreatedRoom()
    {
        Debug.Log($"Đã tạo phòng: {PhotonNetwork.CurrentRoom.Name}");
    }
    
    private void ShowRoomJoinPanel()
    {
        SetPanelActive(mainMenuPanel, false);
        SetPanelActive(roomJoinPanel, true);
        SetPanelActive(roomPanel, false);
        SetPanelActive(loadingPanel, false);
        SetPanelActive(roomCreationPanel, false);
    
        if (roomIdInput != null)
        {
            roomIdInput.text = "";
            roomIdInput.placeholder.GetComponent<TextMeshProUGUI>().text = "Enter Room ID...";
        }
    }

    public void ConfirmJoinRoom()
    {
        if ((PhotonNetwork.IsConnected || PhotonNetwork.OfflineMode) && !PhotonNetwork.InRoom)
        {
            string roomId = roomIdInput.text.Trim();

            if (string.IsNullOrEmpty(roomId))
            {
                ShowError("Please enter Room ID!");
                return;
            }

            if (roomId.Length < 4)
            {
                ShowError("Room ID must be at least 4 characters!");
                return;
            }

            string previousRoom = PhotonManager.Instance?.GetPreviousRoom() ?? "";
            if (!string.IsNullOrEmpty(previousRoom) && previousRoom != roomId)
            {
                Debug.Log($"Joining different room, resetting previous room record");
                PhotonManager.Instance?.ResetPreviousRoom();
            }

            ShowLoadingPanel($"Entering the room '{roomId}'...");

            if (PhotonManager.Instance != null)
            {
                PhotonManager.Instance.OnJoinRoomFailedEvent += HandleJoinRoomFailed;
                PhotonManager.Instance.JoinRoomOnly(roomId);
            }
        }
    }

    public void CancelJoinRoom()
    {
        ShowMainMenu();
    }
    
    public void LeaveRoom()
    {
        if (PhotonNetwork.InRoom)
        {
            if (PhotonManager.Instance != null)
            {
                PhotonManager.Instance.HideAllPiecesOnLeave();
            }

            if (WinConditionManager.Instance != null)
            {
                WinConditionManager.Instance.ResetBoardToInitialState(hideWinPanel: true, resetGameEndedFlag: true);
            }

            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.ResetLocalState(clearNetworkState: PhotonNetwork.IsMasterClient);
            }

            ShowLoadingPanel("Leaving the room...");
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
    
    public override void OnJoinedRoom()
    {
        Debug.Log($"Đã vào phòng: {PhotonNetwork.CurrentRoom.Name}");
    
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

        if (WinConditionManager.Instance != null)
        {
            WinConditionManager.Instance.ResetBoardToInitialState(hideWinPanel: true, resetGameEndedFlag: true);
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ResetLocalState();
        }

        ShowMainMenu();
        UpdateUI();
        UpdateRejoinButton(); // Cập nhật rejoin button sau khi rời phòng
    }
    
    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Tạo phòng thất bại: {message}");
        ShowMainMenu();
        ShowError($"Tạo phòng thất bại: {message}");
    }
    
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.Log($"OnJoinRoomFailed called: {returnCode} - {message}");
    }
    
    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.Log($"Mất kết nối: {cause}");
        ShowMainMenu();
        ShowError($"Mất kết nối: {cause}");
        UpdateUI();
    }
    
    private void HandleJoinRoomFailed(short returnCode, string message)
    {
        if (PhotonManager.Instance != null)
        {
            PhotonManager.Instance.OnJoinRoomFailedEvent -= HandleJoinRoomFailed;
        }

        string roomName = PhotonManager.Instance?.GetLastAttemptedRoom() ?? "unknown";
        string errorMessage = "";

        switch (returnCode)
        {
            case ErrorCode.GameClosed:
                errorMessage = $"Phòng '{roomName}' đã bị khóa.";
                break;
            case ErrorCode.GameFull:
                errorMessage = $"Phòng '{roomName}' đã đầy.";
                break;
            case ErrorCode.GameDoesNotExist:
                errorMessage = $"Phòng '{roomName}' không tồn tại. Vui lòng kiểm tra lại Room ID.";
                break;
            default:
                errorMessage = $"Không thể vào phòng '{roomName}': {message}";
                break;
        }

        ShowMainMenu();
        ShowError(errorMessage);
        UpdateRejoinButton(); // Cập nhật lại rejoin button sau khi thất bại
    }
}
