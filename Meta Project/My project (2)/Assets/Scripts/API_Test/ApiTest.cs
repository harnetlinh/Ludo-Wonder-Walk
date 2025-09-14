using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Text;
using System.Collections.Generic;

public class GeminiAIClient : MonoBehaviour
{
    public TextMeshProUGUI textDisplay;
    public Image imageDisplay;

    [SerializeField] private string apiKey = "YOUR_API_KEY_HERE"; // Thay bằng API key của bạn
    [SerializeField] private string modelId = "gemini-2.5-flash-image-preview";
    [SerializeField] private string liteModelId = "gemini-2.5-flash-lite";
    [SerializeField] private string baseUrl = "";

    // Rate limiting variables
    private float lastRequestTime = 0f;
    private float minRequestInterval = 2f; // Tối thiểu 2 giây giữa các request
    private int requestCount = 0;
    private const int MAX_REQUESTS_PER_MINUTE = 60;

    // Hàm gọi API Gemini để lấy cả ảnh và text
    public void CallGeminiWithImage(string prompt)
    {
        if (CanMakeRequest())
        {
            StartCoroutine(GenerateContentWithImage(prompt));
        }
        else
        {
            textDisplay.text = "Please wait before making another request";
        }
    }

    // Hàm gọi API Gemini chỉ lấy text
    public void CallGeminiTextOnly(string prompt)
    {
        if (CanMakeRequest())
        {
            StartCoroutine(GenerateTextOnly(prompt));
        }
        else
        {
            textDisplay.text = "Please wait before making another request";
        }
    }

    private bool CanMakeRequest()
    {
        float currentTime = Time.time;

        // Kiểm tra rate limiting
        if (currentTime - lastRequestTime < minRequestInterval)
        {
            return false;
        }

        // Reset counter mỗi phút
        if (currentTime - lastRequestTime > 60f)
        {
            requestCount = 0;
        }

        if (requestCount >= MAX_REQUESTS_PER_MINUTE)
        {
            return false;
        }

        lastRequestTime = currentTime;
        requestCount++;
        return true;
    }

    private IEnumerator GenerateContentWithImage(string prompt)
    {
        string url = $"{baseUrl}{modelId}:streamGenerateContent?key={apiKey}";

        // Tạo JSON request
        var requestData = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                responseModalities = new[] { "IMAGE", "TEXT" }
            }
        };

        string json = JsonUtility.ToJson(requestData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                ProcessGeminiResponse(request.downloadHandler.text);
            }
            else
            {
                HandleError(request);
            }
        }
    }

    private IEnumerator GenerateTextOnly(string prompt)
    {
        string url = $"{baseUrl}{liteModelId}:streamGenerateContent?key={apiKey}";

        var requestData = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                thinkingConfig = new
                {
                    thinkingBudget = 0
                }
            }
        };

        string json = JsonUtility.ToJson(requestData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                ProcessTextResponse(request.downloadHandler.text);
            }
            else
            {
                HandleError(request);
            }
        }
    }

    private void ProcessGeminiResponse(string responseJson)
    {
        try
        {
            // Phân tích response JSON (cần thêm logic xử lý cụ thể)
            // Response sẽ chứa cả text và image data
            var response = JsonUtility.FromJson<GeminiResponse>(responseJson);

            if (response != null && response.candidates.Length > 0)
            {
                var candidate = response.candidates[0];

                // Hiển thị text
                textDisplay.text = candidate.content.parts[0].text;

                // Xử lý ảnh nếu có
                if (candidate.content.parts.Length > 1 &&
                    candidate.content.parts[1].inlineData != null)
                {
                    LoadImageFromBase64(candidate.content.parts[1].inlineData.data);
                }
            }
        }
        catch (System.Exception e)
        {
            textDisplay.text = $"Error processing response: {e.Message}";
        }
    }

    private void ProcessTextResponse(string responseJson)
    {
        try
        {
            var response = JsonUtility.FromJson<GeminiResponse>(responseJson);

            if (response != null && response.candidates.Length > 0)
            {
                textDisplay.text = response.candidates[0].content.parts[0].text;
            }
        }
        catch (System.Exception e)
        {
            textDisplay.text = $"Error processing text response: {e.Message}";
        }
    }

    private void LoadImageFromBase64(string base64Data)
    {
        try
        {
            byte[] imageBytes = System.Convert.FromBase64String(base64Data);
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(imageBytes);

            Sprite sprite = Sprite.Create(texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));

            imageDisplay.sprite = sprite;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error loading image: {e.Message}");
        }
    }

    private void HandleError(UnityWebRequest request)
    {
        if (request.responseCode == 429)
        {
            textDisplay.text = "Rate limit exceeded. Please wait before making another request.";
            // Tự động tăng khoảng cách giữa các request khi bị rate limit
            minRequestInterval = Mathf.Min(minRequestInterval * 2f, 10f);
        }
        else
        {
            textDisplay.text = $"Error: {request.responseCode} - {request.error}";
        }

        Debug.LogError($"API Error: {request.responseCode} - {request.error}");
    }

    // Các lớp để parse JSON response
    [System.Serializable]
    private class GeminiResponse
    {
        public Candidate[] candidates;
    }

    [System.Serializable]
    private class Candidate
    {
        public Content content;
    }

    [System.Serializable]
    private class Content
    {
        public Part[] parts;
    }

    [System.Serializable]
    private class Part
    {
        public string text;
        public InlineData inlineData;
    }

    [System.Serializable]
    private class InlineData
    {
        public string mimeType;
        public string data;
    }
}
