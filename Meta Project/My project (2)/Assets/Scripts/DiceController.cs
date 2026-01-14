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

    // Th�m dictionary d? luu v? tr� x�c x?c cho m?i m�u
    private Dictionary<PlayerColor, Vector3> playerDicePositions = new Dictionary<PlayerColor, Vector3>();




    // Trong DiceController.cs
    [Header("Dice Positions for Each Player")]
    public Transform redDicePosition;
    public Transform blueDicePosition;
    public Transform yellowDicePosition;
    public Transform greenDicePosition;

    public bool canRollAgain = true; // Th�m bi?n n�y

    public bool hasRolledThisTurn = false; // Th�m bi?n n�y d? theo d�i d� x�c x?c trong lu?t n�y chua


    public TextMeshProUGUI statusText; // Tham chi?u d?n UI Text d? hi?n th? tr?ng th�i


    [Header("Dice Movement Settings")]
    public float diceMoveDuration = 1.0f;
    public AnimationCurve diceMoveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float diceMoveSmoothingTime = 0.18f;
    public float diceMoveArcHeight = 0.15f;
    public bool isMovingToPlayer = false; // <-- TH�M D�NG N�Y

    // PUN Network Variables
    private int? networkDiceValue = null;
    private bool networkIsRolling = false;
    private PlayerColor networkCurrentPlayer;
    private bool isNetworked = false;


    private DiceFaceDetector diceDetector;
    private NetworkDiceSync networkDiceSync;
    private PlayerColor localPlayerColor = PlayerColor.None;
    // Th�m v�o d?u class
    private GameStateManager gameStateManager;
    private Coroutine diceMoveRoutine;
    private Coroutine ownershipReturnRoutine;

    private static int? NormalizeDiceValue(int? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        int sanitized = value.Value;
        return sanitized >= 1 ? sanitized : (int?)null;
    }

    private static string FormatDiceValue(int? value)
    {
        return value.HasValue ? value.Value.ToString() : "-";
    }

    private static string FormatDiceWithModifiers(PlayerColor playerColor, int? value)
    {
        if (playerColor == PlayerColor.None)
        {
            return FormatDiceValue(value);
        }

        if (!value.HasValue)
        {
            return $"{playerColor}: -";
        }

        if (TryBuildRollEquation(playerColor, value.Value, out string equation))
        {
            return $"{playerColor}: {equation}";
        }

        return $"{playerColor}: {value.Value}";
    }

    private static string FormatStatusWithModifiers(PlayerColor playerColor, int? value)
    {
        if (playerColor == PlayerColor.None)
        {
            return "Waiting for your turn...";
        }

        return value.HasValue
            ? $"{playerColor} roll: {GetRollEquationOrValue(playerColor, value.Value)}"
            : $"Turn of {playerColor}\nNo dice yet";
    }

    private static bool TryBuildRollEquation(PlayerColor playerColor, int finalValue, out string equation)
    {
        equation = null;
        QuestionTurnEffectManager manager = QuestionTurnEffectManager.Instance;
        if (manager != null && manager.TryGetRollBreakdown(playerColor, out QuestionRollBreakdown breakdown))
        {
            string modifierSegment = breakdown.Modifier >= 0
                ? $"+ {breakdown.Modifier}"
                : $"- {Mathf.Abs(breakdown.Modifier)}";
            equation = $"{breakdown.BaseValue} {modifierSegment} = {finalValue}";
            return true;
        }

        return false;
    }

    private static string GetRollEquationOrValue(PlayerColor playerColor, int finalValue)
    {
        if (TryBuildRollEquation(playerColor, finalValue, out string equation))
        {
            return equation;
        }

        return finalValue.ToString();
    }

    private void LogLocalRoll(PlayerColor playerColor, int baseValue, int modifier, int? finalValue, bool forcedSkip)
    {
        string modifierSegment = modifier >= 0 ? $"+ {modifier}" : $"- {Mathf.Abs(modifier)}";
        string breakdown = $"{baseValue} {modifierSegment}";
        string finalSegment = forcedSkip ? "SKIP" : (finalValue.HasValue ? finalValue.Value.ToString() : "-");
        Debug.Log($"[Dice][Offline] {playerColor} rolled {breakdown} => {finalSegment}");
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

        // Thi?t l?p v? tr� x�c x?c cho m?i m�u
        if (redDicePosition != null)
            playerDicePositions[PlayerColor.Red] = redDicePosition.position;
        if (blueDicePosition != null)
            playerDicePositions[PlayerColor.Blue] = blueDicePosition.position;
        if (yellowDicePosition != null)
            playerDicePositions[PlayerColor.Yellow] = yellowDicePosition.position;
        if (greenDicePosition != null)
            playerDicePositions[PlayerColor.Green] = greenDicePosition.position;

        // Kh?i t?o PUN
        if (photonView != null)
        {
            isNetworked = true;
            networkDiceValue = LastDiceValue;
            networkIsRolling = isDiceRolling;
        }
        // Th�m v�o Awake()
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
            // �?m b?o s? h?u tru?c khi d?t l?i v? tr� d? m?i client c� th? c?p nh?t
            NetworkDiceSync diceSync = diceFaceDetector.GetComponent<NetworkDiceSync>();
            if (diceSync != null)
            {
                diceSync.RequestOwnership();
            }
            diceFaceDetector.transform.position = playerDicePositions[color];
            diceFaceDetector.transform.rotation = Quaternion.identity;

            // Reset c�c tr?ng th�i
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
        // N?u c� PUN v� l� master client, g?i RPC
        if (isNetworked && photonView.IsMine && PhotonNetwork.InRoom)
        {
            photonView.RPC("NetworkPrepareToRoll", RpcTarget.All);
        }
        else
        {
            // Ch?y local n?u kh�ng c� PUN
            PrepareToRollLocal();
        }
    }

    private void PrepareToRollLocal()
    {
        isDiceRolling = true;
        if (diceResultText != null)
        {
            diceResultText.text = "Rolling dice...";
        }
        if (statusText != null)
        {
            statusText.text = $"{currentRollingPlayer} rolling dice...";
        }
        LastDiceValue = null;
    }

    public void FinalizeRoll()
    {
        if (diceFaceDetector == null || !diceFaceDetector.IsDiceStopped())
        {
            return;
        }

        int faceValue = diceFaceDetector.GetCurrentFaceValue();

        if (isNetworked && PhotonNetwork.InRoom)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                SubmitRollResult(faceValue, currentRollingPlayer);
            }
            return;
        }

        ApplyLocalRollResult(faceValue, currentRollingPlayer);
    }

    private void ApplyLocalRollResult(int? value, PlayerColor playerColor)
    {
        int? normalizedValue = NormalizeDiceValue(value);
        int baseRollValue = normalizedValue ?? 0;
        QuestionDiceAdjustment adjustment = default;
        bool appliedQuestionModifier = false;

        if (QuestionTurnEffectManager.Instance != null && normalizedValue.HasValue && playerColor != PlayerColor.None)
        {
            adjustment = QuestionTurnEffectManager.Instance.ConsumeModifier(playerColor, normalizedValue.Value);
            appliedQuestionModifier = true;

            if (adjustment.ForcedSkip)
            {
                normalizedValue = null;
            }
            else
            {
                normalizedValue = adjustment.FinalValue;
            }
        }

        LastDiceValue = normalizedValue;

        if (diceResultText != null)
        {
            diceResultText.text = FormatDiceWithModifiers(playerColor, LastDiceValue);
        }

        isDiceRolling = false;
        hasRolledThisTurn = LastDiceValue.HasValue;

        if (statusText != null)
        {
            statusText.text = FormatStatusWithModifiers(playerColor, LastDiceValue);
        }

        LogLocalRoll(playerColor, baseRollValue, appliedQuestionModifier ? adjustment.AppliedModifier : 0, LastDiceValue, appliedQuestionModifier && adjustment.ForcedSkip);

        if (appliedQuestionModifier && adjustment.ForcedSkip)
        {
            if (GameTurnManager.Instance != null)
            {
                GameTurnManager.Instance.ForceSkipTurnDueToQuestionPenalty(playerColor);
            }
            return;
        }

        if (GameTurnManager.Instance != null && !GameTurnManager.Instance.isDeterminingOrder)
        {
            GameTurnManager.Instance.CheckForPossibleMoves();
        }
    }

    private void SubmitRollResult(int faceValue, PlayerColor playerColor)
    {
        if (gameStateManager == null)
        {
            gameStateManager = GameStateManager.Instance;
        }

        if (gameStateManager != null)
        {
            gameStateManager.SetDiceResult(faceValue, playerColor);
        }
        else
        {
            ApplyLocalRollResult(faceValue, playerColor);
        }
    }

    public void AutoRollForCurrentPlayer()
    {
        // CH? Master Client m?i du?c auto roll
        if (!PhotonNetwork.IsMasterClient) 
        {
            Debug.Log("Ch? Master Client m?i du?c auto roll");
            return;
        }
    
        currentRollingPlayer = GetCurrentPlayer();
        Invoke("PerformAutoRoll", autoRollDelay);
    }

    private void PerformAutoRoll()
    {
        PrepareToRoll();
        // Trong ch? d? auto, gi? l?p vi?c x�c x?c d?ng sau 2 gi�y
        Invoke("SimulateDiceStop", 2f);
    }

    private void SimulateDiceStop()
    {
        int simulatedValue;

        if (useCustomDiceValues && customDiceSequence.Count > 0)
        {
            simulatedValue = customDiceSequence[diceSequenceIndex % customDiceSequence.Count];
            diceSequenceIndex++;
        }
        else
        {
            simulatedValue = Random.Range(1, 7);
        }

        SubmitRollResult(simulatedValue, currentRollingPlayer);
    }


    

    // THAY TH? phuong th?c RollDice()
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

    Debug.Log($"?? Master received roll request from {requestingPlayer}");

    // Ki?m tra xem c� ph?i lu?t c?a player n�y kh�ng
    if (GameTurnManager.Instance.IsCurrentPlayer(requestingPlayer))
    {
        currentRollingPlayer = requestingPlayer;
        StartDiceRollProcess();
    }
    else
    {
        Debug.LogWarning($"? Roll request denied: Not {requestingPlayer}'s turn");
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

    if (PhotonNetwork.IsMasterClient && photonView != null && !photonView.IsMine)
    {
        photonView.RequestOwnership();
    }
}

