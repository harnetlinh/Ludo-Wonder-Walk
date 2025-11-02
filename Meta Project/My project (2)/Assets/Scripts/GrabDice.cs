using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Photon.Pun;
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(PhotonView))]
public class GrabDice : MonoBehaviourPun
{
    [Header("Hand Grab Interactors")]
    public HandGrabInteractor leftHand;
    public HandGrabInteractor rightHand;

    private bool isBeingHeld = false;
    private DiceFaceDetector diceDetector;
    private NetworkDiceSync networkDiceSync;
    private HandGrabInteractable diceInteractable;
    private Rigidbody rb;

    // Biến để đồng bộ vị trí với độ chính xác cao
    private Vector3 lastSentPosition;
    private Quaternion lastSentRotation;
    private float lastSendTime;
    public float networkSendRate = 30f;

    // Biến network
    private bool networkIsBeingHeld = false;
    
    [Header("Network Sync Settings")]
    public float networkLerpSpeed = 15f; // Tăng tốc độ lerp
    private Vector3 targetNetworkPosition;
    private Quaternion targetNetworkRotation;
    private bool isNetworkSyncing = false;

    // Biến để quản lý vật lý
    private bool wasKinematicBeforeGrab = false;
    private bool useGravityBeforeGrab = false;

    private void Start()
    {
        diceDetector = GetComponent<DiceFaceDetector>();
        networkDiceSync = GetComponent<NetworkDiceSync>();
        diceInteractable = GetComponent<HandGrabInteractable>();
        rb = GetComponent<Rigidbody>();

        // Đăng ký callback với Photon
        if (photonView != null)
        {
            PhotonNetwork.AddCallbackTarget(this);
        }
    }

    private void OnEnable()
    {
        if (leftHand != null)
            leftHand.WhenStateChanged += OnLeftHandStateChanged;

        if (rightHand != null)
            rightHand.WhenStateChanged += OnRightHandStateChanged;
    }

    private void OnDisable()
    {
        if (leftHand != null)
            leftHand.WhenStateChanged -= OnLeftHandStateChanged;

        if (rightHand != null)
            rightHand.WhenStateChanged -= OnRightHandStateChanged;
    }

    private void OnLeftHandStateChanged(InteractorStateChangeArgs args)
    {
        HandleHandStateChange(args.NewState, "left");
    }

    private void OnRightHandStateChanged(InteractorStateChangeArgs args)
    {
        HandleHandStateChange(args.NewState, "right");
    }

    private void HandleHandStateChange(InteractorState state, string handType)
    {
        // KHÔNG cho phép tương tác nếu dice đang di chuyển
        if (DiceController.Instance != null && DiceController.Instance.IsDiceMoving())
        {
            Debug.Log("Không thể tương tác với dice khi đang di chuyển");
            return;
        }

        // Kiểm tra xem interactor có thực sự chọn xúc xắc này không
        HandGrabInteractor currentHand = handType == "left" ? leftHand : rightHand;
        if (currentHand != null && diceInteractable != null)
        {
            if (currentHand.SelectedInteractable != diceInteractable && state == InteractorState.Select)
            {
                return;
            }
        }

        if (state == InteractorState.Select)
        {
            // Yêu cầu ownership ngay lập tức
            if (photonView != null && !photonView.IsMine)
            {
                photonView.RequestOwnership();
                StartCoroutine(WaitForOwnershipThenGrab());
            }
            else
            {
                StartGrab();
            }

            Debug.Log($"{handType} hand grabbed dice");
        }
        else if (state == InteractorState.Normal && isBeingHeld)
        {
            EndGrab();
            Debug.Log($"{handType} hand released dice");
        }
    }

