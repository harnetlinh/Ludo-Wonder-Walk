// Thêm vào file GrabDice.cs
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class GrabDice : MonoBehaviourPun
{
    [Header("Kéo HandGrabInteractor từ RealHand vào đây")]
    public HandGrabInteractor handGrabInteractor;

    private bool isBeingHeld = false;
    private DiceFaceDetector diceDetector;
    private NetworkDiceSync networkDiceSync;

    // Thêm biến để đồng bộ trạng thái cầm
    private bool networkIsBeingHeld = false;

    private void Start()
    {
        diceDetector = GetComponent<DiceFaceDetector>();
        networkDiceSync = GetComponent<NetworkDiceSync>();

        // Đăng ký callback với Photon
        if (photonView != null)
        {
            PhotonNetwork.AddCallbackTarget(this);
        }
    }

    private void OnDestroy()
    {
        if (photonView != null)
        {
            PhotonNetwork.RemoveCallbackTarget(this);
        }
    }

    private void OnEnable()
    {
        if (handGrabInteractor != null)
            handGrabInteractor.WhenStateChanged += OnGrabStateChanged;
    }

    private void OnDisable()
    {
        if (handGrabInteractor != null)
            handGrabInteractor.WhenStateChanged -= OnGrabStateChanged;
    }

    private void OnGrabStateChanged(InteractorStateChangeArgs args)
    {
        if (args.NewState == InteractorState.Select)
        {
            SetHeldState(true);

            // Gọi RPC để đồng bộ với tất cả client
            if (photonView != null && photonView.IsMine)
            {
                photonView.RPC("RPC_SetHeldState", RpcTarget.Others, true);
            }
        }
        else if (args.NewState == InteractorState.Normal)
        {
            SetHeldState(false);

            // Gọi RPC để đồng bộ với tất cả client
            if (photonView != null && photonView.IsMine)
            {
                photonView.RPC("RPC_SetHeldState", RpcTarget.Others, false);
            }
        }
    }

    [PunRPC]
    private void RPC_SetHeldState(bool heldState)
    {
        // Cập nhật trạng thái từ network
        networkIsBeingHeld = heldState;

        // Nếu không phải client local, cập nhật trạng thái
        if (!photonView.IsMine)
        {
            SetHeldState(heldState);
        }
    }

    private void SetHeldState(bool heldState)
    {
        isBeingHeld = heldState;

        if (heldState)
        {
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

            // Thông báo cho DiceController
            if (DiceController.Instance != null && !DiceController.Instance.hasRolledThisTurn)
            {
                DiceController.Instance.PrepareToRoll();
                DiceController.Instance.UpdateDiceStatus(true);
            }
        }
        else
        {
            // Thông báo kết thúc tương tác
            if (networkDiceSync != null)
            {
                networkDiceSync.OnEndInteraction();
            }

            if (DiceController.Instance != null)
            {
                DiceController.Instance.UpdateDiceStatus(false);
            }
        }
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
        }
        else
        {
            // Nhận trạng thái từ master client
            bool receivedHeldState = (bool)stream.ReceiveNext();
            if (receivedHeldState != networkIsBeingHeld)
            {
                networkIsBeingHeld = receivedHeldState;
                SetHeldState(receivedHeldState);
            }
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