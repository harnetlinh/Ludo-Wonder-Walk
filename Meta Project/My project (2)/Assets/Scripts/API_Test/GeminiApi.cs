using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class GeminiApi : MonoBehaviour
{
    [Header("UI hiển thị")]
    public TextMeshProUGUI textDisplay;  // Text để hiển thị response text
    public RawImage imageDisplay;        // Hiển thị ảnh trả về (nếu có)

    [Header("Config API")]
    [SerializeField] private string apiKey = "YOUR_API_KEY";
    [SerializeField] private string modelId = "gemini-2.5-flash-image-preview";
    [SerializeField] private string userPrompt = "INSERT_INPUT_HERE";

    private string baseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";

    public void CallGeminiAPI()
    {
        StartCoroutine(SendRequest());
    }

    private IEnumerator SendRequest()
    {
        string url = $"{baseUrl}{modelId}:streamGenerateContent?key={apiKey}";

        // JSON body giống file request.json bạn viết
        string jsonBody = $@"
        {{
            ""contents"": [
              {{
                ""role"": ""user"",
                ""parts"": [
                  {{
                    ""text"": ""{userPrompt}""
                  }}
                ]
              }}
            ],
            ""generationConfig"": {{
              ""responseModalities"": [""IMAGE"", ""TEXT""]
            }}
        }}";

        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string response = request.downloadHandler.text;
                Debug.Log("Gemini Response: " + response);

                if (textDisplay != null)
                    textDisplay.text = response; // TODO: parse JSON để lấy text gọn hơn

                // TODO: parse ảnh từ JSON (base64 hoặc URL) rồi hiển thị
                // ví dụ: StartCoroutine(LoadImageFromUrl(imageUrl));
            }
            else
            {
                Debug.LogError("Error: " + request.error);
                if (textDisplay != null)
                    textDisplay.text = "Error: " + request.error;
            }
        }
    }

    // Hàm load ảnh nếu API trả về link ảnh
    private IEnumerator LoadImageFromUrl(string url)
    {
        UnityWebRequest textureRequest = UnityWebRequestTexture.GetTexture(url);
        yield return textureRequest.SendWebRequest();

        if (textureRequest.result == UnityWebRequest.Result.Success)
        {
            Texture2D tex = DownloadHandlerTexture.GetContent(textureRequest);
            imageDisplay.texture = tex;
        }
        else
        {
            Debug.LogError("Image load failed: " + textureRequest.error);
        }
    }
}
