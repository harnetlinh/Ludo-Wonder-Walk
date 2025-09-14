using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEngine;

public class GrabDice : MonoBehaviour
{
    [Header("Kéo HandGrabInteractor từ RealHand vào đây")]
    public HandGrabInteractor handGrabInteractor;

    private bool isBeingHeld = false;
    private DiceFaceDetector diceDetector;

    private void Start()
    {
        diceDetector = GetComponent<DiceFaceDetector>();
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
            //Debug.Log("👉 Tay đang cầm xúc xắc!");
            isBeingHeld = true;

            // Đánh dấu xúc xắc đã được cầm lên ít nhất một lần
            if (diceDetector != null)
            {
                diceDetector.isFirstPickup = false; // <-- QUAN TRỌNG
            }

            // Thông báo cho DiceController rằng xúc xắc đang được cầm
            if (DiceController.Instance != null && !DiceController.Instance.hasRolledThisTurn)
            {
                DiceController.Instance.PrepareToRoll();
                DiceController.Instance.UpdateDiceStatus(true);
            }
        }
        else if (args.NewState == InteractorState.Normal)
        {
            //Debug.Log("👉 Tay đã thả xúc xắc.");
            isBeingHeld = false;

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
}