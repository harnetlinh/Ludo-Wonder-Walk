using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;

[RequireComponent(typeof(PhotonView))]
public class GameStateManager : MonoBehaviourPunCallbacks
{
    public static GameStateManager Instance { get; private set; }

    [Header("Game State Debug")]
    public int currentTurnIndex = 0;
    public PlayerColor currentPlayerColor = PlayerColor.None;
    public int lastDiceValue = 0;
    public bool isDiceRolling = false;
    public List<PlayerColor> playerOrder = new List<PlayerColor>();
    public bool isGameInitialized = false;

    // Events for UI updates
    public System.Action<int, PlayerColor> OnDiceResultChanged;
    public System.Action<int, PlayerColor> OnTurnChanged;
    public System.Action<bool> OnGameInitialized;

    private PhotonView cachedPhotonView;

    private void CachePhotonView()
    {
        if (cachedPhotonView == null)
        {
            cachedPhotonView = GetComponent<PhotonView>();
            if (cachedPhotonView == null)
            {
                Debug.LogError("GameStateManager requires a PhotonView component to synchronize state across the network.");
            }
        }
    }

    private PhotonView GetPhotonView()
    {
        if (cachedPhotonView == null)
        {
            CachePhotonView();
        }
        return cachedPhotonView;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        CachePhotonView();
    }

    private void Start()
    {
        Debug.Log("=== GAME STATE MANAGER STARTED ===");
        
        // Load initial state from room properties if available
        if (PhotonNetwork.InRoom)
        {
            LoadGameStateFromRoomProperties();
        }
    }

    #region PUBLIC METHODS - GAME STATE MANAGEMENT

    /// <summary>
    /// Save current game state to room properties (Master Client only)
    /// </summary>
    public void SaveGameState()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        Debug.Log("💾 Saving game state to room properties...");

        var props = new ExitGames.Client.Photon.Hashtable
        {
            ["CurrentTurnIndex"] = currentTurnIndex,
            ["CurrentPlayerColor"] = currentPlayerColor.ToString(),
            ["LastDiceValue"] = lastDiceValue,
            ["IsDiceRolling"] = isDiceRolling,
            ["IsGameInitialized"] = isGameInitialized
        };

