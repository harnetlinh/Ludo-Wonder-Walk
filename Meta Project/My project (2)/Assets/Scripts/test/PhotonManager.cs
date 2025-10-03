using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class PhotonManager : MonoBehaviourPunCallbacks
{
    void Start()
    {
        Debug.Log("=== PHOTON MANAGER STARTED ===");
        
        // Cấu hình Photon để đồng bộ tốt hơn
        PhotonNetwork.SendRate = 30; // Tăng tần suất gửi dữ liệu
        PhotonNetwork.SerializationRate = 15; // Tăng tần suất đồng bộ
        
        // Debug connection info
        Debug.Log($"Photon App Version: {PhotonNetwork.AppVersion}");
        Debug.Log($"Game Version: {PhotonNetwork.GameVersion}");
        Debug.Log($"Is Connected: {PhotonNetwork.IsConnected}");
        Debug.Log($"Connection State: {PhotonNetwork.NetworkingClient.State}");
        
        Debug.Log("Attempting to connect to Photon...");
        PhotonNetwork.ConnectUsingSettings();
    }


    public override void OnJoinedRoom()
    {
        Debug.Log($"Joined room successfully as {PhotonNetwork.NickName}. Total players: {PhotonNetwork.CurrentRoom.PlayerCount}");
        Debug.Log($"Is Master Client: {PhotonNetwork.IsMasterClient}");

        // Không spawn object nữa - sử dụng object có sẵn trong scene
        Debug.Log("Ready to interact with existing scene objects");
        
        // Tìm tất cả PhotonView trong scene và log thông tin ownership
        PhotonView[] scenePhotonViews = FindObjectsOfType<PhotonView>();
        foreach (PhotonView pv in scenePhotonViews)
        {
            Debug.Log($"Scene PhotonView found: {pv.gameObject.name}, Owner: {pv.Owner?.NickName ?? "None"}, IsMine: {pv.IsMine}");
        }
    }


    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"Player {newPlayer.NickName} joined the room. Total players: {PhotonNetwork.CurrentRoom.PlayerCount}");
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"Player {otherPlayer.NickName} left the room. Total players: {PhotonNetwork.CurrentRoom.PlayerCount}");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"Disconnected from Photon: {cause}");
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("=== CONNECTED TO MASTER SERVER ===");
        Debug.Log($"Server Address: {PhotonNetwork.ServerAddress}");
        Debug.Log($"Cloud Region: {PhotonNetwork.CloudRegion}");
        
        // Đặt nickname cho player
        if (string.IsNullOrEmpty(PhotonNetwork.NickName))
        {
            PhotonNetwork.NickName = "Player" + Random.Range(1000, 9999);
        }
        
        Debug.Log($"Player Nickname: {PhotonNetwork.NickName}");
        
        RoomOptions roomOptions = new RoomOptions();
        roomOptions.MaxPlayers = 4;
        roomOptions.IsVisible = true;
        roomOptions.IsOpen = true;
        
        Debug.Log("Attempting to join or create room 'TestRoom'...");
        PhotonNetwork.JoinOrCreateRoom("TestRoom", roomOptions, TypedLobby.Default);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Failed to join room: {returnCode} - {message}");
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Failed to create room: {returnCode} - {message}");
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.LogError($"Failed to join random room: {returnCode} - {message}");
    }
}