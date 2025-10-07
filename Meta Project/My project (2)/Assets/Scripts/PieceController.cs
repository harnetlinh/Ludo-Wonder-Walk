using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class PieceController : MonoBehaviourPun, IPunObservable
{
    public PlayerColor playerColor;
    public int currentPathIndex = -1;
    public bool isMoving = false;
    public float moveSpeed = 5f;

    [Header("Online/Offline Settings")]
    public bool isOnlineMode = true;

    [Header("Network Settings")]
    public float networkUpdateRate = 20f; // Số lần cập nhật/giây
    public float positionThreshold = 0.001f; // Ngưỡng thay đổi vị trí
    public float rotationThreshold = 0.1f; // Ngưỡng thay đổi xoay

    [Header("Visual Feedback")]
    public GameObject highlightEffect;
    //public Color dragColor = Color.yellow;
    //public Color hoverColor = Color.cyan;

    // Biến lưu vị trí chuồng
    private Vector3 initialStablePosition;
    private int stablePointIndex = -1;

    [System.NonSerialized]
    protected Renderer pieceRenderer;
    [System.NonSerialized]
    protected Color originalColor;
    private Vector3 targetPosition;
    private bool hasValidMove = false;

    public int lastCountryPointIndex = -1;

    // Simple drag variables
    private bool isDragging = false;
    private Camera mainCamera;
    private Vector3 dragOffset;

    // VR variables
    public bool isVRGrabbed = false;

    // Network synchronization variables
    private Vector3 networkPosition;
    private Quaternion networkRotation;
    private bool networkIsMoving;
    private int networkPathIndex;
    private bool networkIsVRGrabbed;

    // High precision sync
    private float lastNetworkTime;
    private Vector3 lastSentPosition;
    private Quaternion lastSentRotation;


    // Thêm vào class PieceController
private static bool isProcessingTurn = false;
private static float lastTurnProcessingTime = 0f;
private const float TURN_COOLDOWN = 1f; // Thời gian chờ giữa các lượt

    protected virtual void Start()
    {
        gameObject.tag = "Piece";
        pieceRenderer = GetComponent<Renderer>();
        originalColor = pieceRenderer.material.color;

        // Lưu vị trí chuồng ban đầu
        SaveInitialStablePosition();

        // Khởi tạo camera
        mainCamera = Camera.main;

        // Khởi tạo biến đồng bộ
        if (isOnlineMode && photonView != null)
        {
            networkPosition = transform.position;
            networkRotation = transform.rotation;
            networkPathIndex = currentPathIndex;
            networkIsMoving = isMoving;
            networkIsVRGrabbed = isVRGrabbed;

            lastSentPosition = transform.position;
            lastSentRotation = transform.rotation;
        }

        Debug.Log($"PieceController ready for {playerColor}. Owner: {photonView.Owner?.NickName}");
    }

    protected virtual void Update()
    {
        // Xử lý input cho owner
        if (photonView == null || photonView.IsMine || !isOnlineMode)
        {
            HandleInput();
        }

        // Đồng bộ cho non-owner
        if (isOnlineMode && photonView != null && !photonView.IsMine)
        {
            SmoothSync();
        }

        //UpdateVisualFeedback();
    }

    /// <summary>
    /// Đồng bộ mượt mà với độ chính xác cao
    /// </summary>
    private void SmoothSync()
    {
        // Đồng bộ tức thì khi được cầm bằng VR
        if (networkIsVRGrabbed)
        {
            transform.position = networkPosition;
            transform.rotation = networkRotation;
        }
        else
        {
            // Sử dụng interpolation cho di chuyển bình thường
            float lerpFactor = Time.deltaTime * networkUpdateRate;
            transform.position = Vector3.Lerp(transform.position, networkPosition, lerpFactor);
            transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation, lerpFactor);
        }

        // Đồng bộ trạng thái
        isMoving = networkIsMoving;
        currentPathIndex = networkPathIndex;
        isVRGrabbed = networkIsVRGrabbed;
    }

    //private void UpdateVisualFeedback()
    //{
    //    if (pieceRenderer == null) return;

    //    if (isDragging || isVRGrabbed)
    //    {
    //        pieceRenderer.material.color = dragColor;
    //    }
    //    else
    //    {
    //        pieceRenderer.material.color = originalColor;
    //    }

    //    if (highlightEffect != null)
    //    {
    //        highlightEffect.SetActive(isDragging || isVRGrabbed);
    //    }
    //}

    private void HandleInput()
    {
        // Không xử lý input chuột nếu đang được cầm bằng VR
        if (isVRGrabbed) return;

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit) && hit.collider.gameObject == this.gameObject)
            {
                if (isOnlineMode && photonView != null && !photonView.IsMine)
                {
                    photonView.RequestOwnership();
                }

                StartDrag();
            }
        }

        if (isDragging && Input.GetMouseButton(0))
        {
            UpdateDrag();
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (isDragging)
            {
                EndDrag();
            }
        }
    }

    private void StartDrag()
    {
        if (isMoving) return;

        isDragging = true;

        Vector3 mousePos = Input.mousePosition;
        mousePos.z = Vector3.Distance(mainCamera.transform.position, transform.position);
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(mousePos);
        dragOffset = transform.position - worldPos;

        Debug.Log($"Started dragging {playerColor} piece by {PhotonNetwork.NickName}");
    }

    private void UpdateDrag()
    {
        if (isOnlineMode && photonView != null && !photonView.IsMine) return;

        Vector3 mousePos = Input.mousePosition;
        mousePos.z = Vector3.Distance(mainCamera.transform.position, transform.position);
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(mousePos);
        transform.position = worldPos + dragOffset;
    }

    private void EndDrag()
    {
        isDragging = false;

        if (IsValidDropPosition())
        {
            Debug.Log($"Valid drop position for {playerColor} piece");
            ProcessValidDrop();
        }
        else
        {
            Debug.Log($"Invalid drop position for {playerColor} piece");
        }

        Debug.Log($"Stopped dragging {playerColor} piece");
    }

    private void ProcessValidDrop()
    {
        if (currentPathIndex == -1 && IsNearStartPoint())
        {
            Transform startPoint = HorseRacePathManager.Instance.GetStartPoint(playerColor);
            if (startPoint != null)
            {
                transform.position = startPoint.position;
                currentPathIndex = HorseRacePathManager.Instance.commonPathPoints.IndexOf(startPoint);
                Debug.Log($"{playerColor} piece moved to start point at index {currentPathIndex}");
            }
        }
    }

    private bool IsNearStartPoint()
    {
        Transform startPoint = HorseRacePathManager.Instance.GetStartPoint(playerColor);
        if (startPoint == null) return false;

        return Vector3.Distance(transform.position, startPoint.position) < 2.0f;
    }

    private bool IsValidDropPosition()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, 10f))
        {
            if (hit.collider.CompareTag("Table"))
            {
                return true;
            }
        }
        return false;
    }

    private void SaveInitialStablePosition()
    {
        if (HorseRacePathManager.Instance == null) return;

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

        if (isOnlineMode && photonView != null && !photonView.IsMine)
        {
            photonView.RequestOwnership();
            StartCoroutine(MoveAfterOwnership(steps));
        }
        else
        {
            MoveLocal(steps);
        }
    }

    private IEnumerator MoveAfterOwnership(int steps)
    {
        float timeout = 2f;
        float elapsed = 0f;

        while (!photonView.IsMine && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (photonView.IsMine)
        {
            MoveLocal(steps);
        }
        else
        {
            Debug.LogWarning($"Failed to get ownership for moving {playerColor} piece");
        }
    }

    protected virtual void MoveLocal(int steps)
    {
        Debug.Log($"MoveLocal called for {playerColor} piece, steps: {steps}");

        if (isMoving) return;

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

    //protected virtual void MoveLocal(int steps)
    //{
    //    Debug.Log($"[DEBUG] MoveLocal called for {playerColor} piece, steps: {steps}");

    //    if (isMoving)
    //    {
    //        Debug.Log($"[DEBUG] Piece is already moving, aborting MoveLocal.");
    //        return;
    //    }

    //    // Nếu đang drag thì kết thúc drag trước
    //    if (isDragging)
    //    {
    //        EndDrag();
    //    }

    //    // VÔ HIỆU HÓA TẠM THỜI PositionOptimizer và PieceArranger trong khi di chuyển
    //    PositionOptimizer optimizer = GetComponent<PositionOptimizer>();
    //    PieceArranger arranger = GetComponent<PieceArranger>();

    //    if (optimizer != null)
    //        optimizer.enabled = false;

    //    if (arranger != null)
    //        arranger.enabled = false;

    //    // Nếu có PUN và là quân cờ của mình, gửi RPC
    //    if (isOnlineMode && photonView != null && photonView.IsMine)
    //    {
    //        Debug.Log($"[DEBUG] Sending NetworkMove RPC.");
    //        photonView.RPC("NetworkMove", RpcTarget.All, steps);
    //    }
    //    else
    //    {
    //        Debug.Log($"[DEBUG] Starting MoveStepByStep coroutine.");
    //        StartCoroutine(MoveStepByStep(steps));
    //    }
    //}

    [PunRPC]
    public void NetworkMove(int steps)
    {
        Debug.Log($"[DEBUG] NetworkMove RPC received, steps: {steps}, isMine: {photonView?.IsMine}");

        if (!photonView.IsMine)
        {
            // Tạm thời vô hiệu hóa vật lý khi di chuyển từ network
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        MoveLocal(steps);
    }

    private void CheckAndKickOpponentPieces(int pathIndex)
    {
        if (HorseRacePathManager.Instance.IsSafeZone(pathIndex, playerColor) ||
            pathIndex >= HorseRacePathManager.Instance.commonPathPoints.Count)
        {
            return;
        }

        PieceController[] allPieces = FindObjectsByType<PieceController>(FindObjectsSortMode.None);
        for (int i = 0; i < allPieces.Length; i++)
        {
            PieceController piece = allPieces[i];
            if (piece != this &&
                piece.currentPathIndex == pathIndex &&
                piece.playerColor != playerColor)
            {
                KickPieceToStable(piece);
            }
        }
    }

    private void KickPieceToStable(PieceController piece)
    {
        if (isOnlineMode && piece.photonView != null && !piece.photonView.IsMine)
        {
            piece.photonView.RequestOwnership();
        }

        piece.currentPathIndex = -1;

        if (piece.stablePointIndex >= 0)
        {
            List<Transform> stablePoints = HorseRacePathManager.Instance.GetStablePoints(piece.playerColor);
            if (piece.stablePointIndex < stablePoints.Count)
            {
                StartCoroutine(MovePieceToStableSmoothly(piece, stablePoints[piece.stablePointIndex].position));
            }
        }
    }

    private IEnumerator MovePieceToStableSmoothly(PieceController piece, Vector3 targetPosition)
    {
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

        piece.transform.position = targetPosition;
        piece.isMoving = false;
    }

    /// <summary>
    /// Đồng bộ mạng với độ chính xác cao
    /// </summary>
    public virtual void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Gửi dữ liệu với tần suất cao
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(isMoving);
            stream.SendNext(currentPathIndex);
            stream.SendNext(playerColor);
            stream.SendNext(isVRGrabbed);
            stream.SendNext(Time.time);
        }
        else
        {
            // Nhận dữ liệu và áp dụng ngay lập tức
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            networkIsMoving = (bool)stream.ReceiveNext();
            networkPathIndex = (int)stream.ReceiveNext();
            PlayerColor receivedColor = (PlayerColor)stream.ReceiveNext();
            networkIsVRGrabbed = (bool)stream.ReceiveNext();
            float sendTime = (float)stream.ReceiveNext();

            // Tính độ trễ và bù trừ nếu cần
            float latency = Time.time - sendTime;
            if (latency > 0.1f) // Nếu độ trễ lớn
            {
                // Dự đoán vị trí (extrapolation)
                // networkPosition += networkVelocity * latency;
            }
        }
    }

    // Các phương thức public
    public bool IsBeingDragged()
    {
        return isDragging;
    }

    public Vector3 GetInitialStablePosition()
    {
        return initialStablePosition;
    }

    public void ResetColor()
    {
        pieceRenderer.material.color = originalColor;
    }

    // Phương thức cho VR
    public void SetGrabbedState(bool grabbed)
    {
        isVRGrabbed = grabbed;

        // THÊM: Đảm bảo vật lý được kích hoạt khi thả quân cờ
        if (!grabbed)
        {
            StartCoroutine(EnsurePhysicsAfterRelease());
        }

        // Đồng bộ ngay lập tức khi trạng thái thay đổi
        if (isOnlineMode && photonView != null && photonView.IsMine)
        {
            photonView.RPC("RPC_SetGrabbedState", RpcTarget.Others, grabbed);
        }
    }

    // THÊM: Coroutine để đảm bảo vật lý được kích hoạt sau khi thả
    private IEnumerator EnsurePhysicsAfterRelease()
    {
        yield return new WaitForEndOfFrame();
        
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Đảm bảo vật lý được kích hoạt
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.WakeUp();
            
            // Đảm bảo không có velocity cũ
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        // Kích hoạt lại PositionOptimizer sau một chút
        yield return new WaitForSeconds(0.2f);
        PositionOptimizer optimizer = GetComponent<PositionOptimizer>();
        if (optimizer != null && !isMoving)
        {
            optimizer.enabled = true;
            optimizer.EnsurePhysicsActivation();
        }
    }

    //[PunRPC]
    //private void RPC_SetGrabbedState(bool grabbed)
    //{
    //    isVRGrabbed = grabbed;
    //    networkIsVRGrabbed = grabbed;
    //}

    //[PunRPC]
    //public void NetworkMove(int steps)
    //{
    //    if (!photonView.IsMine)
    //    {
    //        MoveLocal(steps);
    //    }
    //}

    [PunRPC]
    public void NetworkKickToStable()
    {
        currentPathIndex = -1;
        if (stablePointIndex >= 0)
        {
            List<Transform> stablePoints = HorseRacePathManager.Instance.GetStablePoints(playerColor);
            if (stablePointIndex < stablePoints.Count)
            {
                StartCoroutine(MovePieceToStableSmoothly(this, stablePoints[stablePointIndex].position));
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


    // Sửa phương thức OnCollisionEnter
private void OnCollisionEnter(Collision collision)
{
    if (collision.gameObject.CompareTag("Table"))
    {
        // KIỂM TRA: Nếu dice đang di chuyển, bỏ qua xử lý lượt
        if (DiceController.Instance != null && DiceController.Instance.IsDiceMoving())
        {
            Debug.Log("Dice đang di chuyển, bỏ qua xử lý lượt từ va chạm");
            return;
        }
        // ĐẢM BẢO VẬT LÝ ĐƯỢC KÍCH HOẠT KHI CHẠM BÀN
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (rb.isKinematic && !isMoving && !isVRGrabbed)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
            rb.WakeUp();
        }
        
        // THÊM: Bỏ qua nếu đang di chuyển
        if (isMoving) return;

        if (GameTurnManager.Instance == null || !GameTurnManager.Instance.isInitialized)
        {
            return;
        }

        // THÊM: Kiểm tra cooldown để tránh xử lý nhiều quân cùng lúc
        if (isProcessingTurn && Time.time - lastTurnProcessingTime < TURN_COOLDOWN)
        {
            Debug.Log($"Ignoring collision for {playerColor} piece - turn processing in progress");
            return;
        }

        // THÊM: Kiểm tra xem đã có quân cờ nào được xử lý trong lượt này chưa
        if (GameTurnManager.Instance.HasPieceMovedThisTurn())
        {
            Debug.Log($"Ignoring collision for {playerColor} piece - another piece already moved this turn");
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

            // THÊM: Kiểm tra xem quân cờ đã được xuất chưa
            bool alreadyExited = (currentPathIndex == -1 && 
                                 transform.position != initialStablePosition && 
                                 Vector3.Distance(transform.position, initialStablePosition) > 1.0f);

            if (isNearStable && !alreadyExited)
            {
                // THÊM: Đánh dấu đang xử lý lượt
                isProcessingTurn = true;
                lastTurnProcessingTime = Time.time;

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

                // THÊM: Đánh dấu quân cờ đã xuất để tránh xuất lại
                hasValidMove = true;
                
                // CẬP NHẬT: Kết thúc lượt sau khi xuất quân thành công
                GameTurnManager.Instance.PieceMoved();

                // Cập nhật trạng thái của xúc xắc
                if (DiceController.Instance != null)
                {
                    DiceController.Instance.hasRolledThisTurn = false; // Cho phép roll lại nếu có quân 6
                    DiceController.Instance.diceButton.interactable = true; // Mở nút xúc xắc
                }

                // THÊM: Reset trạng thái xử lý sau một khoảng thời gian
                StartCoroutine(ResetTurnProcessingAfterDelay(TURN_COOLDOWN));
            }
        }
        else if (currentPathIndex >= 0 &&
                GameTurnManager.Instance.IsCurrentPlayer(playerColor))
        {
            // THÊM: Kiểm tra xem đã có quân cờ nào di chuyển trong lượt này chưa
            if (GameTurnManager.Instance.HasPieceMovedThisTurn())
            {
                Debug.Log($"Ignoring movement for {playerColor} piece - another piece already moved this turn");
                return;
            }

            // THÊM: Đánh dấu đang xử lý lượt
            isProcessingTurn = true;
            lastTurnProcessingTime = Time.time;

            // Di chuyển quân theo số xúc xắc
            Move(DiceController.Instance.LastDiceValue);

            // THÊM: Reset trạng thái xử lý sau một khoảng thời gian
            StartCoroutine(ResetTurnProcessingAfterDelay(TURN_COOLDOWN));
        }
    }
}

// THÊM: Coroutine để reset trạng thái xử lý lượt
private IEnumerator ResetTurnProcessingAfterDelay(float delay)
{
    yield return new WaitForSeconds(delay);
    isProcessingTurn = false;
    Debug.Log("Turn processing reset - ready for next piece");
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