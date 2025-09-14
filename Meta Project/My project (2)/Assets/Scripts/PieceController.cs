using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Photon.Pun;

public class PieceController : MonoBehaviourPun, IPunObservable
{
    public PlayerColor playerColor;
    public int currentPathIndex = -1; // -1 = chưa xuất quân
    public bool isMoving = false;
    public float moveSpeed = 5f;

    // Thêm biến để lưu vị trí chuồng ban đầu
    private Vector3 initialStablePosition;
    private int stablePointIndex = -1;

    [System.NonSerialized]
    protected Renderer pieceRenderer;
    [System.NonSerialized]
    protected Color originalColor;
    private Vector3 targetPosition;
    private bool hasValidMove = false;

    public int lastCountryPointIndex = -1;

    // PUN Network Variables
    private Vector3 networkPosition;
    private Quaternion networkRotation;
    private bool networkIsMoving;
    private int networkPathIndex;
    protected bool isNetworked = false;

    protected virtual void Start()
    {
        gameObject.tag = "Piece";
        pieceRenderer = GetComponent<Renderer>();

        // Lưu vị trí chuồng ban đầu
        SaveInitialStablePosition();

        // Khởi tạo PUN
        if (photonView != null)
        {
            isNetworked = true;
            networkPosition = transform.position;
            networkRotation = transform.rotation;
            networkPathIndex = currentPathIndex;
            networkIsMoving = isMoving;
        }
    }

    // Phương thức lưu vị trí chuồng ban đầu
    private void SaveInitialStablePosition()
    {
        // Kiểm tra HorseRacePathManager có tồn tại không
        if (HorseRacePathManager.Instance == null)
        {
            Debug.LogWarning("HorseRacePathManager.Instance is null. Cannot save initial stable position.");
            return;
        }
        
        // Tìm vị trí chuồng gần nhất khi khởi tạo
        List<Transform> stablePoints = HorseRacePathManager.Instance.GetStablePoints(playerColor);
        if (stablePoints.Count > 0)
        {
            float minDistance = float.MaxValue;
            Transform closestStable = null;

            for (int i = 0; i < stablePoints.Count; i++)
            {
                Transform stablePoint = stablePoints[i];
                float distance = Vector3.Distance(transform.position, stablePoint.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestStable = stablePoint;
                }
            }

            if (closestStable != null)
            {
                initialStablePosition = closestStable.position;
                stablePointIndex = stablePoints.IndexOf(closestStable);
            }
        }
    }
    public void Move(int steps)
    {
        if (isMoving) return;

        // Nếu có PUN và là quân cờ của mình, gửi RPC
        if (isNetworked && photonView.IsMine)
        {
            photonView.RPC("NetworkMove", RpcTarget.All, steps);
        }
        else
        {
            // Chạy local nếu không có PUN
            MoveLocal(steps);
        }
    }
    
    protected virtual void MoveLocal(int steps)
    {
        if (isMoving) return;

        // Kiểm tra và sắp xếp các quân cờ trên ô hiện tại trước khi di chuyển
        PieceArranger arranger = GetComponent<PieceArranger>();
        if (arranger != null)
        {
            arranger.ForceArrangeCheck();

            // Đợi một frame để sắp xếp hoàn tất trước khi di chuyển
            StartCoroutine(DelayedMove(steps, 0.1f));
            return;
        }

        // Nếu không có arranger, di chuyển ngay
        StartCoroutine(MoveStepByStep(steps));
    }

    private IEnumerator DelayedMove(int steps, float delay)
    {
        yield return new WaitForSeconds(delay);
        StartCoroutine(MoveStepByStep(steps));
    }

