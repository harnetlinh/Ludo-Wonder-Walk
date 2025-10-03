using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class SimpleOwnership : MonoBehaviourPun, IPunOwnershipCallbacks
{
    void Start()
    {
        PhotonNetwork.AddCallbackTarget(this);
        
        // Master Client sở hữu tất cả objects ban đầu
        if (PhotonNetwork.IsMasterClient && !photonView.IsMine)
        {
            photonView.TransferOwnership(PhotonNetwork.LocalPlayer);
        }
        
        Debug.Log($"SimpleOwnership setup for {gameObject.name}. Owner: {photonView.Owner?.NickName}");
    }

    void OnDestroy()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    public void OnOwnershipRequest(PhotonView targetView, Player requestingPlayer)
    {
        if (targetView == photonView)
        {
            Debug.Log($"Transferring ownership to {requestingPlayer.NickName}");
            targetView.TransferOwnership(requestingPlayer);
        }
    }

    public void OnOwnershipTransfered(PhotonView targetView, Player previousOwner)
    {
        if (targetView == photonView)
        {
            Debug.Log($"Ownership changed: {previousOwner?.NickName} -> {targetView.Owner?.NickName}");
        }
    }

    public void OnOwnershipTransferFailed(PhotonView targetView, Player senderOfFailedRequest)
    {
        Debug.LogWarning($"Ownership transfer failed for {senderOfFailedRequest.NickName}");
    }
}
