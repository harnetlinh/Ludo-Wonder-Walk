using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;

public class DiceController : MonoBehaviourPunCallbacks, IPunObservable
{
    public static DiceController Instance { get; private set; }
    
    public TextMeshProUGUI diceResultText;
    private int? lastDiceValue;
    private const int DiceNullSentinel = -1;
    public int? LastDiceValue
    {
        get => lastDiceValue;
        set => lastDiceValue = NormalizeDiceValue(value);
    }
    public float autoRollDelay = 1f;
    public DiceFaceDetector diceFaceDetector;
    public bool isDiceRolling = false;

    [Header("Custom Settings")]
    public bool useCustomDiceValues = false;
    public List<int> customDiceSequence = new List<int>();
    public int diceSequenceIndex = 0;
    public bool useCustomPlayerOrder = false;
    public List<PlayerColor> customPlayerOrder = new List<PlayerColor>();

    public PlayerColor currentRollingPlayer;

    // Thêm dictionary để lưu vị trí xúc xắc cho mỗi màu
    private Dictionary<PlayerColor, Vector3> playerDicePositions = new Dictionary<PlayerColor, Vector3>();




    // Trong DiceController.cs
    [Header("Dice Positions for Each Player")]
    public Transform redDicePosition;
    public Transform blueDicePosition;
    public Transform yellowDicePosition;
    public Transform greenDicePosition;

    public bool canRollAgain = true; // Thêm biến này

    public bool hasRolledThisTurn = false; // Thêm biến này để theo dõi đã xúc xắc trong lượt này chưa


    public TextMeshProUGUI statusText; // Tham chiếu đến UI Text để hiển thị trạng thái


    [Header("Dice Movement Settings")]
    public float diceMoveDuration = 1.0f;
    public AnimationCurve diceMoveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public bool isMovingToPlayer = false; // <-- THÊM DÒNG NÀY

    // PUN Network Variables
    private int? networkDiceValue = null;
    private bool networkIsRolling = false;
    private PlayerColor networkCurrentPlayer;
    private bool isNetworked = false;


    private DiceFaceDetector diceDetector;
    private NetworkDiceSync networkDiceSync;
    private PlayerColor localPlayerColor = PlayerColor.None;
    // Thêm vào đầu class
    private GameStateManager gameStateManager;
    private Coroutine diceMoveRoutine;

    private static int? NormalizeDiceValue(int? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        int sanitized = value.Value;
        return sanitized >= 1 && sanitized <= 6 ? sanitized : (int?)null;
    }

    private static string FormatDiceValue(int? value)
    {
        return value.HasValue ? value.Value.ToString() : "-";
    }

    private static int ToNetworkValue(int? value)
    {
        return value.HasValue ? value.Value : DiceNullSentinel;
    }

    private static int? FromNetworkValue(int value)
    {
        return value == DiceNullSentinel ? null : NormalizeDiceValue(value);
    }

    private void Start()
    {
        diceDetector = GetComponent<DiceFaceDetector>();
        networkDiceSync = GetComponent<NetworkDiceSync>();

        if (photonView != null)
        {
            PhotonNetwork.AddCallbackTarget(this);

            if (!photonView.IsMine)
            {
                Invoke("RequestInitialSync", 1f);
            }
        }

        if (PhotonManager.Instance != null && PhotonManager.Instance.IsGameStarted())
        {
            Debug.Log("Game da bat dau, kich hoat DiceController");
        }

        if (PhotonManager.Instance != null)
        {
            localPlayerColor = PhotonManager.Instance.GetCurrentPlayerColor();
            PhotonManager.Instance.OnLocalPlayerColorAssigned += HandleLocalPlayerColorAssigned;
        }

        if (GameStateManager.Instance != null)
        {
            gameStateManager = GameStateManager.Instance;
            gameStateManager.OnDiceResultChanged += OnDiceResultChanged;
            gameStateManager.OnTurnChanged += HandleTurnChanged;
            gameStateManager.OnDiceTransformChanged += HandleDiceTransformChanged;

            if (gameStateManager.isGameInitialized && gameStateManager.playerOrder.Count > 0)
            {
                HandleTurnChanged(gameStateManager.currentTurnIndex, gameStateManager.currentPlayerColor);
            }

            if (!gameStateManager.HasDiceTransform && PhotonNetwork.IsMasterClient && diceFaceDetector != null)
            {
                gameStateManager.UpdateDiceTransform(diceFaceDetector.transform.position, diceFaceDetector.transform.rotation);
            }
            else if (gameStateManager.HasDiceTransform)
            {
                HandleDiceTransformChanged(gameStateManager.diceWorldPosition, gameStateManager.diceWorldRotation);
            }
        }
        
    }

