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
    public int? lastDiceValue = null;
    public bool isDiceRolling = false;
    public List<PlayerColor> playerOrder = new List<PlayerColor>();
    public Vector3 diceWorldPosition = Vector3.zero;
    public Quaternion diceWorldRotation = Quaternion.identity;
    public bool HasDiceTransform => hasDiceTransform;
    public bool isGameInitialized = false;

    // Events for UI updates
    public System.Action<int?, PlayerColor> OnDiceResultChanged;
    public System.Action<int, PlayerColor> OnTurnChanged;
    public System.Action<bool> OnGameInitialized;
    public System.Action<Vector3, Quaternion> OnDiceTransformChanged;

    private PhotonView cachedPhotonView;
    private const int DiceNullSentinel = -1;
    private const string DiceTransformKey = "DiceTransform";
    private bool hasDiceTransform = false;

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

    private static int? NormalizeDiceValue(int? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        int sanitized = value.Value;
        return sanitized >= 1 && sanitized <= 6 ? sanitized : (int?)null;
    }

    private static int ToNetworkValue(int? value)
    {
        return value.HasValue ? value.Value : DiceNullSentinel;
    }

    private static int? FromNetworkValue(int value)
    {
        return value == DiceNullSentinel ? null : NormalizeDiceValue(value);
    }

    private static string FormatDiceValue(int? value)
    {
        return value.HasValue ? value.Value.ToString() : "-";
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
            ["LastDiceValue"] = ToNetworkValue(lastDiceValue),
            ["IsDiceRolling"] = isDiceRolling,
            ["IsGameInitialized"] = isGameInitialized
        };

        if (hasDiceTransform)
        {
            props[DiceTransformKey] = SerializeDiceTransform(diceWorldPosition, diceWorldRotation);
        }

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
        Debug.Log($"✅ Game state saved: Turn={currentTurnIndex}, Player={currentPlayerColor}, Dice={FormatDiceValue(lastDiceValue)}");
    }

    /// <summary>
    /// Load game state from room properties
    /// </summary>
    public void LoadGameStateFromRoomProperties()
    {
        int previousTurnIndex = currentTurnIndex;
        PlayerColor previousPlayerColor = currentPlayerColor;
        bool previousHasTransform = hasDiceTransform;
        Vector3 previousDicePosition = diceWorldPosition;
        Quaternion previousDiceRotation = diceWorldRotation;

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
            lastDiceValue = FromNetworkValue((int)props["LastDiceValue"]);
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

        Debug.Log($"📖 Game state loaded: Turn={currentTurnIndex}, Player={currentPlayerColor}, Dice={FormatDiceValue(lastDiceValue)}, Initialized={isGameInitialized}");
        
        // Notify other systems
        OnDiceResultChanged?.Invoke(lastDiceValue, currentPlayerColor);

        bool turnChanged = currentTurnIndex != previousTurnIndex || currentPlayerColor != previousPlayerColor;
        if (turnChanged)
        {
            OnTurnChanged?.Invoke(currentTurnIndex, currentPlayerColor);
        }

        if (props.ContainsKey(DiceTransformKey) &&
            TryDeserializeDiceTransform(props[DiceTransformKey], out Vector3 loadedPosition, out Quaternion loadedRotation))
        {
            diceWorldPosition = loadedPosition;
            diceWorldRotation = loadedRotation;
            hasDiceTransform = true;
        }

        if (hasDiceTransform && (!previousHasTransform ||
            diceWorldPosition != previousDicePosition ||
            diceWorldRotation != previousDiceRotation))
        {
            OnDiceTransformChanged?.Invoke(diceWorldPosition, diceWorldRotation);
        }
    }

    /// <summary>
    /// Set dice result (Master Client only) and sync to all clients
    /// </summary>
   public void SetDiceResult(int? value, PlayerColor playerColor)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("❌ Only Master Client can set dice result!");
            return;
        }
        lastDiceValue = NormalizeDiceValue(value);
        currentPlayerColor = playerColor;
        isDiceRolling = false;
        if (playerOrder != null && playerOrder.Count > 0)
        {
            int colorIndex = playerOrder.IndexOf(playerColor);
            if (colorIndex >= 0)
            {
                currentTurnIndex = colorIndex;
            }
        }
        Debug.Log($"🎲 Dice result set: {playerColor} rolled {FormatDiceValue(lastDiceValue)}");
        // Save to room properties
        SaveGameState();

        // Notify all clients via RPC
        var view = GetPhotonView();
        if (view == null) return;
        view.RPC("RPC_SyncDiceResult", RpcTarget.All, ToNetworkValue(lastDiceValue), playerColor);
    }

    public void UpdateDiceTransform(Vector3 position, Quaternion rotation)
    {
        diceWorldPosition = position;
        diceWorldRotation = rotation;
        hasDiceTransform = true;

        if (PhotonNetwork.IsMasterClient && PhotonNetwork.InRoom)
        {
            var props = new ExitGames.Client.Photon.Hashtable
            {
                [DiceTransformKey] = SerializeDiceTransform(position, rotation)
            };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        }

        OnDiceTransformChanged?.Invoke(position, rotation);
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
        lastDiceValue = null;
        isDiceRolling = false;

        Debug.Log($"[GameStateManager] Turn changed: {currentPlayerColor} (Index: {currentTurnIndex})");

        // Save to room properties
        SaveGameState();

        // Notify all clients
        var view = GetPhotonView();
        if (view == null) return;
        view.RPC("RPC_SyncTurn", RpcTarget.All, currentTurnIndex, currentPlayerColor);
        if (hasDiceTransform)
        {
            view.RPC("RPC_SyncDiceTransform", RpcTarget.All, diceWorldPosition, diceWorldRotation);
        }
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
        int? normalizedValue = FromNetworkValue(value);
        lastDiceValue = normalizedValue;
        currentPlayerColor = playerColor;
        isDiceRolling = false;

        Debug.Log($"📢 Received dice result: {playerColor} rolled {FormatDiceValue(normalizedValue)}");

        // Update UI and game logic
        OnDiceResultChanged?.Invoke(normalizedValue, playerColor);

        // Update DiceController
        if (DiceController.Instance != null)
        {
            DiceController.Instance.LastDiceValue = normalizedValue;
            DiceController.Instance.currentRollingPlayer = playerColor;
            DiceController.Instance.isDiceRolling = false;
            DiceController.Instance.hasRolledThisTurn = normalizedValue.HasValue;

            if (DiceController.Instance.diceResultText != null)
            {
                DiceController.Instance.diceResultText.text = $"{playerColor}: {FormatDiceValue(normalizedValue)}";
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
        lastDiceValue = null;
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
    private void RPC_SyncDiceTransform(Vector3 position, Quaternion rotation)
    {
        diceWorldPosition = position;
        diceWorldRotation = rotation;
        hasDiceTransform = true;
        OnDiceTransformChanged?.Invoke(position, rotation);
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
            if (lastDiceValue.HasValue)
            {
                view.RPC("RPC_SyncDiceResult", newPlayer, ToNetworkValue(lastDiceValue), currentPlayerColor);
            }
            else
            {
                // Send current turn
                view.RPC("RPC_SyncTurn", newPlayer, currentTurnIndex, currentPlayerColor);
            }

            if (hasDiceTransform)
            {
                view.RPC("RPC_SyncDiceTransform", newPlayer, diceWorldPosition, diceWorldRotation);
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
        Debug.Log($"Last Dice Value: {FormatDiceValue(lastDiceValue)}");
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

        if (lastDiceValue.HasValue)
        {
            view.RPC("RPC_SyncDiceResult", RpcTarget.Others, ToNetworkValue(lastDiceValue), currentPlayerColor);
        }
        else
        {
            view.RPC("RPC_SyncTurn", RpcTarget.Others, currentTurnIndex, currentPlayerColor);
        }

        if (hasDiceTransform)
        {
            view.RPC("RPC_SyncDiceTransform", RpcTarget.Others, diceWorldPosition, diceWorldRotation);
        }
    }

    #endregion

    private static float[] SerializeDiceTransform(Vector3 position, Quaternion rotation)
    {
        return new float[]
        {
            position.x, position.y, position.z,
            rotation.x, rotation.y, rotation.z, rotation.w
        };
    }

    private static bool TryDeserializeDiceTransform(object rawData, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        float[] data = null;

        if (rawData is float[] floatArray && floatArray.Length == 7)
        {
            data = floatArray;
        }
        else if (rawData is object[] objectArray && objectArray.Length == 7)
        {
            data = new float[7];
            for (int i = 0; i < 7; i++)
            {
                if (objectArray[i] is float f)
                {
                    data[i] = f;
                }
                else if (objectArray[i] is double d)
                {
                    data[i] = (float)d;
                }
                else
                {
                    return false;
                }
            }
        }

        if (data == null)
        {
            return false;
        }

        position = new Vector3(data[0], data[1], data[2]);
        rotation = new Quaternion(data[3], data[4], data[5], data[6]);
        return true;
    }
}