        // Save player order as string
        if (playerOrder.Count > 0)
        {
            List<string> orderStrings = new List<string>();
            foreach (var color in playerOrder)
            {
                orderStrings.Add(color.ToString());
            }
            props["PlayerOrder"] = string.Join(",", orderStrings);
        }

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        Debug.Log($"✅ Game state saved: Turn={currentTurnIndex}, Player={currentPlayerColor}, Dice={lastDiceValue}");
    }

    /// <summary>
    /// Load game state from room properties
    /// </summary>
    public void LoadGameStateFromRoomProperties()
    {
        if (!PhotonNetwork.InRoom) return;

        var props = PhotonNetwork.CurrentRoom.CustomProperties;
        
        if (props.ContainsKey("CurrentTurnIndex"))
        {
            currentTurnIndex = (int)props["CurrentTurnIndex"];
        }

        if (props.ContainsKey("CurrentPlayerColor"))
        {
            string colorStr = (string)props["CurrentPlayerColor"];
            if (System.Enum.TryParse(colorStr, out PlayerColor color))
            {
                currentPlayerColor = color;
            }
        }

        if (props.ContainsKey("LastDiceValue"))
        {
            lastDiceValue = (int)props["LastDiceValue"];
        }

        if (props.ContainsKey("IsDiceRolling"))
        {
            isDiceRolling = (bool)props["IsDiceRolling"];
        }

        if (props.ContainsKey("IsGameInitialized"))
        {
            isGameInitialized = (bool)props["IsGameInitialized"];
        }

        if (props.ContainsKey("PlayerOrder"))
        {
            string orderStr = (string)props["PlayerOrder"];
            if (!string.IsNullOrEmpty(orderStr))
            {
                playerOrder.Clear();
                string[] colorStrings = orderStr.Split(',');
                foreach (string colorStr in colorStrings)
                {
                    if (System.Enum.TryParse(colorStr, out PlayerColor color))
                    {
                        playerOrder.Add(color);
                    }
                }
            }
        }

        Debug.Log($"📖 Game state loaded: Turn={currentTurnIndex}, Player={currentPlayerColor}, Dice={lastDiceValue}, Initialized={isGameInitialized}");
        
        // Notify other systems
        OnDiceResultChanged?.Invoke(lastDiceValue, currentPlayerColor);
        OnTurnChanged?.Invoke(currentTurnIndex, currentPlayerColor);
    }

    /// <summary>
    /// Set dice result (Master Client only) and sync to all clients
    /// </summary>
    public void SetDiceResult(int value, PlayerColor playerColor)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("❌ Only Master Client can set dice result!");
            return;
        }

        lastDiceValue = value;
        currentPlayerColor = playerColor;
        isDiceRolling = false;

        Debug.Log($"🎲 Dice result set: {playerColor} rolled {value}");

        // Save to room properties
        SaveGameState();

        // Notify all clients via RPC
        var view = GetPhotonView();
        if (view == null) return;
        view.RPC("RPC_SyncDiceResult", RpcTarget.All, value, playerColor);
    }

    /// <summary>
    /// Move to next turn (Master Client only)
    /// </summary>
    public void NextTurn()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        if (playerOrder.Count == 0 && GameTurnManager.Instance != null && GameTurnManager.Instance.playerOrder != null && GameTurnManager.Instance.playerOrder.Count > 0)
        {
            Debug.LogWarning("[GameStateManager] Player order was empty on NextTurn. Attempting to rebuild using GameTurnManager data.");
            SetPlayerOrder(GameTurnManager.Instance.playerOrder);
        }

        if (playerOrder.Count == 0)
        {
            LoadGameStateFromRoomProperties();
        }

        if (playerOrder.Count == 0)
        {
            Debug.LogError("[GameStateManager] Player order is empty, cannot move to next turn.");
            return;
        }

        currentTurnIndex = (currentTurnIndex + 1) % playerOrder.Count;
        currentPlayerColor = playerOrder[currentTurnIndex];
        lastDiceValue = 0;
        isDiceRolling = false;

        Debug.Log($"[GameStateManager] Turn changed: {currentPlayerColor} (Index: {currentTurnIndex})");

        // Save to room properties
        SaveGameState();

        // Notify all clients
        var view = GetPhotonView();
        if (view == null) return;
        view.RPC("RPC_SyncTurn", RpcTarget.All, currentTurnIndex, currentPlayerColor);
    }

    /// <summary>
    /// Set player order (Master Client only)
    /// </summary>
    public void SetPlayerOrder(List<PlayerColor> order)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        if (order == null || order.Count == 0)
        {
            Debug.LogError("GameStateManager.SetPlayerOrder received a null or empty order list. Aborting initialization.");
            return;
        }

        playerOrder = new List<PlayerColor>(order);
        currentTurnIndex = 0;
        if (playerOrder.Count > 0)
        {
            currentPlayerColor = playerOrder[0];
        }
        isGameInitialized = true;

        Debug.Log($"🎯 Player order set: {string.Join(" → ", playerOrder)}");

        // Save to room properties
        SaveGameState();

        // Notify all clients
        int[] orderArray = playerOrder.ConvertAll(color => (int)color).ToArray();
        var view = GetPhotonView();
        if (view == null) return;
        view.RPC("RPC_SyncPlayerOrder", RpcTarget.All, orderArray, currentTurnIndex);
    }

    /// <summary>
    /// Start dice rolling state
    /// </summary>
    public void StartDiceRolling(PlayerColor playerColor)
    {
        isDiceRolling = true;
        currentPlayerColor = playerColor;

        Debug.Log($"🎲 {playerColor} started rolling dice");

        // Save to room properties if master client
        if (PhotonNetwork.IsMasterClient)
        {
            SaveGameState();
        }
    }

    #endregion

    #region PUN RPC METHODS

    [PunRPC]
    private void RPC_SyncDiceResult(int value, PlayerColor playerColor)
    {
        lastDiceValue = value;
        currentPlayerColor = playerColor;
        isDiceRolling = false;

        Debug.Log($"📢 Received dice result: {playerColor} rolled {value}");

        // Update UI and game logic
        OnDiceResultChanged?.Invoke(value, playerColor);

        // Update DiceController
        if (DiceController.Instance != null)
        {
            DiceController.Instance.LastDiceValue = value;
            DiceController.Instance.currentRollingPlayer = playerColor;
            DiceController.Instance.isDiceRolling = false;
            DiceController.Instance.hasRolledThisTurn = true;

            if (DiceController.Instance.diceResultText != null)
            {
                DiceController.Instance.diceResultText.text = $"{playerColor}: {value}";
            }
        }

        // Check for possible moves
        if (GameTurnManager.Instance != null && !GameTurnManager.Instance.isDeterminingOrder)
        {
            GameTurnManager.Instance.CheckForPossibleMoves();
        }
    }

    [PunRPC]
    private void RPC_SyncTurn(int turnIndex, PlayerColor playerColor)
    {
        currentTurnIndex = turnIndex;
        currentPlayerColor = playerColor;
        lastDiceValue = 0;
        isDiceRolling = false;

        Debug.Log($"📢 Received turn change: {playerColor} (Index: {turnIndex})");

        // Update UI
        OnTurnChanged?.Invoke(turnIndex, playerColor);

        // Update GameTurnManager
        if (GameTurnManager.Instance != null)
        {
            GameTurnManager.Instance.currentPlayerIndex = turnIndex;
            if (GameTurnManager.Instance.playerOrder.Count > turnIndex)
            {
                GameTurnManager.Instance.playerOrder = new List<PlayerColor>(playerOrder);
            }
        }

        // Update DiceController
        if (DiceController.Instance != null)
        {
            DiceController.Instance.currentRollingPlayer = playerColor;
            DiceController.Instance.hasRolledThisTurn = false;
            DiceController.Instance.ResetDiceValue();

            if (DiceController.Instance.statusText != null)
            {
                DiceController.Instance.statusText.text = $"Lượt của {playerColor}\nChưa xúc xắc";
            }
        }
    }

    [PunRPC]
    private void RPC_SyncPlayerOrder(int[] orderArray, int startIndex)
    {
        playerOrder.Clear();
        foreach (int colorValue in orderArray)
        {
            playerOrder.Add((PlayerColor)colorValue);
        }
        currentTurnIndex = startIndex;
        if (playerOrder.Count > startIndex)
        {
            currentPlayerColor = playerOrder[startIndex];
        }
        isGameInitialized = true;

        Debug.Log($"📢 Received player order: {string.Join(" → ", playerOrder)}");

        // Update GameTurnManager
        if (GameTurnManager.Instance != null)
        {
            GameTurnManager.Instance.playerOrder = new List<PlayerColor>(playerOrder);
            GameTurnManager.Instance.currentPlayerIndex = startIndex;
            GameTurnManager.Instance.isInitialized = true;
        }

        // Notify UI
        OnGameInitialized?.Invoke(true);
    }

    #endregion

    #region PUN CALLBACKS

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"🔄 New player joined: {newPlayer.NickName}, sending game state...");

        // Master client sends current game state to new player
        if (PhotonNetwork.IsMasterClient && isGameInitialized)
        {
            var view = GetPhotonView();
            if (view == null) return;

            // Send player order
            if (playerOrder.Count > 0)
            {
                int[] orderArray = playerOrder.ConvertAll(color => (int)color).ToArray();
                view.RPC("RPC_SyncPlayerOrder", newPlayer, orderArray, currentTurnIndex);
            }

            // Send current dice state
            if (lastDiceValue > 0)
            {
                view.RPC("RPC_SyncDiceResult", newPlayer, lastDiceValue, currentPlayerColor);
            }
            else
            {
                // Send current turn
                view.RPC("RPC_SyncTurn", newPlayer, currentTurnIndex, currentPlayerColor);
            }
        }
    }

    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
    {
        Debug.Log("🔄 Room properties updated, loading game state...");
        LoadGameStateFromRoomProperties();
    }

    #endregion

    #region DEBUG METHODS

    /// <summary>
    /// Print current game state for debugging
    /// </summary>
    public void PrintGameState()
    {
        Debug.Log($"=== 🎮 GAME STATE DEBUG ===");
        Debug.Log($"Turn Index: {currentTurnIndex}");
        Debug.Log($"Current Player: {currentPlayerColor}");
        Debug.Log($"Last Dice Value: {lastDiceValue}");
        Debug.Log($"Is Dice Rolling: {isDiceRolling}");
        Debug.Log($"Is Game Initialized: {isGameInitialized}");
        Debug.Log($"Player Order: {(playerOrder.Count > 0 ? string.Join(" → ", playerOrder) : "Empty")}");
        Debug.Log($"===========================");
    }

    /// <summary>
    /// Force sync game state to all clients (Master Client only)
    /// </summary>
    public void ForceSyncGameState()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        Debug.Log("🔄 Force syncing game state to all clients...");
        SaveGameState();

        var view = GetPhotonView();
        if (view == null) return;

        if (playerOrder.Count > 0)
        {
            int[] orderArray = playerOrder.ConvertAll(color => (int)color).ToArray();
            view.RPC("RPC_SyncPlayerOrder", RpcTarget.Others, orderArray, currentTurnIndex);
        }

        if (lastDiceValue > 0)
        {
            view.RPC("RPC_SyncDiceResult", RpcTarget.Others, lastDiceValue, currentPlayerColor);
        }
        else
        {
            view.RPC("RPC_SyncTurn", RpcTarget.Others, currentTurnIndex, currentPlayerColor);
        }
    }

    #endregion
}