    private void RequestInitialSync()
    {
        if (photonView == null || photonView.IsMine)
        {
            return;
        }

        Player targetOwner = photonView.Owner ?? PhotonNetwork.MasterClient;
        if (targetOwner != null)
        {
            photonView.RPC(nameof(RequestStateSync), targetOwner);
        }
        else
        {
            Debug.LogWarning("RequestInitialSync: No valid owner found for dice PhotonView.");
        }
    }

    [PunRPC]
    private void RequestStateSync(PhotonMessageInfo info)
    {
        if (photonView == null || !photonView.IsMine)
        {
            return;
        }

        Player requestingPlayer = info.Sender;
        if (requestingPlayer == null)
        {
            Debug.LogWarning("RequestStateSync invoked without a valid sender.");
            return;
        }

        photonView.RPC(
            nameof(ReceiveDiceState),
            requestingPlayer,
            ToNetworkValue(LastDiceValue),
            isDiceRolling,
            currentRollingPlayer,
            hasRolledThisTurn
        );
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

        /*if (diceButton != null)
        {
            diceButton.onClick.AddListener(OnDiceClick);
            diceButton.interactable = false;
        }
        else
        {
            Debug.LogWarning("DiceController: 'diceButton' is not assigned in Inspector.");
        }*/

        // Thiết lập vị trí xúc xắc cho mỗi màu
        if (redDicePosition != null)
            playerDicePositions[PlayerColor.Red] = redDicePosition.position;
        if (blueDicePosition != null)
            playerDicePositions[PlayerColor.Blue] = blueDicePosition.position;
        if (yellowDicePosition != null)
            playerDicePositions[PlayerColor.Yellow] = yellowDicePosition.position;
        if (greenDicePosition != null)
            playerDicePositions[PlayerColor.Green] = greenDicePosition.position;

        // Khởi tạo PUN
        if (photonView != null)
        {
            isNetworked = true;
            networkDiceValue = LastDiceValue;
            networkIsRolling = isDiceRolling;
        }
        // Thêm vào Awake()
        gameStateManager = GameStateManager.Instance;
    }

    public void SetDicePositionForPlayer(PlayerColor color, Vector3 position)
    {
        playerDicePositions[color] = position;
    }

