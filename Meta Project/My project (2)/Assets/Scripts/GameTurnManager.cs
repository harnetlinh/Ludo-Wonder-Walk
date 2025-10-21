using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Photon.Pun;

public class GameTurnManager : MonoBehaviourPun, IPunObservable
{
    public static GameTurnManager Instance { get; private set; }

    public List<PlayerColor> playerOrder = new List<PlayerColor>();
    public int currentPlayerIndex = 0;

    public bool autoPlayAllPlayers = false; // Thêm biến điều khiển chế độ test
    public bool isDeterminingOrder = false; // Thêm biến này

    public bool isInitialized = false;

    // PUN Network Variables
    private int networkCurrentPlayerIndex = 0;
    private List<PlayerColor> networkPlayerOrder = new List<PlayerColor>();
    private bool networkIsInitialized = false;
    private bool isNetworked = false;

    // Thêm vào class GameTurnManager
    private bool pieceMovedThisTurn = false;
    private bool isInitializing = false; // THÊM: Cờ bảo vệ tránh khởi tạo nhiều lần
    private bool noMovesNotifiedThisTurn = false;
    private const float NoMoveEndTurnDelaySeconds = 0.25f;

// Thêm vào đầu class
    private GameStateManager gameStateManager;

    void Start()
    {
        bool shouldInitialize = PhotonNetwork.IsMasterClient && !isInitialized && !isInitializing;

        if (shouldInitialize)
        {
            if (PhotonManager.Instance != null)
            {
                if (PhotonManager.Instance.IsGameStarted())
                {
                    Debug.Log("Master Client kh?i t?o player order sau khi ph?ng s?n s?ng...");
                    InitializePlayerOrder(DiceController.Instance);
                }
                else
                {
                    Debug.Log("Dang ch? ph?ng full v? GameStarted tr??c khi kh?i t?o player order.");
                }
            }
            else
            {
                Debug.LogWarning("Kh?ng t?m th?y PhotonManager, kh?i t?o player order ngay l?p t?c.");
                InitializePlayerOrder(DiceController.Instance);
            }
        }
        else
        {
            if (isInitialized)
            {
                Debug.Log("Game da du?c kh?i t?o tru?c d?");
            }
            else if (isInitializing)
            {
                Debug.Log("Game dang du?c kh?i t?o");
            }
            else
            {
                Debug.Log("Client dang ch? Master Client kh?i t?o game...");
            }
        }
    }

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
        }

        // KHỞI TẠO GameStateManager NGAY TỪ ĐẦU
        gameStateManager = GameStateManager.Instance;
        if (gameStateManager == null)
        {
            Debug.LogWarning("GameStateManager not found on Awake, will try again later");
        }

        // Khởi tạo PUN
        if (photonView != null)
        {
            isNetworked = true;
            networkCurrentPlayerIndex = currentPlayerIndex;
            networkPlayerOrder = new List<PlayerColor>(playerOrder);
            networkIsInitialized = isInitialized;
        }
    }

    // SỬA phương thức InitializePlayerOrder()
    public void InitializePlayerOrder(DiceController diceController)
    {
        if (gameStateManager == null)
        {
            gameStateManager = GameStateManager.Instance;
            if (gameStateManager == null)
            {
                Debug.LogError("GameStateManager is null in InitializePlayerOrder!");
                return;
            }
        }

        if (isNetworked && !PhotonNetwork.IsMasterClient)
        {
            Debug.Log("Chi Master Client moi duoc khoi tao game - bo qua tren client nay");
            return;
        }

        if (isInitializing || isInitialized)
        {
            Debug.Log("Game dang khoi tao hoac da khoi tao, bo qua");
            return;
        }

        List<PlayerColor> roomColors = null;
        if (PhotonManager.Instance != null)
        {
            roomColors = PhotonManager.Instance.GetRoomPlayerColors();
            if (!IsRoomReadyForTurnOrder(roomColors))
            {
                Debug.LogWarning("Phong chua san sang (chua du nguoi hoac chua gan mau), se thu khoi tao lai sau.");
                return;
            }
        }

        isInitializing = true;

        if (roomColors != null && roomColors.Count > 0)
        {
            Debug.Log($"Bat dau khoi tao luot choi voi {roomColors.Count} nguoi choi: {string.Join(", ", roomColors)}");

            if (PhotonNetwork.IsMasterClient)
            {
                gameStateManager.SetPlayerOrder(roomColors);
            }

            StartCoroutine(DeterminePlayerOrder(diceController, roomColors));
            return;
        }

        Debug.LogWarning("Chua co thong tin nguoi choi tu PhotonManager, cho...");
        isInitializing = false;
    }


    private bool IsRoomReadyForTurnOrder(List<PlayerColor> assignedColors)
    {
        if (!PhotonNetwork.InRoom)
        {
            return assignedColors != null && assignedColors.Count > 0;
        }

        var room = PhotonNetwork.CurrentRoom;
        if (room == null)
        {
            return false;
        }

        int currentPlayers = room.PlayerCount;
        int maxPlayers = room.MaxPlayers;

        if (maxPlayers > 0 && currentPlayers < maxPlayers)
        {
            Debug.Log($"Dang doi phong full truoc khi khoi tao luot choi ({currentPlayers}/{maxPlayers}).");
            return false;
        }

        if (assignedColors == null || assignedColors.Count == 0)
        {
            Debug.Log("Chua co danh sach mau nguoi choi, doi them...");
            return false;
        }

        if (assignedColors.Count < currentPlayers)
        {
            Debug.Log($"Chua gan mau cho tat ca nguoi choi ({assignedColors.Count}/{currentPlayers}).");
            return false;
        }

        return true;
    }

    private PlayerColor SyncLocalTurnState()
    {
        if (gameStateManager == null)
        {
            gameStateManager = GameStateManager.Instance;
        }

        if (gameStateManager != null &&
            gameStateManager.playerOrder != null &&
            gameStateManager.playerOrder.Count > 0)
        {
            if (playerOrder == null || !playerOrder.SequenceEqual(gameStateManager.playerOrder))
            {
                playerOrder = new List<PlayerColor>(gameStateManager.playerOrder);
            }

            int clampedIndex = Mathf.Clamp(gameStateManager.currentTurnIndex, 0, playerOrder.Count - 1);
            currentPlayerIndex = clampedIndex;

            if (gameStateManager.isGameInitialized && !isInitialized)
            {
                isInitialized = true;
            }

            if (gameStateManager.currentPlayerColor != PlayerColor.None)
            {
                return gameStateManager.currentPlayerColor;
            }

            if (playerOrder.Count > currentPlayerIndex)
            {
                return playerOrder[currentPlayerIndex];
            }

            return PlayerColor.None;
        }

        if (playerOrder != null && playerOrder.Count > 0)
        {
            currentPlayerIndex = Mathf.Clamp(currentPlayerIndex, 0, playerOrder.Count - 1);
            return playerOrder[currentPlayerIndex];
        }

        return PlayerColor.None;
    }

    public void ApplySyncedTurn(IList<PlayerColor> syncedOrder, int turnIndex, PlayerColor activeColor)
    {
        if (syncedOrder != null && syncedOrder.Count > 0)
        {
            if (playerOrder == null ||
                playerOrder.Count != syncedOrder.Count ||
                !playerOrder.SequenceEqual(syncedOrder))
            {
                playerOrder = new List<PlayerColor>(syncedOrder);
            }

            int targetIndex = Mathf.Clamp(turnIndex, 0, playerOrder.Count - 1);
            currentPlayerIndex = targetIndex;

            if (activeColor != PlayerColor.None)
            {
                int colorIndex = playerOrder.IndexOf(activeColor);
                if (colorIndex >= 0)
                {
                    currentPlayerIndex = colorIndex;
                }
            }
        }
        else
        {
            if (playerOrder == null)
            {
                playerOrder = new List<PlayerColor>();
            }
            else
            {
                playerOrder.Clear();
            }

            currentPlayerIndex = Mathf.Max(0, turnIndex);
        }

        pieceMovedThisTurn = false;
        noMovesNotifiedThisTurn = false;

        if (!isInitialized && playerOrder.Count > 0)
        {
            isInitialized = true;
        }

        if (gameStateManager == null)
        {
            gameStateManager = GameStateManager.Instance;
        }
    }

    public PlayerColor CurrentPlayer
    {
        get
        {
            return SyncLocalTurnState();
        }
    }

    // SỬA: Phương thức DeterminePlayerOrder để chỉ xét những player có trong room
    private System.Collections.IEnumerator DeterminePlayerOrder(DiceController diceController,
        List<PlayerColor> availableColors = null)
    {
        isDeterminingOrder = true;
        Dictionary<PlayerColor, int> playerRolls = new Dictionary<PlayerColor, int>();

        // Sử dụng danh sách màu từ PhotonManager nếu có
        List<PlayerColor> colorsToProcess = availableColors ??
                                            new List<PlayerColor>
                                            {
                                                PlayerColor.Red, PlayerColor.Blue, PlayerColor.Yellow, PlayerColor.Green
                                            };

        

        foreach (PlayerColor color in colorsToProcess)
        {
            // Bỏ qua PlayerColor.None và những màu không có trong room
            if (color == PlayerColor.None) continue;

            int rollValue = diceController != null
                ? diceController.SimulateDiceForInitialization(color)
                : Random.Range(1, 7);

            playerRolls[color] = Mathf.Clamp(rollValue, 1, 6);
            yield return null;
        }

        playerOrder.Clear();
        foreach (var entry in playerRolls.OrderByDescending(x => x.Value))
        {
            playerOrder.Add(entry.Key);
        }

        currentPlayerIndex = 0;

        if (PhotonNetwork.IsMasterClient)
        {
            if (gameStateManager == null)
            {
                gameStateManager = GameStateManager.Instance;
            }

            if (gameStateManager != null && playerOrder.Count > 0)
            {
                gameStateManager.SetPlayerOrder(new List<PlayerColor>(playerOrder));
            }
            else if (gameStateManager == null)
            {
                Debug.LogError("GameTurnManager: Unable to sync player order with GameStateManager because instance is missing.");
            }
        }

        isDeterminingOrder = false;

        // THÊM: Đồng bộ với các client khác
        if (playerOrder.Count > 0 && isNetworked && photonView.IsMine)
        {
            int[] playerOrderArray = playerOrder.Select(color => (int)color).ToArray();
            photonView.RPC("NetworkSyncPlayerOrder", RpcTarget.Others, playerOrderArray, currentPlayerIndex);
        }

        if (autoPlayAllPlayers)
        {
            if (DiceController.Instance != null)
            {
                DiceController.Instance.AutoRollForCurrentPlayer();
            }
        }
        else
        {
            if (diceController != null)
            {
                diceController.EnableDiceForCurrentPlayer();
            }
        }

        // THÊM: Đánh dấu đã khởi tạo xong
        isInitialized = true;
        isInitializing = false; // THÊM: Reset cờ khởi tạo
        isDeterminingOrder = false;

        Debug.Log("Khởi tạo lượt chơi HOÀN TẤT");
    }

