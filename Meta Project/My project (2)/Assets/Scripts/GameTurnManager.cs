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

    void Start()
    {
        // Gọi khởi tạo game
        GameTurnManager.Instance.InitializePlayerOrder(DiceController.Instance);
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

    public void InitializePlayerOrder(DiceController diceController)
    {
        StartCoroutine(DeterminePlayerOrder(diceController));
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

    private System.Collections.IEnumerator DeterminePlayerOrder(DiceController diceController)
    {
        isDeterminingOrder = true; // Bật flag khi bắt đầu xác định thứ tự
        Dictionary<PlayerColor, int> playerRolls = new Dictionary<PlayerColor, int>();
        
        // Kiểm tra null trước khi truy cập diceButton
        if (diceController != null && diceController.diceButton != null)
        {
        diceController.diceButton.interactable = false;
        }

        foreach (PlayerColor color in System.Enum.GetValues(typeof(PlayerColor)))
        {
            // Bỏ qua PlayerColor.None
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

        isDeterminingOrder = false; // Tắt flag khi hoàn thành

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
        isInitialized = true; // Thêm dòng này sau khi khởi tạo xong
        isDeterminingOrder = false;
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

    public void RollDiceForCurrentPlayer()
    {
        if (!DiceController.Instance.diceButton.interactable) // Chỉ roll nếu người chơi chưa roll
        {
            DiceController.Instance.RollDiceForPlayer(CurrentPlayer);
        }
    }

    public void EndTurn()
    {
        // Nếu có PUN và là master client, gửi RPC
        if (isNetworked && photonView.IsMine)
        {
            photonView.RPC("NetworkEndTurn", RpcTarget.All);
        }
        else
        {
            // Chạy local nếu không có PUN
            EndTurnLocal();
        }
    }

    public bool IsCurrentPlayer(PlayerColor color)
    {
        if (!isInitialized || playerOrder.Count == 0)
        {
            return false;
        }
        return CurrentPlayer == color;
    }


    public void CheckForPossibleMoves()
    {
        int diceValue = DiceController.Instance.LastDiceValue;
        PlayerColor currentPlayer = CurrentPlayer;

        Debug.Log($"CheckForPossibleMoves: Player {currentPlayer}, Dice value: {diceValue}");

        // Kiểm tra xem có quân cờ nào có thể di chuyển với số xúc xắc hiện tại không
        bool canMove = HasValidMoves(currentPlayer, diceValue);

        Debug.Log($"Can move: {canMove}");

        if (!canMove)
        {
            // Nếu không thể di chuyển, chuyển lượt sau 2 giây (tăng thời gian để người chơi thấy)
            Debug.Log("No valid moves available, ending turn in 2 seconds");
            Invoke("EndTurn", 2f);
        }
        else
        {
            // Nếu có thể di chuyển, khóa nút xúc xắc
            DiceController.Instance.canRollAgain = false;
            DiceController.Instance.diceButton.interactable = false;
            
            // Cập nhật status text để thông báo cho người chơi
            if (DiceController.Instance.statusText != null)
            {
                DiceController.Instance.statusText.text = $"Lượt của {currentPlayer}\nHãy di chuyển quân cờ!";
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

    private void EndTurnLocal()
    {
        currentPlayerIndex = (currentPlayerIndex + 1) % playerOrder.Count;
        Invoke("StartTurn", 1f);
    }

    // RPC để bắt đầu lượt
    [PunRPC]
    public void NetworkStartTurn()
    {
        StartTurnLocal();
    }

    private void StartTurnLocal()
    {
        if (WinConditionManager.Instance != null && WinConditionManager.Instance.IsGameEnded())
        {
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
}