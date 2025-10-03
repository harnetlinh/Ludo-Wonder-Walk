using UnityEngine;
using Photon.Pun;

public class SimpleDragOnly : MonoBehaviourPun
{
    private bool isDragging = false;
    private Vector3 offset;
    private Camera mainCamera;
    
    void Start()
    {
        mainCamera = Camera.main;
        Debug.Log($"SimpleDragOnly ready for {gameObject.name}");
    }

    void Update()
    {
        // Chỉ xử lý input - không quan tâm ownership
        HandleInput();
    }

    void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit) && hit.collider.gameObject == this.gameObject)
            {
                // Yêu cầu ownership để có thể di chuyển
                if (!photonView.IsMine)
                {
                    photonView.RequestOwnership();
                }
                
                isDragging = true;
                Vector3 mousePos = Input.mousePosition;
                mousePos.z = Vector3.Distance(mainCamera.transform.position, transform.position);
                Vector3 worldPos = mainCamera.ScreenToWorldPoint(mousePos);
                offset = transform.position - worldPos;
                
                Debug.Log($"Started dragging by {PhotonNetwork.NickName}");
            }
        }

        if (isDragging && Input.GetMouseButton(0))
        {
            // Chỉ di chuyển nếu có ownership
            if (photonView.IsMine)
            {
                Vector3 mousePos = Input.mousePosition;
                mousePos.z = Vector3.Distance(mainCamera.transform.position, transform.position);
                Vector3 worldPos = mainCamera.ScreenToWorldPoint(mousePos);
                transform.position = worldPos + offset;
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (isDragging)
            {
                Debug.Log($"Stopped dragging by {PhotonNetwork.NickName}");
            }
            isDragging = false;
        }
    }
}
