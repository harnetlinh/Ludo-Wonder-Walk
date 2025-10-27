using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

// Tự động bật chế độ Offline (không cần mạng) khi không có internet
public static class OfflineBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            if (!PhotonNetwork.OfflineMode)
            {
                PhotonNetwork.OfflineMode = true;
            }

            if (!PhotonNetwork.InRoom)
            {
                var ro = new RoomOptions
                {
                    IsVisible = false,
                    MaxPlayers = 1 // Offline: chỉ 1 người chơi cục bộ
                };
                PhotonNetwork.CreateRoom("LocalOfflineRoom", ro);
            }

            // Tạo runner để đánh dấu GameStarted và tinh chỉnh room khi đã vào phòng
            var runnerGO = new GameObject("__OfflineInitRunner");
            Object.DontDestroyOnLoad(runnerGO);
            runnerGO.AddComponent<OfflineInitRunner>();
        }
    }
}

// Runner đảm bảo phòng offline được đánh dấu GameStarted và MaxPlayers phù hợp
public class OfflineInitRunner : MonoBehaviourPunCallbacks
{
    private bool initialized;

    private void Start()
    {
        TryInit();
    }

    public override void OnJoinedRoom()
    {
        TryInit();
    }

    private void TryInit()
    {
        if (initialized) return;
        if (!PhotonNetwork.OfflineMode) return;
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return;

        // Đặt MaxPlayers = 1 (chơi cục bộ), và đánh dấu GameStarted để khởi tạo lượt chơi
        PhotonNetwork.CurrentRoom.MaxPlayers = 1;

        // Bật đủ 4 màu cho chế độ offline (gắn vào ID giả lập)
        string offlinePlayerColors = string.Join(";", new string[]
        {
            "offline_red:Red",
            "offline_blue:Blue",
            "offline_yellow:Yellow",
            "offline_green:Green"
        });

        var props = new ExitGames.Client.Photon.Hashtable
        {
            { "GameStarted", true },
            { "PiecesPerPlayer", 4 },
            { "PlayerColors", offlinePlayerColors }
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);

        // Kích hoạt quân cờ theo danh sách màu đã đặt
        if (PhotonManager.Instance != null)
        {
            PhotonManager.Instance.ActivatePiecesForPlayers();
        }

        initialized = true;
        Debug.Log("OfflineInitRunner: Room prepared for offline play (GameStarted=true, MaxPlayers=1)");
    }
}
