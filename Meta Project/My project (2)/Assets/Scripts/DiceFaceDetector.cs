using UnityEngine;
using System.Collections.Generic;
using Photon.Pun;

public class DiceFaceDetector : MonoBehaviour
{
    public List<Transform> diceFaces;
    [SerializeField] private int currentFaceValue = 0;
    public float checkInterval = 0.1f;
    private float timer = 0f;
    public float velocityThreshold = 0.01f;
    private Rigidbody rb;
    private PhotonView localPhotonView;
    public bool isFirstPickup = true;
    public bool hasLanded = false;
    private bool wasStoppedLastFrame = false;
    private float stoppedTime = 0f;
    public float requiredStoppedDuration = 0.5f; // Thời gian cần dừng ổn định

    public delegate void DiceStoppedEventHandler(int faceValue);
    public event DiceStoppedEventHandler OnDiceStopped;

    // Thêm reference đến GrabDice
    private GrabDice grabDice;

    [Header("Roll Detection Settings")]
    public float minSpeedToConfirmRoll = 0.35f;
    public float quickDropTimeThreshold = 0.2f;
    public float autoArmDelay = 0.6f;

    private bool isTrackingRoll = false;
    private bool hasSentRollRequest = false;
    private float potentialRollStartTime = 0f;
    private float maxObservedSpeed = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        grabDice = GetComponent<GrabDice>(); // Lấy component GrabDice
        localPhotonView = GetComponent<PhotonView>();

        if (rb == null)
        {
            Debug.LogError("Dice requires a Rigidbody component!");
        }

