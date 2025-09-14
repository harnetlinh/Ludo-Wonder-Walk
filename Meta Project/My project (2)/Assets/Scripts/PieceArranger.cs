using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PieceArranger : MonoBehaviour
{
    [Header("Arrangement Settings")]
    [Tooltip("Khoảng cách giữa các quân cờ khi được sắp xếp")]
    public float spacing = 0.15f;

    [Tooltip("Thời gian di chuyển khi sắp xếp (giây)")]
    public float arrangementDuration = 0.5f;

    [Header("Formation Settings")]
    [Tooltip("Kiểu sắp xếp các quân cờ")]
    public FormationType formationType = FormationType.Circle;

    [Tooltip("Bán kính vòng tròn khi sắp xếp kiểu Circle")]
    public float circleRadius = 0.3f;

    [Header("Debug")]
    public bool showDebugGizmos = true;
    public Color arrangementZoneColor = new Color(0, 1, 1, 0.2f);

    public enum FormationType
    {
        Circle,
        Line,
        Grid
    }

    private PieceController pieceController;
    private Rigidbody rb;
    private bool isBeingArranged = false;

    private void Awake()
    {
        pieceController = GetComponent<PieceController>();
        rb = GetComponent<Rigidbody>();

        if (pieceController == null)
        {
            Debug.LogError("PieceArranger requires PieceController component!");
            enabled = false;
            return;
        }
    }

    // Kiểm tra và sắp xếp các quân cờ cùng màu trên cùng một ô
    public void CheckAndArrangePieces()
    {
        if (pieceController.isMoving || pieceController.currentPathIndex == -1 || isBeingArranged)
            return;

        // Tìm tất cả quân cờ cùng màu trên cùng một ô
        List<PieceController> piecesOnSameCell = FindPiecesOnSameCell();

        // Nếu có từ 2 quân cờ trở lên trên cùng một ô, thực hiện sắp xếp
        if (piecesOnSameCell.Count > 1)
        {
            StartCoroutine(ArrangePieces(piecesOnSameCell));
        }
    }

    // Tìm tất cả quân cờ cùng màu trên cùng một ô
    private List<PieceController> FindPiecesOnSameCell()
    {
        List<PieceController> piecesOnSameCell = new List<PieceController>();

        // Lấy vị trí chính xác từ PathManager
        Transform correctPosition = HorseRacePathManager.Instance.GetCurrentPoint(
            pieceController.currentPathIndex,
            pieceController.playerColor);

        if (correctPosition == null)
            return piecesOnSameCell;

        // Tìm tất cả quân cờ cùng màu
        PieceController[] allPieces = FindObjectsByType<PieceController>(FindObjectsSortMode.None);
        foreach (PieceController piece in allPieces)
        {
            if (piece.playerColor == pieceController.playerColor &&
                piece.currentPathIndex == pieceController.currentPathIndex &&
                piece != pieceController)
            {
                // Kiểm tra khoảng cách để xác định có trên cùng ô không
                float distance = Vector3.Distance(piece.transform.position, correctPosition.position);
                if (distance < 0.5f) // Ngưỡng xác định cùng một ô
                {
                    piecesOnSameCell.Add(piece);
                }
            }
        }

        // Thêm quân cờ hiện tại vào danh sách
        piecesOnSameCell.Add(pieceController);

        return piecesOnSameCell;
    }

    // Sắp xếp các quân cờ
    // Thêm property để kiểm tra trạng thái sắp xếp từ bên ngoài
    public bool IsBeingArranged()
    {
        return isBeingArranged;
    }

    // Sửa phương thức ArrangePieces để phối hợp với Optimizer
    private IEnumerator ArrangePieces(List<PieceController> pieces)
    {
        isBeingArranged = true;

        // Lấy vị trí trung tâm (vị trí chính xác của ô)
        Transform centerPosition = HorseRacePathManager.Instance.GetCurrentPoint(
            pieceController.currentPathIndex,
            pieceController.playerColor);

        if (centerPosition == null)
        {
            isBeingArranged = false;
            yield break;
        }

        // Tính toán vị trí mới cho từng quân cờ
        Vector3[] targetPositions = CalculateTargetPositions(pieces.Count, centerPosition.position);

        // Di chuyển các quân cờ đến vị trí mới
        List<Coroutine> moveCoroutines = new List<Coroutine>();
        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i] != null)
            {
                // Vô hiệu hóa tạm thời PositionOptimizer để tránh xung đột
                PositionOptimizer optimizer = pieces[i].GetComponent<PositionOptimizer>();
                if (optimizer != null)
                {
                    optimizer.SetIsBeingHandled(true);
                }

                // Di chuyển quân cờ đến vị trí mới
                Coroutine moveCoroutine = StartCoroutine(
                    MovePieceToPosition(pieces[i], targetPositions[i], arrangementDuration));
                moveCoroutines.Add(moveCoroutine);
            }
        }

        // Chờ cho tất cả quân cờ di chuyển xong
        foreach (Coroutine coroutine in moveCoroutines)
        {
            yield return coroutine;
        }

        // Kích hoạt lại PositionOptimizer sau một khoảng thời gian ngắn
        yield return new WaitForSeconds(0.1f);

        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i] != null)
            {
                PositionOptimizer optimizer = pieces[i].GetComponent<PositionOptimizer>();
                if (optimizer != null)
                {
                    optimizer.SetIsBeingHandled(false);
                }
            }
        }

        isBeingArranged = false;
    }

    // Tính toán vị trí mới dựa trên kiểu sắp xếp
    private Vector3[] CalculateTargetPositions(int pieceCount, Vector3 centerPosition)
    {
        Vector3[] positions = new Vector3[pieceCount];

        switch (formationType)
        {
            case FormationType.Circle:
                for (int i = 0; i < pieceCount; i++)
                {
                    float angle = i * Mathf.PI * 2f / pieceCount;
                    float x = Mathf.Cos(angle) * circleRadius;
                    float z = Mathf.Sin(angle) * circleRadius;
                    positions[i] = centerPosition + new Vector3(x, 0, z);
                }
                break;

            case FormationType.Line:
                for (int i = 0; i < pieceCount; i++)
                {
                    float offset = (i - (pieceCount - 1) / 2f) * spacing;
                    positions[i] = centerPosition + new Vector3(offset, 0, 0);
                }
                break;

            case FormationType.Grid:
                int columns = Mathf.CeilToInt(Mathf.Sqrt(pieceCount));
                int rows = Mathf.CeilToInt((float)pieceCount / columns);

                for (int i = 0; i < pieceCount; i++)
                {
                    int row = i / columns;
                    int col = i % columns;

                    float xOffset = (col - (columns - 1) / 2f) * spacing;
                    float zOffset = (row - (rows - 1) / 2f) * spacing;

                    positions[i] = centerPosition + new Vector3(xOffset, 0, zOffset);
                }
                break;
        }

        return positions;
    }

    // Di chuyển quân cờ đến vị trí mới
    private IEnumerator MovePieceToPosition(PieceController piece, Vector3 targetPosition, float duration)
    {
        if (piece.isMoving) yield break;

        Rigidbody pieceRb = piece.GetComponent<Rigidbody>();
        if (pieceRb != null)
        {
            pieceRb.isKinematic = true;
            pieceRb.linearVelocity = Vector3.zero;
            pieceRb.angularVelocity = Vector3.zero;
        }

        float elapsed = 0f;
        Vector3 startPos = piece.transform.position;
        Quaternion startRot = piece.transform.rotation;
        Quaternion targetRot = Quaternion.identity;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            piece.transform.position = Vector3.Lerp(startPos, targetPosition, t);
            piece.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        piece.transform.position = targetPosition;
        piece.transform.rotation = targetRot;

        if (pieceRb != null)
        {
            pieceRb.isKinematic = false;
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

        // Vẽ vùng sắp xếp
        Gizmos.color = arrangementZoneColor;
        Gizmos.DrawWireSphere(correctPosition.position, 0.5f);
    }

    // Gọi từ bên ngoài để kích hoạt sắp xếp
    public void ForceArrangeCheck()
    {
        if (!isBeingArranged)
        {
            CheckAndArrangePieces();
        }
    }

    // Có thể gọi phương thức này từ PositionOptimizer sau khi reset vị trí
    public void OnPositionReset()
    {
        StartCoroutine(DelayedArrangeCheck(0.1f));
    }

    private IEnumerator DelayedArrangeCheck(float delay)
    {
        yield return new WaitForSeconds(delay);
        ForceArrangeCheck();
    }



    // Thêm phương thức mới để ép sắp xếp ngay lập tức
    public void ArrangeImmediately()
    {
        List<PieceController> piecesOnSameCell = FindPiecesOnSameCell();
        if (piecesOnSameCell.Count > 1)
        {
            StopAllCoroutines();
            StartCoroutine(ArrangePiecesImmediately(piecesOnSameCell));
        }
    }

    private IEnumerator ArrangePiecesImmediately(List<PieceController> pieces)
    {
        isBeingArranged = true;

        Transform centerPosition = HorseRacePathManager.Instance.GetCurrentPoint(
            pieceController.currentPathIndex,
            pieceController.playerColor);

        if (centerPosition == null)
        {
            isBeingArranged = false;
            yield break;
        }

        Vector3[] targetPositions = CalculateTargetPositions(pieces.Count, centerPosition.position);

        // Di chuyển ngay lập tức không animation
        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i] != null)
            {
                PositionOptimizer optimizer = pieces[i].GetComponent<PositionOptimizer>();
                if (optimizer != null)
                {
                    optimizer.SetIsBeingHandled(true);
                }

                // Đặt vị trí ngay lập tức
                pieces[i].transform.position = targetPositions[i];
                pieces[i].transform.rotation = Quaternion.identity;

                Rigidbody pieceRb = pieces[i].GetComponent<Rigidbody>();
                if (pieceRb != null)
                {
                    pieceRb.linearVelocity = Vector3.zero;
                    pieceRb.angularVelocity = Vector3.zero;
                }
            }
        }

        // Kích hoạt lại PositionOptimizer sau khi sắp xếp
        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i] != null)
            {
                PositionOptimizer optimizer = pieces[i].GetComponent<PositionOptimizer>();
                if (optimizer != null)
                {
                    optimizer.SetIsBeingHandled(false);
                }
            }
        }

        isBeingArranged = false;
        yield return null;
    }

    // Sửa phương thức ForceArrangeCheck để dùng ArrangeImmediately khi cần
    public void ForceArrangeCheck(bool immediate = false)
    {
        if (!isBeingArranged)
        {
            if (immediate)
            {
                ArrangeImmediately();
            }
            else
            {
                CheckAndArrangePieces();
            }
        }
    }
}