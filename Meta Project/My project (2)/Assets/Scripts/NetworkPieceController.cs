using UnityEngine;
using Photon.Pun;

/// <summary>
/// NetworkPieceController - Quản lý quân cờ có thể kéo thả trong môi trường multiplayer
/// Kế thừa từ PieceController và thêm chức năng network drag & drop
/// </summary>
public class NetworkPieceController : MonoBehaviour
{
    //[Header("Network Drag Settings")]
    //public float dragSensitivity = 1f;
    //public float dragHeight = 0.5f;
    //public LayerMask groundLayer = 1;
    
    //[Header("Visual Feedback")]
    //public GameObject highlightEffect;
    //public Color dragColor = Color.yellow;
    //public Color hoverColor = Color.cyan;
    
    //// Network drag variables
    //private bool isBeingDragged = false;
    //private bool isHovered = false;
    //private Vector3 dragOffset;
    //private Vector3 lastValidPosition;
    //private Camera playerCamera;
    //private Rigidbody pieceRigidbody;
    //private Collider pieceCollider;
    //private Renderer networkPieceRenderer;
    //private Color networkOriginalColor;
    
    //// Network synchronization
    //private Vector3 networkDragPosition;
    //private bool networkIsDragged;
    //private bool networkIsHovered;
    //private bool isNetworked = false;

    //protected override void Start()
    //{
    //    base.Start();
        
    //    // Lấy các component cần thiết
    //    pieceRigidbody = GetComponent<Rigidbody>();
    //    pieceCollider = GetComponent<Collider>();
    //    networkPieceRenderer = GetComponent<Renderer>();
        
    //    if (networkPieceRenderer != null)
    //    {
    //        networkOriginalColor = networkPieceRenderer.material.color;
    //    }
        
    //    // Tìm camera
    //    playerCamera = Camera.main;
    //    if (playerCamera == null)
    //    {
    //        playerCamera = FindFirstObjectByType<Camera>();
    //    }
        
    //    // Khởi tạo network variables
    //    if (photonView != null)
    //    {
    //        isNetworked = true;
    //        networkDragPosition = transform.position;
    //        networkIsDragged = false;
    //        networkIsHovered = false;
    //    }
    //}

    //protected void Update()
    //{
    //    // Không gọi base.Update() vì PieceController không có Update()
        
    //    // Chỉ xử lý input nếu là quân cờ của mình
    //    if (photonView != null && !photonView.IsMine) return;
        
    //    HandleInput();
    //    UpdateVisualFeedback();
    //}

    //private void HandleInput()
    //{
    //    // Kiểm tra click chuột
    //    if (Input.GetMouseButtonDown(0))
    //    {
    //        if (IsMouseOverPiece())
    //        {
    //            StartDrag();
    //        }
    //    }
        
    //    // Xử lý drag
    //    if (isBeingDragged && Input.GetMouseButton(0))
    //    {
    //        UpdateDrag();
    //    }
    //    else if (isBeingDragged && Input.GetMouseButtonUp(0))
    //    {
    //        EndDrag();
    //    }
        
    //    // Kiểm tra hover
    //    bool currentlyHovered = IsMouseOverPiece();
    //    if (currentlyHovered != isHovered)
    //    {
    //        isHovered = currentlyHovered;
    //        if (isNetworked && photonView.IsMine)
    //        {
    //            photonView.RPC("NetworkSetHovered", RpcTarget.All, isHovered);
    //        }
    //    }
    //}

    //private bool IsMouseOverPiece()
    //{
    //    if (playerCamera == null) return false;
        
    //    Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
    //    RaycastHit hit;
        
    //    if (Physics.Raycast(ray, out hit))
    //    {
    //        return hit.collider == pieceCollider;
    //    }
        
    //    return false;
    //}

    //private void StartDrag()
    //{
    //    if (isMoving) return; // Không thể drag khi đang di chuyển
        
    //    // Chỉ cho phép drag nếu là quân cờ của mình hoặc không phải multiplayer
    //    if (isNetworked && !photonView.IsMine) return;
        
    //    isBeingDragged = true;
    //    lastValidPosition = transform.position;
        
    //    // Tắt physics khi drag
    //    if (pieceRigidbody != null)
    //    {
    //        pieceRigidbody.isKinematic = true;
    //    }
        
    //    // Tính toán offset
    //    Vector3 mouseWorldPos = GetMouseWorldPosition();
    //    dragOffset = transform.position - mouseWorldPos;
        
    //    // Gửi RPC để thông báo bắt đầu drag
    //    if (isNetworked && photonView.IsMine)
    //    {
    //        photonView.RPC("NetworkStartDrag", RpcTarget.All, transform.position);
    //    }
        
    //    Debug.Log($"Bắt đầu drag quân cờ {playerColor}");
    //}

    //private void UpdateDrag()
    //{
    //    Vector3 mouseWorldPos = GetMouseWorldPosition();
    //    Vector3 targetPosition = mouseWorldPos + dragOffset;
        
    //    // Giới hạn chiều cao drag
    //    targetPosition.y = Mathf.Max(targetPosition.y, dragHeight);
        
    //    transform.position = targetPosition;
        
    //    // Gửi RPC để đồng bộ vị trí drag
    //    if (isNetworked && photonView.IsMine)
    //    {
    //        photonView.RPC("NetworkUpdateDragPosition", RpcTarget.All, targetPosition);
    //    }
    //}

    //private void EndDrag()
    //{
    //    isBeingDragged = false;
        
    //    // Kiểm tra vị trí hợp lệ
    //    if (IsValidDropPosition())
    //    {
    //        // Vị trí hợp lệ - giữ nguyên vị trí
    //        lastValidPosition = transform.position;
            
