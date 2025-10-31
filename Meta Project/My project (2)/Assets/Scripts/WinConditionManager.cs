using System;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class WinConditionManager : MonoBehaviourPunCallbacks
{
    public static WinConditionManager Instance { get; private set; }

    [Header("UI References")]
    public TextMeshProUGUI winnerText;
    public GameObject winPanel;
    public Button restartButton;
    public Button mainMenuButton;

    [Header("Game Settings")]
    public int piecesToWin = 4;

    private const string GameOverPropertyKey = "GameOver";

    private readonly Dictionary<PlayerColor, int> finishedPieces = new Dictionary<PlayerColor, int>();
    private bool gameEnded;
    private Coroutine piecesSyncRoutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        finishedPieces.Clear();
        foreach (PlayerColor color in Enum.GetValues(typeof(PlayerColor)))
        {
            finishedPieces[color] = 0;
        }

        EnsurePiecesToWinSynced();

        if (restartButton != null)
            restartButton.onClick.AddListener(RestartGame);

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);

        if (winPanel != null)
            winPanel.SetActive(false);
    }

    private void EnsurePiecesToWinSynced()
    {
        if (piecesSyncRoutine != null)
        {
            StopCoroutine(piecesSyncRoutine);
        }

        piecesSyncRoutine = StartCoroutine(SyncPiecesToWinRoutine());
    }

    private IEnumerator SyncPiecesToWinRoutine()
    {
        const float timeoutSeconds = 10f;
        const float retryDelay = 0.5f;
        float elapsed = 0f;

        while (elapsed < timeoutSeconds)
        {
            if (TrySyncPiecesToWinFromRoom())
            {
                piecesSyncRoutine = null;
                yield break;
            }

            yield return new WaitForSeconds(retryDelay);
            elapsed += retryDelay;
        }

        Debug.LogWarning("[WinConditionManager] Timed out waiting for PiecesPerPlayer; keeping existing piecesToWin value.");
        piecesSyncRoutine = null;
    }

    private bool TrySyncPiecesToWinFromRoom()
    {
        try
        {
            if (PhotonManager.Instance != null)
            {
                int roomPieces = PhotonManager.Instance.GetPiecesPerPlayer();
                if (roomPieces > 0 && piecesToWin != roomPieces)
                {
                    piecesToWin = roomPieces;
                    Debug.Log($"[WinConditionManager] Synced piecesToWin from room: {piecesToWin}");
                    return true;
                }

                if (roomPieces > 0)
                {
                    return true;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WinConditionManager] Could not sync PiecesPerPlayer from room: {e.Message}");
        }

        return false;
    }

    private void HandleGameOverLocal(PlayerColor winnerColor)
    {
        if (gameEnded)
            return;

        EndGame(winnerColor);
    }

    private void AnnounceGameOver(PlayerColor winnerColor)
    {
        if (gameEnded)
            return;

        bool syncedWithRoom = false;

        if (PhotonNetwork.InRoom && !PhotonNetwork.OfflineMode && PhotonNetwork.CurrentRoom != null)
        {
            syncedWithRoom = TryPublishGameOverToRoom(winnerColor);
        }

        HandleGameOverLocal(winnerColor);

        if (!syncedWithRoom)
        {
            Debug.Log("[WinConditionManager] Game over handled locally (no room sync).");
        }
    }

    private bool TryPublishGameOverToRoom(PlayerColor winnerColor)
    {
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(GameOverPropertyKey, out object existingValue))
        {
            if (existingValue is int existingWinner && !gameEnded)
            {
                HandleGameOverLocal((PlayerColor)existingWinner);
            }

            return true;
        }

        Hashtable properties = new Hashtable
        {
            { GameOverPropertyKey, (int)winnerColor }
        };

        bool success = PhotonNetwork.CurrentRoom.SetCustomProperties(properties);
        if (!success)
        {
            Debug.LogWarning("[WinConditionManager] Failed to publish GameOver property to room.");
        }

        return success;
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();
        EnsurePiecesToWinSynced();

        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(GameOverPropertyKey, out object existingValue) &&
            existingValue is int winnerInt)
        {
            HandleGameOverLocal((PlayerColor)winnerInt);
        }
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        base.OnRoomPropertiesUpdate(propertiesThatChanged);

        if (propertiesThatChanged == null)
            return;

        if (propertiesThatChanged.ContainsKey("PiecesPerPlayer"))
        {
            EnsurePiecesToWinSynced();
        }

        if (propertiesThatChanged.ContainsKey(GameOverPropertyKey))
        {
            object rawValue = propertiesThatChanged[GameOverPropertyKey];
            if (rawValue is int winner)
            {
                HandleGameOverLocal((PlayerColor)winner);
            }
            else if (rawValue == null)
            {
                gameEnded = false;
            }
        }
    }

    public void PieceFinished(PlayerColor color)
    {
        if (gameEnded)
            return;

        if (!finishedPieces.ContainsKey(color))
        {
            finishedPieces[color] = 0;
        }

        finishedPieces[color]++;
        Debug.Log($"{color} has {finishedPieces[color]} pieces finished");

        if (finishedPieces[color] >= piecesToWin)
        {
            AnnounceGameOver(color);
        }
    }

    public bool IsPieceFinished(int pathIndex, PlayerColor playerColor)
    {
        List<Transform> privatePath = HorseRacePathManager.Instance.GetPrivatePath(playerColor);

        if (pathIndex >= HorseRacePathManager.Instance.commonPathPoints.Count)
        {
            int privateIndex = pathIndex - HorseRacePathManager.Instance.commonPathPoints.Count;
            return privateIndex >= privatePath.Count - 1;
        }

        return false;
    }

    private void EndGame(PlayerColor winnerColor)
    {
        gameEnded = true;
        Debug.Log($"Game Over! {winnerColor} wins!");

        if (winPanel != null)
        {
            winPanel.SetActive(true);
            if (winnerText != null)
            {
                winnerText.text = $"{GetColorName(winnerColor)} chiến thắng!";
                winnerText.color = GetColor(winnerColor);
            }
        }

        PieceController[] allPieces = FindObjectsByType<PieceController>(FindObjectsSortMode.None);
        foreach (PieceController piece in allPieces)
        {
            piece.StopAllCoroutines();
            piece.isMoving = false;
        }

        StartCoroutine(VictoryEffects(winnerColor));
    }

    private IEnumerator VictoryEffects(PlayerColor winnerColor)
    {
        PieceController[] winnerPieces = FindObjectsByType<PieceController>(FindObjectsSortMode.None);
        foreach (PieceController piece in winnerPieces)
        {
            if (piece.playerColor == winnerColor)
            {
                StartCoroutine(JumpEffect(piece.transform));
            }
        }

        yield return new WaitForSeconds(2f);

        ResetBoardToInitialState(hideWinPanel: false, resetGameEndedFlag: false);

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ResetLocalState(clearNetworkState: PhotonNetwork.IsMasterClient);
        }
    }

    private IEnumerator JumpEffect(Transform pieceTransform)
    {
        const float jumpHeight = 0.5f;
        const float jumpDuration = 0.5f;
        Vector3 startPosition = pieceTransform.position;

        for (int i = 0; i < 3; i++)
        {
            float elapsed = 0f;
            while (elapsed < jumpDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / jumpDuration;
                float y = startPosition.y + Mathf.Sin(t * Mathf.PI) * jumpHeight;
                pieceTransform.position = new Vector3(startPosition.x, y, startPosition.z);
                yield return null;
            }

            pieceTransform.position = startPosition;
            yield return new WaitForSeconds(0.2f);
        }
    }

    public void ResetBoardToInitialState(bool hideWinPanel = false, bool resetGameEndedFlag = true)
    {
        if (resetGameEndedFlag)
        {
            gameEnded = false;
        }

        foreach (PlayerColor color in Enum.GetValues(typeof(PlayerColor)))
        {
            finishedPieces[color] = 0;
        }

        if (hideWinPanel && winPanel != null)
        {
            winPanel.SetActive(false);
        }

        PieceController.ResetAllPiecesToStablePositions();
    }

    public void RestartGame()
    {
        ClearNetworkGameOverFlag();
        EnsurePiecesToWinSynced();

        ResetBoardToInitialState(hideWinPanel: true, resetGameEndedFlag: true);

        if (GameTurnManager.Instance != null)
        {
            GameTurnManager.Instance.currentPlayerIndex = 0;
            GameTurnManager.Instance.InitializePlayerOrder(DiceController.Instance);
        }
    }

    private void ClearNetworkGameOverFlag()
    {
        if (PhotonNetwork.InRoom && !PhotonNetwork.OfflineMode && PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom != null)
        {
            Hashtable clearProps = new Hashtable
            {
                { GameOverPropertyKey, null }
            };

            PhotonNetwork.CurrentRoom.SetCustomProperties(clearProps);
        }
    }

    public void ReturnToMainMenu()
    {
        Debug.Log("Returning to main menu...");
        // SceneManager.LoadScene("MainMenu");
    }

    private string GetColorName(PlayerColor color)
    {
        switch (color)
        {
            case PlayerColor.Red:
                return "Đỏ";
            case PlayerColor.Blue:
                return "Xanh Dương";
            case PlayerColor.Yellow:
                return "Vàng";
            case PlayerColor.Green:
                return "Xanh Lá";
            default:
                return color.ToString();
        }
    }

    private Color GetColor(PlayerColor color)
    {
        switch (color)
        {
            case PlayerColor.Red:
                return Color.red;
            case PlayerColor.Blue:
                return Color.blue;
            case PlayerColor.Yellow:
                return Color.yellow;
            case PlayerColor.Green:
                return Color.green;
            default:
                return Color.white;
        }
    }

    public bool IsGameEnded()
    {
        return gameEnded;
    }
}
