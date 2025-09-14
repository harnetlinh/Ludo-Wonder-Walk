using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class FactResponse
{
    public string title;
    public string image;
    public string description;
}

public class FactManager : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;
    public Image factImage;

    private string apiUrl = "";

    private FactResponse factVi;
    private FactResponse factEn;

    private bool isVietnamese = false; // trạng thái ngôn ngữ hiện tại


    public static FactManager Instance { get; private set; }

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

    public void GetFact(string country = "Vietnam")
    {
        Debug.Log($"[DEBUG] FactManager.GetFact called with country: {country}");
        StartCoroutine(CallAPI(country, "vi"));
        StartCoroutine(CallAPI(country, "en"));
    }

    IEnumerator CallAPI(string country, string lang)
    {
        string url = $"{apiUrl}?country={country}&lang={lang}";
        Debug.Log($"[DEBUG] Calling API: {url}");
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"[DEBUG] API Error: {request.error}");
            }
            else
            {
                Debug.Log($"[DEBUG] API Response ({lang}): {request.downloadHandler.text}");
                FactResponse fact = JsonUtility.FromJson<FactResponse>(request.downloadHandler.text);

                if (lang == "vi")
                {
                    factVi = fact;
                    Debug.Log($"[DEBUG] factVi assigned. isVietnamese: {isVietnamese}");
                    if (isVietnamese) UpdateUI(factVi);
                }
                else
                {
                    factEn = fact;
                    Debug.Log($"[DEBUG] factEn assigned. isVietnamese: {isVietnamese}");
                    if (!isVietnamese) UpdateUI(factEn);
                }
            }
        }
    }

    private void UpdateUI(FactResponse fact)
    {
        Debug.Log($"[DEBUG] UpdateUI called. fact is null: {fact == null}");
        if (fact == null) return;

        Debug.Log($"[DEBUG] Updating UI with title: {fact.title}");
        Debug.Log($"[DEBUG] titleText is null: {titleText == null}");
        Debug.Log($"[DEBUG] descriptionText is null: {descriptionText == null}");
        Debug.Log($"[DEBUG] factImage is null: {factImage == null}");

        if (titleText != null) titleText.text = fact.title;
        if (descriptionText != null) descriptionText.text = fact.description;
        if (factImage != null) StartCoroutine(LoadImage(fact.image));
    }

    IEnumerator LoadImage(string imageUrl)
    {
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Texture2D tex = DownloadHandlerTexture.GetContent(request);
            factImage.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
        }
        else
        {
            Debug.LogError("Image load error: " + request.error);
        }
    }

    // Hàm gọi từ Button để đổi ngôn ngữ
    public void ToggleLanguage()
    {
        isVietnamese = !isVietnamese;

        if (isVietnamese && factVi != null)
        {
            UpdateUI(factVi);
        }
        else if (!isVietnamese && factEn != null)
        {
            UpdateUI(factEn);
        }
    }
}