    private IEnumerator WaitForOwnershipThenGrab()
    {
        float timeout = 1f;
        float elapsed = 0f;

        while (!photonView.IsMine && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (photonView.IsMine)
        {
            StartGrab();
        }
    }

    private void StartGrab()
    {
        // KIỂM TRA: Không cho phép cầm nếu đang di chuyển
        if (DiceController.Instance != null && DiceController.Instance.IsDiceMoving())
        {
            Debug.Log("Không thể cầm xúc xắc khi đang di chuyển giữa các lượt");
            return;
        }
        
        isBeingHeld = true;

        // Lưu trạng thái vật lý trước khi cầm - SỬA ĐỔI
        if (rb != null)
        {
            wasKinematicBeforeGrab = rb.isKinematic;
            useGravityBeforeGrab = rb.useGravity;
        
            // Đảm bảo xúc xắc có vật lý khi được cầm
            rb.isKinematic = true;  // Kinematic để di chuyển mượt
            rb.useGravity = false;  // Tắt gravity khi cầm
        
            // Reset velocities
        }

        // THÊM: Đồng bộ với NetworkDiceSync
        if (networkDiceSync != null)
        {
            networkDiceSync.SetKinematic(true, true);
        }

        // Thông báo bắt đầu tương tác
        if (networkDiceSync != null)
        {
            networkDiceSync.OnStartInteraction();
        }

        // Đánh dấu xúc xắc đã được cầm lên
        if (diceDetector != null)
        {
            diceDetector.isFirstPickup = false;
        }

        // Gửi RPC đồng bộ trạng thái cầm
        if (photonView != null && photonView.IsMine)
        {
            photonView.RPC("RPC_SetHeldState", RpcTarget.Others, true);
        }

        // Lưu vị trí ban đầu
        lastSentPosition = transform.position;
        lastSentRotation = transform.rotation;
        lastSendTime = Time.time;
    }

    private void EndGrab()
    {
        isBeingHeld = false;

        // Khôi phục trạng thái vật lý khi thả
        // KHÔI PHỤC VẬT LÝ KHI THẢ - SỬA QUAN TRỌNG
        if (rb != null)
        {
            // Luôn đảm bảo vật lý được kích hoạt khi thả
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.WakeUp();
        
            // Đảm bảo không có velocity cũ
        }
        // THÊM: Đồng bộ với NetworkDiceSync
        if (networkDiceSync != null)
        {
            networkDiceSync.SetKinematic(false, true);
            networkDiceSync.EnsurePhysicsActivation();
        }

        // Thông báo kết thúc tương tác
        if (networkDiceSync != null)
        {
            networkDiceSync.OnEndInteraction();
        }

        

        // Gửi RPC đồng bộ trạng thái thả
        if (photonView != null && photonView.IsMine)
        {
            photonView.RPC("RPC_SetHeldState", RpcTarget.Others, false);
            // Gửi vị trí cuối cùng
            photonView.RPC("RPC_UpdateGrabbedPosition", RpcTarget.Others,
                transform.position, transform.rotation);
        }

        // Ownership will be returned to the master once the turn flow requests it.
    }

    private void Update()
    {
        // Đồng bộ vị trí liên tục khi đang cầm
        if (isBeingHeld && photonView != null && photonView.IsMine)
        {
            // Gửi theo tần suất cố định
            if (Time.time - lastSendTime >= 1f / networkSendRate)
            {
                // Chỉ gửi nếu có thay đổi đáng kể
                if (Vector3.Distance(transform.position, lastSentPosition) > 0.001f ||
                    Quaternion.Angle(transform.rotation, lastSentRotation) > 0.1f)
                {
                    photonView.RPC("RPC_UpdateGrabbedPosition", RpcTarget.Others,
                        transform.position, transform.rotation);

                    lastSentPosition = transform.position;
                    lastSentRotation = transform.rotation;
                    lastSendTime = Time.time;
                }
            }
        }

        // Đồng bộ mượt cho non-owner - SỬ DỤNG CÁCH TIẾP CẬN GIỐNG PIECE
        if (photonView != null && !photonView.IsMine && isNetworkSyncing)
        {
            // Áp dụng ngay lập tức không lerp khi đang được cầm
            if (networkIsBeingHeld)
            {
                transform.position = targetNetworkPosition;
                transform.rotation = targetNetworkRotation;
            }
            else
            {
                // Chỉ lerp khi không được cầm
                float lerpFactor = Time.deltaTime * networkLerpSpeed;
                transform.position = Vector3.Lerp(transform.position, targetNetworkPosition, lerpFactor);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetNetworkRotation, lerpFactor);
            }
        }
    }

