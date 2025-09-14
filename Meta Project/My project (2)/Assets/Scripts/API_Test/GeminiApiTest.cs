using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.Text;
using System.Linq;
using Newtonsoft.Json.Linq;

public class GeminiApiTest : MonoBehaviour
{
    [Header("API Settings")]
    [SerializeField] private string apiKey = "YOUR_API_KEY_HERE";
    [SerializeField] private string modelId = "gemini-2.5-flash-lite";
    private string apiUrl = "https://generativelanguage.googleapis.com/v1beta/models/";

    [Header("UI Display")]
    public TMP_InputField inputField;
    public TextMeshProUGUI outputText;
    public TextMeshProUGUI tokenCountText; // Optional: để hiển thị số token

    public void OnSendRequest()
    {
        string userInput = inputField.text;
        StartCoroutine(SendRequest(userInput));
    }

    private IEnumerator SendRequest(string userMessage)
    {
        string fullUrl = $"{apiUrl}{modelId}:streamGenerateContent?key={apiKey}";

        // JSON body giống hệt request.json trong Bash
        string jsonBody = $@"
        {{
            ""contents"": [
                {{
                    ""role"": ""user"",
                    ""parts"": [
                        {{
                            ""text"": ""{userMessage}""
                        }}
                    ]
                }}
            ],
            ""generationConfig"": {{
                ""thinkingConfig"": {{
                    ""thinkingBudget"": 0
                }}
            }}
        }}";

        using (UnityWebRequest request = new UnityWebRequest(fullUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError(request.error);
                outputText.text = "Error: " + request.error;
            }
            else
            {
                string responseText = request.downloadHandler.text;
                Debug.Log("Raw Response: " + responseText);

                // Xử lý phản hồi stream
                string extractedText = ExtractTextFromStreamResponse(responseText);
                outputText.text = extractedText;

                // Optional: Hiển thị thông tin token count
                DisplayTokenInfo(responseText);
            }
        }
    }

    private string ExtractTextFromStreamResponse(string jsonResponse)
    {
        StringBuilder fullText = new StringBuilder();
        int totalTokens = 0;

        try
        {
            // Phân tích phản hồi stream (mảng JSON objects)
            string cleanedResponse = jsonResponse.Trim();
            if (!cleanedResponse.StartsWith("["))
            {
                cleanedResponse = "[" + cleanedResponse + "]";
            }

            JArray responseArray = JArray.Parse(cleanedResponse);

            foreach (JObject responseObj in responseArray.OfType<JObject>())
            {
                // Trích xuất text từ mỗi chunk
                if (responseObj["candidates"] != null && responseObj["candidates"].Any())
                {
                    var candidate = responseObj["candidates"][0];
                    if (candidate["content"]?["parts"] != null && candidate["content"]["parts"].Any())
                    {
                        string text = candidate["content"]["parts"][0]?["text"]?.ToString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            fullText.Append(text);
                        }
                    }
                }

                // Lấy thông tin token count từ chunk cuối cùng
                if (responseObj["usageMetadata"] != null)
                {
                    totalTokens = responseObj["usageMetadata"]?["totalTokenCount"]?.Value<int>() ?? 0;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error parsing response: " + e.Message);
            return "Error parsing response: " + e.Message;
        }

        Debug.Log($"Total tokens used: {totalTokens}");
        return fullText.ToString();
    }

    private void DisplayTokenInfo(string jsonResponse)
    {
        try
        {
            string cleanedResponse = jsonResponse.Trim();
            if (!cleanedResponse.StartsWith("["))
            {
                cleanedResponse = "[" + cleanedResponse + "]";
            }

            JArray responseArray = JArray.Parse(cleanedResponse);

            // Lấy thông tin từ chunk cuối cùng (thường chứa thông tin đầy đủ)
            JObject lastChunk = responseArray.Last as JObject;
            if (lastChunk != null && lastChunk["usageMetadata"] != null)
            {
                int promptTokens = lastChunk["usageMetadata"]?["promptTokenCount"]?.Value<int>() ?? 0;
                int candidatesTokens = lastChunk["usageMetadata"]?["candidatesTokenCount"]?.Value<int>() ?? 0;
                int totalTokens = lastChunk["usageMetadata"]?["totalTokenCount"]?.Value<int>() ?? 0;

                if (tokenCountText != null)
                {
                    tokenCountText.text = $"Tokens: Prompt {promptTokens} + Response {candidatesTokens} = Total {totalTokens}";
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error extracting token info: " + e.Message);
        }
    }
}