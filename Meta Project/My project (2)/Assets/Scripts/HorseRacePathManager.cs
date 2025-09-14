
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;



[System.Serializable]
public class CountryPoint
{
    public int pointIndex;
    public string countryCode;
    public PlayerColor exclusiveColor = PlayerColor.None; // Mặc định là None (không độc quyền)
}

public class HorseRacePathManager : MonoBehaviour
{
    // Singleton pattern để dễ dàng truy cập từ mọi nơi
    public static HorseRacePathManager Instance { get; private set; }

    // Các điểm đường đi chung (từ 1 đến 52)
    public List<Transform> commonPathPoints = new List<Transform>();

    // Các điểm đường đi riêng cho mỗi màu (đỏ, xanh, vàng, xanh lá)
    public List<Transform> redPrivatePath;
    public List<Transform> bluePrivatePath;
    public List<Transform> yellowPrivatePath;
    public List<Transform> greenPrivatePath;

    // Các điểm xuất chuồng cho mỗi màu
    public Transform redStartPoint;
    public Transform blueStartPoint;
    public Transform yellowStartPoint;
    public Transform greenStartPoint;

    // Các ô safe zone (an toàn)
    public List<int> safeZoneIndices = new List<int>() { 3, 8, 16, 21, 29, 34, 42, 47 };

    // Các ô chuồng cho mỗi màu (để đặt lại quân khi bị đá)
    public List<Transform> redStablePoints = new List<Transform>();
    public List<Transform> blueStablePoints = new List<Transform>();
    public List<Transform> yellowStablePoints = new List<Transform>();
    public List<Transform> greenStablePoints = new List<Transform>();




    [Header("Country Points Management")]
    public List<CountryPoint> countryPoints = new List<CountryPoint>();

    private Dictionary<int, CountryPoint> countryPointDict = new Dictionary<int, CountryPoint>();

    //// Thêm vào đầu file HorseRacePathManager.cs
    //[System.Serializable]
    //public class SpecialPointImage
    //{
    //    public int pointIndex;
    //    public Sprite image;
    //}

    // Thêm dictionary để lưu điểm chuyển tiếp sang đường riêng
    private Dictionary<PlayerColor, int> transitionPoints = new Dictionary<PlayerColor, int>()
    {
        { PlayerColor.Red, 19 },
        { PlayerColor.Blue, 6 },
        { PlayerColor.Yellow, 45 },
        { PlayerColor.Green, 32 }
    };

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

