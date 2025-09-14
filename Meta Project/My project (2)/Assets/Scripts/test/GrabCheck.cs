
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEngine;

public class HandGrabCheck : MonoBehaviour
{
    [Header("Kéo HandGrabInteractor từ RealHand vào đây")]
    public HandGrabInteractor handGrabInteractor;

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
            Debug.Log("👉 Tay đang cầm vật!");
        }
        else if (args.NewState == InteractorState.Normal)
        {
            Debug.Log("👉 Tay đã thả vật.");
        }
    }
}