    public void ResetDiceToPlayerPosition(PlayerColor color)
    {
        if (playerDicePositions.ContainsKey(color) && diceFaceDetector != null)
        {
            // Đảm bảo sở hữu trước khi đặt lại vị trí để mọi client có thể cập nhật
            NetworkDiceSync diceSync = diceFaceDetector.GetComponent<NetworkDiceSync>();
            if (diceSync != null)
            {
                diceSync.RequestOwnership();
            }
            diceFaceDetector.transform.position = playerDicePositions[color];
            diceFaceDetector.transform.rotation = Quaternion.identity;

            // Reset các trạng thái
            diceFaceDetector.isFirstPickup = true;
            diceFaceDetector.hasLanded = false;

            Rigidbody rb = diceFaceDetector.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = false;
            }

            if (gameStateManager == null)
            {
                gameStateManager = GameStateManager.Instance;
            }

            if (PhotonNetwork.IsMasterClient && gameStateManager != null)
            {
                gameStateManager.UpdateDiceTransform(diceFaceDetector.transform.position, diceFaceDetector.transform.rotation);
            }
        }
    }




    private PlayerColor GetCurrentPlayer()
    {
        if (useCustomPlayerOrder && customPlayerOrder.Count > 0)
        {
            int currentIndex = GameTurnManager.Instance.currentPlayerIndex % customPlayerOrder.Count;
            return customPlayerOrder[currentIndex];
        }
        return GameTurnManager.Instance.CurrentPlayer;
    }

    public void OnDiceClick()
    {
        RollDice();
    }

    public void PrepareToRoll()
    {
        // Nếu có PUN và là master client, gửi RPC
        if (isNetworked && photonView.IsMine && PhotonNetwork.InRoom)
        {
            photonView.RPC("NetworkPrepareToRoll", RpcTarget.All);
        }
        else
        {
            // Chạy local nếu không có PUN
            PrepareToRollLocal();
        }
    }

    private void PrepareToRollLocal()
    {
        isDiceRolling = true;
        if (diceResultText != null)
        {
            diceResultText.text = "Dang xuc xac...";
        }
        if (statusText != null)
        {
            statusText.text = $"{currentRollingPlayer} dang xuc xac...";
        }
        LastDiceValue = null;
    }

    public void FinalizeRoll()
    {
        // Nếu có PUN và là master client, gửi RPC
        if (isNetworked && photonView.IsMine && PhotonNetwork.InRoom)
        {
            photonView.RPC("NetworkFinalizeRoll", RpcTarget.All);
        }
        else
        {
            // Chạy local nếu không có PUN
            FinalizeRollLocal();
        }
    }

    private void FinalizeRollLocal()
    {
        if (isDiceRolling && diceFaceDetector != null && diceFaceDetector.IsDiceStopped())
        {
            LastDiceValue = diceFaceDetector.GetCurrentFaceValue();
            diceResultText.text = $"{currentRollingPlayer}: {FormatDiceValue(LastDiceValue)}";
            if (statusText != null)
            {
                statusText.text = $"{currentRollingPlayer} xúc ra số {FormatDiceValue(LastDiceValue)}";
            }
            isDiceRolling = false;
            hasRolledThisTurn = LastDiceValue.HasValue;

            if (!GameTurnManager.Instance.isDeterminingOrder)
            {
                GameTurnManager.Instance.CheckForPossibleMoves();
            }
        }
    }

    

    public void AutoRollForCurrentPlayer()
    {
        // CHỈ Master Client mới được auto roll
        if (!PhotonNetwork.IsMasterClient) 
        {
            Debug.Log("Chỉ Master Client mới được auto roll");
            return;
        }
    
        currentRollingPlayer = GetCurrentPlayer();
        Invoke("PerformAutoRoll", autoRollDelay);
    }

    private void PerformAutoRoll()
    {
        PrepareToRoll();
        // Trong chế độ auto, giả lập việc xúc xắc dừng sau 2 giây
        Invoke("SimulateDiceStop", 2f);
    }

    private void SimulateDiceStop()
    {
        if (useCustomDiceValues && customDiceSequence.Count > 0)
        {
            LastDiceValue = customDiceSequence[diceSequenceIndex % customDiceSequence.Count];
            diceSequenceIndex++;
        }
        else
        {
            LastDiceValue = Random.Range(1, 7);
        }

        diceResultText.text = $"{currentRollingPlayer}: {FormatDiceValue(LastDiceValue)}";
        isDiceRolling = false;
        hasRolledThisTurn = LastDiceValue.HasValue;

        if (!GameTurnManager.Instance.isDeterminingOrder)
        {
            //HighlightManager.Instance.ClearAllHighlights();
            GameTurnManager.Instance.CheckForPossibleMoves();
        }
    }


    

    // THAY THẾ phương thức RollDice()