    private IEnumerator MoveStepByStep(int totalSteps)
    {
        isMoving = true;
        hasValidMove = true;

        // Tắt trọng lực và kinematic trong khi di chuyển
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Tính toán đường đi
        List<Vector3> pathPoints = new List<Vector3>();
        List<int> pathIndices = new List<int>();
        int tempIndex = currentPathIndex;

        for (int i = 0; i < totalSteps; i++)
        {
            bool isPrivatePath;
            Transform nextPoint = HorseRacePathManager.Instance.GetNextPoint(tempIndex, playerColor, out isPrivatePath);
            if (nextPoint == null) break;

            int newIndex = isPrivatePath ?
                HorseRacePathManager.Instance.commonPathPoints.Count +
                HorseRacePathManager.Instance.GetPrivatePath(playerColor).IndexOf(nextPoint) :
                HorseRacePathManager.Instance.commonPathPoints.IndexOf(nextPoint);

            pathPoints.Add(nextPoint.position);
            pathIndices.Add(newIndex);
            tempIndex = newIndex;
        }

        if (pathPoints.Count == 0)
        {
            isMoving = false;
            yield break;
        }

        // Di chuyển qua các điểm và kiểm tra đá quân tại mỗi điểm
        for (int i = 0; i < pathPoints.Count; i++)
        {
            Vector3 startPos = transform.position;
            Vector3 endPos = pathPoints[i];
            currentPathIndex = pathIndices[i];



            float duration = 0.3f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                transform.position = Vector3.Lerp(startPos, endPos, t);

                if (endPos != startPos)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(endPos - startPos);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t * 0.5f);
                }
                yield return null;
            }

            transform.position = endPos;
            transform.rotation = Quaternion.identity;

            // Kiểm tra và đá quân đối thủ tại điểm hiện tại
            CheckAndKickOpponentPieces(currentPathIndex);
            // Kiểm tra nếu quân cờ đã về đích
            if (WinConditionManager.Instance.IsPieceFinished(currentPathIndex, playerColor))
            {
                WinConditionManager.Instance.PieceFinished(playerColor);
                // Đánh dấu quân cờ đã hoàn thành
                currentPathIndex = -2; // -2 = đã về đích
            }
            CheckAndShowCountryInfo(currentPathIndex);
        }

        // Bật lại trọng lực ở điểm cuối cùng
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;

            yield return new WaitForSeconds(0.2f);
            transform.position = pathPoints.Last();
            transform.rotation = Quaternion.identity;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        isMoving = false;
        CheckAndShowCountryInfo(currentPathIndex);
        if (hasValidMove)
        {
            GameTurnManager.Instance.PieceMoved();

            // Di chuyển xúc xắc đến người chơi tiếp theo sau khi di chuyển xong
            if (DiceController.Instance != null)
            {
                DiceController.Instance.MoveDiceToCurrentPlayer();
            }
        }
    }

    // Phương thức kiểm tra và đá quân đối thủ
    private void CheckAndKickOpponentPieces(int pathIndex)
    {
        // Không đá quân trong safe zone hoặc đường riêng
        if (HorseRacePathManager.Instance.IsSafeZone(pathIndex, playerColor) ||
            pathIndex >= HorseRacePathManager.Instance.commonPathPoints.Count)
        {
            return;
        }

        // Tìm tất cả quân cờ khác tại cùng vị trí
        PieceController[] allPieces = FindObjectsByType<PieceController>(FindObjectsSortMode.None);
        for (int i = 0; i < allPieces.Length; i++)
        {
            PieceController piece = allPieces[i];
            if (piece != this &&
                piece.currentPathIndex == pathIndex &&
                piece.playerColor != playerColor)
            {
                // Đá quân đối thủ về chuồng
                KickPieceToStable(piece);
                Debug.Log($"{playerColor} đá quân {piece.playerColor} tại vị trí {pathIndex}");
            }
        }
    }

    // Phương thức đá quân về chuồng
    // Phương thức đá quân về chuồng
    private void KickPieceToStable(PieceController piece)
    {
        // Nếu có PUN và là quân cờ của mình, gửi RPC
        if (piece.isNetworked && piece.photonView.IsMine)
        {
            piece.photonView.RPC("NetworkKickToStable", RpcTarget.All);
        }
        else
        {
            // Chạy local nếu không có PUN
            KickPieceToStableLocal(piece);
        }
    }

    private void KickPieceToStableLocal(PieceController piece)
    {
        piece.currentPathIndex = -1; // Reset về chuồng

        // Đặt quân về vị trí chuồng ban đầu thay vì ngẫu nhiên
        if (piece.stablePointIndex >= 0)
        {
            List<Transform> stablePoints = HorseRacePathManager.Instance.GetStablePoints(piece.playerColor);
            if (piece.stablePointIndex < stablePoints.Count)
            {
                // Sử dụng coroutine để di chuyển mượt mà về chuồng
                StartCoroutine(MovePieceToStableSmoothly(piece, stablePoints[piece.stablePointIndex].position));
                return;
            }
        }

        // Fallback: nếu không tìm thấy vị trí chuồng ban đầu, sử dụng vị trí gần nhất
        List<Transform> fallbackStablePoints = HorseRacePathManager.Instance.GetStablePoints(piece.playerColor);
        if (fallbackStablePoints.Count > 0)
        {
            // Tìm vị trí chuồng gần nhất
            Transform closestStable = null;
            float minDistance = float.MaxValue;

            for (int i = 0; i < fallbackStablePoints.Count; i++)
            {
                Transform stablePoint = fallbackStablePoints[i];
                float distance = Vector3.Distance(piece.transform.position, stablePoint.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestStable = stablePoint;
                }
            }

            if (closestStable != null)
            {
                StartCoroutine(MovePieceToStableSmoothly(piece, closestStable.position));
            }
        }
    }

    // Coroutine di chuyển mượt mà về chuồng
    private IEnumerator MovePieceToStableSmoothly(PieceController piece, Vector3 targetPosition)
    {
        // Tạm thời vô hiệu hóa vật lý
        Rigidbody rb = piece.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        piece.isMoving = true;

        Vector3 startPosition = piece.transform.position;
        float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / duration);
            piece.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        // Đảm bảo chính xác vị trí cuối cùng
        piece.transform.position = targetPosition;
        piece.transform.rotation = Quaternion.identity;

        piece.lastCountryPointIndex = -1;

        // Bật lại vật lý
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        piece.isMoving = false;

        // Thông báo cho GameTurnManager
        if (GameTurnManager.Instance != null)
        {
            GameTurnManager.Instance.OnPieceKicked(piece.playerColor);
        }

        Debug.Log($"{piece.playerColor} bị đá về chuồng tại vị trí ban đầu");
    }




    public void ResetColor()
    {
        pieceRenderer.material.color = originalColor;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Table"))
        {
            if (GameTurnManager.Instance == null || !GameTurnManager.Instance.isInitialized)
            {
                //Debug.LogWarning("GameTurnManager is not ready!");
                return;
            }


            // Kiểm tra nếu đặt vào vị trí hợp lệ
            // Trong OnCollisionEnter, sửa phần xuất quân
            if (currentPathIndex == -1 &&
                GameTurnManager.Instance.IsCurrentPlayer(playerColor) &&
                DiceController.Instance.LastDiceValue == 6)
            {
                var stablePoints = HorseRacePathManager.Instance.GetStablePoints(playerColor);
                bool isNearStable = stablePoints.Any(point =>
                    Vector3.Distance(transform.position, point.position) < 2.0f);

                if (isNearStable)
                {
                    Transform startPoint = HorseRacePathManager.Instance.GetStartPoint(playerColor);
                    transform.position = startPoint.position;
                    currentPathIndex = HorseRacePathManager.Instance.commonPathPoints.IndexOf(startPoint);

                    // Sắp xếp ngay sau khi xuất quân
                    PieceArranger arranger = GetComponent<PieceArranger>();
                    if (arranger != null)
                    {
                        arranger.ForceArrangeCheck(true); // Sắp xếp ngay lập tức
                    }

                    GameTurnManager.Instance.PieceMoved();
                }
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Table"))
        {

            if (GameTurnManager.Instance.IsCurrentPlayer(playerColor) &&
                DiceController.Instance.LastDiceValue == 6 &&
                currentPathIndex == -1)
            {
                // Hiển thị vị trí được phép đặt (điểm xuất phát)
                Transform startPoint = HorseRacePathManager.Instance.GetStartPoint(playerColor);
                //HighlightManager.Instance.HighlightPosition(startPoint.position);
            }
        }
    }
    public Vector3 GetInitialStablePosition()
    {
        return initialStablePosition;
    }

    // PUN Network Synchronization
    public virtual void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Gửi dữ liệu đến các client khác
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(isMoving);
            stream.SendNext(currentPathIndex);
            stream.SendNext(playerColor);
        }
        else
        {
            // Nhận dữ liệu từ master client
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            networkIsMoving = (bool)stream.ReceiveNext();
            networkPathIndex = (int)stream.ReceiveNext();
            PlayerColor networkPlayerColor = (PlayerColor)stream.ReceiveNext();
            
            // Cập nhật nếu không phải là quân cờ của mình
            if (!photonView.IsMine)
            {
                UpdateFromNetwork();
            }
        }
    }

    private void UpdateFromNetwork()
    {
        // Cập nhật vị trí và xoay từ network
        if (Vector3.Distance(transform.position, networkPosition) > 0.1f)
        {
            transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * 10f);
        }
        
        if (Quaternion.Angle(transform.rotation, networkRotation) > 1f)
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation, Time.deltaTime * 10f);
        }

        // Cập nhật trạng thái di chuyển
        if (isMoving != networkIsMoving)
        {
            isMoving = networkIsMoving;
        }

        // Cập nhật path index
        if (currentPathIndex != networkPathIndex)
        {
            currentPathIndex = networkPathIndex;
        }
    }

    // RPC để di chuyển quân cờ
    [PunRPC]
    public void NetworkMove(int steps)
    {
        MoveLocal(steps);
    }

    // RPC để đặt quân cờ về chuồng
    [PunRPC]
    public void NetworkKickToStable()
    {
        currentPathIndex = -1;
        // Đặt về vị trí chuồng ban đầu
        if (stablePointIndex >= 0)
        {
            List<Transform> stablePoints = HorseRacePathManager.Instance.GetStablePoints(playerColor);
            if (stablePointIndex < stablePoints.Count)
            {
                StartCoroutine(MovePieceToStableSmoothly(this, stablePoints[stablePointIndex].position));
            }
        }
    }

    // RPC để cập nhật màu sắc
    [PunRPC]
    public void NetworkChangeColor(float r, float g, float b, float a)
    {
        if (pieceRenderer != null)
        {
            pieceRenderer.material.color = new Color(r, g, b, a);
        }
    }



    // Kiểm tra và hiển thị thông tin quốc gia
    // Kiểm tra và hiển thị thông tin quốc gia
    private void CheckAndShowCountryInfo(int pointIndex)
    {
        if (isMoving) return;
        if (pointIndex < 0) return;

        Debug.Log($"[DEBUG] CheckAndShowCountryInfo called for point {pointIndex}, player {playerColor}");

        // Sử dụng hàm mới có kiểm tra playerColor
        if (HorseRacePathManager.Instance.IsCountryPoint(pointIndex, playerColor))
        {
            Debug.Log($"[DEBUG] Point {pointIndex} is a country point for {playerColor}");
            string countryCode = HorseRacePathManager.Instance.GetCountryCode(pointIndex, playerColor);
            Debug.Log($"[DEBUG] Country code for point {pointIndex}: {countryCode}");

            if (!string.IsNullOrEmpty(countryCode) && pointIndex != lastCountryPointIndex)
            {
                lastCountryPointIndex = pointIndex;
                if (FactManager.Instance != null)
                {
                    Debug.Log($"[DEBUG] Calling FactManager.GetFact({countryCode})");
                    FactManager.Instance.GetFact(countryCode);
                    Debug.Log($"{playerColor} piece entered {countryCode} country point at index {pointIndex}");
                }
                else
                {
                    Debug.LogError("[DEBUG] FactManager.Instance is null!");
                }
            }
            else
            {
                Debug.Log($"[DEBUG] Country code is empty or same as last point. countryCode: '{countryCode}', lastCountryPointIndex: {lastCountryPointIndex}");
            }
        }
        else
        {
            Debug.Log($"[DEBUG] Point {pointIndex} is NOT a country point for {playerColor}");
            lastCountryPointIndex = -1;
        }
    }
}