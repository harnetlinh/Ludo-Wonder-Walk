using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEngine;
using Photon.Pun;
using System.Collections;

public class GrabPiece : MonoBehaviourPun
{
    [Header("Hand Grab Interactors")]
    public HandGrabInteractor leftHand;
    public HandGrabInteractor rightHand;

    private PositionOptimizer positionOptimizer;
    private PieceController pieceController;
    private bool wasGrabbed = false;
    private bool isNetworked = false;

    // Biến để đồng bộ vị trí với độ chính xác cao
    private Vector3 lastSentPosition;
    private Quaternion lastSentRotation;
    private float lastSendTime;
    public float networkSendRate = 30f; // Gửi 30 lần/giây

    private void Awake()
    {
        positionOptimizer = GetComponent<PositionOptimizer>();
        pieceController = GetComponent<PieceController>();

        if (pieceController != null)
        {
            isNetworked = pieceController.isOnlineMode && photonView != null;
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

        if (args.NewState == InteractorState.Select)
            Debug.Log("✋ Tay trái đang cầm vật!");
        else if (args.NewState == InteractorState.Normal)
            Debug.Log("✋ Tay trái đã thả vật.");
    }

    private void OnRightHandStateChanged(InteractorStateChangeArgs args)
    {
        HandleHandStateChange(args.NewState, "right");

        if (args.NewState == InteractorState.Select)
            Debug.Log("🤚 Tay phải đang cầm vật!");
        else if (args.NewState == InteractorState.Normal)
            Debug.Log("🤚 Tay phải đã thả vật.");
    }

    // Trong GrabPiece.cs

    private void HandleHandStateChange(InteractorState state, string handType)
    {
        if (positionOptimizer == null || pieceController == null) return;

        // KIỂM TRA: Nếu dice đang di chuyển, không cho phép thay đổi trạng thái game
        if (DiceController.Instance != null && DiceController.Instance.IsDiceMoving())
        {
            Debug.Log("Dice đang di chuyển, tạm thời không xử lý tương tác với piece");
            return;
        }

        // THÊM KIỂM TRA QUAN TRỌNG: Không cho phép chuẩn bị roll khi đang cầm piece
        if (state == InteractorState.Select)
        {
            // Yêu cầu ownership ngay lập tức
            if (isNetworked && !photonView.IsMine)
            {
                photonView.RequestOwnership();
                StartCoroutine(WaitForOwnershipThenGrab());
            }
            else
            {
                StartGrab();
            }

            Debug.Log($"{handType} hand grabbed {gameObject.name}");
        }
        else if (state == InteractorState.Normal && wasGrabbed)
        {
            EndGrab();
            Debug.Log($"{handType} hand released {gameObject.name}");
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
        wasGrabbed = true;

        // Tắt PositionOptimizer
        if (positionOptimizer != null)
        {
            positionOptimizer.enabled = false;
            positionOptimizer.SetIsBeingHandled(true);
        }

        // Thông báo cho PieceController
        pieceController.SetGrabbedState(true);

        // Gửi RPC đồng bộ trạng thái cầm
        if (isNetworked && photonView.IsMine)
        {
            photonView.RPC("RPC_SetGrabbedState", RpcTarget.Others, true);
        }

        // Lưu vị trí ban đầu
        lastSentPosition = transform.position;
        lastSentRotation = transform.rotation;
        lastSendTime = Time.time;
    }

    private void EndGrab()
    {
        wasGrabbed = false;

        // THÊM: Đánh dấu đã thả để PositionOptimizer biết
        if (positionOptimizer != null)
        {
            positionOptimizer.OnPieceReleased();
        }

        // ĐẢM BẢO VẬT LÝ ĐƯỢC KÍCH HOẠT ĐÚNG CÁCH
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;

            // Đảm bảo vật lý được kích hoạt
            StartCoroutine(ActivatePhysicsAfterRelease());
        }

        // Thông báo cho PieceController
        pieceController.SetGrabbedState(false);

        // Gửi RPC đồng bộ trạng thái thả
        if (isNetworked && photonView.IsMine)
        {
            photonView.RPC("RPC_SetGrabbedState", RpcTarget.Others, false);
            // Gửi vị trí cuối cùng
            photonView.RPC("RPC_UpdateGrabbedPosition", RpcTarget.Others,
                transform.position, transform.rotation);
        }

        // Bật lại PositionOptimizer sau delay
        if (positionOptimizer != null)
        {
            positionOptimizer.SetIsBeingHandled(false);
            StartCoroutine(EnablePositionOptimizerAfterDelay(0.2f)); // Tăng delay lên 0.2s
        }
    }

    // Thêm coroutine mới để kích hoạt vật lý
    private IEnumerator ActivatePhysicsAfterRelease()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Đợi 1 frame để đảm bảo mọi thứ đã sẵn sàng
            yield return new WaitForEndOfFrame();

            // Kích hoạt lại vật lý
            rb.isKinematic = false;
            rb.useGravity = true;

            //// Đảm bảo không có velocity cũ
            //rb.linearVelocity = Vector3.zero;
            //rb.angularVelocity = Vector3.zero;

            // Wake up rigidbody nếu nó đang sleep
            rb.WakeUp();
        }
    }

    private void Update()
    {
        // Đồng bộ vị trí liên tục khi đang cầm
        if (wasGrabbed && isNetworked && photonView.IsMine)
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
    }

    [PunRPC]
    private void RPC_SetGrabbedState(bool isGrabbed)
    {
        if (positionOptimizer != null)
        {
            if (isGrabbed)
            {
                positionOptimizer.enabled = false;
                positionOptimizer.SetIsBeingHandled(true);
            }
            else
            {
                positionOptimizer.SetIsBeingHandled(false);
                StartCoroutine(EnablePositionOptimizerAfterDelay(0.2f));
            }
        }

        if (pieceController != null)
        {
            pieceController.SetGrabbedState(isGrabbed);
        }
    }

    [PunRPC]
    private void RPC_UpdateGrabbedPosition(Vector3 position, Quaternion rotation)
    {
        // Áp dụng ngay lập tức không lerp
        if (!photonView.IsMine && pieceController != null && pieceController.isVRGrabbed)
        {
            transform.position = position;
            transform.rotation = rotation;
        }
    }

    private IEnumerator EnablePositionOptimizerAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (positionOptimizer != null && !wasGrabbed)
        {
            positionOptimizer.enabled = true;
        }
    }
}