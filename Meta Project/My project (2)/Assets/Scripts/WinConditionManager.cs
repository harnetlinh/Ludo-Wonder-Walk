using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WinConditionManager : MonoBehaviour
{
    public static WinConditionManager Instance { get; private set; }

    [Header("UI References")]
    public TextMeshProUGUI winnerText;
    public GameObject winPanel;
    public Button restartButton;
    public Button mainMenuButton;

    [Header("Game Settings")]
    public int piecesToWin = 4; // Số quân cần về đích để thắng

    private Dictionary<PlayerColor, int> finishedPieces = new Dictionary<PlayerColor, int>();
    private bool gameEnded = false;

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
        // Khởi tạo dictionary đếm số quân về đích
        foreach (PlayerColor color in System.Enum.GetValues(typeof(PlayerColor)))
        {
            finishedPieces.Add(color, 0);
        }

        // Thiết lập nút bấm
        if (restartButton != null)
            restartButton.onClick.AddListener(RestartGame);

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);

        // Ẩn panel thắng cuộc ban đầu
        if (winPanel != null)
            winPanel.SetActive(false);
    }

    // Phương thức được gọi khi một quân cờ về đích
    public void PieceFinished(PlayerColor color)
    {
        if (gameEnded) return;

        finishedPieces[color]++;
        Debug.Log($"{color} has {finishedPieces[color]} pieces finished");

        // Kiểm tra điều kiện thắng
        if (finishedPieces[color] >= piecesToWin)
        {
            EndGame(color);
        }
    }

    // Kiểm tra xem một quân cờ đã về đích chưa
    public bool IsPieceFinished(int pathIndex, PlayerColor playerColor)
    {
        // Lấy đường đi riêng của người chơi
        List<Transform> privatePath = HorseRacePathManager.Instance.GetPrivatePath(playerColor);

        // Kiểm tra nếu quân cờ đang ở điểm cuối cùng của đường riêng
        if (pathIndex >= HorseRacePathManager.Instance.commonPathPoints.Count)
        {
            int privateIndex = pathIndex - HorseRacePathManager.Instance.commonPathPoints.Count;
            return privateIndex >= privatePath.Count - 1; // Điểm cuối cùng
        }

        return false;
    }

    // Kết thúc game và hiển thị người chiến thắng
    private void EndGame(PlayerColor winnerColor)
    {
        gameEnded = true;
        Debug.Log($"Game Over! {winnerColor} wins!");

        // Hiển thị UI thắng cuộc
        if (winPanel != null)
        {
            winPanel.SetActive(true);
            if (winnerText != null)
            {
                winnerText.text = $"{GetColorName(winnerColor)} Chiến Thắng!";
                winnerText.color = GetColor(winnerColor);
            }
        }

        // Vô hiệu hóa xúc xắc và các tương tác
        if (DiceController.Instance != null)
        {
            DiceController.Instance.diceButton.interactable = false;
        }

        // Dừng tất cả các di chuyển đang thực hiện
        PieceController[] allPieces = FindObjectsByType<PieceController>(FindObjectsSortMode.None);
        foreach (PieceController piece in allPieces)
        {
            piece.StopAllCoroutines();
            piece.isMoving = false;
        }

        // Hiệu ứng chiến thắng (có thể thêm âm thanh, particle effects, etc.)
        StartCoroutine(VictoryEffects(winnerColor));
    }

    // Hiệu ứng chiến thắng
    private IEnumerator VictoryEffects(PlayerColor winnerColor)
    {
        // Tìm tất cả quân cờ của người chiến thắng
        PieceController[] winnerPieces = FindObjectsByType<PieceController>(FindObjectsSortMode.None);
        foreach (PieceController piece in winnerPieces)
        {
            if (piece.playerColor == winnerColor)
            {
                // Thêm hiệu ứng nhảy hoặc xoay cho quân cờ chiến thắng
                StartCoroutine(JumpEffect(piece.transform));
            }
        }

        // Phát âm thanh chiến thắng
        // AudioManager.Instance.PlayVictorySound();

        yield return new WaitForSeconds(2f);

        // Có thể thêm hiệu ứng firework hoặc confetti ở đây
    }

    // Hiệu ứng nhảy cho quân cờ
    private IEnumerator JumpEffect(Transform pieceTransform)
    {
        float jumpHeight = 0.5f;
        float jumpDuration = 0.5f;
        Vector3 startPosition = pieceTransform.position;

        for (int i = 0; i < 3; i++) // Nhảy 3 lần
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

    // Khởi động lại game
    public void RestartGame()
    {
        gameEnded = false;

        // Reset số quân về đích
        foreach (PlayerColor color in finishedPieces.Keys)
        {
            finishedPieces[color] = 0;
        }

        // Ẩn panel thắng cuộc
        if (winPanel != null)
            winPanel.SetActive(false);

        // Reset tất cả quân cờ về chuồng
        PieceController[] allPieces = FindObjectsByType<PieceController>(FindObjectsSortMode.None);
        foreach (PieceController piece in allPieces)
        {
            piece.currentPathIndex = -1;
            piece.transform.position = piece.GetInitialStablePosition();
        }

        // Khởi động lại lượt chơi
        if (GameTurnManager.Instance != null)
        {
            GameTurnManager.Instance.currentPlayerIndex = 0;
            GameTurnManager.Instance.InitializePlayerOrder(DiceController.Instance);
        }

        // Kích hoạt lại xúc xắc
        if (DiceController.Instance != null)
        {
            DiceController.Instance.diceButton.interactable = true;
        }
    }

    // Quay về menu chính
    public void ReturnToMainMenu()
    {
        // Implement logic để quay về menu chính
        // SceneManager.LoadScene("MainMenu");
        Debug.Log("Returning to main menu...");
    }

    // Hàm trợ giúp để lấy tên màu
    private string GetColorName(PlayerColor color)
    {
        switch (color)
        {
            case PlayerColor.Red: return "Đỏ";
            case PlayerColor.Blue: return "Xanh Dương";
            case PlayerColor.Yellow: return "Vàng";
            case PlayerColor.Green: return "Xanh Lá";
            default: return color.ToString();
        }
    }

    // Hàm trợ giúp để lấy màu sắc
    private Color GetColor(PlayerColor color)
    {
        switch (color)
        {
            case PlayerColor.Red: return Color.red;
            case PlayerColor.Blue: return Color.blue;
            case PlayerColor.Yellow: return Color.yellow;
            case PlayerColor.Green: return Color.green;
            default: return Color.white;
        }
    }

    // Kiểm tra game đã kết thúc chưa
    public bool IsGameEnded()
    {
        return gameEnded;
    }
}