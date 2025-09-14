using TMPro;
using UnityEngine;

public class PieceStackVisualizer : MonoBehaviour
{
    public TextMeshProUGUI stackText;
    public Canvas stackCanvas;

    //public void SetStackCount(int count, bool isLeader)
    //{
    //    if (stackCanvas == null || stackText == null) return;

    //    if (isLeader && count > 1)
    //    {
    //        stackCanvas.enabled = true;
    //        stackText.text = count.ToString();
    //    }
    //    else
    //    {
    //        stackCanvas.enabled = false;
    //        stackText.text = "";
    //    }
    //}
}
