using UnityEngine;
using Photon.Pun;

// Điều khiển desktop mô phỏng VR: WASD di chuyển, chuột xoay camera
public class DesktopVRController : MonoBehaviourPun
{
    [Header("Di chuyển")]
    public float moveSpeed = 5f;
    public float runSpeed = 8f;
    
    [Header("Xoay camera")]
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 80f;
    
    [Header("Nhảy")]
    public float jumpForce = 8f;
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;
    
    [Header("Tham chiếu")]
    public Transform cameraTransform;
    public Transform bodyTransform;
    
    private Rigidbody rb;
    private bool isGrounded;
    private float xRotation = 0f;
    private Vector2 mouseInput;
    private Vector3 moveDirection;
    
    private void Start()
    {
        // Chỉ điều khiển nếu là của mình
        if (!photonView.IsMine)
        {
            // Tắt camera của người khác để tránh xung đột
            if (cameraTransform != null)
                cameraTransform.gameObject.SetActive(false);
            return;
        }
        
        rb = GetComponent<Rigidbody>();
        
        // Khóa con trỏ chuột vào giữa màn hình
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // Tìm camera nếu chưa gán
        if (cameraTransform == null)
            cameraTransform = Camera.main?.transform;
            
        if (bodyTransform == null)
            bodyTransform = transform;
    }
    
    private void Update()
    {
        if (!photonView.IsMine) return;
        
        HandleMouseLook();
        HandleMovement();
        HandleJump();
        
        // ESC để thoát khóa chuột
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
    
    private void HandleMouseLook()
    {
        // Lấy input chuột
        mouseInput.x = Input.GetAxis("Mouse X") * mouseSensitivity;
        mouseInput.y = Input.GetAxis("Mouse Y") * mouseSensitivity;
        
        // Xoay body theo trục Y (trái/phải)
        bodyTransform.Rotate(Vector3.up * mouseInput.x);
        
        // Xoay camera theo trục X (lên/xuống)
        xRotation -= mouseInput.y;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);
        
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }
    
    private void HandleMovement()
    {
        // Lấy input di chuyển
        float horizontal = Input.GetAxis("Horizontal"); // A/D
        float vertical = Input.GetAxis("Vertical");     // W/S
        
        // Tính hướng di chuyển theo camera
        Vector3 forward = cameraTransform != null ? cameraTransform.forward : transform.forward;
        Vector3 right = cameraTransform != null ? cameraTransform.right : transform.right;
        
        // Bỏ trục Y để di chuyển trên mặt phẳng
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();
        
        // Tính hướng di chuyển cuối cùng
        moveDirection = (forward * vertical + right * horizontal).normalized;
        
        // Chạy nhanh khi giữ Shift
        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : moveSpeed;
        
        // Áp dụng vận tốc (giữ nguyên trục Y để không ảnh hưởng nhảy)
        Vector3 velocity = moveDirection * currentSpeed;
        velocity.y = rb.linearVelocity.y; // Giữ vận tốc Y hiện tại
        rb.linearVelocity = velocity;
    }
    
    private void HandleJump()
    {
        // Kiểm tra đang đứng trên mặt đất
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        
        // Nhảy khi nhấn Space và đang đứng trên đất
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // Vẽ sphere để debug vùng kiểm tra đất
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
        }
    }
    
    private void OnDestroy()
    {
        // Khôi phục con trỏ chuột khi thoát
        if (photonView.IsMine)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
