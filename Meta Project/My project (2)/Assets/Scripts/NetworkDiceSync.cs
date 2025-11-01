using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

[RequireComponent(typeof(PhotonView), typeof(Rigidbody), typeof(PhotonTransformView))]
public class NetworkDiceSync : MonoBehaviourPun, IPunOwnershipCallbacks
{
    [Header("Ownership & Global Rates")]
    public bool useOwnershipTransfer = true;
    public int desiredSendRate = 60;
    public int desiredSerializationRate = 60;

    private Rigidbody rb;
    private PhotonTransformViewClassic transformView;
    private bool isKinematicOverride = false;
    
    // THÊM VÀO NetworkDiceSync.cs
    [Header("Sync Optimization")]
    public float positionSyncThreshold = 0.01f;
    public float rotationSyncThreshold = 1f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        transformView = GetComponent<PhotonTransformViewClassic>();
        PhotonNetwork.AddCallbackTarget(this);

        // Cấu hình Rigidbody để đồng bộ tốt hơn
        if (rb != null)
        {
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Đảm bảo Rigidbody không kinematic mặc định
            if (photonView.IsMine)
            {
                rb.isKinematic = false;
            }
            else
            {
                // Client khác: tạm thời kinematic để tránh vật lý không đồng bộ
                rb.isKinematic = true;
            }
        }

        // Nâng tick rates để gần FPS
        if (desiredSendRate > 0)
        {
            PhotonNetwork.SendRate = desiredSendRate;
        }
        if (desiredSerializationRate > 0)
        {
            PhotonNetwork.SerializationRate = desiredSerializationRate;
        }

        // Master client sở hữu tất cả objects ban đầu
        if (PhotonNetwork.IsMasterClient && !photonView.IsMine)
        {
            photonView.TransferOwnership(PhotonNetwork.LocalPlayer);
        }
    }

    void OnDestroy()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    void FixedUpdate()
    {
        // Chỉ request ownership khi cần thiết, không phải mọi frame
        if (useOwnershipTransfer && !photonView.IsMine && ShouldRequestOwnership())
        {
            photonView.RequestOwnership();
        }
    }

    private bool ShouldRequestOwnership()
    {
        DiceController diceController = DiceController.Instance;
        if (diceController != null && diceController.IsDiceMoving())
        {
            return false;
        }

        // Chỉ request ownership khi người chơi tương tác với xúc xắc
        GrabDice grabDice = GetComponent<GrabDice>();
        if (grabDice != null && grabDice.IsBeingHeld())
        {
            return true;
        }

        // Hoặc khi xúc xắc đang di chuyển mạnh
        if (rb != null && rb.linearVelocity.magnitude > 0.1f)
        {
            return true;
        }

        return false;
    }

    // Ownership callbacks
    public void OnOwnershipRequest(PhotonView targetView, Player requestingPlayer)
    {
        if (targetView == photonView && useOwnershipTransfer)
        {
            // Luôn chấp nhận yêu cầu ownership
            photonView.TransferOwnership(requestingPlayer);
        }
    }

    public void OnOwnershipTransfered(PhotonView targetView, Player previousOwner)
    {
        if (targetView == photonView)
        {
            Debug.Log($"Dice ownership transferred to: {targetView.Owner?.NickName}");

            // Cập nhật trạng thái Rigidbody khi ownership thay đổi
            if (rb != null)
            {
                if (photonView.IsMine)
                {
                    rb.isKinematic = isKinematicOverride;
                }
                else
                {
                    rb.isKinematic = true;
                }
            }
        }
    }

    public void OnOwnershipTransferFailed(PhotonView targetView, Player senderOfFailedRequest)
    {
        Debug.LogWarning($"Dice ownership transfer failed for: {senderOfFailedRequest.NickName}");
    }

    // Phương thức để chuyển ownership ngay lập tức
    public void RequestOwnership()
    {
        if (!photonView.IsMine && useOwnershipTransfer)
        {
            photonView.RequestOwnership();
        }
    }

    // Phương thức để tạm thời override kinematic state
    
// Sửa phương thức SetKinematic để đồng bộ tốt hơn
    public void SetKinematic(bool kinematic, bool isOverride = false)
    {
        if (rb != null)
        {
            if (isOverride)
            {
                isKinematicOverride = kinematic;
            }

            if (photonView.IsMine)
            {
                rb.isKinematic = kinematic;
                // THÊM: Đồng bộ useGravity với kinematic
                rb.useGravity = !kinematic;
            
                if (!kinematic)
                {
                    rb.WakeUp();
                }
            }
        }
    }

    // Gọi khi bắt đầu tương tác với xúc xắc
    // Trong NetworkDiceSync.cs, thêm kiểm tra:
    public void OnStartInteraction()
    {
        // Chỉ xử lý vật lý cho remote clients
        PhotonView photonView = GetComponent<PhotonView>();
        if (photonView != null && !photonView.IsMine)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }
        }
    }

    public void OnEndInteraction()
    {
        // Chỉ xử lý vật lý cho remote clients
        PhotonView photonView = GetComponent<PhotonView>();
        if (photonView != null && !photonView.IsMine)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
            }
        }
    }
    private void ReleaseOwnershipIfNeeded()
    {
        // Chỉ master client mới nên giữ ownership khi không có ai tương tác
        if (photonView.IsMine && !PhotonNetwork.IsMasterClient)
        {
            // Chuyển ownership về master client
            photonView.TransferOwnership(PhotonNetwork.MasterClient);
        }
    }
    
    [PunRPC]
    private void RPC_ForceSync(Vector3 position, Quaternion rotation)
    {
        if (!photonView.IsMine)
        {
            transform.position = position;
            transform.rotation = rotation;
        }
    }
    
    // THÊM VÀO NetworkDiceSync.cs
    public void EnsurePhysicsActivation()
    {
        if (rb != null && photonView.IsMine)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.WakeUp();
        
            // Đảm bảo không có velocity cũ
        }
    }
}