    [PunRPC]
    private void RPC_SetHeldState(bool heldState)
    {
        networkIsBeingHeld = heldState;
    
        // Xử lý vật lý cho remote clients
        if (photonView != null && !photonView.IsMine && rb != null)
        {
            if (heldState)
            {
                // Khi bắt đầu cầm - lưu trạng thái và đặt kinematic
                wasKinematicBeforeGrab = rb.isKinematic;
                useGravityBeforeGrab = rb.useGravity;
                
                rb.isKinematic = true;
                rb.useGravity = false;
                
                // Reset velocities
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            else
            {
                // Khi thả - khôi phục trạng thái vật lý
                rb.isKinematic = wasKinematicBeforeGrab;
                rb.useGravity = useGravityBeforeGrab;
                
                // Đảm bảo vật lý được kích hoạt
                if (!rb.isKinematic)
                {
                    rb.WakeUp();
                }
            }
        }

        // Reset interpolation khi bắt đầu/tạm dừng đồng bộ
        if (photonView != null && !photonView.IsMine)
        {
            if (heldState)
            {
                // Bắt đầu cầm - chuẩn bị interpolation
                targetNetworkPosition = transform.position;
                targetNetworkRotation = transform.rotation;
                isNetworkSyncing = true;
            }
            else
            {
                // Kết thúc cầm - giữ interpolation một lúc rồi tắt
                Invoke("StopNetworkSync", 0.5f);
            }
        }
    }

    [PunRPC]
    private void RPC_UpdateGrabbedPosition(Vector3 position, Quaternion rotation)
    {
        if (photonView != null && !photonView.IsMine)
        {
            targetNetworkPosition = position;
            targetNetworkRotation = rotation;
            isNetworkSyncing = true;
        }
    }

    private void StopNetworkSync()
    {
        isNetworkSyncing = false;
    }

    public bool IsBeingHeld()
    {
        return isBeingHeld;
    }

    // Đồng bộ trạng thái cho người chơi mới
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Gửi trạng thái hiện tại
            stream.SendNext(isBeingHeld);
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
        }
        else
        {
            // Nhận trạng thái từ master client
            bool receivedHeldState = (bool)stream.ReceiveNext();
            Vector3 receivedPosition = (Vector3)stream.ReceiveNext();
            Quaternion receivedRotation = (Quaternion)stream.ReceiveNext();
            
            if (receivedHeldState != networkIsBeingHeld)
            {
                networkIsBeingHeld = receivedHeldState;
                RPC_SetHeldState(receivedHeldState); // Gọi lại RPC để xử lý vật lý
            }
            
            // Cập nhật vị trí cho remote clients
            if (photonView != null && !photonView.IsMine)
            {
                targetNetworkPosition = receivedPosition;
                targetNetworkRotation = receivedRotation;
                isNetworkSyncing = true;
            }
        }
    }

    private void OnDestroy()
    {
        if (photonView != null)
        {
            PhotonNetwork.RemoveCallbackTarget(this);
        }
    }

    // Phương thức để force sync cho client mới
    [PunRPC]
    public void SyncGrabState(bool heldState, bool isFirstPickup)
    {
        isBeingHeld = heldState;
        networkIsBeingHeld = heldState;

        if (diceDetector != null)
        {
            diceDetector.isFirstPickup = isFirstPickup;
        }
    }

    // Gọi khi client mới vào phòng
    public void RequestStateSync()
    {
        if (photonView != null && photonView.IsMine)
        {
            photonView.RPC("SyncGrabState", RpcTarget.Others, isBeingHeld,
                diceDetector != null ? diceDetector.isFirstPickup : true);
        }
    }
}