public void RollDice()
{
    if (gameStateManager == null)
    {
        gameStateManager = GameStateManager.Instance;
    }

    bool bypassTurnValidation = ShouldForceRollForInitialization();

    if (!bypassTurnValidation)
    {
        if (!IsLocalPlayersTurn())
        {
            Debug.LogWarning("Cannot roll dice: not your turn");
            return;
        }

        if (hasRolledThisTurn)
        {
            Debug.LogWarning("Cannot roll dice: already rolled this turn");
            return;
        }
    }
    else if (photonView != null && !photonView.IsMine)
    {
        photonView.RequestOwnership();
    }

    GameTurnManager turnManager = GameTurnManager.Instance;
    if (!bypassTurnValidation && turnManager != null && !turnManager.IsCurrentPlayer(currentRollingPlayer))
    {
        Debug.LogWarning(
            $"RollDice detected local turn desync for {currentRollingPlayer}. Forwarding request to master for authoritative validation.");
    }

    if (PhotonNetwork.IsMasterClient)
    {
        StartDiceRollProcess();
    }
    else
    {
        photonView.RPC("RPC_RequestRollDice", RpcTarget.MasterClient, currentRollingPlayer);
    }
}

[PunRPC]
private void RPC_RequestRollDice(PlayerColor requestingPlayer)
{
    if (!PhotonNetwork.IsMasterClient) return;

    Debug.Log($"🎲 Master received roll request from {requestingPlayer}");

    // Kiểm tra xem có phải lượt của player này không
    if (GameTurnManager.Instance.IsCurrentPlayer(requestingPlayer))
    {
        currentRollingPlayer = requestingPlayer;
        StartDiceRollProcess();
    }
    else
    {
        Debug.LogWarning($"❌ Roll request denied: Not {requestingPlayer}'s turn");
    }
}

private void StartDiceRollProcess()
{
    if (gameStateManager == null)
    {
        gameStateManager = GameStateManager.Instance;
    }

    if (gameStateManager == null)
    {
        Debug.LogError("DiceController: GameStateManager missing, aborting roll");
        return;
    }

    gameStateManager.StartDiceRolling(currentRollingPlayer);

    PrepareToRoll();

    if (PhotonNetwork.IsMasterClient)
    {
        int diceValue = useCustomDiceValues && customDiceSequence.Count > 0
            ? customDiceSequence[diceSequenceIndex++ % customDiceSequence.Count]
            : Random.Range(1, 7);

        gameStateManager.SetDiceResult(diceValue, currentRollingPlayer);
    }
}

// THÊM phương thức để nhận kết quả xúc xắc từ GameStateManager
private void OnDiceResultChanged(int? value, PlayerColor playerColor)
{
    if (playerColor != currentRollingPlayer)
    {
        if (currentRollingPlayer != PlayerColor.None)
        {
            Debug.LogWarning(
                $"DiceController: Sync mismatch on dice result. Expected {currentRollingPlayer}, received {playerColor}. Updating to server state.");
        }

        currentRollingPlayer = playerColor;
    }

    LastDiceValue = value;
    if (diceResultText != null)
    {
        diceResultText.text = $"{playerColor}: {FormatDiceValue(LastDiceValue)}";
    }
    isDiceRolling = false;
    hasRolledThisTurn = value.HasValue;

    if (statusText != null)
    {
        statusText.text = value.HasValue
            ? $"{playerColor} xuc ra so {FormatDiceValue(value)}"
            : $"Luot cua {playerColor}\nChua xuc xac";
    }

    if (GameTurnManager.Instance != null && !GameTurnManager.Instance.isDeterminingOrder)
    {
        GameTurnManager.Instance.CheckForPossibleMoves();
    }
    
}
private void HandleLocalPlayerColorAssigned(PlayerColor color)
{
    localPlayerColor = color;
}

private void HandleTurnChanged(int turnIndex, PlayerColor playerColor)
{
    currentRollingPlayer = playerColor;
    hasRolledThisTurn = false;
    isDiceRolling = false;
    ResetDiceValue();

    if (statusText != null)
    {
        if (playerColor == PlayerColor.None)
        {
            statusText.text = "Dang cho luot...";
        }
        else
        {
            statusText.text = $"Luot cua {playerColor}\nChua xuc xac";
        }
    }
    
    if (PhotonNetwork.IsMasterClient)
    {
        MoveDiceToCurrentPlayer();
    }
}

