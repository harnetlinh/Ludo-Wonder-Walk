using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshProUGUI), typeof(PhotonView))]
public class SyncText : MonoBehaviourPun, IPunObservable
{
    [Header("Text Sync Settings")]
    public bool syncText = true;
    public bool syncColor = true;
    public bool syncFontSize = true;
    public bool syncVisibility = true;

    private TextMeshProUGUI textComponent;
    private string currentText = "";
    private Color currentColor = Color.white;
    private float currentFontSize = 24f;
    private bool isVisible = true;

    void Start()
    {
        textComponent = GetComponent<TextMeshProUGUI>();

        // Lưu giá trị ban đầu
        if (textComponent != null)
        {
            currentText = textComponent.text;
            currentColor = textComponent.color;
            currentFontSize = textComponent.fontSize;
            isVisible = textComponent.gameObject.activeSelf;
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Gửi dữ liệu đến các client khác - gửi cả trạng thái sync
            stream.SendNext(syncText);
            stream.SendNext(syncColor);
            stream.SendNext(syncFontSize);
            stream.SendNext(syncVisibility);

            if (syncText) stream.SendNext(textComponent.text);
            if (syncColor) stream.SendNext(textComponent.color);
            if (syncFontSize) stream.SendNext(textComponent.fontSize);
            if (syncVisibility) stream.SendNext(textComponent.gameObject.activeSelf);
        }
        else
        {
            // Nhận trạng thái sync từ client gửi
            bool senderSyncText = (bool)stream.ReceiveNext();
            bool senderSyncColor = (bool)stream.ReceiveNext();
            bool senderSyncFontSize = (bool)stream.ReceiveNext();
            bool senderSyncVisibility = (bool)stream.ReceiveNext();

            // Nhận dữ liệu theo đúng thứ tự và chỉ nhận những gì client gửi đã gửi
            if (senderSyncText) currentText = (string)stream.ReceiveNext();
            if (senderSyncColor) currentColor = (Color)stream.ReceiveNext();
            if (senderSyncFontSize) currentFontSize = (float)stream.ReceiveNext();
            if (senderSyncVisibility) isVisible = (bool)stream.ReceiveNext();

            // Áp dụng các thay đổi
            ApplyTextChanges();
        }
    }

    private void ApplyTextChanges()
    {
        if (textComponent == null) return;

        if (syncText && textComponent.text != currentText)
            textComponent.text = currentText;

        if (syncColor && textComponent.color != currentColor)
            textComponent.color = currentColor;

        if (syncFontSize && textComponent.fontSize != currentFontSize)
            textComponent.fontSize = currentFontSize;

        if (syncVisibility && textComponent.gameObject.activeSelf != isVisible)
            textComponent.gameObject.SetActive(isVisible);
    }

    // Phương thức để thay đổi text từ bất kỳ client nào
    [PunRPC]
    public void RPC_SetText(string newText)
    {
        textComponent.text = newText;
        currentText = newText;
    }

    [PunRPC]
    public void RPC_SetColor(Color newColor)
    {
        textComponent.color = newColor;
        currentColor = newColor;
    }

    [PunRPC]
    public void RPC_SetFontSize(float newSize)
    {
        textComponent.fontSize = newSize;
        currentFontSize = newSize;
    }

    [PunRPC]
    public void RPC_SetVisible(bool visible)
    {
        textComponent.gameObject.SetActive(visible);
        isVisible = visible;
    }

    // Phương thức public để gọi từ các script khác
    public void SetText(string newText)
    {
        if (PhotonNetwork.IsConnected)
        {
            photonView.RPC("RPC_SetText", RpcTarget.All, newText);
        }
        else
        {
            textComponent.text = newText;
            currentText = newText;
        }
    }

    public void SetColor(Color newColor)
    {
        if (PhotonNetwork.IsConnected)
        {
            photonView.RPC("RPC_SetColor", RpcTarget.All, newColor);
        }
        else
        {
            textComponent.color = newColor;
            currentColor = newColor;
        }
    }

    public void SetFontSize(float newSize)
    {
        if (PhotonNetwork.IsConnected)
        {
            photonView.RPC("RPC_SetFontSize", RpcTarget.All, newSize);
        }
        else
        {
            textComponent.fontSize = newSize;
            currentFontSize = newSize;
        }
    }

    public void SetVisible(bool visible)
    {
        if (PhotonNetwork.IsConnected)
        {
            photonView.RPC("RPC_SetVisible", RpcTarget.All, visible);
        }
        else
        {
            textComponent.gameObject.SetActive(visible);
            isVisible = visible;
        }
    }
}