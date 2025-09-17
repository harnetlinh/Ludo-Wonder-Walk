using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PositionOptimizer : MonoBehaviour
{
    [Header("Reset Settings")]
    [Tooltip("Khoảng cách tối đa cho phép trước khi reset vị trí")]
    public float resetDistanceThreshold = 0.5f;

    [Tooltip("Thời gian giữa các lần kiểm tra vị trí (giây)")]
    public float checkInterval = 0.5f;

    [Header("Debug")]
    public bool showDebugGizmos = true;
    public Color validPositionColor = Color.green;
    public Color outOfRangeColor = Color.red;
    public Color resetZoneColor = new Color(0, 1, 0, 0.3f);

    private PieceController pieceController;
    private Rigidbody rb;
    private Coroutine checkPositionCoroutine;
    private bool isBeingHandled = false;


    // Thêm các biến mới
    private bool wasReleased = false;
    private float releaseTime = 0f;
    private const float STABLE_CHECK_DELAY = 0.5f;

    private void Awake()
    {
        pieceController = GetComponent<PieceController>();
        rb = GetComponent<Rigidbody>();

        if (pieceController == null)
        {
            Debug.LogError("PositionOptimizer requires PieceController component!");
            enabled = false;
            return;
        }
    }

    private void OnEnable()
    {
        if (checkPositionCoroutine != null)
        {
            StopCoroutine(checkPositionCoroutine);
        }
        checkPositionCoroutine = StartCoroutine(CheckPositionRoutine());
    }

    private void OnDisable()
    {
        if (checkPositionCoroutine != null)
        {
            StopCoroutine(checkPositionCoroutine);
            checkPositionCoroutine = null;
        }
    }



    // Trong PositionOptimizer.cs, sửa phương thức CheckAndResetPosition
    public void CheckAndResetPosition()
    {
        // Bỏ qua nếu đang di chuyển, ở trong chuồng, đã về đích, hoặc đang được xử lý
        if (pieceController.isMoving || pieceController.currentPathIndex == -1 || pieceController.currentPathIndex == -2 || isBeingHandled)
            return;

        // THÊM: Bỏ qua nếu không phải lượt của người chơi này
        if (GameTurnManager.Instance != null && !GameTurnManager.Instance.IsCurrentPlayer(pieceController.playerColor))
            return;

        // THÊM: Bỏ qua nếu người chơi chưa xúc xắc hoặc đang có thể di chuyển
        if (DiceController.Instance != null && !DiceController.Instance.hasRolledThisTurn)
            return;

        // Kiểm tra xem có đang trong quá trình sắp xếp không
        PieceArranger arranger = GetComponent<PieceArranger>();
        if (arranger != null && arranger.IsBeingArranged())
        {
            return; // Bỏ qua nếu đang sắp xếp
        }

        // THÊM: Kiểm tra xem có đang được grab không
        GrapPiece grabPiece = GetComponent<GrapPiece>();
        if (grabPiece != null)
        {
            // Nếu có GrapPiece component, có thể đang được cầm
            return;
        }

        // Lấy vị trí chính xác từ PathManager
        Transform correctPosition = HorseRacePathManager.Instance.GetCurrentPoint(
            pieceController.currentPathIndex,
            pieceController.playerColor);

        if (correctPosition == null)
        {
            Debug.LogWarning($"Cannot find correct position for {pieceController.playerColor} piece at index {pieceController.currentPathIndex}");
            return;
        }

        // Tính khoảng cách từ vị trí hiện tại đến vị trí đúng
        float distance = Vector3.Distance(transform.position, correctPosition.position);

        // TĂNG ngưỡng reset để tránh can thiệp khi người chơi đang di chuyển quân
        float dynamicThreshold = resetDistanceThreshold;
        
        // Nếu quân cờ vừa được xuất (ở vị trí xuất phát), cho phép khoảng cách lớn hơn
        Transform startPoint = HorseRacePathManager.Instance.GetStartPoint(pieceController.playerColor);
        if (correctPosition == startPoint)
        {
            dynamicThreshold = resetDistanceThreshold * 2f; // Tăng gấp đôi ngưỡng cho điểm xuất phát
        }

        // Nếu vượt quá ngưỡng cho phép, reset vị trí
        if (distance > dynamicThreshold)
        {
            Debug.Log($"Resetting position for {pieceController.playerColor} piece at index {pieceController.currentPathIndex} (distance: {distance})");
            StartCoroutine(ResetToCorrectPosition(correctPosition));
        }
    }

    // Thêm phương thức mới để kiểm tra trạng thái sắp xếp
    public bool IsPositionBeingOptimized()
    {
        return isBeingHandled;
    }

    private IEnumerator ResetToCorrectPosition(Transform targetPosition)
    {
        if (pieceController.isMoving || isBeingHandled) yield break;

        isBeingHandled = true;

        // Tạm thời vô hiệu hóa vật lý để đặt vị trí
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Tăng thời gian di chuyển để chậm hơn
        float duration = 1f; // Tăng từ 0.3f lên   1f
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Quaternion targetRot = Quaternion.identity; // Góc đứng

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Di chuyển mượt mà cả vị trí và góc xoay
            transform.position = Vector3.Lerp(startPos, targetPosition.position, t);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        // Đảm bảo chính xác vị trí và góc xoay cuối cùng
        transform.position = targetPosition.position;
        transform.rotation = targetRot;

        // Bật lại vật lý nếu có
        if (rb != null)
        {
            rb.isKinematic = false;
        }

        isBeingHandled = false;

        // Gọi sắp xếp sau khi reset vị trí
        PieceArranger arranger = GetComponent<PieceArranger>();
        if (arranger != null)
        {
            arranger.OnPositionReset();
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || !Application.isPlaying) return;
        if (pieceController == null || pieceController.currentPathIndex == -1) return;

        // Lấy vị trí chính xác từ PathManager
        Transform correctPosition = HorseRacePathManager.Instance.GetCurrentPoint(
            pieceController.currentPathIndex,
            pieceController.playerColor);

        if (correctPosition == null) return;

        // Vẽ sphere tại vị trí đúng
        Gizmos.color = validPositionColor;
        Gizmos.DrawWireSphere(correctPosition.position, 0.1f);

        // Vẽ sphere tại vị trí hiện tại nếu nó lệch
        float distance = Vector3.Distance(transform.position, correctPosition.position);
        if (distance > 0.01f)
        {
            Gizmos.color = outOfRangeColor;
            Gizmos.DrawWireSphere(transform.position, 0.1f);

            // Vẽ đường nối giữa vị trí hiện tại và vị trí đúng
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, correctPosition.position);
        }

        // Vẽ vòng tròn phạm vi reset
        Gizmos.color = resetZoneColor;
        Gizmos.DrawWireSphere(correctPosition.position, resetDistanceThreshold);
    }

    public void ForcePositionCheck()
    {
        if (!isBeingHandled)
        {
            CheckAndResetPosition();
        }
    }

    // Thêm phương thức public để điều khiển trạng thái từ bên ngoài
    public void SetIsBeingHandled(bool handled)
    {
        isBeingHandled = handled;
    }

    // Thêm phương thức mới
    public void OnPieceReleased()
    {
        wasReleased = true;
        releaseTime = Time.time;
    }

    // Thêm kiểm tra trong Coroutine
    private IEnumerator CheckPositionRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(checkInterval);

            // TĂNG thời gian chờ để tránh can thiệp quá sớm
            float extendedDelay = STABLE_CHECK_DELAY * 2f; // Tăng gấp đôi thời gian chờ

            // Nếu vừa được thả và đã qua thời gian chờ
            if (wasReleased && Time.time - releaseTime > extendedDelay)
            {
                // Kiểm tra nếu quân cờ đã ổn định (không di chuyển)
                if (rb != null && rb.linearVelocity.magnitude < 0.05f) // Giảm ngưỡng velocity
                {
                    wasReleased = false;
                    if (!isBeingHandled)
                    {
                        CheckAndResetPosition();
                    }
                }
            }
            else if (!wasReleased && !isBeingHandled)
            {
                // CHỈ kiểm tra nếu không phải lượt hiện tại hoặc đã di chuyển xong
                if (GameTurnManager.Instance != null && 
                    (!GameTurnManager.Instance.IsCurrentPlayer(pieceController.playerColor) ||
                     DiceController.Instance.hasRolledThisTurn))
                {
                    CheckAndResetPosition();
                }
            }
        }
    }

    // Thêm kiểm tra trong OnCollisionEnter
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Table"))
        {
            // Nếu vừa được thả ra và chạm bàn
            if (wasReleased)
            {
                wasReleased = false;
                if (!isBeingHandled)
                {
                    CheckAndResetPosition();
                }
            }
        }
    }


    // Thêm phương thức kiểm tra ưu tiên sắp xếp
    public void CheckAndArrangeFirst()
    {
        // Kiểm tra sắp xếp trước, sau đó mới reset vị trí
        PieceArranger arranger = GetComponent<PieceArranger>();
        if (arranger != null)
        {
            arranger.ForceArrangeCheck(true); // Sắp xếp ngay lập tức

            // Sau khi sắp xếp, kiểm tra lại vị trí
            StartCoroutine(DelayedPositionCheck(0.1f));
            return;
        }

        // Nếu không có arranger, kiểm tra vị trí bình thường
        CheckAndResetPosition();
    }

    private IEnumerator DelayedPositionCheck(float delay)
    {
        yield return new WaitForSeconds(delay);
        CheckAndResetPosition();
    }

    // Sửa phương thức CheckAndResetPosition để gọi CheckAndArrangeFirst
    

    // Thêm phương thức tìm quân trên cùng ô
    private List<PieceController> FindPiecesOnSameCell()
    {
        List<PieceController> piecesOnSameCell = new List<PieceController>();

        Transform correctPosition = HorseRacePathManager.Instance.GetCurrentPoint(
            pieceController.currentPathIndex,
            pieceController.playerColor);

        if (correctPosition == null)
            return piecesOnSameCell;

        PieceController[] allPieces = FindObjectsOfType<PieceController>();
        foreach (PieceController piece in allPieces)
        {
            if (piece.playerColor == pieceController.playerColor &&
                piece.currentPathIndex == pieceController.currentPathIndex &&
                piece != pieceController)
            {
                float distance = Vector3.Distance(piece.transform.position, correctPosition.position);
                if (distance < 0.5f)
                {
                    piecesOnSameCell.Add(piece);
                }
            }
        }

        piecesOnSameCell.Add(pieceController);
        return piecesOnSameCell;
    }
}