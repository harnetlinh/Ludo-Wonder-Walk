using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEngine;

public class GrapPiece : MonoBehaviour
{
    [Header("Hand Grab Interactors")]
    public HandGrabInteractor leftHand;
    public HandGrabInteractor rightHand;

    private PositionOptimizer positionOptimizer;
    private bool wasGrabbed = false;

    private void Awake()
    {
        positionOptimizer = GetComponent<PositionOptimizer>();
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
        HandleHandStateChange(args.NewState);

        if (args.NewState == InteractorState.Select)
            Debug.Log("✋ Tay trái đang cầm vật!");
        else if (args.NewState == InteractorState.Normal)
            Debug.Log("✋ Tay trái đã thả vật.");
    }

    private void OnRightHandStateChanged(InteractorStateChangeArgs args)
    {
        HandleHandStateChange(args.NewState);

        if (args.NewState == InteractorState.Select)
            Debug.Log("🤚 Tay phải đang cầm vật!");
        else if (args.NewState == InteractorState.Normal)
            Debug.Log("🤚 Tay phải đã thả vật.");
    }

    private void HandleHandStateChange(InteractorState state)
    {
        if (positionOptimizer == null) return;

        if (state == InteractorState.Select)
        {
            // Tắt PositionOptimizer khi cầm vật
            positionOptimizer.enabled = false;
            positionOptimizer.SetIsBeingHandled(true);
            wasGrabbed = true;
        }
        else if (state == InteractorState.Normal && wasGrabbed)
        {
            // Chỉ đánh dấu là đã thả, không bật lại ngay
            wasGrabbed = false;
            positionOptimizer.SetIsBeingHandled(false);

            // Gọi hàm kiểm tra sau khi thả (sẽ xử lý khi chạm đất)
            positionOptimizer.OnPieceReleased();
        }
    }
}