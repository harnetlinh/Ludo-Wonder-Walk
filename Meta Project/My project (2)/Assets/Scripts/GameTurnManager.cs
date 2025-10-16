using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Photon.Pun;

public class GameTurnManager : MonoBehaviourPun, IPunObservable
{
    public static GameTurnManager Instance { get; private set; }

    public List<PlayerColor> playerOrder = new List<PlayerColor>();
    public int currentPlayerIndex = 0;

    public bool autoPlayAllPlayers = true; // Thêm biến điều khiển chế độ test
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

void Start()
{
    // CHỈ Master Client mới gọi khởi tạo game và chỉ khi chưa khởi tạo
    if (PhotonNetwork.IsMasterClient && !isInitialized && !isInitializing)
    {
        Debug.Log("Master Client đang khởi tạo player order...");
        InitializePlayerOrder(DiceController.Instance);
    }
    else
    {
        if (isInitialized)
        {
            Debug.Log("Game đã được khởi tạo trước đó");
        }
        else if (isInitializing)
        {
            Debug.Log("Game đang được khởi tạo");
        }
        else
        {
            Debug.Log("Client đang chờ Master Client khởi tạo game...");
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

        // Khởi tạo PUN
        if (photonView != null)
        {
            isNetworked = true;
            networkCurrentPlayerIndex = currentPlayerIndex;
            networkPlayerOrder = new List<PlayerColor>(playerOrder);
            networkIsInitialized = isInitialized;
        }
    }

    // SỬA: Phương thức khởi tạo player order chỉ cho Master Client
    // SỬA: Phương thức InitializePlayerOrder để đảm bảo chỉ xử lý màu có trong game
    public void InitializePlayerOrder(DiceController diceController)
    {
        // CHỈ Master Client mới được khởi tạo game
        if (isNetworked && !PhotonNetwork.IsMasterClient)
        {
            Debug.Log("Chỉ Master Client mới được khởi tạo game - bỏ qua trên client này");
            return;
        }

        if (isInitializing || isInitialized)
        {
            Debug.Log("Game đang khởi tạo hoặc đã khởi tạo, bỏ qua");
            return;
        }

        isInitializing = true;

        if (PhotonManager.Instance != null)
        {
            List<PlayerColor> roomColors = PhotonManager.Instance.GetRoomPlayerColors();
            if (roomColors.Count > 0)
            {
                Debug.Log($"Bắt đầu khởi tạo lượt chơi với {roomColors.Count} người chơi: {string.Join(", ", roomColors)}");
            
                // CHỈ thêm những màu có trong room
                playerOrder.Clear();
                playerOrder.AddRange(roomColors);
            
                currentPlayerIndex = 0;
                isInitialized = true;
                isInitializing = false;
            
                Debug.Log($"Khởi tạo thành công: {playerOrder.Count} players in order");
            
                // Đồng bộ với các client khác
                if (isNetworked && photonView.IsMine)
                {
                    int[] playerOrderArray = playerOrder.Select(color => (int)color).ToArray();
                    photonView.RPC("NetworkSyncPlayerOrder", RpcTarget.Others, playerOrderArray, currentPlayerIndex);
                }
            
                // Bắt đầu lượt đầu tiên
                StartTurn();
            }
            else
            {
                Debug.LogWarning("Chưa có người chơi trong room, chờ...");
                isInitializing = false;
            }
        }
    }
    public PlayerColor CurrentPlayer
    {
        get
        {
            if (playerOrder == null || playerOrder.Count == 0 || currentPlayerIndex < 0 || currentPlayerIndex >= playerOrder.Count)
            {
                //Debug.LogError("Player order is not initialized or invalid currentPlayerIndex!");
                return PlayerColor.Red; // Hoặc giá trị mặc định khác
            }
            return playerOrder[currentPlayerIndex];
        }
    }

    // SỬA: Phương thức DeterminePlayerOrder để chỉ xét những player có trong room
    private System.Collections.IEnumerator DeterminePlayerOrder(DiceController diceController, List<PlayerColor> availableColors = null)
{
    isDeterminingOrder = true;
    Dictionary<PlayerColor, int> playerRolls = new Dictionary<PlayerColor, int>();

    // Sử dụng danh sách màu từ PhotonManager nếu có
    List<PlayerColor> colorsToProcess = availableColors ?? 
                                        new List<PlayerColor> { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Yellow, PlayerColor.Green };

    // Kiểm tra null trước khi truy cập diceButton
    if (diceController != null && diceController.diceButton != null)
    {
        diceController.diceButton.interactable = false;
    }

    foreach (PlayerColor color in colorsToProcess)
    {
        // Bỏ qua PlayerColor.None và những màu không có trong room
        if (color == PlayerColor.None) continue;
    
        if (diceController != null)
        {
            diceController.RollDiceForPlayer(color);
            yield return new WaitUntil(() => diceController.LastDiceValue > 0);
            playerRolls[color] = diceController.LastDiceValue;
            diceController.ResetDiceValue();
        }
        yield return null;
    }

    playerOrder.Clear();
    foreach (var entry in playerRolls.OrderByDescending(x => x.Value))
    {
        playerOrder.Add(entry.Key);
    }

    isDeterminingOrder = false;
    
    // THÊM: Đồng bộ với các client khác
    if (isNetworked && photonView.IsMine)
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
        return playerOrder.Contains(color);
    }


    // Cập nhật StartTurn để highlight các quân có thể di chuyển
    // Cập nhật StartTurn
    // Sửa phương thức StartTurn
    // SỬA: Phương thức StartTurn để chỉ master client điều khiển
    public void StartTurn()
    {
        // CHỈ Master Client mới được bắt đầu lượt
        if (isNetworked && !PhotonNetwork.IsMasterClient) 
        {
            Debug.Log("Client đang chờ master client bắt đầu lượt...");
            return;
        }

        if (isNetworked && photonView.IsMine)
        {
            photonView.RPC("NetworkStartTurn", RpcTarget.All);
        }
        else
        {
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

    public void RollDiceForCurrentPlayer()
    {
        if (!DiceController.Instance.diceButton.interactable) // Chỉ roll nếu người chơi chưa roll
        {
            DiceController.Instance.RollDiceForPlayer(CurrentPlayer);
        }
    }

    // SỬA: Phương thức EndTurn để chỉ master client điều khiển
    public void EndTurn()
    {
        pieceMovedThisTurn = false;
    
        // CHỈ Master Client mới được kết thúc lượt
        if (isNetworked && !PhotonNetwork.IsMasterClient) 
        {
            Debug.Log("Client đang chờ master client kết thúc lượt...");
            return;
        }

        if (isNetworked && photonView.IsMine)
        {
            photonView.RPC("NetworkEndTurn", RpcTarget.All);
        }
        else
        {
            EndTurnLocal();
        }
    }

// THÊM: Phương thức để client yêu cầu chuyển lượt
    [PunRPC]
    public void RPC_RequestEndTurn()
    {
        if (!PhotonNetwork.IsMasterClient) return;
    
        // Master client xác nhận và xử lý
        EndTurn();
    }

    // SỬA: Phương thức IsCurrentPlayer để kiểm tra cả việc màu có trong game không
    public bool IsCurrentPlayer(PlayerColor color)
    {
        if (!isInitialized || playerOrder.Count == 0 || !IsColorInGame(color))
        {
            return false;
        }
        return CurrentPlayer == color;
    }


    // SỬA: Phương thức CheckForPossibleMoves
    public void CheckForPossibleMoves()
    {
        // CHỈ Master Client xử lý logic di chuyển
        if (isNetworked && !PhotonNetwork.IsMasterClient) 
        {
            Debug.Log("Client đang chờ master client kiểm tra nước đi...");
            return;
        }

        int diceValue = DiceController.Instance.LastDiceValue;
        PlayerColor currentPlayer = CurrentPlayer;

        Debug.Log($"Master client checking moves: Player {currentPlayer}, Dice value: {diceValue}");

        bool canMove = HasValidMoves(currentPlayer, diceValue);
        Debug.Log($"Can move: {canMove}");

        if (!canMove)
        {
            Debug.Log("No valid moves available, ending turn");
            Invoke("EndTurn", 2f);
        }
        else
        {
            DiceController.Instance.canRollAgain = false;
            DiceController.Instance.diceButton.interactable = false;

            // Đồng bộ UI với tất cả client
            if (DiceController.Instance.photonView != null)
            {
                DiceController.Instance.photonView.RPC(
                    "RPC_UpdateDiceStatus", 
                    RpcTarget.All, 
                    $"Lượt của {currentPlayer}\nHãy di chuyển quân cờ!"
                );
            }
        }
    }


    // Cập nhật phương thức CanCurrentPlayerMove
    public bool CanCurrentPlayerMove()
    {
        int diceValue = DiceController.Instance.LastDiceValue;
        PlayerColor currentPlayer = CurrentPlayer;

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
        // Gọi khi người chơi đã di chuyển quân cờ xong
        Invoke("EndTurn", 1f);
    }


    // Trong GameTurnManager.cs
    public void MovePiece(PieceController piece)
    {
        if (!IsCurrentPlayer(piece.playerColor)) return;

        piece.Move(DiceController.Instance.LastDiceValue);
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

// Cập nhật phương thức EndTurnLocal
    private void EndTurnLocal()
    {
        currentPlayerIndex = (currentPlayerIndex + 1) % playerOrder.Count;
    
        // Thông báo lượt chơi hoàn thành
        OnTurnCompleted();
    
        Invoke("StartTurn", 1f);
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
    private void StartTurnLocal()
    {
        if (WinConditionManager.Instance != null && WinConditionManager.Instance.IsGameEnded())
        {
            return;
        }

        // THÊM: Kiểm tra xem current player có online không
        PlayerColor currentPlayer = CurrentPlayer;
        if (!IsPlayerOnline(currentPlayer))
        {
            Debug.Log($"Player {currentPlayer} is offline, skipping turn");
            Invoke("EndTurn", 1f);
            return;
        }

        if (DiceController.Instance != null)
        {
            DiceController.Instance.EnableDiceForCurrentPlayer();
        }

        if (DiceController.Instance.statusText != null)
        {
            DiceController.Instance.statusText.text = $"Lượt của {CurrentPlayer}\nChưa xúc xắc";
        }

        DiceController.Instance.hasRolledThisTurn = false;
        DiceController.Instance.ResetDiceValue();
    
        // THÊM: Đảm bảo vật lý xúc xắc được kích hoạt
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
        playerOrder.Clear();
        foreach (int colorValue in playerOrderArray)
        {
            playerOrder.Add((PlayerColor)colorValue);
        }
        currentPlayerIndex = startPlayerIndex;
        isInitialized = true;
        
        Debug.Log($"Đã nhận player order từ Master Client: {string.Join(", ", playerOrder)}");
        
        // Bắt đầu lượt chơi đầu tiên
        StartTurn();
    }
}
}