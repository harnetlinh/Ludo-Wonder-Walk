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

    public Button diceButton;
    public TextMeshProUGUI diceResultText;
    public int LastDiceValue { get; set; }
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
    private int networkDiceValue = 0;
    private bool networkIsRolling = false;
    private PlayerColor networkCurrentPlayer;
    private bool isNetworked = false;


    private DiceFaceDetector diceDetector;
    private NetworkDiceSync networkDiceSync;
    private PlayerColor localPlayerColor = PlayerColor.None;
    // Thêm vào đầu class
    private GameStateManager gameStateManager;

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

            if (gameStateManager.isGameInitialized && gameStateManager.playerOrder.Count > 0)
            {
                HandleTurnChanged(gameStateManager.currentTurnIndex, gameStateManager.currentPlayerColor);
            }
        }

        UpdateDiceButtonInteractivity();
    }

    private void RequestInitialSync()
    {
        if (photonView != null && !photonView.IsMine)
        {
            photonView.RPC("RequestStateSync", photonView.Owner);
        }
    }

    [PunRPC]
    private void RequestStateSync()
    {
        // Master client gửi trạng thái hiện tại
        if (photonView.IsMine)
        {
            RequestStateSync();
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

        if (diceButton != null)
        {
            diceButton.onClick.AddListener(OnDiceClick);
            diceButton.interactable = false;
        }
        else
        {
            Debug.LogWarning("DiceController: 'diceButton' is not assigned in Inspector.");
        }

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
        LastDiceValue = 0;
        UpdateDiceButtonInteractivity();
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
            diceResultText.text = $"{currentRollingPlayer}: {LastDiceValue}";
            if (statusText != null)
            {
                statusText.text = $"{currentRollingPlayer} xúc ra số {LastDiceValue}";
            }
            isDiceRolling = false;
            hasRolledThisTurn = true;

            if (!GameTurnManager.Instance.isDeterminingOrder)
            {
                GameTurnManager.Instance.CheckForPossibleMoves();
            }
        }
    }

    public void UpdateDiceStatus(bool isHeld)
    {
        //if (isHeld)
        //{
        //    statusText.text += "\nXúc xắc đang được cầm";
        //}
        //else
        //{
        //    statusText.text += "\nXúc xắc đã đặt xuống";
        //}
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

        diceResultText.text = $"{currentRollingPlayer}: {LastDiceValue}";
        isDiceRolling = false;

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

    if (!IsLocalPlayersTurn())
    {
        Debug.LogWarning("Cannot roll dice: not your turn");
        UpdateDiceButtonInteractivity();
        return;
    }

    if (hasRolledThisTurn)
    {
        Debug.LogWarning("Cannot roll dice: already rolled this turn");
        UpdateDiceButtonInteractivity();
        return;
    }

    GameTurnManager turnManager = GameTurnManager.Instance;
    if (turnManager != null && !turnManager.IsCurrentPlayer(currentRollingPlayer))
    {
        Debug.LogWarning("Cannot roll dice: turn data out of sync");
        UpdateDiceButtonInteractivity();
        return;
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
    UpdateDiceButtonInteractivity();

    if (PhotonNetwork.IsMasterClient)
    {
        int diceValue = useCustomDiceValues && customDiceSequence.Count > 0
            ? customDiceSequence[diceSequenceIndex++ % customDiceSequence.Count]
            : Random.Range(1, 7);

        gameStateManager.SetDiceResult(diceValue, currentRollingPlayer);
    }
}

// THÊM phương thức để nhận kết quả xúc xắc từ GameStateManager
private void OnDiceResultChanged(int value, PlayerColor playerColor)
{
    if (playerColor != currentRollingPlayer)
    {
        return;
    }

    LastDiceValue = value;
    if (diceResultText != null)
    {
        diceResultText.text = $"{playerColor}: {value}";
    }
    isDiceRolling = false;
    hasRolledThisTurn = true;

    if (statusText != null)
    {
        statusText.text = $"{playerColor} xuc ra so {value}";
    }

    if (GameTurnManager.Instance != null && !GameTurnManager.Instance.isDeterminingOrder)
    {
        GameTurnManager.Instance.CheckForPossibleMoves();
    }

    UpdateDiceButtonInteractivity();
}
private void HandleLocalPlayerColorAssigned(PlayerColor color)
{
    localPlayerColor = color;
    UpdateDiceButtonInteractivity();
}

private void HandleTurnChanged(int turnIndex, PlayerColor playerColor)
{
    currentRollingPlayer = playerColor;
    hasRolledThisTurn = false;
    isDiceRolling = false;

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

    UpdateDiceButtonInteractivity();
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

private void UpdateDiceButtonInteractivity()
{
    if (diceButton == null)
    {
        return;
    }

    bool canInteract = !isDiceRolling && !hasRolledThisTurn && IsLocalPlayersTurn();
    diceButton.interactable = canInteract;
}



    //public void AutoRollForCurrentPlayer()
    //{
    //    currentRollingPlayer = GetCurrentPlayer();
    //    Invoke("PerformAutoRoll", autoRollDelay);
    //}

    //private void PerformAutoRoll()
    //{
    //    RollDice();
    //}

    public void RollDiceForPlayer(PlayerColor playerColor)
    {
        currentRollingPlayer = playerColor;
        RollDice();
    }

    public void ResetDiceValue()
    {
        LastDiceValue = 0;
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

        PlayerColor currentPlayer = GetCurrentPlayer();
        if (playerDicePositions.ContainsKey(currentPlayer))
        {
            StartCoroutine(MoveDiceToPosition(playerDicePositions[currentPlayer]));
        }
    }

    //private IEnumerator MoveDiceToPosition(Vector3 targetPosition)
    //{
    //    isMovingToPlayer = true; // <-- BẬT CỜ: đang di chuyển xúc xắc

    //    if (diceFaceDetector == null)
    //    {
    //        isMovingToPlayer = false;
    //        yield break;
    //    }

    //    Rigidbody rb = diceFaceDetector.GetComponent<Rigidbody>();
    //    if (rb != null)
    //    {
    //        rb.isKinematic = true;
    //        rb.linearVelocity = Vector3.zero;
    //        rb.angularVelocity = Vector3.zero;
    //    }

    //    Vector3 startPosition = diceFaceDetector.transform.position;
    //    Quaternion startRotation = diceFaceDetector.transform.rotation;
    //    float elapsed = 0f;

    //    while (elapsed < diceMoveDuration)
    //    {
    //        elapsed += Time.deltaTime;
    //        float t = diceMoveCurve.Evaluate(elapsed / diceMoveDuration);

    //        diceFaceDetector.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
    //        diceFaceDetector.transform.rotation = Quaternion.Slerp(startRotation, Quaternion.identity, t);

    //        yield return null;
    //    }

    //    diceFaceDetector.transform.position = targetPosition;
    //    diceFaceDetector.transform.rotation = Quaternion.identity;

    //    if (rb != null)
    //    {
    //        rb.isKinematic = false;
    //    }

    //    // Reset trạng thái xúc xắc
    //    diceFaceDetector.isFirstPickup = true;
    //    diceFaceDetector.hasLanded = false;

    //    isMovingToPlayer = false; // <-- TẮT CỜ: đã di chuyển xong


    //    NetworkDiceSync diceSync = diceFaceDetector.GetComponent<NetworkDiceSync>();
    //    if (diceSync != null)
    //    {
    //        diceSync.ForceNetworkSync();
    //    }
    //}

    // Sửa phương thức EnableDiceForCurrentPlayer
    // Trong DiceController.cs

    // Trong DiceController.cs, sửa phương thức EnableDiceForCurrentPlayer:

    // Trong DiceController.cs, sửa phương thức EnableDiceForCurrentPlayer:

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
        UpdateDiceButtonInteractivity();
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
            stream.SendNext(LastDiceValue);
            stream.SendNext(isDiceRolling);
            stream.SendNext(currentRollingPlayer);
            stream.SendNext(hasRolledThisTurn);
        }
        else
        {
            // Nhận dữ liệu từ master client
            networkDiceValue = (int)stream.ReceiveNext();
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
                diceResultText.text = $"{networkCurrentPlayer}: {LastDiceValue}";
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

        UpdateDiceButtonInteractivity();
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
        diceResultText.text = $"{currentRollingPlayer}: {LastDiceValue}";
        UpdateDiceButtonInteractivity();

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
        LastDiceValue = 0;
        UpdateDiceButtonInteractivity();
    }

    // RPC để hoàn thành xúc xắc
    [PunRPC]
    public void NetworkFinalizeRoll()
    {
        if (isDiceRolling && diceFaceDetector != null && diceFaceDetector.IsDiceStopped())
        {
            LastDiceValue = diceFaceDetector.GetCurrentFaceValue();
            diceResultText.text = $"{currentRollingPlayer}: {LastDiceValue}";
            if (statusText != null)
            {
                statusText.text = $"{currentRollingPlayer} xúc ra số {LastDiceValue}";
            }
            isDiceRolling = false;
            hasRolledThisTurn = true;

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

        if (GameTurnManager.Instance != null && !GameTurnManager.Instance.IsCurrentPlayer(reportingColor))
        {
            Debug.LogWarning($"Ignoring dice result {reportedValue} from {reportingColor} because it is not their turn.");
            return;
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

    // Sửa phương thức MoveDiceToCurrentPlayer để đồng bộ tốt hơn
    private IEnumerator MoveDiceToPosition(Vector3 targetPosition)
{
    isMovingToPlayer = true;

    if (diceFaceDetector == null)
    {
        isMovingToPlayer = false;
        yield break;
    }

    NetworkDiceSync diceSync = diceFaceDetector.GetComponent<NetworkDiceSync>();
    if (diceSync != null)
    {
        diceSync.RequestOwnership();
        diceSync.SetKinematic(true, true); // Tạm thời kinematic để di chuyển
    }

    Rigidbody rb = diceFaceDetector.GetComponent<Rigidbody>();
    if (rb != null)
    {
        rb.isKinematic = true;
        rb.useGravity = false; // Tắt gravity khi di chuyển
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    Vector3 startPosition = diceFaceDetector.transform.position;
    Quaternion startRotation = diceFaceDetector.transform.rotation;
    float elapsed = 0f;

    while (elapsed < diceMoveDuration)
    {
        elapsed += Time.deltaTime;
        float t = diceMoveCurve.Evaluate(elapsed / diceMoveDuration);

        diceFaceDetector.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
        diceFaceDetector.transform.rotation = Quaternion.Slerp(startRotation, Quaternion.identity, t);

        yield return null;
    }

    diceFaceDetector.transform.position = targetPosition;
    diceFaceDetector.transform.rotation = Quaternion.identity;

    // QUAN TRỌNG: KHÔI PHỤC VẬT LÝ SAU KHI DI CHUYỂN
    if (diceSync != null)
    {
        diceSync.SetKinematic(false, true); // Khôi phục vật lý
        diceSync.EnsurePhysicsActivation();
    }

    if (rb != null)
    {
        rb.isKinematic = false;
        rb.useGravity = true; // Bật gravity lại
        rb.WakeUp();
    }

    // Reset trạng thái
    diceFaceDetector.isFirstPickup = true;
    diceFaceDetector.hasLanded = false;
    isMovingToPlayer = false;
    
    Debug.Log("Di chuyển xúc xắc hoàn tất - Vật lý đã được kích hoạt");
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
            LastDiceValue,
            isDiceRolling,
            currentRollingPlayer,
            hasRolledThisTurn
        );
    }

    [PunRPC]
    private void ReceiveDiceState(int value, bool rolling, PlayerColor currentPlayer, bool hasRolled)
    {
        LastDiceValue = value;
        isDiceRolling = rolling;
        currentRollingPlayer = currentPlayer;
        hasRolledThisTurn = hasRolled;

        if (diceResultText != null)
        {
            diceResultText.text = isDiceRolling
                ? "Dang xuc xac..."
                : $"{currentRollingPlayer}: {LastDiceValue}";
        }

        if (statusText != null)
        {
            statusText.text = isDiceRolling
                ? $"{currentRollingPlayer} dang xuc xac..."
                : (hasRolledThisTurn
                    ? $"{currentRollingPlayer} da xuc xac"
                    : $"Luot cua {currentRollingPlayer}\nChua xuc xac");
        }

        UpdateDiceButtonInteractivity();
    }
    // THÊM vào OnDestroy() để hủy đăng ký event
    private void OnDestroy()
    {
        if (gameStateManager != null)
        {
            gameStateManager.OnDiceResultChanged -= OnDiceResultChanged;
            gameStateManager.OnTurnChanged -= HandleTurnChanged;
        }

        if (PhotonManager.Instance != null)
        {
            PhotonManager.Instance.OnLocalPlayerColorAssigned -= HandleLocalPlayerColorAssigned;
        }
    }
}