    //        // Gửi RPC để thông báo kết thúc drag thành công
    //        if (isNetworked && photonView.IsMine)
    //        {
    //            photonView.RPC("NetworkEndDrag", RpcTarget.All, transform.position, true);
    //        }
            
    //        Debug.Log($"Kết thúc drag quân cờ {playerColor} tại vị trí hợp lệ");
    //    }
    //    else
    //    {
    //        // Vị trí không hợp lệ - trở về vị trí cũ
    //        transform.position = lastValidPosition;
            
    //        // Gửi RPC để thông báo kết thúc drag thất bại
    //        if (isNetworked && photonView.IsMine)
    //        {
    //            photonView.RPC("NetworkEndDrag", RpcTarget.All, lastValidPosition, false);
    //        }
            
    //        Debug.Log($"Kết thúc drag quân cờ {playerColor} - trở về vị trí cũ");
    //    }
        
    //    // Bật lại physics
    //    if (pieceRigidbody != null)
    //    {
    //        pieceRigidbody.isKinematic = false;
    //    }
    //}

    //private bool IsValidDropPosition()
    //{
    //    // Kiểm tra xem có thể đặt quân cờ tại vị trí này không
    //    // Có thể thêm logic kiểm tra theo luật Ludo ở đây
        
    //    // Kiểm tra có chạm đất không
    //    RaycastHit hit;
    //    if (Physics.Raycast(transform.position, Vector3.down, out hit, 10f, groundLayer))
    //    {
    //        // Kiểm tra có phải là bàn cờ không
    //        if (hit.collider.CompareTag("Table"))
    //        {
    //            return true;
    //        }
    //    }
        
    //    return false;
    //}

    //private Vector3 GetMouseWorldPosition()
    //{
    //    if (playerCamera == null) return Vector3.zero;
        
    //    Vector3 mousePos = Input.mousePosition;
    //    mousePos.z = playerCamera.WorldToScreenPoint(transform.position).z;
    //    return playerCamera.ScreenToWorldPoint(mousePos);
    //}

    //private void UpdateVisualFeedback()
    //{
    //    if (networkPieceRenderer == null) return;
        
    //    if (isBeingDragged)
    //    {
    //        networkPieceRenderer.material.color = dragColor;
    //    }
    //    else if (isHovered)
    //    {
    //        networkPieceRenderer.material.color = hoverColor;
    //    }
    //    else
    //    {
    //        networkPieceRenderer.material.color = networkOriginalColor;
    //    }
        
    //    // Hiển thị/ẩn highlight effect
    //    if (highlightEffect != null)
    //    {
    //        highlightEffect.SetActive(isHovered || isBeingDragged);
    //    }
    //}

    //// Network RPC Methods
    //[PunRPC]
    //public void NetworkStartDrag(Vector3 position)
    //{
    //    if (!photonView.IsMine)
    //    {
    //        isBeingDragged = true;
    //        transform.position = position;
    //    }
    //}

    //[PunRPC]
    //public void NetworkUpdateDragPosition(Vector3 position)
    //{
    //    if (!photonView.IsMine)
    //    {
    //        transform.position = position;
    //    }
    //}

    //[PunRPC]
    //public void NetworkEndDrag(Vector3 position, bool isValid)
    //{
    //    if (!photonView.IsMine)
    //    {
    //        isBeingDragged = false;
    //        transform.position = position;
    //    }
    //}

    //[PunRPC]
    //public void NetworkSetHovered(bool hovered)
    //{
    //    if (!photonView.IsMine)
    //    {
    //        isHovered = hovered;
    //    }
    //}

    //// Override OnPhotonSerializeView để thêm drag data
    //public override void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    //{
    //    base.OnPhotonSerializeView(stream, info);
        
    //    if (stream.IsWriting)
    //    {
    //        // Gửi thêm dữ liệu drag
    //        stream.SendNext(isBeingDragged);
    //        stream.SendNext(isHovered);
    //        stream.SendNext(transform.position);
    //    }
    //    else
    //    {
    //        // Nhận thêm dữ liệu drag
    //        networkIsDragged = (bool)stream.ReceiveNext();
    //        networkIsHovered = (bool)stream.ReceiveNext();
    //        networkDragPosition = (Vector3)stream.ReceiveNext();
            
    //        // Cập nhật nếu không phải là quân cờ của mình
    //        if (!photonView.IsMine)
    //        {
    //            if (networkIsDragged != isBeingDragged)
    //            {
    //                isBeingDragged = networkIsDragged;
    //            }
                
    //            if (networkIsHovered != isHovered)
    //            {
    //                isHovered = networkIsHovered;
    //            }
                
    //            if (Vector3.Distance(transform.position, networkDragPosition) > 0.1f)
    //            {
    //                transform.position = Vector3.Lerp(transform.position, networkDragPosition, Time.deltaTime * 10f);
    //            }
    //        }
    //    }
    //}

    //// Public methods để kiểm tra trạng thái
    //public bool IsBeingDragged()
    //{
    //    return isBeingDragged;
    //}

    //public bool IsHovered()
    //{
    //    return isHovered;
    //}

    //// Override để tắt drag khi đang di chuyển
    //// Override để xử lý sắp xếp trong môi trường network
    //protected override void MoveLocal(int steps)
    //{
    //    Debug.Log($"[NETWORK] MoveLocal called for {playerColor} piece, steps: {steps}");

    //    if (isMoving) return;

    //    // Nếu đang drag thì kết thúc drag trước
    //    if (isBeingDragged)
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
    //    if (isNetworked && photonView.IsMine)
    //    {
    //        photonView.RPC("NetworkMove", RpcTarget.All, steps);
    //    }
    //    else
    //    {
    //        // Chạy local nếu không có PUN
    //        StartCoroutine(MoveStepByStep(steps));
    //    }
    //}


    
}
