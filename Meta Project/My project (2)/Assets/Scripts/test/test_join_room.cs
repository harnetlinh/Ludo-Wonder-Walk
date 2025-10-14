using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class test_join_room : MonoBehaviourPunCallbacks
{
    public string name_room;
    
    void Start()
    {
        // Kết nối đến Photon server nếu chưa kết nối
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
        }
        else
        {
            JoinOrCreateRoom();
        }
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Photon Master Server");
        JoinOrCreateRoom();
    }

    void JoinOrCreateRoom()
    {
        if (string.IsNullOrEmpty(name_room))
        {
            Debug.LogError("Room name is empty!");
            return;
        }

        // Thử join phòng trước, nếu không tồn tại sẽ tạo mới
        PhotonNetwork.JoinRoom(name_room);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.Log($"Room {name_room} not found, creating new room...");
        
        // Tạo phòng mới với các options cơ bản
        RoomOptions roomOptions = new RoomOptions();
        roomOptions.MaxPlayers = 4; // Số người chơi tối đa
        roomOptions.IsVisible = true; // Phòng có thể nhìn thấy
        roomOptions.IsOpen = true; // Phòng mở để join
        
        PhotonNetwork.CreateRoom(name_room, roomOptions);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"Successfully joined room: {name_room}");
        Debug.Log($"Room has {PhotonNetwork.CurrentRoom.PlayerCount} players");
        
        // Có thể thêm logic khác ở đây khi vào phòng thành công
        // Ví dụ: load scene game, spawn player, etc.
    }

    public override void OnCreatedRoom()
    {
        Debug.Log($"Successfully created room: {name_room}");
    }
}