private void HandleDiceTransformChanged(Vector3 position, Quaternion rotation)
{
    if (diceFaceDetector == null)
    {
        return;
    }

    if (diceMoveRoutine != null)
    {
        StopCoroutine(diceMoveRoutine);
        diceMoveRoutine = null;
    }

    isMovingToPlayer = false;
    diceFaceDetector.transform.SetPositionAndRotation(position, rotation);

    Rigidbody rb = diceFaceDetector.GetComponent<Rigidbody>();
    if (rb != null)
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.WakeUp();
    }

    diceFaceDetector.isFirstPickup = true;
    diceFaceDetector.hasLanded = false;
    diceFaceDetector.ResetRollTrackingState();
}

private bool IsLocalPlayersTurn()
{
    if (currentRollingPlayer == PlayerColor.None)
    {
        return false;
    }

    if (PhotonManager.Instance == null || !PhotonNetwork.InRoom)
    {
        return true;
    }

    if (localPlayerColor == PlayerColor.None)
    {
        localPlayerColor = PhotonManager.Instance.GetCurrentPlayerColor();
    }

    return localPlayerColor != PlayerColor.None && localPlayerColor == currentRollingPlayer;
}



private bool ShouldForceRollForInitialization()
{
    if (!PhotonNetwork.IsMasterClient)
    {
        return false;
    }

    GameTurnManager turnManager = GameTurnManager.Instance;
    if (turnManager == null)
    {
        return false;
    }

    return turnManager.isDeterminingOrder;
}



    public void RollDiceForPlayer(PlayerColor playerColor)
    {
        currentRollingPlayer = playerColor;
        RollDice();
    }

    public void ResetDiceValue()
    {
        LastDiceValue = null;
        hasRolledThisTurn = false;
        if (diceResultText != null)
        {
            diceResultText.text = currentRollingPlayer == PlayerColor.None
                ? "-"
                : $"{currentRollingPlayer}: {FormatDiceValue(LastDiceValue)}";
        }
    }

    public void SetCustomDiceSequence(List<int> sequence)
    {
        customDiceSequence = new List<int>(sequence);
        diceSequenceIndex = 0;
        useCustomDiceValues = true;
    }

    public void SetCustomPlayerOrder(List<PlayerColor> order)
    {
        customPlayerOrder = new List<PlayerColor>(order);
        useCustomPlayerOrder = true;
        GameTurnManager.Instance.playerOrder = new List<PlayerColor>(order);
        GameTurnManager.Instance.currentPlayerIndex = 0;
    }

    public void DisableCustomSettings()
    {
        useCustomDiceValues = false;
        useCustomPlayerOrder = false;
    }



    // Thêm phương thức di chuyển xúc xắc đến vị trí người chơi hiện tại
    public void MoveDiceToCurrentPlayer()
    {
        if (diceFaceDetector == null) return;
        if (isMovingToPlayer) return;

        PlayerColor currentPlayer = GetCurrentPlayer();
        if (playerDicePositions.TryGetValue(currentPlayer, out Vector3 targetPosition))
        {
            bool broadcast = photonView != null && photonView.IsMine && PhotonNetwork.IsMasterClient;
            StartDiceMove(targetPosition, Quaternion.identity, broadcast);
        }
    }


    public void EnableDiceForCurrentPlayer()
    {
        // CHỈ Master client mới được điều khiển dice
        if (!PhotonNetwork.IsMasterClient) 
        {
            Debug.Log("Chỉ Master client mới được điều khiển dice");
            return;
        }
    
        // KHÔNG cho phép kích hoạt nếu đang di chuyển
        if (isMovingToPlayer) 
        {
            Debug.Log("Dice đang di chuyển, không thể kích hoạt ngay lúc này");
            return;
        }
    
        currentRollingPlayer = GetCurrentPlayer();
    
        // Kiểm tra player có trong game và online
        if (GameTurnManager.Instance != null && 
            (!GameTurnManager.Instance.IsColorInGame(currentRollingPlayer) ||
             !IsPlayerWithColorOnline(currentRollingPlayer)))
        {
            Debug.Log($"Player color {currentRollingPlayer} is not in game or offline, skipping turn");
            GameTurnManager.Instance.EndTurn();
            return;
        }
    
        hasRolledThisTurn = false;

        // Di chuyển xúc xắc đến vị trí người chơi hiện tại
        MoveDiceToCurrentPlayer();

        // Cập nhật thông báo cho tất cả client
        photonView.RPC("RPC_UpdateDiceStatus", RpcTarget.All, $"Luot cua {currentRollingPlayer}\nChua xuc xac");

        if (diceResultText != null)
        {
            diceResultText.text = hasRolledThisTurn
                ? "Ban da xuc xac trong luot nay"
                : "Cam xuc xac len de nem";
        }
    }

    [PunRPC]
    private void RPC_UpdateDiceStatus(string status)
    {
        if (statusText != null)
        {
            statusText.text = status;
        }
    }

