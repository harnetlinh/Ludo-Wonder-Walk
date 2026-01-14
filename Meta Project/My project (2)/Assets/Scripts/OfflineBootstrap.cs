using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

// Ensures we can bootstrap Photon OfflineMode both automatically (when there is no internet)
// and manually when another system wants to skip the online connection flow.
public static class OfflineBootstrap
{
    private const string RunnerObjectName = "__OfflineInitRunner";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            EnsureOfflineMode("No internet detected. Starting in OfflineMode.");
        }
    }

    public static void EnsureOfflineMode(string logMessage = null)
    {
        if (!string.IsNullOrEmpty(logMessage))
        {
            Debug.Log(logMessage);
        }

        if (!PhotonNetwork.OfflineMode)
        {
            PhotonNetwork.OfflineMode = true;
        }

        if (!PhotonNetwork.InRoom)
        {
            var ro = new RoomOptions
            {
                IsVisible = false,
                MaxPlayers = 1 // Offline: single local player
            };
            PhotonNetwork.CreateRoom("LocalOfflineRoom", ro);
        }

        EnsureRunnerExists();
    }

    private static void EnsureRunnerExists()
    {
        GameObject runnerGO = GameObject.Find(RunnerObjectName);
        if (runnerGO == null)
        {
            runnerGO = new GameObject(RunnerObjectName);
            Object.DontDestroyOnLoad(runnerGO);
        }

        if (runnerGO.GetComponent<OfflineInitRunner>() == null)
        {
            runnerGO.AddComponent<OfflineInitRunner>();
        }
    }
}

// Runner that prepares the room properties used by offline play.
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

        PhotonNetwork.CurrentRoom.MaxPlayers = 1;

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

        if (PhotonManager.Instance != null)
        {
            PhotonManager.Instance.ActivatePiecesForPlayers();
        }

        initialized = true;
        Debug.Log("OfflineInitRunner: Room prepared for offline play (GameStarted=true, MaxPlayers=1)");
    }
}