        ResetRollTrackingState();
    }

    // Sửa phương thức Update để kiểm tra thêm điều kiện không được cầm
    void Update()
    {
        // THÊM ĐIỀU KIỆN NÀY: không kiểm tra nếu xúc xắc đang được di chuyển
        if (DiceController.Instance != null && DiceController.Instance.isMovingToPlayer)
        {
            wasStoppedLastFrame = false;
            stoppedTime = 0f;
            return; // Bỏ qua toàn bộ logic detection
        }

        timer += Time.deltaTime;

        if (timer >= checkInterval)
        {
            timer = 0f;

            // Chỉ kiểm tra nếu xúc xắc không được cầm
            if (grabDice != null && grabDice.IsBeingHeld())
            {
                wasStoppedLastFrame = false;
                stoppedTime = 0f;
                return;
            }

            bool isStoppedNow = IsDiceStopped();

            // Nếu xúc xắc đang dừng và không được cầm
            if (isStoppedNow)
            {
                if (!wasStoppedLastFrame)
                {
                    // Bắt đầu đếm thời gian dừng
                    stoppedTime = 0f;
                }
                else
                {
                    // Tăng thời gian dừng
                    stoppedTime += checkInterval;

                    // Kiểm tra nếu đã dừng đủ lâu, chưa xử lý, và không được cầm
                    // BỎ điều kiện !isFirstPickup ở đây để cho phép detection ngay cả lần đầu
                    // SỬA: Trong phương thức Update, khi xúc xắc dừng
                    if (stoppedTime >= requiredStoppedDuration && !hasLanded)
                    {
                        if (!hasSentRollRequest && (DiceController.Instance == null || !DiceController.Instance.isDiceRolling))
                        {
                            ResetRollTrackingState();
                            hasLanded = true;
                        }
                        else
                        {
                            hasLanded = true;
                            CheckTopFace();
    
                            // THÊM: Phân biệt master client và client thường
                            if (PhotonNetwork.IsMasterClient)
                            {
                                // Master client xử lý trực tiếp
                                OnDiceStopped?.Invoke(currentFaceValue);
                                DiceController.Instance.FinalizeRoll();
                            }
                            else
                            {
                                // Client gửi kết quả lên master
                                DiceController diceController = DiceController.Instance;
                                if (diceController != null && diceController.photonView != null && hasSentRollRequest)
                                {
                                    diceController.photonView.RPC(
                                        "RPC_ReportDiceResult", 
                                        RpcTarget.MasterClient, 
                                        currentFaceValue, 
                                        diceController.currentRollingPlayer
                                    );
                                }
                            }

                            ResetRollTrackingState();
                        }
                    }
                }
            }
            else // Xúc xắc đang di chuyển
            {
                stoppedTime = 0f;
                if (hasLanded)
                {
                    // Nếu xúc xắc lại di chuyển sau khi đã dừng
                    hasLanded = false;
                }
            }

            wasStoppedLastFrame = isStoppedNow;
        }

        if (isTrackingRoll)
        {
            TrackPotentialRoll();
        }
    }

    public bool IsDiceStopped()
    {
        // THÊM ĐIỀU KIỆN: không coi là dừng nếu đang di chuyển
        if (DiceController.Instance != null && DiceController.Instance.isMovingToPlayer)
        {
            return false;
        }
    
        // THÊM: Không kiểm tra trạng thái dừng nếu không phải chủ sở hữu
        PhotonView photonView = GetComponent<PhotonView>();
        if (photonView != null && !photonView.IsMine)
        {
            return false; // Remote clients không quyết định trạng thái dừng
        }
    
        if (rb == null) return false;
        return rb.linearVelocity.magnitude < velocityThreshold &&
               rb.angularVelocity.magnitude < velocityThreshold;
    }

    void CheckTopFace()
    {
        if (diceFaces == null || diceFaces.Count != 6)
        {
            //Debug.LogWarning("Dice faces not properly set up!");
            return;
        }

        Transform topFace = null;
        float highestY = -Mathf.Infinity;
        int faceIndex = -1;

        for (int i = 0; i < diceFaces.Count; i++)
        {
            float faceY = diceFaces[i].position.y;
            if (faceY > highestY)
            {
                highestY = faceY;
                topFace = diceFaces[i];
                faceIndex = i;
            }
        }

        if (faceIndex != -1)
        {
            currentFaceValue = faceIndex + 1;
            Debug.Log($"Dice settled on face: {currentFaceValue}");
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Table1"))
        {
            if (isTrackingRoll)
            {
                HandlePotentialRollLanding();
            }
        }

    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Table1"))
        {
            Debug.Log("Dice lifted from table");
            
            // THÊM KIỂM TRA QUAN TRỌNG: Chỉ chuẩn bị roll nếu:
            // 1. Xúc xắc đã từng được cầm 
            // 2. Chưa roll trong lượt này
            // 3. Dice KHÔNG đang di chuyển giữa các người chơi
            DiceController diceController = DiceController.Instance;
            if (!isFirstPickup && 
                diceController != null &&
                localPhotonView != null &&
                localPhotonView.IsMine &&
                !diceController.hasRolledThisTurn &&
                !diceController.isDiceRolling &&
                !diceController.IsDiceMoving()) // <-- THÊM ĐIỀU KIỆN NÀY
            {
                BeginPotentialRoll();
            }
        }
    }

    public void ResetRollTrackingState()
    {
        isTrackingRoll = false;
        hasSentRollRequest = false;
        potentialRollStartTime = 0f;
        maxObservedSpeed = 0f;
    }

    private void BeginPotentialRoll()
    {
        isTrackingRoll = true;
        hasSentRollRequest = false;
        potentialRollStartTime = Time.time;
        maxObservedSpeed = 0f;
        hasLanded = false;
    }

    private void TrackPotentialRoll()
    {
        if (rb == null) return;

        float currentSpeed = rb.linearVelocity.magnitude;
        if (currentSpeed > maxObservedSpeed)
        {
            maxObservedSpeed = currentSpeed;
        }

        bool isCurrentlyHeld = grabDice != null && grabDice.IsBeingHeld();

        if (!hasSentRollRequest && !isCurrentlyHeld)
        {
            bool exceededSpeed = maxObservedSpeed >= minSpeedToConfirmRoll;
            bool fallbackReady = Time.time - potentialRollStartTime >= autoArmDelay;

            if ((exceededSpeed || fallbackReady) && DiceController.Instance != null)
            {
                DiceController diceController = DiceController.Instance;
                if (!diceController.isDiceRolling && !diceController.hasRolledThisTurn)
                {
                    diceController.RollDice();
                    hasSentRollRequest = true;
                }
            }
        }
    }

    private void HandlePotentialRollLanding()
    {
        float airTime = Time.time - potentialRollStartTime;
        bool shouldCancel = !hasSentRollRequest &&
                            airTime <= quickDropTimeThreshold &&
                            maxObservedSpeed < minSpeedToConfirmRoll;

        if (shouldCancel)
        {
            ResetRollTrackingState();
            return;
        }

        if (!hasSentRollRequest && DiceController.Instance != null)
        {
            DiceController diceController = DiceController.Instance;
            if (!diceController.isDiceRolling && !diceController.hasRolledThisTurn)
            {
                diceController.RollDice();
                hasSentRollRequest = true;
            }
        }
    }

    public int GetCurrentFaceValue()
    {
        return currentFaceValue;
    }

    public void ForceCheck()
    {
        if (IsDiceStopped())
        {
            CheckTopFace();
        }
    }
    
    
}