// TH�M phuong th?c d? nh?n k?t qu? x�c x?c t? GameStateManager
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
        diceResultText.text = FormatDiceWithModifiers(playerColor, LastDiceValue);
    }
    isDiceRolling = false;
    hasRolledThisTurn = value.HasValue;

    if (statusText != null)
    {
        statusText.text = FormatStatusWithModifiers(playerColor, LastDiceValue);
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
    CancelPendingOwnershipReturn();

    currentRollingPlayer = playerColor;
    hasRolledThisTurn = false;
    isDiceRolling = false;
    ResetDiceValue();

    if (statusText != null)
    {
        if (playerColor == PlayerColor.None)
        {
            statusText.text = "Waiting for your turn...";
        }
        else
        {
            statusText.text = $"Turn of {playerColor}\nNo dice yet";
        }
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

        // Trong ch? d? Offline, cho ph�p di?u khi?n m?i lu?t nhu pass-and-play
        if (PhotonNetwork.OfflineMode)
        {
            return true;
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

    public int SimulateDiceForInitialization(PlayerColor playerColor)
    {
        int value;

        if (useCustomDiceValues && customDiceSequence.Count > 0)
        {
            value = customDiceSequence[diceSequenceIndex++ % customDiceSequence.Count];
        }
        else
        {
            value = Random.Range(1, 7);
        }

        Debug.Log($"Simulated initialization roll for {playerColor}: {value}");
        return value;
    }

    public void ResetDiceValue()
    {
        LastDiceValue = null;
        hasRolledThisTurn = false;
        if (diceResultText != null)
        {
            diceResultText.text = currentRollingPlayer == PlayerColor.None
                ? "-"
                : FormatDiceWithModifiers(currentRollingPlayer, LastDiceValue);
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



    // Th�m phuong th?c di chuy?n x�c x?c d?n v? tr� ngu?i choi hi?n t?i
    public void MoveDiceToCurrentPlayer(PlayerColor targetPlayer = PlayerColor.None)
    {
        if (diceFaceDetector == null) return;
        if (isMovingToPlayer) return;

        if (isNetworked && PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
        {
            Debug.Log("MoveDiceToCurrentPlayer: Only master client may drive dice movement.");
            return;
        }

        if (PhotonNetwork.IsMasterClient && photonView != null && !photonView.IsMine)
        {
            photonView.RequestOwnership();
        }

        PlayerColor resolvedPlayer = targetPlayer != PlayerColor.None
            ? targetPlayer
            : GetCurrentPlayer();

        if (resolvedPlayer == PlayerColor.None)
        {
            Debug.LogWarning("MoveDiceToCurrentPlayer: target player could not be resolved, skipping dice move.");
            return;
        }

        if (!playerDicePositions.TryGetValue(resolvedPlayer, out Vector3 targetPosition))
        {
            Debug.LogWarning($"MoveDiceToCurrentPlayer: No configured dice position for {resolvedPlayer}, skipping dice move.");
            return;
        }

        bool broadcast = photonView != null && photonView.IsMine && PhotonNetwork.IsMasterClient;
        StartDiceMove(targetPosition, Quaternion.identity, broadcast);
    }

    public void MoveDiceToCurrentPlayer()
    {
        MoveDiceToCurrentPlayer(PlayerColor.None);
    }


    public void EnableDiceForCurrentPlayer()
    {
        // CH? Master client m?i du?c di?u khi?n dice
        if (!PhotonNetwork.IsMasterClient) 
        {
            Debug.Log("Ch? Master client m?i du?c di?u khi?n dice");
            return;
        }
    
        // KH�NG cho ph�p k�ch ho?t n?u dang di chuy?n
        if (isMovingToPlayer) 
        {
            Debug.Log("Dice dang di chuy?n, kh�ng th? k�ch ho?t ngay l�c n�y");
            return;
        }
    
        currentRollingPlayer = GetCurrentPlayer();
    
        // Ki?m tra player c� trong game v� online
        if (GameTurnManager.Instance != null && 
            (!GameTurnManager.Instance.IsColorInGame(currentRollingPlayer) ||
             !IsPlayerWithColorOnline(currentRollingPlayer)))
        {
            Debug.Log($"Player color {currentRollingPlayer} is not in game or offline, skipping turn");
            GameTurnManager.Instance.EndTurn();
            return;
        }
    
        hasRolledThisTurn = false;

        // Di chuy?n x�c x?c d?n v? tr� ngu?i choi hi?n t?i
        MoveDiceToCurrentPlayer(currentRollingPlayer);

        // C?p nh?t th�ng b�o cho t?t c? client
        photonView.RPC("RPC_UpdateDiceStatus", RpcTarget.All, $"Luot cua {currentRollingPlayer}\nChua xuc xac");

        if (diceResultText != null)
        {
            diceResultText.text = hasRolledThisTurn
                ? "You have dice this turn"
                : "Feel free to add the dice to the spring rolls.";
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

// TH�M: Ki?m tra xem player v?i m�u c? th? c� dang online kh�ng
    private bool IsPlayerWithColorOnline(PlayerColor color)
    {
        if (PhotonManager.Instance == null) return true; // Fallback cho offline
    
        List<PlayerColor> onlineColors = PhotonManager.Instance.GetRoomPlayerColors();
        return onlineColors.Contains(color);
    }

// Th�m phuong th?c ki?m tra tr?ng th�i
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
            // G?i d? li?u d?n c�c client kh�c
            stream.SendNext(ToNetworkValue(LastDiceValue));
            stream.SendNext(isDiceRolling);
            stream.SendNext(currentRollingPlayer);
            stream.SendNext(hasRolledThisTurn);
        }
        else
        {
            // Nh?n d? li?u t? master client
            networkDiceValue = FromNetworkValue((int)stream.ReceiveNext());
            networkIsRolling = (bool)stream.ReceiveNext();
            networkCurrentPlayer = (PlayerColor)stream.ReceiveNext();
            bool networkHasRolled = (bool)stream.ReceiveNext();

            // C?p nh?t n?u kh�ng ph?i l� master client
            if (!photonView.IsMine)
            {
                UpdateFromNetwork(networkHasRolled);
            }
        }
    }

    private void UpdateFromNetwork(bool networkHasRolled)
    {
        // C?p nh?t gi� tr? x�c x?c
        if (LastDiceValue != networkDiceValue)
        {
            LastDiceValue = networkDiceValue;
            if (diceResultText != null)
            {
                diceResultText.text = FormatDiceWithModifiers(networkCurrentPlayer, LastDiceValue);
            }
        }

        // C?p nh?t tr?ng th�i rolling
        if (isDiceRolling != networkIsRolling)
        {
            isDiceRolling = networkIsRolling;
            if (diceResultText != null && isDiceRolling)
            {
                diceResultText.text = "Rolling the dice...";
            }
        }

        // C?p nh?t ngu?i choi hi?n t?i
        if (currentRollingPlayer != networkCurrentPlayer)
        {
            currentRollingPlayer = networkCurrentPlayer;
        }

        // C?p nh?t tr?ng th�i d� x�c x?c
        if (hasRolledThisTurn != networkHasRolled)
        {
            hasRolledThisTurn = networkHasRolled;
        }

        // �?ng b? UI co b?n cho client m?i/kh�ng ph?i ch?
        if (statusText != null)
        {
            statusText.text = isDiceRolling
                ? $"{currentRollingPlayer} rolling dice..."
                : (hasRolledThisTurn
                    ? FormatStatusWithModifiers(currentRollingPlayer, LastDiceValue)
                    : $"Turn of {currentRollingPlayer}\nNo dice yet");
        }
        
    }

    // RPC d? x�c x?c
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
        int rollValue;

        if (useCustomDiceValues && customDiceSequence.Count > 0)
        {
            rollValue = customDiceSequence[diceSequenceIndex % customDiceSequence.Count];
            diceSequenceIndex++;
        }
        else
        {
            rollValue = Random.Range(1, 7);
        }

        ApplyLocalRollResult(rollValue, currentRollingPlayer);
    }

    // RPC d? chu?n b? x�c x?c
    [PunRPC]
    public void NetworkPrepareToRoll()
    {
        isDiceRolling = true;
        if (diceResultText != null)
        {
            diceResultText.text = "Rolling the dice...";
        }
        if (statusText != null)
        {
            statusText.text = $"{currentRollingPlayer} rolling the dice...";
        }
        LastDiceValue = null;
    }

    // RPC d? ho�n th�nh x�c x?c

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
                    $"DiceController: Ignoring dice result {reportedValue} from {reportingColor} � color not present in current turn order.");
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
            SubmitRollResult(reportedValue, reportingColor);
        }
        else
        {
            OnDiceResultChanged(reportedValue, reportingColor);
        }
    }

    // Trong DiceController.cs, th�m c�c phuong th?c sau:

    // Khi d�ng PhotonTransformViewClassic, kh�ng c?n sync th? c�ng
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

        NetworkDiceSync diceSync = diceFaceDetector.GetComponent<NetworkDiceSync>();
        bool isPhotonContext = photonView != null && PhotonNetwork.InRoom;
        bool intendsToControl =
            !isPhotonContext ||
            PhotonNetwork.IsMasterClient ||
            photonView.IsMine;

        if (diceSync != null && intendsToControl)
        {
            diceSync.RequestOwnership();
        }

        if (intendsToControl && photonView != null && !photonView.IsMine)
        {
            float ownershipWait = 0f;
            while (!photonView.IsMine && ownershipWait < 0.5f)
            {
                ownershipWait += Time.deltaTime;
                yield return null;
            }
        }

        float movementDuration = Mathf.Max(0.01f, diceMoveDuration);

        if (!intendsToControl)
        {
            yield return new WaitForSeconds(movementDuration);

            diceFaceDetector.isFirstPickup = true;
            diceFaceDetector.hasLanded = false;
            diceFaceDetector.ResetRollTrackingState();
            isMovingToPlayer = false;
            diceMoveRoutine = null;
            yield break;
        }

        bool hasOwnership = photonView == null || photonView.IsMine;

        if (hasOwnership && diceSync != null)
        {
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
        float smoothingTime = Mathf.Max(0f, diceMoveSmoothingTime);
        Vector3 smoothVelocity = Vector3.zero;

        while (elapsed < movementDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / movementDuration);
            float t = diceMoveCurve.Evaluate(normalizedTime);

            Vector3 lerpPosition = Vector3.Lerp(startPosition, targetPosition, t);
            if (diceMoveArcHeight > 0f)
            {
                float arcOffset = Mathf.Sin(Mathf.PI * normalizedTime) * diceMoveArcHeight;
                lerpPosition += Vector3.up * arcOffset;
            }
            if (smoothingTime > 0f)
            {
                diceFaceDetector.transform.position = Vector3.SmoothDamp(
                    diceFaceDetector.transform.position,
                    lerpPosition,
                    ref smoothVelocity,
                    smoothingTime,
                    float.PositiveInfinity,
                    Time.deltaTime);
            }
            else
            {
                diceFaceDetector.transform.position = lerpPosition;
            }

            Quaternion lerpRotation = Quaternion.Slerp(startRotation, targetRotation, t);
            if (smoothingTime > 0f)
            {
                float rotationBlend = Mathf.Clamp01(Time.deltaTime / Mathf.Max(0.0001f, smoothingTime));
                diceFaceDetector.transform.rotation = Quaternion.Slerp(
                    diceFaceDetector.transform.rotation,
                    lerpRotation,
                    rotationBlend);
            }
            else
            {
                diceFaceDetector.transform.rotation = lerpRotation;
            }

            yield return null;
        }

        diceFaceDetector.transform.SetPositionAndRotation(targetPosition, targetRotation);

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.WakeUp();
        }

        if (hasOwnership && diceSync != null)
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

        Debug.Log("Di chuy?n x�c x?c ho�n t?t - V?t l� d� du?c k�ch ho?t");
    }

    private void CancelPendingOwnershipReturn()
    {
        if (ownershipReturnRoutine != null)
        {
            StopCoroutine(ownershipReturnRoutine);
            ownershipReturnRoutine = null;
        }
    }

    public void RequestReturnOwnershipToMaster(float delaySeconds = 0f)
    {
        if (photonView == null || !PhotonNetwork.InRoom || PhotonNetwork.MasterClient == null)
        {
            return;
        }

        CancelPendingOwnershipReturn();

        if (delaySeconds <= 0f)
        {
            ReturnOwnershipToMaster();
        }
        else
        {
            ownershipReturnRoutine = StartCoroutine(ReturnOwnershipDelayed(delaySeconds));
        }
    }

    private IEnumerator ReturnOwnershipDelayed(float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);
        ReturnOwnershipToMaster();
    }

    public void ReturnOwnershipToMaster()
    {
        CancelPendingOwnershipReturn();

        if (photonView == null || !PhotonNetwork.InRoom || PhotonNetwork.MasterClient == null)
        {
            return;
        }

        int masterActorNumber = PhotonNetwork.MasterClient.ActorNumber;

        if (photonView.OwnerActorNr == masterActorNumber)
        {
            return;
        }

        if (photonView.IsMine || PhotonNetwork.IsMasterClient)
        {
            photonView.TransferOwnership(masterActorNumber);
        }
        else if (photonView.Owner != null)
        {
            photonView.RPC(nameof(RPC_RequestOwnershipReturnToMaster), photonView.Owner, masterActorNumber);
        }
    }

    [PunRPC]
    private void RPC_RequestOwnershipReturnToMaster(int masterActorNumber, PhotonMessageInfo info)
    {
        if (photonView == null)
        {
            return;
        }

        if (!photonView.IsMine)
        {
            return;
        }

        photonView.TransferOwnership(masterActorNumber);
    }

    [PunRPC]
    private void RPC_BeginDiceMove(Vector3 targetPosition, Quaternion targetRotation)
    {
        StartDiceMove(targetPosition, targetRotation, false);
    }

// TH�M: Phuong th?c d?m b?o v?t l� du?c k�ch ho?t khi b?t d?u lu?t m?i
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

    // Late-join synchronization: g?i tr?ng th�i x�c x?c cho ngu?i choi m?i
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
                ? "Rolling the dice..."
                : FormatDiceWithModifiers(currentRollingPlayer, LastDiceValue);
        }

        if (statusText != null)
        {
            statusText.text = isDiceRolling
                ? $"{currentRollingPlayer} rolling the dice..."
                : (hasRolledThisTurn
                    ? FormatStatusWithModifiers(currentRollingPlayer, LastDiceValue)
                    : $"Turn of {currentRollingPlayer}\nNo dice yet");
        }
        
    }
    // TH�M v�o OnDestroy() d? h?y dang k� event
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

