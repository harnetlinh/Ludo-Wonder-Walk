using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PieceStackManager : MonoBehaviour
{
    public static PieceStackManager Instance;

    // Dictionary để lưu leader của mỗi stack index
    private Dictionary<int, PieceController> stackLeaders = new Dictionary<int, PieceController>();

    //private void Awake()
    //{
    //    Instance = this;
    //}

    //void Update()
    //{
    //    UpdateStacks();
    //}

    //private void UpdateStacks()
    //{
    //    // Sử dụng FindObjectsOfType với includeInactive = true để tìm TẤT CẢ quân cờ (kể cả đang tắt)
    //    PieceController[] allPieces = FindObjectsOfType<PieceController>(true);

    //    // Gom nhóm theo index đường đi
    //    Dictionary<int, List<PieceController>> stacks = new Dictionary<int, List<PieceController>>();

    //    foreach (var piece in allPieces)
    //    {
    //        if (piece.currentPathIndex >= 0) // chỉ tính quân trên bàn cờ
    //        {
    //            if (!stacks.ContainsKey(piece.currentPathIndex))
    //                stacks[piece.currentPathIndex] = new List<PieceController>();

    //            stacks[piece.currentPathIndex].Add(piece);
    //        }
    //        else
    //        {
    //            // Quân trong chuồng hoặc đã về đích thì bật lại
    //            piece.gameObject.SetActive(true);
    //            var vis = piece.GetComponent<PieceStackVisualizer>();
    //            if (vis != null) vis.SetStackCount(0, true);
    //        }
    //    }

    //    // Xử lý từng stack
    //    foreach (var kvp in stacks)
    //    {
    //        int stackIndex = kvp.Key;
    //        List<PieceController> stackPieces = kvp.Value;

    //        // ĐẦU TIÊN: bật TẤT CẢ quân trong stack này lên để đảm bảo đếm đủ
    //        foreach (var piece in stackPieces)
    //        {
    //            piece.gameObject.SetActive(true);

    //        }

    //        if (stackPieces.Count > 1)
    //        {
    //            // Xác định leader - ƯU TIÊN leader cũ nếu có
    //            PieceController leader = DetermineLeader(stackIndex, stackPieces);

    //            // Đảm bảo leader được active và hiển thị stack count
    //            leader.gameObject.SetActive(true);
    //            var vis = leader.GetComponent<PieceStackVisualizer>();
    //            if (vis != null) vis.SetStackCount(stackPieces.Count, true);

    //            // Tắt các quân còn lại (không phải leader)
    //            for (int i = 0; i < stackPieces.Count; i++)
    //            {
    //                if (stackPieces[i] != leader)
    //                {
    //                    stackPieces[i].gameObject.SetActive(false);
    //                }
    //            }

    //            // Lưu leader cho lần sau
    //            stackLeaders[stackIndex] = leader;
    //        }
    //        else
    //        {
    //            // Nếu chỉ có 1 quân
    //            PieceController solo = stackPieces[0];
    //            solo.gameObject.SetActive(true);

    //            var vis = solo.GetComponent<PieceStackVisualizer>();
    //            if (vis != null) vis.SetStackCount(0, false);

    //            // Xóa leader cũ nếu stack chỉ còn 1 quân
    //            if (stackLeaders.ContainsKey(stackIndex))
    //            {
    //                stackLeaders.Remove(stackIndex);
    //            }
    //        }
    //    }
    //}

    //// Phương thức xác định leader cho stack
    //private PieceController DetermineLeader(int stackIndex, List<PieceController> stackPieces)
    //{
    //    // Ưu tiên sử dụng leader cũ nếu có và vẫn còn trong stack
    //    if (stackLeaders.ContainsKey(stackIndex) && stackPieces.Contains(stackLeaders[stackIndex]))
    //    {
    //        return stackLeaders[stackIndex];
    //    }

    //    // Nếu không có leader cũ, chọn quân đầu tiên làm leader
    //    return stackPieces[0];
    //}
}