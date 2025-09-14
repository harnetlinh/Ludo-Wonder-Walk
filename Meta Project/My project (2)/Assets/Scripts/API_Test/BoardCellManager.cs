using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class BoardCellManager : MonoBehaviour
{
    //public static BoardCellManager Instance { get; private set; }

    //[System.Serializable]
    //public class BoardCell
    //{
    //    public int cellIndex;
    //    public string country;
    //    public string description;
    //    public Sprite image;
    //    public Transform cellTransform;
    //}

    //public List<BoardCell> boardCells = new List<BoardCell>();
    //public GameObject cellInfoPanel;
    //public TextMeshProUGUI cellCountryText;
    //public TextMeshProUGUI cellDescriptionText;
    //public UnityEngine.UI.Image cellImage;

    //private void Awake()
    //{
    //    if (Instance == null)
    //    {
    //        Instance = this;
    //        DontDestroyOnLoad(gameObject);
    //    }
    //    else
    //    {
    //        Destroy(gameObject);
    //    }
    //}

    //public void InitializeBoardCells()
    //{
    //    // Khởi tạo các ô trên bàn cờ từ các điểm đường đi
    //    InitializeCommonPathCells();
    //    InitializePrivatePathCells();
    //}

    //private void InitializeCommonPathCells()
    //{
    //    for (int i = 0; i < HorseRacePathManager.Instance.commonPathPoints.Count; i++)
    //    {
    //        BoardCell cell = new BoardCell
    //        {
    //            cellIndex = i,
    //            country = GetCountryForCommonCell(i),
    //            description = GetDescriptionForCommonCell(i),
    //            cellTransform = HorseRacePathManager.Instance.commonPathPoints[i]
    //        };

    //        boardCells.Add(cell);
    //    }
    //}

    //private void InitializePrivatePathCells()
    //{
    //    int baseIndex = HorseRacePathManager.Instance.commonPathPoints.Count;

    //    // Thêm các ô đường riêng cho mỗi màu
    //    AddPrivatePathCells(PlayerColor.Red, HorseRacePathManager.Instance.redPrivatePath, baseIndex);
    //    AddPrivatePathCells(PlayerColor.Blue, HorseRacePathManager.Instance.bluePrivatePath, baseIndex + 10);
    //    AddPrivatePathCells(PlayerColor.Yellow, HorseRacePathManager.Instance.yellowPrivatePath, baseIndex + 20);
    //    AddPrivatePathCells(PlayerColor.Green, HorseRacePathManager.Instance.greenPrivatePath, baseIndex + 30);
    //}

    //private void AddPrivatePathCells(PlayerColor color, List<Transform> path, int startIndex)
    //{
    //    for (int i = 0; i < path.Count; i++)
    //    {
    //        BoardCell cell = new BoardCell
    //        {
    //            cellIndex = startIndex + i,
    //            country = GetCountryForPrivateCell(color, i),
    //            description = GetDescriptionForPrivateCell(color, i),
    //            cellTransform = path[i]
    //        };

    //        boardCells.Add(cell);
    //    }
    //}

    //private string GetCountryForCommonCell(int index)
    //{
    //    // Logic xác định quốc gia dựa trên vị trí ô
    //    // Bạn có thể thay đổi logic này theo nhu cầu
    //    string[] countries = {
    //        "Vietnam", "Thailand", "Japan", "Korea",
    //        "China", "India", "Indonesia", "Malaysia",
    //        "Singapore", "Philippines", "Laos", "Cambodia",
    //        "Myanmar", "Brunei", "Timor-Leste", "Mongolia",
    //        "Nepal", "Bhutan", "Bangladesh", "Sri Lanka",
    //        "Pakistan", "Afghanistan", "Iran", "Iraq",
    //        "Saudi Arabia", "UAE", "Qatar", "Kuwait",
    //        "Oman", "Yemen", "Jordan", "Israel",
    //        "Lebanon", "Syria", "Turkey", "Russia",
    //        "Germany", "France", "UK", "Italy",
    //        "Spain", "Portugal", "Greece", "Egypt",
    //        "Morocco", "Algeria", "Tunisia", "Libya",
    //        "Sudan", "Ethiopia", "Kenya", "South Africa"
    //    };

    //    if (index < countries.Length)
    //    {
    //        return countries[index];
    //    }

    //    return "Unknown";
    //}

    //private string GetDescriptionForCommonCell(int index)
    //{
    //    return $"Common path cell {index}. This cell represents {GetCountryForCommonCell(index)} in the game world.";
    //}

    //private string GetCountryForPrivateCell(PlayerColor color, int index)
    //{
    //    // Ô đường riêng có thể có quốc gia đặc biệt
    //    switch (color)
    //    {
    //        case PlayerColor.Red: return $"RedSpecial{index}";
    //        case PlayerColor.Blue: return $"BlueSpecial{index}";
    //        case PlayerColor.Yellow: return $"YellowSpecial{index}";
    //        case PlayerColor.Green: return $"GreenSpecial{index}";
    //        default: return "PrivateCell";
    //    }
    //}

    //private string GetDescriptionForPrivateCell(PlayerColor color, int index)
    //{
    //    return $"{color} private path cell {index}. This is a special area for {color} player.";
    //}

    //public BoardCell GetCellInfo(int cellIndex)
    //{
    //    return boardCells.Find(cell => cell.cellIndex == cellIndex);
    //}

    //public BoardCell GetCellInfo(Transform cellTransform)
    //{
    //    return boardCells.Find(cell => cell.cellTransform == cellTransform);
    //}

    //public void ShowCellInfo(int cellIndex)
    //{
    //    BoardCell cell = GetCellInfo(cellIndex);
    //    if (cell != null)
    //    {
    //        ShowCellInfo(cell);
    //    }
    //}

    //public void ShowCellInfo(Transform cellTransform)
    //{
    //    BoardCell cell = GetCellInfo(cellTransform);
    //    if (cell != null)
    //    {
    //        ShowCellInfo(cell);
    //    }
    //}

    //public void ShowCellInfo(BoardCell cell)
    //{
    //    cellCountryText.text = cell.country;
    //    cellDescriptionText.text = cell.description;

    //    if (cell.image != null)
    //    {
    //        cellImage.sprite = cell.image;
    //        cellImage.gameObject.SetActive(true);
    //    }
    //    else
    //    {
    //        cellImage.gameObject.SetActive(false);
    //    }

    //    cellInfoPanel.SetActive(true);
    //}

    //public void HideCellInfo()
    //{
    //    cellInfoPanel.SetActive(false);
    //}

    //// Tìm ô gần nhất với vị trí
    //public BoardCell FindNearestCell(Vector3 position)
    //{
    //    BoardCell nearestCell = null;
    //    float minDistance = float.MaxValue;

    //    foreach (BoardCell cell in boardCells)
    //    {
    //        if (cell.cellTransform == null) continue;

    //        float distance = Vector3.Distance(position, cell.cellTransform.position);
    //        if (distance < minDistance)
    //        {
    //            minDistance = distance;
    //            nearestCell = cell;
    //        }
    //    }

    //    return nearestCell;
    //}
}