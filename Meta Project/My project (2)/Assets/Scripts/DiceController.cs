using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

public class DiceController : MonoBehaviourPun, IPunObservable
{
    public static DiceController Instance { get; private set; }

    public Button diceButton;
    public TextMeshProUGUI diceResultText;
    public int LastDiceValue { get; private set; }
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
    }

    public void SetDicePositionForPlayer(PlayerColor color, Vector3 position)
    {
        playerDicePositions[color] = position;
    }

    public void ResetDiceToPlayerPosition(PlayerColor color)
    {
        if (playerDicePositions.ContainsKey(color) && diceFaceDetector != null)
        {
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
        diceButton.interactable = false;
        diceResultText.text = "Đang xúc xắc...";
        if (statusText != null)
        {
            statusText.text = $"{currentRollingPlayer} đang xúc xắc...";
        }
        LastDiceValue = 0;
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


    //public void EnableDiceForCurrentPlayer()
    //{
    //    currentRollingPlayer = GetCurrentPlayer();
    //    diceButton.interactable = true;
    //    diceResultText.text = "Nhấn để ném xúc xắc";
    //}

    //private PlayerColor GetCurrentPlayer()
    //{
    //    if (useCustomPlayerOrder && customPlayerOrder.Count > 0)
    //    {
    //        int currentIndex = GameTurnManager.Instance.currentPlayerIndex % customPlayerOrder.Count;
    //        return customPlayerOrder[currentIndex];
    //    }
    //    return GameTurnManager.Instance.CurrentPlayer;
    //}

    //public void OnDiceClick()
    //{
    //    RollDice();
    //}

    public void RollDice()
    {
        // Nếu có PUN và là master client, gửi RPC
        if (isNetworked && photonView.IsMine && PhotonNetwork.InRoom)
        {
            photonView.RPC("NetworkRollDice", RpcTarget.All);
        }
        else
        {
            // Chạy local nếu không có PUN
            RollDiceLocal();
        }
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

    private IEnumerator MoveDiceToPosition(Vector3 targetPosition)
    {
        isMovingToPlayer = true; // <-- BẬT CỜ: đang di chuyển xúc xắc

        if (diceFaceDetector == null)
        {
            isMovingToPlayer = false;
            yield break;
        }

        Rigidbody rb = diceFaceDetector.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
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

        if (rb != null)
        {
            rb.isKinematic = false;
        }

        // Reset trạng thái xúc xắc
        diceFaceDetector.isFirstPickup = true;
        diceFaceDetector.hasLanded = false;

        isMovingToPlayer = false; // <-- TẮT CỜ: đã di chuyển xong


        NetworkDiceSync diceSync = diceFaceDetector.GetComponent<NetworkDiceSync>();
        if (diceSync != null)
        {
            diceSync.ForceNetworkSync();
        }
    }

    // Sửa phương thức EnableDiceForCurrentPlayer
    public void EnableDiceForCurrentPlayer()
    {
        currentRollingPlayer = GetCurrentPlayer();
        hasRolledThisTurn = false;

        // Di chuyển xúc xắc đến vị trí người chơi hiện tại
        MoveDiceToCurrentPlayer();

        // Cập nhật thông báo
        statusText.text = $"Lượt của {currentRollingPlayer}\nChưa xúc xắc";

        diceButton.interactable = !hasRolledThisTurn;
        diceResultText.text = !hasRolledThisTurn ? "Cầm xúc xắc lên để ném" : "Bạn đã xúc xắc trong lượt này";
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
        diceButton.interactable = false;

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
        diceButton.interactable = false;
        diceResultText.text = "Đang xúc xắc...";
        if (statusText != null)
        {
            statusText.text = $"{currentRollingPlayer} đang xúc xắc...";
        }
        LastDiceValue = 0;
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
}