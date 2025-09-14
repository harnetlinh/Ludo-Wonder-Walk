using UnityEngine;
using System.Collections.Generic;

public class DiceFaceDetector : MonoBehaviour
{
    public List<Transform> diceFaces;
    [SerializeField] private int currentFaceValue = 0;
    public float checkInterval = 0.1f;
    private float timer = 0f;
    public float velocityThreshold = 0.01f;
    private Rigidbody rb;
    public bool isFirstPickup = true;
    public bool hasLanded = false;
    private bool wasStoppedLastFrame = false;
    private float stoppedTime = 0f;
    public float requiredStoppedDuration = 0.5f; // Thời gian cần dừng ổn định

    public delegate void DiceStoppedEventHandler(int faceValue);
    public event DiceStoppedEventHandler OnDiceStopped;

    // Thêm reference đến GrabDice
    private GrabDice grabDice;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        grabDice = GetComponent<GrabDice>(); // Lấy component GrabDice

        if (rb == null)
        {
            Debug.LogError("Dice requires a Rigidbody component!");
        }
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
                    if (stoppedTime >= requiredStoppedDuration && !hasLanded)
                    {
                        hasLanded = true;
                        CheckTopFace();
                        OnDiceStopped?.Invoke(currentFaceValue);
                        DiceController.Instance.FinalizeRoll();
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
    }

    public bool IsDiceStopped()
    {
        // THÊM ĐIỀU KIỆN: không coi là dừng nếu đang di chuyển
        if (DiceController.Instance != null && DiceController.Instance.isMovingToPlayer)
        {
            return false;
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
            // CHỈ đánh dấu là đã chạm bàn nếu xúc xắc KHÔNG được cầm
            // Và đã từng được cầm lên (tức là người chơi đã thực sự ném xúc xắc)
            GrabDice grabDice = GetComponent<GrabDice>();
            if (grabDice != null && !grabDice.IsBeingHeld() && !isFirstPickup)
            {
                DiceController.Instance.UpdateDiceStatus(false);
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Table1"))
        {
            Debug.Log("Dice lifted from table");

            GrabDice grabDice = GetComponent<GrabDice>();
            // CHỈ cập nhật trạng thái "đang cầm" nếu xúc xắc thực sự được cầm
            if (grabDice != null && grabDice.IsBeingHeld())
            {
                DiceController.Instance.UpdateDiceStatus(true);
            }

            // CHỈ chuẩn bị roll nếu xúc xắc đã từng được cầm và chưa roll trong lượt này
            if (!isFirstPickup && !DiceController.Instance.hasRolledThisTurn)
            {
                DiceController.Instance.PrepareToRoll();
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