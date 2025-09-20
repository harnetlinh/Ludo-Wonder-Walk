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

    [Header("Online/Offline Settings")]
    public bool isOnlineMode = true;

    [Header("Network Drag Settings")]
    public float dragSensitivity = 1f;
    public float dragHeight = 0.5f;
    public LayerMask groundLayer = 1;

    [Header("Visual Feedback")]
    public GameObject highlightEffect;
    public Color dragColor = Color.yellow;
    public Color hoverColor = Color.cyan;

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

    // Network drag variables
    private bool isBeingDragged = false;
    private bool isHovered = false;
    private Vector3 dragOffset;
    private Vector3 lastValidPosition;
    private Camera playerCamera;
    private Rigidbody pieceRigidbody;
    private Collider pieceCollider;

    // PUN Network Variables
    private Vector3 networkPosition;
    private Quaternion networkRotation;
    private bool networkIsMoving;
    private int networkPathIndex;
    protected bool isNetworked = false;

    [Header("Network Sync Settings")]
    public float positionLerpSpeed = 10f;
    public float rotationLerpSpeed = 10f;

    
    // Network synchronization
    private Vector3 networkDragPosition;
    private bool networkIsDragged;
    private bool networkIsHovered;

    // Thêm biến để xử lý đồng bộ mượt mà
    private Vector3 networkVelocity;
    private bool isBeingHeld = false;

    protected virtual void Start()
    {
        gameObject.tag = "Piece";
        pieceRenderer = GetComponent<Renderer>();
        originalColor = pieceRenderer.material.color;

        // Lưu vị trí chuồng ban đầu
        SaveInitialStablePosition();

        // Khởi tạo PUN
        if (isOnlineMode && photonView != null)
        {
            isNetworked = true;
            networkPosition = transform.position;
            networkRotation = transform.rotation;
            networkPathIndex = currentPathIndex;
            networkIsMoving = isMoving;

            // Cải thiện cài đặt vật lý để đồng bộ mượt mà hơn
            if (pieceRigidbody != null)
            {
                pieceRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                pieceRigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }
        }

        // Lấy các component cần thiết cho drag
        pieceRigidbody = GetComponent<Rigidbody>();
        pieceCollider = GetComponent<Collider>();

        // Tìm camera
        playerCamera = Camera.main;
        if (playerCamera == null)
        {
            playerCamera = FindFirstObjectByType<Camera>();
        }

        // Khởi tạo network variables
        if (isNetworked)
        {
            networkDragPosition = transform.position;
            networkIsDragged = false;
            networkIsHovered = false;
        }
    }

    protected virtual void Update()
    {
        // Chỉ xử lý input nếu là quân cờ của mình hoặc offline mode
        if (isNetworked && !photonView.IsMine)
        {
            SmoothSync();
            return;
        }

        HandleInput();
        UpdateVisualFeedback();
    }

    // Thêm phương thức SmoothSync tương tự như trong NetworkDiceSync
    private void SmoothSync()
    {
        // Nếu đang được cầm/drag, sử dụng interpolation vị trí thông thường
        if (isBeingHeld || networkIsDragged)
        {
            transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * positionLerpSpeed);
            transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation, Time.deltaTime * rotationLerpSpeed);
        }
        else
        {
            // Đối với vật thể vật lý không được cầm, sử dụng interpolation vật lý
            if (pieceRigidbody != null && !pieceRigidbody.isKinematic)
            {
                // Sử dụng Velocity-based interpolation để mượt mà hơn
                Vector3 targetVelocity = (networkPosition - transform.position) * positionLerpSpeed;
                pieceRigidbody.linearVelocity = Vector3.Lerp(pieceRigidbody.linearVelocity, targetVelocity, Time.deltaTime * 5f);

                // Đồng bộ xoay thông qua angular velocity
                Quaternion rotationDiff = networkRotation * Quaternion.Inverse(transform.rotation);
                rotationDiff.ToAngleAxis(out float angle, out Vector3 axis);

                if (angle > 180f) angle -= 360f;
                if (Mathf.Abs(angle) > 0.5f)
                {
                    Vector3 angularVelocity = (axis * angle * Mathf.Deg2Rad) * rotationLerpSpeed;
                    pieceRigidbody.angularVelocity = Vector3.Lerp(pieceRigidbody.angularVelocity, angularVelocity, Time.deltaTime * 5f);
                }
            }
            else
            {
                // Fallback: interpolation thông thường
                transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * positionLerpSpeed);
                transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation, Time.deltaTime * rotationLerpSpeed);
            }
        }

        // Cập nhật các trạng thái khác từ network
        isMoving = networkIsMoving;
        currentPathIndex = networkPathIndex;
        isBeingDragged = networkIsDragged;
        isHovered = networkIsHovered;
    }

    private void HandleInput()
    {
        // Kiểm tra click chuột
        if (Input.GetMouseButtonDown(0))
        {
            if (IsMouseOverPiece())
            {
                StartDrag();
            }
        }

        // Xử lý drag
        if (isBeingDragged && Input.GetMouseButton(0))
        {
            UpdateDrag();
        }
        else if (isBeingDragged && Input.GetMouseButtonUp(0))
        {
            EndDrag();
        }

        // Kiểm tra hover
        bool currentlyHovered = IsMouseOverPiece();
        if (currentlyHovered != isHovered)
        {
            isHovered = currentlyHovered;
            if (isNetworked && photonView.IsMine)
            {
                photonView.RPC("NetworkSetHovered", RpcTarget.All, isHovered);
            }
        }
    }

    private bool IsMouseOverPiece()
    {
        if (playerCamera == null) return false;

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            return hit.collider == pieceCollider;
        }

        return false;
    }

    private void StartDrag()
    {
        if (isMoving) return; // Không thể drag khi đang di chuyển

        // Chỉ cho phép drag nếu là quân cờ của mình hoặc không phải multiplayer
        if (isNetworked && !photonView.IsMine) return;

        isBeingDragged = true;
        lastValidPosition = transform.position;

        // Tắt physics khi drag
        if (pieceRigidbody != null)
        {
            pieceRigidbody.isKinematic = true;
        }

        // Tính toán offset
        Vector3 mouseWorldPos = GetMouseWorldPosition();
        dragOffset = transform.position - mouseWorldPos;

        // Gửi RPC để thông báo bắt đầu drag
        if (isNetworked && photonView.IsMine)
        {
            photonView.RPC("NetworkStartDrag", RpcTarget.All, transform.position);
        }

        Debug.Log($"Bắt đầu drag quân cờ {playerColor}");
    }

    private void UpdateDrag()
    {
        Vector3 mouseWorldPos = GetMouseWorldPosition();
        Vector3 targetPosition = mouseWorldPos + dragOffset;

        // Giới hạn chiều cao drag
        targetPosition.y = Mathf.Max(targetPosition.y, dragHeight);

        transform.position = targetPosition;

        // Gửi RPC để đồng bộ vị trí drag
        if (isNetworked && photonView.IsMine)
        {
            photonView.RPC("NetworkUpdateDragPosition", RpcTarget.All, targetPosition);
        }
    }

    private void EndDrag()
    {
        isBeingDragged = false;

        // Kiểm tra vị trí hợp lệ
        if (IsValidDropPosition())
        {
            // Vị trí hợp lệ - giữ nguyên vị trí
            lastValidPosition = transform.position;

            // Gửi RPC để thông báo kết thúc drag thành công
            if (isNetworked && photonView.IsMine)
            {
                photonView.RPC("NetworkEndDrag", RpcTarget.All, transform.position, true);
            }

            Debug.Log($"Kết thúc drag quân cờ {playerColor} tại vị trí hợp lệ");
        }
        else
        {
            // Vị trí không hợp lệ - trở về vị trí cũ
            transform.position = lastValidPosition;

            // Gửi RPC để thông báo kết thúc drag thất bại
            if (isNetworked && photonView.IsMine)
            {
                photonView.RPC("NetworkEndDrag", RpcTarget.All, lastValidPosition, false);
            }

            Debug.Log($"Kết thúc drag quân cờ {playerColor} - trở về vị trí cũ");
        }

        // Bật lại physics
        if (pieceRigidbody != null)
        {
            pieceRigidbody.isKinematic = false;
        }
        pieceRigidbody.isKinematic = false;
    }

    private bool IsValidDropPosition()
    {
        // Kiểm tra xem có thể đặt quân cờ tại vị trí này không
        // Có thể thêm logic kiểm tra theo luật Ludo ở đây

        // Kiểm tra có chạm đất không
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, 10f, groundLayer))
        {
            // Kiểm tra có phải là bàn cờ không
            if (hit.collider.CompareTag("Table"))
            {
                return true;
            }
        }

        return false;
    }

    private Vector3 GetMouseWorldPosition()
    {
        if (playerCamera == null) return Vector3.zero;

        Vector3 mousePos = Input.mousePosition;
        mousePos.z = playerCamera.WorldToScreenPoint(transform.position).z;
        return playerCamera.ScreenToWorldPoint(mousePos);
    }

    private void UpdateVisualFeedback()
    {
        if (pieceRenderer == null) return;

        if (isBeingDragged)
        {
            pieceRenderer.material.color = dragColor;
        }
        else if (isHovered)
        {
            pieceRenderer.material.color = hoverColor;
        }
        else
        {
            pieceRenderer.material.color = originalColor;
        }

        // Hiển thị/ẩn highlight effect
        if (highlightEffect != null)
        {
            highlightEffect.SetActive(isHovered || isBeingDragged);
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

    // Override để xử lý sắp xếp trong môi trường network
    protected virtual void MoveLocal(int steps)
    {
        Debug.Log($"[NETWORK] MoveLocal called for {playerColor} piece, steps: {steps}");

        if (isMoving) return;

        // Nếu đang drag thì kết thúc drag trước
        if (isBeingDragged)
        {
            EndDrag();
        }

        // VÔ HIỆU HÓA TẠM THỜI PositionOptimizer và PieceArranger trong khi di chuyển
        PositionOptimizer optimizer = GetComponent<PositionOptimizer>();
        PieceArranger arranger = GetComponent<PieceArranger>();

        if (optimizer != null)
            optimizer.enabled = false;

        if (arranger != null)
            arranger.enabled = false;

        // Nếu có PUN và là quân cờ của mình, gửi RPC
        if (isNetworked && photonView.IsMine)
        {
            photonView.RPC("NetworkMove", RpcTarget.All, steps);
        }
        else
        {
            // Chạy local nếu không có PUN
            StartCoroutine(MoveStepByStep(steps));
        }
    }

    private IEnumerator DelayedMove(int steps, float delay)
    {
        yield return new WaitForSeconds(delay);
        StartCoroutine(MoveStepByStep(steps));
    }

    protected IEnumerator MoveStepByStep(int totalSteps)
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

        // KIỂM TRA VÀ SẮP XẾP TRƯỚC TẠI ĐIỂM ĐẾN CUỐI CÙNG
        int finalIndex = pathIndices[pathIndices.Count - 1];
        yield return StartCoroutine(CheckAndArrangeAtDestination(finalIndex));

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

        // KÍCH HOẠT LẠI PositionOptimizer và PieceArranger sau khi di chuyển
        PositionOptimizer optimizer = GetComponent<PositionOptimizer>();
        PieceArranger arranger = GetComponent<PieceArranger>();

        yield return new WaitForSeconds(0.1f); // Đợi một chút để ổn định

        if (optimizer != null)
            optimizer.enabled = true;

        if (arranger != null)
            arranger.enabled = true;

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
            // THÊM: Bỏ qua nếu đang di chuyển
            if (isMoving) return;

            if (GameTurnManager.Instance == null || !GameTurnManager.Instance.isInitialized)
            {
                return;
            }

            // Kiểm tra nếu đặt vào vị trí hợp lệ
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

                    PositionOptimizer optimizer = GetComponent<PositionOptimizer>();
                    if (optimizer != null)
                    {
                        optimizer.SetIsBeingHandled(true);
                    }

                    transform.position = startPoint.position;
                    currentPathIndex = HorseRacePathManager.Instance.commonPathPoints.IndexOf(startPoint);

                    Debug.Log($"{playerColor} piece moved to start point at index {currentPathIndex}");

                    PieceArranger arranger = GetComponent<PieceArranger>();
                    if (arranger != null)
                    {
                        arranger.ForceArrangeCheck(true);
                    }

                    if (optimizer != null)
                    {
                        StartCoroutine(ReEnableOptimizerAfterDelay(optimizer, 1f));
                    }

                    // QUAN TRỌNG: XÓA dòng này để không kết thúc lượt ngay
                    // GameTurnManager.Instance.PieceMoved();

                    // Thay vào đó, chỉ cập nhật trạng thái của xúc xắc
                    if (DiceController.Instance != null)
                    {
                        DiceController.Instance.hasRolledThisTurn = false; // Cho phép roll lại nếu có quân 6
                        DiceController.Instance.diceButton.interactable = true; // Mở nút xúc xắc
                    }
                }
            }
            else if (currentPathIndex >= 0 &&
                    GameTurnManager.Instance.IsCurrentPlayer(playerColor))
            {
                // Di chuyển quân theo số xúc xắc
                Move(DiceController.Instance.LastDiceValue);
            }
        }
    }

    // Thêm coroutine để kích hoạt lại PositionOptimizer
    private IEnumerator ReEnableOptimizerAfterDelay(PositionOptimizer optimizer, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (optimizer != null)
        {
            optimizer.SetIsBeingHandled(false);
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

            // Gửi thêm dữ liệu drag
            stream.SendNext(isBeingDragged);
            stream.SendNext(isHovered);

            // Gửi thông tin vật lý
            if (pieceRigidbody != null)
            {
                stream.SendNext(pieceRigidbody.linearVelocity);
                stream.SendNext(pieceRigidbody.angularVelocity);
                stream.SendNext(isBeingHeld);
            }
        }
        else
        {
            // Nhận dữ liệu từ master client
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            networkIsMoving = (bool)stream.ReceiveNext();
            networkPathIndex = (int)stream.ReceiveNext();
            PlayerColor networkPlayerColor = (PlayerColor)stream.ReceiveNext();

            // Nhận thêm dữ liệu drag
            networkIsDragged = (bool)stream.ReceiveNext();
            networkIsHovered = (bool)stream.ReceiveNext();

            // Nhận thông tin vật lý
            if (pieceRigidbody != null)
            {
                networkVelocity = (Vector3)stream.ReceiveNext();
                Vector3 networkAngularVelocity = (Vector3)stream.ReceiveNext();
                isBeingHeld = (bool)stream.ReceiveNext();

                // Nếu đang được cầm, tắt vật lý tạm thời
                if (isBeingHeld)
                {
                    pieceRigidbody.isKinematic = true;
                }
                else
                {
                    pieceRigidbody.isKinematic = false;
                }
            }

            // Cập nhật nếu không phải là quân cờ của mình
            if (!photonView.IsMine)
            {
                // Không cần gọi UpdateFromNetwork() nữa vì đã có SmoothSync()
            }
        }
    }

    [PunRPC]
    public void NetworkMove(int steps)
    {
        // Thêm kiểm tra để đảm bảo chỉ xử lý khi không phải là quân cờ của mình
        if (!photonView.IsMine)
        {
            // Tạm thời vô hiệu hóa vật lý khi di chuyển từ network
            if (pieceRigidbody != null)
            {
                pieceRigidbody.isKinematic = true;
                pieceRigidbody.useGravity = false;
            }
        }

        MoveLocal(steps);
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

        // Cập nhật drag state
        if (networkIsDragged != isBeingDragged)
        {
            isBeingDragged = networkIsDragged;
        }

        if (networkIsHovered != isHovered)
        {
            isHovered = networkIsHovered;
        }
    }

    // RPC để di chuyển quân cờ
    

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

    // Network RPC Methods
    [PunRPC]
    public void NetworkStartDrag(Vector3 position)
    {
        if (!photonView.IsMine)
        {
            isBeingDragged = true;
            transform.position = position;
        }
    }

    [PunRPC]
    public void NetworkUpdateDragPosition(Vector3 position)
    {
        if (!photonView.IsMine)
        {
            transform.position = position;
        }
    }

    [PunRPC]
    public void NetworkEndDrag(Vector3 position, bool isValid)
    {
        if (!photonView.IsMine)
        {
            isBeingDragged = false;
            transform.position = position;
        }
    }

    [PunRPC]
    public void NetworkSetHovered(bool hovered)
    {
        if (!photonView.IsMine)
        {
            isHovered = hovered;
        }
    }

    [PunRPC]
    private void RPC_SetHeldState(bool heldState)
    {
        isBeingHeld = heldState;

        if (pieceRigidbody != null)
        {
            pieceRigidbody.isKinematic = heldState;

            // Nếu vừa được thả ra, áp dụng velocity từ network
            if (!heldState)
            {
                pieceRigidbody.linearVelocity = networkVelocity;
            }
        }
    }

    // Public methods để kiểm tra trạng thái
    public bool IsBeingDragged()
    {
        return isBeingDragged;
    }

    public bool IsHovered()
    {
        return isHovered;
    }

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

    // Thêm phương thức mới để kiểm tra và sắp xếp các quân cờ cùng màu tại điểm đến
    private IEnumerator CheckAndArrangeAtDestination(int targetIndex)
    {
        // Tìm tất cả quân cờ cùng màu tại điểm đến
        List<PieceController> piecesAtDestination = new List<PieceController>();

        PieceController[] allPieces = FindObjectsByType<PieceController>(FindObjectsSortMode.None);
        foreach (PieceController piece in allPieces)
        {
            if (piece.playerColor == playerColor &&
                piece.currentPathIndex == targetIndex &&
                piece != this)
            {
                piecesAtDestination.Add(piece);
            }
        }

        // Nếu có quân cờ cùng màu tại điểm đến, sắp xếp chúng trước
        if (piecesAtDestination.Count > 0)
        {
            // Tìm tất cả PieceArranger tại điểm đến và sắp xếp
            List<PieceArranger> arrangers = new List<PieceArranger>();
            foreach (PieceController piece in piecesAtDestination)
            {
                PieceArranger arranger = piece.GetComponent<PieceArranger>();
                if (arranger != null)
                {
                    arrangers.Add(arranger);
                }
            }

            // Sắp xếp ngay lập tức
            foreach (PieceArranger arranger in arrangers)
            {
                arranger.ForceArrangeCheck(true); // Sắp xếp ngay lập tức
            }

            // Đợi một chút để hoàn thành sắp xếp
            yield return new WaitForSeconds(0.2f);
        }
    }
}