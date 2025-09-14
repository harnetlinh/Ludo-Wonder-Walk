using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

// Trình khởi động mạng PUN 2: kết nối, join/create room và spawn Player
public class NetworkLauncher : MonoBehaviourPunCallbacks
{
    // Phiên bản game để phân tách người chơi (hai bản test phải giống nhau)
    [SerializeField] private string gameVersion = "1.0";

    // Tên prefab trong Assets/Resources dùng để spawn qua mạng
    [SerializeField] private string playerPrefabName = "Player";

    // Số người tối đa trong phòng
    [SerializeField] private byte maxPlayersPerRoom = 4;

    // Điểm spawn tùy chọn; nếu rỗng sẽ random gần gốc tọa độ
    [SerializeField] private Transform[] optionalSpawnPoints;

    private void Awake()
    {
        // Bật đồng bộ scene theo MasterClient (Unity 6 không có checkbox trong Inspector)
        PhotonNetwork.AutomaticallySyncScene = true;
        // Chạy nền để không ngắt kết nối khi cửa sổ không focus
        Application.runInBackground = true;
    }

    private void Start()
    {
        // Tắt tự động kết nối - để UI quản lý
        // Chỉ setup cơ bản, không tự động connect
        PhotonNetwork.GameVersion = gameVersion;
        
        // Không tự động kết nối nữa - để MultiplayerTestUI quản lý
        Debug.Log("NetworkLauncher ready - waiting for UI to connect");
    }

    // Gọi khi đã kết nối tới Master server
    public override void OnConnectedToMaster()
    {
        // Không tự động join room nữa - để UI quản lý
        Debug.Log("Connected to Master - waiting for UI to create/join room");
    }

    // Thử join phòng ngẫu nhiên; nếu thất bại sẽ tạo phòng mới
    private void TryJoinRandomRoom()
    {
        PhotonNetwork.JoinRandomRoom();
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        PhotonNetwork.CreateRoom(null, new RoomOptions { MaxPlayers = maxPlayersPerRoom });
    }

    // Khi đã vào phòng: spawn player cho client hiện tại
    public override void OnJoinedRoom()
    {
        Debug.Log($"Joined room: {PhotonNetwork.CurrentRoom.Name}");
        Vector3 spawnPosition = GetSpawnPosition();
        PhotonNetwork.Instantiate(playerPrefabName, spawnPosition, Quaternion.identity);
    }
    
    // Public method để UI có thể gọi khi cần spawn player
    public void SpawnPlayer()
    {
        if (PhotonNetwork.InRoom)
        {
            Vector3 spawnPosition = GetSpawnPosition();
            PhotonNetwork.Instantiate(playerPrefabName, spawnPosition, Quaternion.identity);
        }
    }

    // Tính vị trí spawn: dùng điểm định sẵn nếu có, ngược lại random nhẹ
    private Vector3 GetSpawnPosition()
    {
        if (optionalSpawnPoints != null && optionalSpawnPoints.Length > 0)
        {
            Transform chosen = optionalSpawnPoints[Random.Range(0, optionalSpawnPoints.Length)];
            if (chosen != null)
            {
                return chosen.position;
            }
        }

        return new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));
    }
}
