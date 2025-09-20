using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

[RequireComponent(typeof(PhotonView), typeof(Rigidbody))]
public class NetworkDiceSync : MonoBehaviourPun, IPunObservable
{
    [Header("Network Sync Settings")]
    public float positionLerpSpeed = 10f;
    public float rotationLerpSpeed = 10f;
    public float velocityThreshold = 0.1f;

    private Rigidbody rb;
    private Vector3 networkPosition;
    private Quaternion networkRotation;
    private Vector3 networkVelocity;
    private Vector3 networkAngularVelocity;
    private bool isBeingHeld = false;

    // Tham chiếu đến GrabDice để kiểm tra trạng thái cầm xúc xắc
    private GrabDice grabDice;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        grabDice = GetComponent<GrabDice>();

        // Khởi tạo network variables
        networkPosition = transform.position;
        networkRotation = transform.rotation;
        networkVelocity = Vector3.zero;
        networkAngularVelocity = Vector3.zero;

        // Cải thiện cài đặt vật lý để đồng bộ mượt mà hơn
        if (rb != null)
        {
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        // Đặt tốc độ đồng bộ cao hơn trong Photon
        if (photonView != null)
        {
            photonView.Synchronization = ViewSynchronization.UnreliableOnChange;
            photonView.ObservedComponents.Clear();
            photonView.ObservedComponents.Add(this);
        }
    }

    void Update()
    {
        // Chỉ xử lý đồng bộ nếu không phải là client điều khiển object này
        if (!photonView.IsMine)
        {
            SmoothSync();
        }
    }

    void FixedUpdate()
    {
        // Nếu là client điều khiển, gửi thông tin vật lý
        if (photonView.IsMine)
        {
            UpdateNetworkPhysics();
        }
    }

    //private void UpdateNetworkPhysics()
    //{
    //    // Kiểm tra nếu xúc xắc đang được cầm
    //    bool currentlyHeld = (grabDice != null && grabDice.IsBeingHeld());

    //    // Nếu trạng thái thay đổi, gửi RPC
    //    if (currentlyHeld != isBeingHeld)
    //    {
    //        isBeingHeld = currentlyHeld;
    //        photonView.RPC("RPC_SetHeldState", RpcTarget.Others, isBeingHeld);
    //    }
    //}

    // Trong phương thức SmoothSync()
    private void SmoothSync()
    {
        // Nếu đang được cầm, sử dụng interpolation vị trí thông thường
        if (isBeingHeld)
        {
            transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * positionLerpSpeed);
            transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation, Time.deltaTime * rotationLerpSpeed);
        }
        else
        {
            // Đối với vật thể vật lý không được cầm, sử dụng interpolation vật lý
            if (rb != null && !rb.isKinematic)
            {
                // Sử dụng Velocity-based interpolation để mượt mà hơn
                Vector3 targetVelocity = (networkPosition - transform.position) * positionLerpSpeed;
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, Time.deltaTime * 5f);

                // Đồng bộ xoay thông qua angular velocity
                Quaternion rotationDiff = networkRotation * Quaternion.Inverse(transform.rotation);
                rotationDiff.ToAngleAxis(out float angle, out Vector3 axis);

                if (angle > 180f) angle -= 360f;
                if (Mathf.Abs(angle) > 0.5f)
                {
                    Vector3 angularVelocity = (axis * angle * Mathf.Deg2Rad) * rotationLerpSpeed;
                    rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, angularVelocity, Time.deltaTime * 5f);
                }
            }
            else
            {
                // Fallback: interpolation thông thường
                transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * positionLerpSpeed);
                transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation, Time.deltaTime * rotationLerpSpeed);
            }
        }
    }

    // Thêm phương thức để cập nhật vật lý mượt mà hơn
    private void UpdateNetworkPhysics()
    {
        // Kiểm tra nếu xúc xắc đang được cầm
        bool currentlyHeld = (grabDice != null && grabDice.IsBeingHeld());

        // Nếu trạng thái thay đổi, gửi RPC
        if (currentlyHeld != isBeingHeld)
        {
            isBeingHeld = currentlyHeld;
            photonView.RPC("RPC_SetHeldState", RpcTarget.Others, isBeingHeld);

            // Đồng bộ ngay lập tức khi thay đổi trạng thái
            if (photonView.IsMine)
            {
                photonView.RPC("RPC_ForceSync", RpcTarget.Others,
                    transform.position,
                    transform.rotation,
                    rb != null ? rb.linearVelocity : Vector3.zero,
                    rb != null ? rb.angularVelocity : Vector3.zero);
            }
        }

        // Gửi update thường xuyên hơn cho vật thể đang di chuyển
        if (photonView.IsMine && rb != null && rb.linearVelocity.magnitude > 0.1f)
        {
            // Gửi update vật lý mỗi 0.1s cho vật thể đang di chuyển nhanh
            if (Time.frameCount % 6 == 0) // ~10 lần/giây
            {
                photonView.RPC("RPC_UpdatePhysics", RpcTarget.Others,
                    transform.position,
                    transform.rotation,
                    rb.linearVelocity,
                    rb.angularVelocity);
            }
        }
    }

    [PunRPC]
    private void RPC_UpdatePhysics(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
    {
        if (!photonView.IsMine)
        {
            networkPosition = position;
            networkRotation = rotation;
            networkVelocity = velocity;
            networkAngularVelocity = angularVelocity;

            // Áp dụng ngay lập tức cho vật thể vật lý
            if (rb != null && !isBeingHeld)
            {
                rb.linearVelocity = velocity;
                rb.angularVelocity = angularVelocity;
            }
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Gửi dữ liệu đến các client khác
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);

            // Gửi thông tin vật lý
            if (rb != null)
            {
                stream.SendNext(rb.linearVelocity);
                stream.SendNext(rb.angularVelocity);
                stream.SendNext(isBeingHeld);
            }
        }
        else
        {
            // Nhận dữ liệu từ master client
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();

            if (rb != null)
            {
                networkVelocity = (Vector3)stream.ReceiveNext();
                networkAngularVelocity = (Vector3)stream.ReceiveNext();
                isBeingHeld = (bool)stream.ReceiveNext();

                // Nếu đang được cầm, tắt vật lý tạm thời
                if (isBeingHeld)
                {
                    rb.isKinematic = true;
                }
                else
                {
                    rb.isKinematic = false;
                }
            }
        }
    }

    [PunRPC]
    private void RPC_SetHeldState(bool heldState)
    {
        isBeingHeld = heldState;

        if (rb != null)
        {
            rb.isKinematic = heldState;

            // Nếu vừa được thả ra, áp dụng velocity từ network
            if (!heldState)
            {
                rb.linearVelocity = networkVelocity;
                rb.angularVelocity = networkAngularVelocity;
            }
        }
    }

    // Phương thức để force sync khi cần thiết (ví dụ khi di chuyển xúc xắc đến người chơi)
    [PunRPC]
    public void RPC_ForceSync(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
    {
        networkPosition = position;
        networkRotation = rotation;
        networkVelocity = velocity;
        networkAngularVelocity = angularVelocity;

        transform.position = position;
        transform.rotation = rotation;

        if (rb != null)
        {
            rb.linearVelocity = velocity;
            rb.angularVelocity = angularVelocity;
        }
    }

    // Gọi phương thức này khi di chuyển xúc xắc đến người chơi
    public void ForceNetworkSync()
    {
        if (photonView.IsMine)
        {
            photonView.RPC("RPC_ForceSync", RpcTarget.Others,
                transform.position,
                transform.rotation,
                Vector3.zero,
                Vector3.zero);
        }
    }
}