// THÊM: Kiểm tra xem player với màu cụ thể có đang online không
    private bool IsPlayerWithColorOnline(PlayerColor color)
    {
        if (PhotonManager.Instance == null) return true; // Fallback cho offline
    
        List<PlayerColor> onlineColors = PhotonManager.Instance.GetRoomPlayerColors();
        return onlineColors.Contains(color);
    }

// Thêm phương thức kiểm tra trạng thái
    public bool IsDiceMoving()
    {
        return isMovingToPlayer;
    }

    public bool CanInteractWithDice()
    {
        return !isMovingToPlayer && !isDiceRolling;
    }

    // PUN Network Synchronization
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Gửi dữ liệu đến các client khác
            stream.SendNext(ToNetworkValue(LastDiceValue));
            stream.SendNext(isDiceRolling);
            stream.SendNext(currentRollingPlayer);
            stream.SendNext(hasRolledThisTurn);
        }
        else
        {
            // Nhận dữ liệu từ master client
            networkDiceValue = FromNetworkValue((int)stream.ReceiveNext());
            networkIsRolling = (bool)stream.ReceiveNext();
            networkCurrentPlayer = (PlayerColor)stream.ReceiveNext();
            bool networkHasRolled = (bool)stream.ReceiveNext();

            // Cập nhật nếu không phải là master client
            if (!photonView.IsMine)
            {
                UpdateFromNetwork(networkHasRolled);
            }
        }
    }

    private void UpdateFromNetwork(bool networkHasRolled)
    {
        // Cập nhật giá trị xúc xắc
        if (LastDiceValue != networkDiceValue)
        {
            LastDiceValue = networkDiceValue;
            if (diceResultText != null)
            {
                diceResultText.text = $"{networkCurrentPlayer}: {FormatDiceValue(LastDiceValue)}";
            }
        }

        // Cập nhật trạng thái rolling
        if (isDiceRolling != networkIsRolling)
        {
            isDiceRolling = networkIsRolling;
            if (diceResultText != null && isDiceRolling)
            {
                diceResultText.text = "Đang xúc xắc...";
            }
        }

        // Cập nhật người chơi hiện tại
        if (currentRollingPlayer != networkCurrentPlayer)
        {
            currentRollingPlayer = networkCurrentPlayer;
        }

        // Cập nhật trạng thái đã xúc xắc
        if (hasRolledThisTurn != networkHasRolled)
        {
            hasRolledThisTurn = networkHasRolled;
        }

        // Đồng bộ UI cơ bản cho client mới/không phải chủ
        if (statusText != null)
        {
            statusText.text = isDiceRolling
                ? $"{currentRollingPlayer} đang xúc xắc..."
                : (hasRolledThisTurn
                    ? $"{currentRollingPlayer} đã xúc xắc"
                    : $"Lượt của {currentRollingPlayer}\nChưa xúc xắc");
        }
        
    }

    // RPC để xúc xắc
    [PunRPC]
    public void NetworkRollDice()
    {
        if (photonView.IsMine)
        {
            RollDiceLocal();
        }
    }

    private void RollDiceLocal()
    {
        if (useCustomDiceValues && customDiceSequence.Count > 0)
        {
            LastDiceValue = customDiceSequence[diceSequenceIndex % customDiceSequence.Count];
            diceSequenceIndex++;
        }
        else
        {
            LastDiceValue = Random.Range(1, 7);
        }

        // Cập nhật text hiển thị
        diceResultText.text = $"{currentRollingPlayer}: {FormatDiceValue(LastDiceValue)}";
        hasRolledThisTurn = LastDiceValue.HasValue;

        if (!GameTurnManager.Instance.isDeterminingOrder)
        {
            GameTurnManager.Instance.CheckForPossibleMoves();
        }
    }

    // RPC để chuẩn bị xúc xắc
    [PunRPC]
    public void NetworkPrepareToRoll()
    {
        isDiceRolling = true;
        if (diceResultText != null)
        {
            diceResultText.text = "Dang xuc xac...";
        }
        if (statusText != null)
        {
            statusText.text = $"{currentRollingPlayer} dang xuc xac...";
        }
        LastDiceValue = null;
    }

    // RPC để hoàn thành xúc xắc
    [PunRPC]
    public void NetworkFinalizeRoll()
    {
        if (isDiceRolling && diceFaceDetector != null && diceFaceDetector.IsDiceStopped())
        {
            LastDiceValue = diceFaceDetector.GetCurrentFaceValue();
            diceResultText.text = $"{currentRollingPlayer}: {FormatDiceValue(LastDiceValue)}";
            if (statusText != null)
            {
                statusText.text = $"{currentRollingPlayer} xúc ra số {FormatDiceValue(LastDiceValue)}";
            }
            isDiceRolling = false;
            hasRolledThisTurn = LastDiceValue.HasValue;

            if (!GameTurnManager.Instance.isDeterminingOrder)
            {
                GameTurnManager.Instance.CheckForPossibleMoves();
            }
        }
    }

    [PunRPC]
    private void RPC_ReportDiceResult(int reportedValue, PlayerColor reportingColor, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning($"RPC_ReportDiceResult received on non-master client from {info.Sender?.NickName}");
            return;
        }
        bool isValidTurn = GameTurnManager.Instance == null
                           || GameTurnManager.Instance.IsCurrentPlayer(reportingColor);
        if (!isValidTurn && GameTurnManager.Instance != null)
        {
            if (GameTurnManager.Instance.ForceSetCurrentPlayer(reportingColor))
            {
                Debug.LogWarning(
                    $"DiceController: Detected turn desync from {info.Sender?.NickName}. Aligning turn to {reportingColor} before applying dice result {reportedValue}.");
            }
            else
            {
                Debug.LogWarning(
                    $"DiceController: Ignoring dice result {reportedValue} from {reportingColor} – color not present in current turn order.");
                return;
            }
        }
        currentRollingPlayer = reportingColor;
        if (gameStateManager == null)
        {
            gameStateManager = GameStateManager.Instance;
        }

        if (gameStateManager != null)
        {
            gameStateManager.SetDiceResult(reportedValue, reportingColor);
        }
        else
        {
            OnDiceResultChanged(reportedValue, reportingColor);
        }
    }

    // Trong DiceController.cs, thêm các phương thức sau:

    // Khi dùng PhotonTransformViewClassic, không cần sync thủ công
    public void ForceDiceSync() { }
    public void SyncDiceForNewTurn() { }

    private void StartDiceMove(Vector3 targetPosition, Quaternion targetRotation, bool broadcastToOthers)
    {
        if (diceFaceDetector == null)
        {
            return;
        }

        if (diceMoveRoutine != null)
        {
            StopCoroutine(diceMoveRoutine);
            diceMoveRoutine = null;
        }

        diceMoveRoutine = StartCoroutine(MoveDiceToPositionRoutine(targetPosition, targetRotation, broadcastToOthers));
    }

    private IEnumerator MoveDiceToPositionRoutine(Vector3 targetPosition, Quaternion targetRotation, bool broadcastToOthers)
    {
        isMovingToPlayer = true;

        if (diceFaceDetector == null)
        {
            isMovingToPlayer = false;
            yield break;
        }

        if (broadcastToOthers && photonView != null && PhotonNetwork.InRoom)
        {
            photonView.RPC(nameof(RPC_BeginDiceMove), RpcTarget.Others, targetPosition, targetRotation);
        }

        bool isOwner = photonView == null || photonView.IsMine;

        NetworkDiceSync diceSync = diceFaceDetector.GetComponent<NetworkDiceSync>();
        if (isOwner && diceSync != null)
        {
            diceSync.RequestOwnership();
            diceSync.SetKinematic(true, true);
        }

        Rigidbody rb = diceFaceDetector.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Vector3 startPosition = diceFaceDetector.transform.position;
        Quaternion startRotation = diceFaceDetector.transform.rotation;
        float elapsed = 0f;

        while (elapsed < diceMoveDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / diceMoveDuration);
            float t = diceMoveCurve.Evaluate(normalizedTime);

            diceFaceDetector.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            diceFaceDetector.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);

            yield return null;
        }

        diceFaceDetector.transform.SetPositionAndRotation(targetPosition, targetRotation);

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.WakeUp();
        }

        if (isOwner && diceSync != null)
        {
            diceSync.SetKinematic(false, true);
            diceSync.EnsurePhysicsActivation();
        }

        diceFaceDetector.isFirstPickup = true;
        diceFaceDetector.hasLanded = false;
        diceFaceDetector.ResetRollTrackingState();
        isMovingToPlayer = false;
        diceMoveRoutine = null;

        if (broadcastToOthers && PhotonNetwork.IsMasterClient)
        {
            if (gameStateManager == null)
            {
                gameStateManager = GameStateManager.Instance;
            }

            if (gameStateManager != null)
            {
                gameStateManager.UpdateDiceTransform(targetPosition, targetRotation);
            }
        }

        Debug.Log("Di chuyển xúc xắc hoàn tất - Vật lý đã được kích hoạt");
    }

    [PunRPC]
    private void RPC_BeginDiceMove(Vector3 targetPosition, Quaternion targetRotation)
    {
        StartDiceMove(targetPosition, targetRotation, false);
    }

