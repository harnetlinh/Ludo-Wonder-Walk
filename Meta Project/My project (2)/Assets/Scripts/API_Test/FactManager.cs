using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;

[System.Serializable]
public class FactResponse
{
    public string title;
    public string image;
    public string description;
}

[RequireComponent(typeof(PhotonView))]
public class FactManager : MonoBehaviourPunCallbacks
{
    [Header("UI Elements")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;
    public Image factImage;

    private string apiUrl = "https://ludo-mr.sapca.ai.vn/api/fact";

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

        if (string.IsNullOrEmpty(country))
        {
            Debug.LogWarning("[DEBUG] Country parameter is null or empty. Abort fetching fact.");
            return;
        }

        if (PhotonNetwork.IsConnected)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                StartFactFetch(country);
            }
            else
            {
                photonView.RPC(nameof(RPC_RequestFact), RpcTarget.MasterClient, country);
            }
        }
        else
        {
            StartFactFetch(country);
        }
    }

    private void StartFactFetch(string country)
    {
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

                HandleFactResponse(lang, fact);
            }
        }
    }

    private void HandleFactResponse(string lang, FactResponse fact)
    {
        if (fact == null)
        {
            Debug.LogWarning($"[DEBUG] Received null fact for lang {lang}");
            return;
        }

        if (lang == "vi")
        {
            factVi = fact;
            Debug.Log($"[DEBUG] factVi assigned. isVietnamese: {isVietnamese}");
        }
        else
        {
            factEn = fact;
            Debug.Log($"[DEBUG] factEn assigned. isVietnamese: {isVietnamese}");
        }

        if (PhotonNetwork.IsConnected && PhotonNetwork.IsMasterClient)
        {
            photonView.RPC(
                nameof(RPC_ReceiveFact),
                RpcTarget.Others,
                lang,
                fact.title ?? string.Empty,
                fact.description ?? string.Empty,
                fact.image ?? string.Empty
            );
        }

        bool shouldUpdateUI = (isVietnamese && lang == "vi") || (!isVietnamese && lang == "en");
        if (shouldUpdateUI)
        {
            UpdateUI(fact);
        }
    }

    [PunRPC]
    private void RPC_RequestFact(string country)
    {
        Debug.Log($"[DEBUG] RPC_RequestFact received for country: {country}. IsMasterClient: {PhotonNetwork.IsMasterClient}");
        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }

        StartFactFetch(country);
    }

    [PunRPC]
    private void RPC_ReceiveFact(string lang, string title, string description, string imageUrl)
    {
        Debug.Log($"[DEBUG] RPC_ReceiveFact lang:{lang}, title:{title}");
        FactResponse fact = new FactResponse
        {
            title = title,
            description = description,
            image = imageUrl
        };

        if (lang == "vi")
        {
            factVi = fact;
        }
        else
        {
            factEn = fact;
        }

        bool shouldUpdateUI = (isVietnamese && lang == "vi") || (!isVietnamese && lang == "en");
        if (shouldUpdateUI)
        {
            UpdateUI(fact);
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