// THÊM: Kiểm tra xem player color có trong lượt chơi không
    public bool IsColorInGame(PlayerColor color)
    {
        if (color == PlayerColor.None)
        {
            return false;
        }

        if (gameStateManager != null &&
            gameStateManager.playerOrder != null &&
            gameStateManager.playerOrder.Count > 0)
        {
            if (playerOrder == null || !playerOrder.SequenceEqual(gameStateManager.playerOrder))
            {
                playerOrder = new List<PlayerColor>(gameStateManager.playerOrder);
            }

            return gameStateManager.playerOrder.Contains(color);
        }

        return playerOrder != null && playerOrder.Contains(color);
    }


    // Cập nhật StartTurn để highlight các quân có thể di chuyển
    // Cập nhật StartTurn
    // Sửa phương thức StartTurn
    public void StartTurn()
    {
        // Nếu có PUN và là master client, gửi RPC
        if (isNetworked && photonView.IsMine)
        {
            photonView.RPC("NetworkStartTurn", RpcTarget.All);
        }
        else
        {
            // Chạy local nếu không có PUN
            StartTurnLocal();
        }
    }

    public bool HasValidMoves(PlayerColor playerColor, int diceValue)
    {
        Debug.Log($"HasValidMoves checking for {playerColor} with dice value {diceValue}");

        // Kiểm tra nếu có thể xuất quân (xúc xắc = 6)
        if (diceValue == 6 && HasPiecesInStable(playerColor))
        {
            Debug.Log($"Can deploy piece from stable for {playerColor}");
            return true;
        }

        // Kiểm tra các quân đang trên bàn có thể di chuyển
        PieceController[] pieces = FindObjectsOfType<PieceController>();
        int piecesOnBoard = 0;
        int movablePieces = 0;

        foreach (PieceController piece in pieces)
        {
            if (piece.playerColor == playerColor && piece.currentPathIndex >= 0 && piece.currentPathIndex != -2)
            {
                piecesOnBoard++;

                // Kiểm tra có thể di chuyển diceValue bước không
                int tempIndex = piece.currentPathIndex;
                bool canMoveSteps = true;

                for (int step = 0; step < diceValue; step++)
                {
                    bool isPrivatePath;
                    Transform nextPoint = HorseRacePathManager.Instance.GetNextPoint(
                        tempIndex,
                        playerColor,
                        out isPrivatePath);

                    if (nextPoint == null)
                    {
                        canMoveSteps = false;
                        break;
                    }

                    // Cập nhật tempIndex cho bước tiếp theo
                    if (isPrivatePath)
                    {
                        tempIndex = HorseRacePathManager.Instance.commonPathPoints.Count +
                                    HorseRacePathManager.Instance.GetPrivatePath(playerColor).IndexOf(nextPoint);
                    }
                    else
                    {
                        tempIndex = HorseRacePathManager.Instance.commonPathPoints.IndexOf(nextPoint);
                    }
                }

                if (canMoveSteps)
                {
                    movablePieces++;
                    Debug.Log($"Piece at index {piece.currentPathIndex} can move {diceValue} steps");
                }
            }
        }

        Debug.Log($"Found {piecesOnBoard} pieces on board, {movablePieces} can move");
        return movablePieces > 0;
    }

    //private void HighlightMovablePieces()
    //{
    //    int diceValue = DiceController.Instance.LastDiceValue;
    //    PieceController[] pieces = FindObjectsOfType<PieceController>();

    //    foreach (PieceController piece in pieces)
    //    {
    //        if (piece.playerColor == CurrentPlayer)
    //        {
    //            bool canMove = false;

    //            // Kiểm tra có thể xuất quân
    //            if (diceValue == 6 && piece.currentPathIndex == -1)
    //            {
    //                canMove = true;
    //            }
    //            // Kiểm tra có thể di chuyển quân trên bàn
    //            else if (piece.currentPathIndex >= 0)
    //            {
    //                bool isPrivatePath;
    //                Transform nextPoint = HorseRacePathManager.Instance.GetNextPoint(
    //                    piece.currentPathIndex,
    //                    CurrentPlayer,
    //                    out isPrivatePath);
    //                canMove = (nextPoint != null);
    //            }

    //            // Highlight quân cờ nếu có thể di chuyển
    //            piece.GetComponent<Renderer>().material.color = canMove ? Color.green : Color.white;
    //        }
    //    }
    //}

    // Xóa phương thức RollDiceForCurrentPlayer() không cần thiết

   

    public void EndTurn()
    {
        if (isNetworked && PhotonNetwork.InRoom)
        {
            if (PhotonNetwork.IsMasterClient && photonView != null && photonView.IsMine)
            {
                photonView.RPC("NetworkEndTurn", RpcTarget.All);
            }
            else
            {
                Debug.Log("EndTurn invoked on non-master client; waiting for master to advance turn.");
            }
            return;
        }
        if (DiceController.Instance != null)
        {
            // Reset trạng thái đã xúc xắc trong lượt này
                
            DiceController.Instance.ResetDiceValue();
            DiceController.Instance.hasRolledThisTurn = false;
            
            // Di chuyển xúc xắc đến người chơi tiếp theo sau khi di chuyển xong
            DiceController.Instance.MoveDiceToCurrentPlayer();
        }
        // Chạy local nếu không có PUN
        EndTurnLocal();
    }

    // SỬA: Phương thức IsCurrentPlayer để kiểm tra cả việc màu có trong game không
    public bool IsCurrentPlayer(PlayerColor color)
    {
        if (color == PlayerColor.None)
        {
            return false;
        }

        if (!isInitialized && (playerOrder == null || playerOrder.Count == 0))
        {
            return false;
        }

        if (!IsColorInGame(color))
        {
            return false;
        }

        PlayerColor activeColor = SyncLocalTurnState();
        return activeColor != PlayerColor.None && activeColor == color;
    }
    /// <summary>
    /// Forcefully aligns the current player index with the provided color.
    /// Used by the master client to recover from desyncs reported by remote results.
    /// </summary>
    public bool ForceSetCurrentPlayer(PlayerColor color)
    {
        if (playerOrder == null || playerOrder.Count == 0)
        {
            return false;
        }
        int index = playerOrder.IndexOf(color);
        if (index < 0)
        {
            return false;
        }
        currentPlayerIndex = index;
        return true;
    }


    public void CheckForPossibleMoves()
    {
        int? diceValueNullable = DiceController.Instance.LastDiceValue;
        if (!diceValueNullable.HasValue)
        {
            Debug.LogWarning("CheckForPossibleMoves called without a dice value.");
            return;
        }

        int diceValue = diceValueNullable.Value;
        PlayerColor currentPlayer = SyncLocalTurnState();
        if (currentPlayer == PlayerColor.None)
        {
            Debug.LogWarning("CheckForPossibleMoves: Current player is undefined, cannot evaluate moves.");
            return;
        }

        Debug.Log($"CheckForPossibleMoves: Player {currentPlayer}, Dice value: {diceValue}");

        // Kiểm tra xem có quân cờ nào có thể di chuyển với số xúc xắc hiện tại không
        bool canMove = HasValidMoves(currentPlayer, diceValue);

        Debug.Log($"Can move: {canMove}");

        DiceController diceController = DiceController.Instance;
        if (diceController != null)
        {
            if (canMove)
            {
                diceController.canRollAgain = false;

                if (diceController.statusText != null)
                {
                    diceController.statusText.text = $"Lượt của {currentPlayer}\nHãy di chuyển quân cờ!";
                }

                noMovesNotifiedThisTurn = false;
            }
            else
            {
                if (diceController.statusText != null)
                {
                    diceController.statusText.text = $"Lượt của {currentPlayer}\nKhông có nước đi hợp lệ";
                }
            }
        }

        if (!canMove)
        {
            HandleNoMovesAvailable(currentPlayer, diceValue, diceController);
            return;
        }

        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }

        Debug.Log("Valid moves available - waiting for player to finish their move.");
    }

    private void HandleNoMovesAvailable(PlayerColor currentPlayer, int diceValue, DiceController diceController)
    {
        if (noMovesNotifiedThisTurn)
        {
            Debug.Log($"No-move condition already processed for {currentPlayer} this turn.");
            return;
        }

        noMovesNotifiedThisTurn = true;

        if (diceController != null)
        {
            diceController.canRollAgain = false;
            diceController.RequestReturnOwnershipToMaster(0.3f);
        }

        bool shouldNotifyMaster = isNetworked && PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient;

        if (!shouldNotifyMaster)
        {
            Debug.Log("No valid moves available, ending turn shortly.");
            CancelInvoke(nameof(EndTurn));
            Invoke(nameof(EndTurn), NoMoveEndTurnDelaySeconds);
            return;
        }

        if (photonView != null)
        {
            Debug.Log($"Reporting no valid moves for {currentPlayer} (dice {diceValue}) to master client.");
            photonView.RPC(nameof(RPC_ReportNoMovesAvailable), RpcTarget.MasterClient, (int)currentPlayer, diceValue);
        }
        else
        {
            Debug.LogWarning("PhotonView missing when attempting to report no moves; ending turn locally as fallback.");
            CancelInvoke(nameof(EndTurn));
            Invoke(nameof(EndTurn), NoMoveEndTurnDelaySeconds);
        }
    }

    [PunRPC]
    private void RPC_ReportNoMovesAvailable(int reportedColorValue, int diceValue, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || photonView == null || !photonView.IsMine)
        {
            Debug.LogWarning("RPC_ReportNoMovesAvailable ignored on non-master instance.");
            return;
        }

        PlayerColor reportedColor = (PlayerColor)reportedColorValue;
        string reporterName = info.Sender != null ? info.Sender.NickName : "Unknown";
        Debug.Log($"Received no-move report from {reporterName} for {reportedColor} with dice {diceValue}.");

        bool isCurrentPlayer = IsCurrentPlayer(reportedColor);
        if (!isCurrentPlayer)
        {
            if (ForceSetCurrentPlayer(reportedColor))
            {
                Debug.LogWarning($"Turn desync detected while handling no-move report. Aligning turn to {reportedColor}.");
            }
            else
            {
                Debug.LogWarning($"Ignoring no-move report for {reportedColor}: color not present in current turn order.");
                return;
            }
        }

        noMovesNotifiedThisTurn = true;

        DiceController diceController = DiceController.Instance;
        if (diceController != null)
        {
            diceController.ReturnOwnershipToMaster();

            if (diceController.statusText != null)
            {
                diceController.statusText.text = $"Lượt của {reportedColor}\nKhông có nước đi hợp lệ";
            }
        }

        CancelInvoke(nameof(EndTurn));
        Invoke(nameof(EndTurn), NoMoveEndTurnDelaySeconds);
    }

    // C?p nh?t phuong th?c CanCurrentPlayerMove
    public bool CanCurrentPlayerMove()
    {
        int? diceValueNullable = DiceController.Instance.LastDiceValue;
        if (!diceValueNullable.HasValue)
        {
            return false;
        }

        int diceValue = diceValueNullable.Value;
        PlayerColor currentPlayer = SyncLocalTurnState();
        if (currentPlayer == PlayerColor.None)
        {
            return false;
        }

        // Kiểm tra nếu có thể xuất quân (xúc xắc = 6)
        if (diceValue == 6 && HasPiecesInStable(currentPlayer))
        {
            return true;
        }

        // Kiểm tra các quân đang trên bàn có thể di chuyển
        PieceController[] pieces = FindObjectsOfType<PieceController>();
        foreach (PieceController piece in pieces)
        {
            if (piece.playerColor == currentPlayer && piece.currentPathIndex >= 0)
            {
                bool isPrivatePath;
                Transform nextPoint = HorseRacePathManager.Instance.GetNextPoint(
                    piece.currentPathIndex,
                    currentPlayer,
                    out isPrivatePath);

                if (nextPoint != null) // Có thể di chuyển
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Thêm các phương thức kiểm tra logic di chuyển
    private bool HasPiecesInStable(PlayerColor playerColor)
    {
        // Lấy tất cả quân cờ của người chơi
        PieceController[] pieces = FindObjectsOfType<PieceController>();
        foreach (PieceController piece in pieces)
        {
            if (piece.playerColor == playerColor && piece.currentPathIndex == -1)
            {
                return true; // Có quân trong chuồng
            }
        }

        return false;
    }

    //private bool HasPiecesOnBoardCanMove(PlayerColor playerColor, int diceValue)
    //{
    //    // Lấy tất cả quân cờ của người chơi đang trên bàn
    //    PieceController[] pieces = FindObjectsOfType<PieceController>();
    //    foreach (PieceController piece in pieces)
    //    {
    //        if (piece.playerColor == playerColor && piece.currentPathIndex >= 0)
    //        {
    //            // Kiểm tra xem quân này có thể di chuyển diceValue bước không
    //            bool isPrivatePath;
    //            Transform nextPoint = HorseRacePathManager.Instance.GetNextPoint(
    //                piece.currentPathIndex,
    //                playerColor,
    //                out isPrivatePath);

    //            if (nextPoint != null) // Có thể di chuyển
    //            {
    //                return true;
    //            }
    //        }
    //    }
    //    return false;
    //}

    public void PieceMoved()
    {
        pieceMovedThisTurn = true;
        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }

        DiceController diceController = DiceController.Instance;
        diceController?.RequestReturnOwnershipToMaster(0.3f);
        
        // Gọi khi người chơi đã di chuyển quân cờ xong
        Invoke(nameof(EndTurn), 1f);
    }


    // Trong GameTurnManager.cs
    public void MovePiece(PieceController piece)
    {
        if (!IsCurrentPlayer(piece.playerColor)) return;

        int? diceValueNullable = DiceController.Instance.LastDiceValue;
        if (!diceValueNullable.HasValue)
        {
            Debug.LogWarning("Attempted to move piece without a dice roll value.");
            return;
        }

        piece.Move(diceValueNullable.Value);
    }

    // Thêm phương thức để xử lý khi quân bị đá
    public void OnPieceKicked(PlayerColor kickedPlayerColor)
    {
        // Có thể thêm hiệu ứng âm thanh, hình ảnh, hoặc thông báo
        Debug.Log($"Quân {kickedPlayerColor} bị đá về chuồng!");
    }

    // PUN Network Synchronization
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Gửi dữ liệu đến các client khác
            stream.SendNext(currentPlayerIndex);
            stream.SendNext(playerOrder.Count);
            for (int i = 0; i < playerOrder.Count; i++)
            {
                stream.SendNext(playerOrder[i]);
            }

            stream.SendNext(isInitialized);
            stream.SendNext(isDeterminingOrder);
        }
        else
        {
            // Nhận dữ liệu từ master client
            networkCurrentPlayerIndex = (int)stream.ReceiveNext();
            int orderCount = (int)stream.ReceiveNext();
            networkPlayerOrder.Clear();
            for (int i = 0; i < orderCount; i++)
            {
                networkPlayerOrder.Add((PlayerColor)stream.ReceiveNext());
            }

            networkIsInitialized = (bool)stream.ReceiveNext();
            bool networkIsDeterminingOrder = (bool)stream.ReceiveNext();

            // Cập nhật nếu không phải là master client
            if (!photonView.IsMine)
            {
                UpdateFromNetwork(networkIsDeterminingOrder);
            }
        }
    }

    private void UpdateFromNetwork(bool networkIsDeterminingOrder)
    {
        // Cập nhật chỉ số người chơi hiện tại
        if (currentPlayerIndex != networkCurrentPlayerIndex)
        {
            currentPlayerIndex = networkCurrentPlayerIndex;
        }

        // Cập nhật thứ tự người chơi
        if (!playerOrder.SequenceEqual(networkPlayerOrder))
        {
            playerOrder = new List<PlayerColor>(networkPlayerOrder);
        }

        // Cập nhật trạng thái khởi tạo
        if (isInitialized != networkIsInitialized)
        {
            isInitialized = networkIsInitialized;
        }

        // Cập nhật trạng thái xác định thứ tự
        if (isDeterminingOrder != networkIsDeterminingOrder)
        {
            isDeterminingOrder = networkIsDeterminingOrder;
        }
    }

    // RPC để chuyển lượt
    [PunRPC]
    public void NetworkEndTurn()
    {
        if (photonView.IsMine)
        {
            EndTurnLocal();
        }
    }

    // Thêm vào class GameTurnManager
    public void OnTurnCompleted()
    {
        // Gọi khi một lượt chơi hoàn thành
        if (PhotonManager.Instance != null)
        {
            PhotonManager.Instance.IncrementTurnCount();
        }
    }


    // SỬA phương thức EndTurnLocal()
    private void EndTurnLocal()
    {
        // KIỂM TRA NULL TRƯỚC KHI SỬ DỤNG
        if (gameStateManager == null)
        {
            gameStateManager = GameStateManager.Instance;
            if (gameStateManager == null)
            {
                Debug.LogError("GameStateManager is null in EndTurnLocal!");
                return;
            }
        }

        if (!gameStateManager.isGameInitialized || gameStateManager.playerOrder == null ||
            gameStateManager.playerOrder.Count == 0)
        {
            Debug.LogWarning("Khong the ket thuc luot vi player order chua san sang.");
            return;
        }

        // Sử dụng GameStateManager để chuyển lượt (chỉ Master Client)
        if (PhotonNetwork.IsMasterClient)
        {
            DiceController diceController = DiceController.Instance;
            diceController?.ReturnOwnershipToMaster();
            gameStateManager.NextTurn();
        }

        // Thông báo lượt chơi hoàn thành
        OnTurnCompleted();
    }

    // RPC để bắt đầu lượt
    [PunRPC]
    public void NetworkStartTurn()
    {
        StartTurnLocal();
    }

    // Trong GameTurnManager.cs, thêm phương thức:

// THÊM: Kiểm tra xem player với màu cụ thể có đang online
    public bool IsPlayerOnline(PlayerColor color)
    {
        if (PhotonManager.Instance == null) return true; // Fallback cho offline mode

        List<PlayerColor> onlineColors = PhotonManager.Instance.GetRoomPlayerColors();
        return onlineColors.Contains(color);
    }

// SỬA: Phương thức StartTurnLocal để kiểm tra player online
    // SỬA phương thức StartTurnLocal()
    private void StartTurnLocal()
    {
        // KIỂM TRA NULL
        if (gameStateManager == null)
        {
            gameStateManager = GameStateManager.Instance;
            if (gameStateManager == null)
            {
                Debug.LogError("GameStateManager is null in StartTurnLocal!");
                return;
            }
        }

        if (WinConditionManager.Instance != null && WinConditionManager.Instance.IsGameEnded())
        {
            return;
        }

        // Sử dụng thông tin từ GameStateManager thay vì local state
        PlayerColor currentPlayer = SyncLocalTurnState();
        if (currentPlayer == PlayerColor.None)
        {
            Debug.LogWarning("StartTurnLocal: Current player could not be resolved.");
            return;
        }

        pieceMovedThisTurn = false;
        noMovesNotifiedThisTurn = false;

        // Kiểm tra player có online không
        if (!IsPlayerOnline(currentPlayer))
        {
            Debug.Log($"Player {currentPlayer} is offline, skipping turn");
            Invoke("EndTurn", 1f);
            return;
        }

        if (DiceController.Instance != null)
        {
            DiceController.Instance.currentRollingPlayer = currentPlayer;
            DiceController.Instance.EnableDiceForCurrentPlayer();
        }

        if (DiceController.Instance.statusText != null)
        {
            DiceController.Instance.statusText.text = $"Lượt của {currentPlayer}\nChưa xúc xắc";
        }

        DiceController.Instance.hasRolledThisTurn = false;
        DiceController.Instance.ResetDiceValue();

        // Đảm bảo vật lý xúc xắc được kích hoạt
        if (DiceController.Instance != null)
        {
            DiceController.Instance.EnsurePhysicsForNewTurn();
        }

        if (autoPlayAllPlayers)
        {
            DiceController.Instance.AutoRollForCurrentPlayer();
        }
    }


    // RPC để di chuyển quân cờ
    [PunRPC]
    public void NetworkMovePiece(int pieceIndex, int steps)
    {
        if (photonView.IsMine)
        {
            PieceController[] pieces = FindObjectsOfType<PieceController>();
            if (pieceIndex < pieces.Length)
            {
                pieces[pieceIndex].Move(steps);
            }
        }
    }

    // RPC để đá quân về chuồng
    [PunRPC]
    public void NetworkKickPiece(int pieceIndex)
    {
        if (photonView.IsMine)
        {
            PieceController[] pieces = FindObjectsOfType<PieceController>();
            if (pieceIndex < pieces.Length)
            {
                pieces[pieceIndex].photonView.RPC("NetworkKickToStable", RpcTarget.All);
            }
        }
    }



    public bool HasPieceMovedThisTurn()
    {
        return pieceMovedThisTurn;
    }

    [PunRPC]
    public void NetworkSyncPlayerOrder(int[] playerOrderArray, int startPlayerIndex)
    {
        if (!PhotonNetwork.IsMasterClient) // Chỉ các client khác mới nhận
        {
            List<PlayerColor> syncedOrder = new List<PlayerColor>(playerOrderArray.Length);
            foreach (int colorValue in playerOrderArray)
            {
                syncedOrder.Add((PlayerColor)colorValue);
            }

            PlayerColor activeColor = PlayerColor.None;
            if (syncedOrder.Count > 0 && startPlayerIndex >= 0 && startPlayerIndex < syncedOrder.Count)
            {
                activeColor = syncedOrder[startPlayerIndex];
            }

            ApplySyncedTurn(syncedOrder, startPlayerIndex, activeColor);

            Debug.Log($"Đã nhận player order từ Master Client: {string.Join(", ", playerOrder)}");

            // Bắt đầu lượt chơi đầu tiên
            StartTurn();
        }
    }
}