// THÊM: Phương thức đảm bảo vật lý được kích hoạt khi bắt đầu lượt mới
public void EnsurePhysicsForNewTurn()
{
    if (diceFaceDetector != null)
    {
        NetworkDiceSync diceSync = diceFaceDetector.GetComponent<NetworkDiceSync>();
        if (diceSync != null)
        {
            diceSync.EnsurePhysicsActivation();
        }
        
        Rigidbody rb = diceFaceDetector.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.WakeUp();
        }
    }
}

    // Late-join synchronization: gửi trạng thái xúc xắc cho người chơi mới
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (!isNetworked) return;
        if (!PhotonNetwork.IsMasterClient) return;

        photonView.RPC(
            nameof(ReceiveDiceState),
            newPlayer,
            ToNetworkValue(LastDiceValue),
            isDiceRolling,
            currentRollingPlayer,
            hasRolledThisTurn
        );
    }

    [PunRPC]
    private void ReceiveDiceState(int value, bool rolling, PlayerColor currentPlayer, bool hasRolled)
    {
        LastDiceValue = FromNetworkValue(value);
        isDiceRolling = rolling;
        currentRollingPlayer = currentPlayer;
        hasRolledThisTurn = hasRolled;

        if (diceResultText != null)
        {
            diceResultText.text = isDiceRolling
                ? "Dang xuc xac..."
                : $"{currentRollingPlayer}: {FormatDiceValue(LastDiceValue)}";
        }

        if (statusText != null)
        {
            statusText.text = isDiceRolling
                ? $"{currentRollingPlayer} dang xuc xac..."
                : (hasRolledThisTurn
                    ? $"{currentRollingPlayer} da xuc xac"
                    : $"Luot cua {currentRollingPlayer}\nChua xuc xac");
        }
        
    }
    // THÊM vào OnDestroy() để hủy đăng ký event
    private void OnDestroy()
    {
        if (gameStateManager != null)
        {
            gameStateManager.OnDiceResultChanged -= OnDiceResultChanged;
            gameStateManager.OnTurnChanged -= HandleTurnChanged;
            gameStateManager.OnDiceTransformChanged -= HandleDiceTransformChanged;
        }

        if (PhotonManager.Instance != null)
        {
            PhotonManager.Instance.OnLocalPlayerColorAssigned -= HandleLocalPlayerColorAssigned;
        }
    }
}