        // Khởi tạo dictionary từ list
        Debug.Log($"[DEBUG] Initializing country points. Total count: {countryPoints.Count}");
        foreach (var point in countryPoints)
        {
            countryPointDict[point.pointIndex] = point;
            Debug.Log($"[DEBUG] Added country point: Index {point.pointIndex}, Country: {point.countryCode}, ExclusiveColor: {point.exclusiveColor}");
        }
    }

    public Transform GetNextPoint(int currentIndex, PlayerColor playerColor, out bool isPrivatePath)
    {
        isPrivatePath = false;

        // Kiểm tra index hợp lệ
        if (currentIndex < 0)
        {
            return null;
        }

        // Đang ở đường chung
        if (currentIndex < commonPathPoints.Count)
        {
            // Kiểm tra điểm chuyển sang đường riêng
            if (IsTransitionToPrivatePath(currentIndex, playerColor))
            {
                isPrivatePath = true;
                var firstPrivate = GetFirstPrivatePathPoint(playerColor);
                return firstPrivate;
            }

            // SỬA LẠI: Tính toán điểm tiếp theo theo chiều thuận
            int nextIndex = (currentIndex + 1) % commonPathPoints.Count;
            if (nextIndex < 0 || nextIndex >= commonPathPoints.Count)
            {
                return null;
            }

            return commonPathPoints[nextIndex];
        }
        // Đang ở đường riêng
        else
        {
            isPrivatePath = true;
            List<Transform> privatePath = GetPrivatePath(playerColor);
            int privateIndex = currentIndex - commonPathPoints.Count;

            if (privateIndex < 0 || privateIndex >= privatePath.Count - 1)
            {
                return null; // Đã đến cuối đường riêng
            }

            return privatePath[privateIndex + 1];
        }
    }

    // Kiểm tra xem có phải điểm chuyển sang đường riêng không
    private bool IsTransitionToPrivatePath(int currentIndex, PlayerColor playerColor)
    {
        return currentIndex == transitionPoints[playerColor];
    }

    // Lấy điểm đầu tiên của đường riêng
    private Transform GetFirstPrivatePathPoint(PlayerColor playerColor)
    {
        return GetPrivatePath(playerColor)[0];
    }

    // Lấy đường đi riêng tương ứng với màu
    public List<Transform> GetPrivatePath(PlayerColor playerColor)
    {
        switch (playerColor)
        {
            case PlayerColor.Red: return redPrivatePath;
            case PlayerColor.Blue: return bluePrivatePath;
            case PlayerColor.Yellow: return yellowPrivatePath;
            case PlayerColor.Green: return greenPrivatePath;
            default: return redPrivatePath;
        }
    }

    // Lấy điểm xuất chuồng tương ứng với màu
    public Transform GetStartPoint(PlayerColor playerColor)
    {
        switch (playerColor)
        {
            case PlayerColor.Red: return redStartPoint;
            case PlayerColor.Blue: return blueStartPoint;
            case PlayerColor.Yellow: return yellowStartPoint;
            case PlayerColor.Green: return greenStartPoint;
            default: return redStartPoint;
        }
    }

    // Lấy danh sách ô chuồng tương ứng với màu
    public List<Transform> GetStablePoints(PlayerColor playerColor)
    {
        switch (playerColor)
        {
            case PlayerColor.Red: return redStablePoints;
            case PlayerColor.Blue: return blueStablePoints;
            case PlayerColor.Yellow: return yellowStablePoints;
            case PlayerColor.Green: return greenStablePoints;
            default: return redStablePoints;
        }
    }

    // Kiểm tra xem một điểm có phải là safe zone không
    public bool IsSafeZone(int pointIndex, PlayerColor pieceColor)
    {
        // Nếu là đường chung
        if (pointIndex < commonPathPoints.Count)
        {
            // Kiểm tra safe zone thông thường
            if (safeZoneIndices.Contains(pointIndex))
                return true;

            // Kiểm tra điểm xuất phát của chính người chơi cũng là safe zone
            Transform startPoint = GetStartPoint(pieceColor);
            int startIndex = commonPathPoints.IndexOf(startPoint);
            if (pointIndex == startIndex)
                return true;
        }

        // Đường riêng: chỉ an toàn nếu là đường riêng của màu đó
        if (pointIndex >= commonPathPoints.Count)
        {
            int privateIndex = pointIndex - commonPathPoints.Count;
            var privatePath = GetPrivatePath(pieceColor);
            return privateIndex >= 0 && privateIndex < privatePath.Count;
        }

        return false;
    }

    public Transform GetCurrentPoint(int currentIndex, PlayerColor playerColor)
    {
        if (currentIndex < 0) return null;

        if (currentIndex < commonPathPoints.Count)
        {
            return commonPathPoints[currentIndex];
        }
        else
        {
            List<Transform> privatePath = GetPrivatePath(playerColor);
            int privateIndex = currentIndex - commonPathPoints.Count;

            if (privateIndex >= 0 && privateIndex < privatePath.Count)
            {
                return privatePath[privateIndex];
            }
            else
            {
                Debug.LogError($"Invalid private index: {privateIndex} for {playerColor}");
                return null;
            }
        }
    }

    public Transform GetPreviousPoint(int currentIndex, PlayerColor playerColor)
    {
        // Nếu đang ở điểm xuất phát (index 0), đi đến điểm cuối cùng của đường chung
        if (currentIndex == 0)
        {
            return commonPathPoints[commonPathPoints.Count - 1];
        }

        // Nếu đang ở đường chung, đi đến điểm trước đó
        if (currentIndex < commonPathPoints.Count)
        {
            return commonPathPoints[currentIndex - 1];
        }

        // Nếu đang ở đường riêng, xử lý tương tự như GetNextPoint
        List<Transform> privatePath = GetPrivatePath(playerColor);
        int privateIndex = currentIndex - commonPathPoints.Count;

        if (privateIndex > 0)
        {
            return privatePath[privateIndex - 1];
        }
        else
        {
            // Nếu ở điểm đầu tiên của đường riêng, quay về điểm chuyển tiếp
            return GetTransitionPoint(playerColor);
        }
    }

    // Thêm phương thức lấy điểm chuyển tiếp
    private Transform GetTransitionPoint(PlayerColor playerColor)
    {
        return commonPathPoints[transitionPoints[playerColor]];
    }


    public Transform GetEndPoint(PlayerColor playerColor)
    {
        List<Transform> privatePath = GetPrivatePath(playerColor);
        if (privatePath.Count > 0)
        {
            return privatePath[privatePath.Count - 1];
        }
        return null;
    }


    // Thêm phương thức mới để lấy thông tin quốc gia của điểm
    // Xóa dictionary countryPointDict và thay thế các hàm liên quan

    public CountryPoint GetCountryPointInfo(int pointIndex, PlayerColor playerColor)
    {
        foreach (var point in countryPoints)
        {
            if (point.pointIndex == pointIndex)
            {
                // Tương tự logic trên: nếu không độc quyền hoặc độc quyền và khớp màu
                if (point.exclusiveColor == PlayerColor.None || point.exclusiveColor == playerColor)
                {
                    return point;
                }
            }
        }
        return null;
    }

    public bool IsCountryPoint(int pointIndex, PlayerColor playerColor)
    {
        foreach (var point in countryPoints)
        {
            if (point.pointIndex == pointIndex)
            {
                if (point.exclusiveColor == PlayerColor.None || point.exclusiveColor == playerColor)
                {
                    return true;
                }
            }
        }
        return false;
    }

    // Lấy mã quốc gia của điểm (nếu có)
    public string GetCountryCode(int pointIndex, PlayerColor playerColor)
    {
        Debug.Log($"[DEBUG] GetCountryCode called for point {pointIndex}, player {playerColor}");

        // Duyệt qua tất cả các điểm quốc gia để tìm điểm phù hợp
        foreach (var countryPoint in countryPoints)
        {
            if (countryPoint.pointIndex == pointIndex)
            {
                Debug.Log($"[DEBUG] Found country point: Index {countryPoint.pointIndex}, Country: {countryPoint.countryCode}, ExclusiveColor: {countryPoint.exclusiveColor}");

                // Nếu điểm không độc quyền (exclusiveColor là None) -> trả về luôn
                if (countryPoint.exclusiveColor == PlayerColor.None)
                {
                    Debug.Log($"[DEBUG] Returning countryCode: '{countryPoint.countryCode}' (non-exclusive)");
                    return countryPoint.countryCode;
                }
                // Nếu điểm độc quyền và màu người chơi khớp -> trả về
                else if (countryPoint.exclusiveColor == playerColor)
                {
                    Debug.Log($"[DEBUG] Returning countryCode: '{countryPoint.countryCode}' (exclusive for {playerColor})");
                    return countryPoint.countryCode;
                }
            }
        }

        Debug.Log($"[DEBUG] No valid country point found for index {pointIndex} and player {playerColor}");
        return null;
    }
}

// Enum màu cho người chơi
public enum PlayerColor
{
    None,    // Thêm giá trị None để đại diện cho null
    Red,
    Blue,
    Yellow,
    Green
}
