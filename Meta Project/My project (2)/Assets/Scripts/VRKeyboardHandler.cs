using UnityEngine;
using TMPro;

public class MetaKeyboardOverlay : MonoBehaviour
{
    public TMP_InputField inputField;
    private TouchScreenKeyboard overlayKeyboard;

    void Start()
    {
        inputField.onSelect.AddListener(OnInputFocus);
    }

    void Update()
    {
        if (overlayKeyboard != null)
        {
            // Cập nhật text từ bàn phím vào InputField
            inputField.text = overlayKeyboard.text;
        }
    }

    void OnInputFocus(string text)
    {
        // Mở bàn phím hệ thống (overlay)
        overlayKeyboard = TouchScreenKeyboard.Open(
            inputField.text,
            TouchScreenKeyboardType.Default,
            false,  // autocorrect
            false,  // multiline
            false   // secure
        );
    }
}
