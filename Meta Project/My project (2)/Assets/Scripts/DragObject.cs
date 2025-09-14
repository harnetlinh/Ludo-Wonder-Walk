using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class DragObject : MonoBehaviourPun
{
    private Vector3 mOffset;
    private float mZCoord;
    private bool isDragging = false;
    private GameObject draggedObject;
    private Rigidbody draggedRigidbody;
    private bool wasKinematic;
    private float originalLinearDamping;
    private PlayerColor draggedPieceColor;
    private Vector3 originalPosition;

    void Update()
    {
        // Chỉ xử lý input nếu là master client hoặc không có PUN
        if (photonView != null && !photonView.IsMine) return;
        
        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            
            // Tìm camera chính (có thể không phải Camera.main)
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                mainCamera = FindFirstObjectByType<Camera>();
            }
            
            if (mainCamera == null)
            {
                Debug.LogWarning("No camera found for raycast!");
                return;
            }
            
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            Debug.Log($"Raycast from mouse position: {Input.mousePosition}, Camera: {mainCamera.name}");
            
            // Vẽ ray trong Scene view để debug
            Debug.DrawRay(ray.origin, ray.direction * 100f, Color.red, 1f);

            // Thử raycast với tất cả layers trước
            if (Physics.Raycast(ray, out hit, Mathf.Infinity))
            {
                Debug.Log($"Raycast hit: {hit.collider.name} at position {hit.point}, distance: {hit.distance}");
                
                // Kiểm tra nếu vật thể được click là quân cờ và đúng lượt
                if (hit.collider.CompareTag("Piece"))
                {
                    PieceController pieceController = hit.collider.GetComponent<PieceController>();
                    
                    // Trong multiplayer, chỉ cho phép drag quân cờ của mình
                    if (PhotonNetwork.IsConnected)
                    {
                        if (pieceController != null)
                        {
                            // Debug thông tin
                            Debug.Log($"Multiplayer drag check: Piece color = {pieceController.playerColor}, Current player = {GameTurnManager.Instance.CurrentPlayer}");
                            
                            // Chỉ cho phép drag quân cờ của lượt hiện tại
                            if (pieceController.playerColor != GameTurnManager.Instance.CurrentPlayer)
                            {
                                Debug.Log("Cannot drag - not your turn");
                                return;
                            }
                        }
                    }
                    
                    // Bỏ qua nếu có NetworkPieceController (để NetworkPieceController xử lý)
                    if (hit.collider.GetComponent<NetworkPieceController>() != null)
                    {
                        return;
                    }
                    
                    if (pieceController != null)
                    {
                        draggedPieceColor = pieceController.playerColor;
                        if (draggedPieceColor == GameTurnManager.Instance.CurrentPlayer)
                        {
                            draggedObject = hit.collider.gameObject;
                            draggedRigidbody = draggedObject.GetComponent<Rigidbody>();
                            originalPosition = draggedObject.transform.position;

                            if (draggedRigidbody != null)
                            {
                                wasKinematic = draggedRigidbody.isKinematic;
                                originalLinearDamping = draggedRigidbody.linearDamping;

                                draggedRigidbody.isKinematic = true;
                                draggedRigidbody.linearDamping = Mathf.Infinity;
                            }

                            StartDragging(draggedObject);
                            isDragging = true;
                            
                            // Gửi RPC để thông báo bắt đầu drag
                            if (photonView != null)
                            {
                                photonView.RPC("NetworkStartDrag", RpcTarget.All, draggedObject.transform.position);
                            }
                        }
                    }
                }
            }
        }

        if (isDragging && Input.GetMouseButton(0))
        {
            if (draggedObject != null)
            {
                Vector3 newPosition = GetMouseWorldPos() + mOffset;
                draggedObject.transform.position = newPosition;
                
                // Gửi RPC để đồng bộ vị trí drag
                if (photonView != null)
                {
                    photonView.RPC("NetworkUpdateDragPosition", RpcTarget.All, newPosition);
                }
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (isDragging && draggedObject != null)
            {
                RaycastHit hit;
                bool isValidDrop = Physics.Raycast(draggedObject.transform.position, Vector3.down, out hit) && hit.collider.CompareTag("Table");
                
                if (!isValidDrop)
                {
                    // Vị trí không hợp lệ - trở về vị trí cũ
                    draggedObject.transform.position = originalPosition;
                }

                // Khôi phục trạng thái physics
                if (draggedRigidbody != null)
                {
                    draggedRigidbody.isKinematic = wasKinematic;
                    draggedRigidbody.linearDamping = originalLinearDamping;
                    draggedRigidbody.linearVelocity = Vector3.zero;
                    draggedRigidbody.angularVelocity = Vector3.zero;
                }

                // Gửi RPC để thông báo kết thúc drag
                if (photonView != null)
                {
                    photonView.RPC("NetworkEndDrag", RpcTarget.All, draggedObject.transform.position, isValidDrop);
                }

                isDragging = false;
                draggedObject = null;
                draggedRigidbody = null;
            }
        }
    }






    private void StartDragging(GameObject obj)
    {
        mZCoord = Camera.main.WorldToScreenPoint(obj.transform.position).z;
        mOffset = obj.transform.position - GetMouseWorldPos();
    }

    private Vector3 GetMouseWorldPos()
    {
        Vector3 mousePoint = Input.mousePosition;
        mousePoint.z = mZCoord;
        return Camera.main.ScreenToWorldPoint(mousePoint);
    }

    // Network RPC Methods
    [PunRPC]
    public void NetworkStartDrag(Vector3 position)
    {
        if (!photonView.IsMine && draggedObject != null)
        {
            draggedObject.transform.position = position;
        }
    }

    [PunRPC]
    public void NetworkUpdateDragPosition(Vector3 position)
    {
        if (!photonView.IsMine && draggedObject != null)
        {
            draggedObject.transform.position = position;
        }
    }

    [PunRPC]
    public void NetworkEndDrag(Vector3 position, bool isValid)
    {
        if (!photonView.IsMine && draggedObject != null)
        {
            draggedObject.transform.position = position;
        }
    }
}