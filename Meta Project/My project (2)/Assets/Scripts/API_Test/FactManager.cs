using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
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

[System.Serializable]
public class QuestionLocalization
{
    [TextArea]
    public string question;

    [Tooltip("Exactly 3 answer options for this language.")]
    public string[] answers = new string[3];
}

[System.Serializable]
public class QuestionEntry
{
    [Tooltip("Optional identifier to help distinguish questions in logs.")]
    public string id;

    public QuestionLocalization vietnamese = new QuestionLocalization();
    public QuestionLocalization english = new QuestionLocalization();

    [Range(0, 2)]
    public int correctAnswerIndex;
}

[System.Serializable]
public class QuestionBank
{
    public QuestionEntry[] questions;
}

[System.Serializable]
public class OfflineFactEntry
{
    public string country;
    public string lang;
    public FactResponse fact;
}

[System.Serializable]
public class OfflineFactBank
{
    public OfflineFactEntry[] facts;
}

[RequireComponent(typeof(PhotonView))]
public class FactManager : MonoBehaviourPunCallbacks
{
    private enum PanelMode
    {
        None,
        Fact,
        Question
    }

    [Header("Panel References")]
    public GameObject factPanel;
    public GameObject questionPanel;

    [Header("Fact UI Elements")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;
    public Image factImage;

    [Header("Question UI Elements")]
    public TextMeshProUGUI questionText;
    public TextMeshProUGUI[] answerTexts = new TextMeshProUGUI[3];
    public Button[] answerButtons = new Button[0];

    [Header("Question Feedback")]
    public TextMeshProUGUI questionFeedbackText;
    [TextArea]
    public string correctMessageVietnamese = "Bạn trả lời đúng!";
    [TextArea]
    public string incorrectMessageVietnamese = "Bạn trả lời sai.";
    [TextArea]
    public string correctMessageEnglish = "You answered correctly!";
    [TextArea]
    public string incorrectMessageEnglish = "That is not correct.";
    [Min(0f)]
    public float questionAutoHideDelay = 3f;

    [Header("Question Bank")]
    [Tooltip("Optional JSON file that defines the question list. Leave empty to configure questions manually.")]
    public TextAsset questionBankJson;
    [Tooltip("Automatically load the question list from the assigned JSON when this component awakens.")]
    public bool loadQuestionsFromJsonOnAwake = true;
    public List<QuestionEntry> questionEntries = new List<QuestionEntry>();

    [Header("Offline Fact Fallback")]
    [Tooltip("Optional JSON asset used when the API is unavailable or the device is offline.")]
    public TextAsset offlineFactJson;
    [Tooltip("If true, skips API calls when there is no internet connection and relies on the offline JSON.")]
    public bool useOfflineWhenNoInternet = true;

    private string apiUrl = "";

    private FactResponse factVi;
    private FactResponse factEn;
    private readonly Dictionary<string, List<FactResponse>> offlineFactsByKey = new Dictionary<string, List<FactResponse>>();
    private bool offlineFactsLoaded = false;
    private readonly Dictionary<string, Sprite> resourceSpriteCache = new Dictionary<string, Sprite>();

    private bool isVietnamese = false; // current language mode (true = VI)

    private QuestionEntry currentQuestion;
    private int currentQuestionIndex = -1;
    private PanelMode currentPanelMode = PanelMode.None;
    private Coroutine questionHideCoroutine;
    private bool isQuestionAnswered = false;
    private bool lastAnswerWasCorrect = false;


    public static FactManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializePanelState();
            SetupAnswerButtonCallbacks();
            if (loadQuestionsFromJsonOnAwake)
            {
                LoadQuestionsFromJson();
            }
            EnsureOfflineFactsLoaded();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializePanelState()
    {
        currentPanelMode = PanelMode.None;
        ResetQuestionUIState();
        SetPanelActive(factPanel, false);
        SetPanelActive(questionPanel, false);
    }

    private void SetPanelActive(GameObject panel, bool shouldBeActive)
    {
        if (panel != null)
        {
            panel.SetActive(shouldBeActive);
        }
    }

    private void SetupAnswerButtonCallbacks()
    {
        if (answerButtons == null)
        {
            return;
        }

        for (int i = 0; i < answerButtons.Length; i++)
        {
            Button button = answerButtons[i];
            if (button == null)
            {
                continue;
            }

            int capturedIndex = i;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnQuestionAnswerSelected(capturedIndex));
        }
    }

    private void LoadQuestionsFromJson()
    {
        if (questionBankJson == null)
        {
            Debug.Log("[DEBUG] No questionBankJson assigned. Skipping JSON question load.");
            return;
        }

        string jsonContent = questionBankJson.text;
        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            Debug.LogWarning("[DEBUG] questionBankJson is assigned but empty. Skipping question load.");
            return;
        }

        QuestionBank parsedBank = null;
        try
        {
            parsedBank = JsonUtility.FromJson<QuestionBank>(jsonContent);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DEBUG] Failed to parse questionBankJson. Exception: {ex}");
            return;
        }

        if (parsedBank == null || parsedBank.questions == null || parsedBank.questions.Length == 0)
        {
            Debug.LogWarning("[DEBUG] Parsed questionBankJson but it did not contain any questions.");
            return;
        }

        if (questionEntries == null)
        {
            questionEntries = new List<QuestionEntry>(parsedBank.questions.Length);
        }
        else
        {
            questionEntries.Clear();
        }

        foreach (QuestionEntry entry in parsedBank.questions)
        {
            if (entry == null)
            {
                continue;
            }

            NormalizeQuestionEntry(entry);
            questionEntries.Add(entry);
        }

        Debug.Log($"[DEBUG] Loaded {questionEntries.Count} questions from JSON.");
    }

    private void NormalizeQuestionEntry(QuestionEntry entry)
    {
        if (entry == null)
        {
            return;
        }

        if (entry.vietnamese == null)
        {
            entry.vietnamese = new QuestionLocalization();
        }

        if (entry.english == null)
        {
            entry.english = new QuestionLocalization();
        }

        NormalizeLocalization(entry.vietnamese);
        NormalizeLocalization(entry.english);

        entry.correctAnswerIndex = Mathf.Clamp(entry.correctAnswerIndex, 0, 2);
    }

    private void NormalizeLocalization(QuestionLocalization localization)
    {
        if (localization.answers == null)
        {
            localization.answers = new string[3];
        }
        else if (localization.answers.Length != 3)
        {
            string[] resized = new string[3];
            int copyLength = Mathf.Min(localization.answers.Length, resized.Length);
            for (int i = 0; i < copyLength; i++)
            {
                resized[i] = localization.answers[i];
            }
            localization.answers = resized;
        }

        for (int i = 0; i < localization.answers.Length; i++)
        {
            if (localization.answers[i] == null)
            {
                localization.answers[i] = string.Empty;
            }
        }

        if (localization.question == null)
        {
            localization.question = string.Empty;
        }
    }

    private void ResetQuestionUIState(bool cancelHideRoutine = true)
    {
        if (cancelHideRoutine)
        {
            CancelQuestionHideRoutine();
        }
        isQuestionAnswered = false;
        lastAnswerWasCorrect = false;

        if (questionFeedbackText != null)
        {
            questionFeedbackText.text = string.Empty;
            questionFeedbackText.gameObject.SetActive(false);
        }

        if (answerButtons != null)
        {
            foreach (Button button in answerButtons)
            {
                if (button == null) continue;
                button.interactable = true;
                button.gameObject.SetActive(true);
            }
        }
    }

    private void CancelQuestionHideRoutine()
    {
        if (questionHideCoroutine != null)
        {
            StopCoroutine(questionHideCoroutine);
            questionHideCoroutine = null;
        }
    }

    private void EnsureOfflineFactsLoaded()
    {
        if (offlineFactsLoaded)
        {
            return;
        }

        offlineFactsLoaded = true;
        offlineFactsByKey.Clear();

        if (offlineFactJson == null)
        {
            return;
        }

        string jsonContent = offlineFactJson.text;
        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            Debug.LogWarning("[DEBUG] offlineFactJson is assigned but empty. Skipping offline fact load.");
            return;
        }

        OfflineFactBank parsedBank = null;
        try
        {
            parsedBank = JsonUtility.FromJson<OfflineFactBank>(jsonContent);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DEBUG] Failed to parse offline fact JSON. Exception: {ex.Message}");
            return;
        }

        if (parsedBank?.facts == null || parsedBank.facts.Length == 0)
        {
            Debug.LogWarning("[DEBUG] Parsed offlineFactJson but it did not contain any facts.");
            return;
        }

        foreach (OfflineFactEntry entry in parsedBank.facts)
        {
            if (entry == null || entry.fact == null)
            {
                continue;
            }

            string key = BuildOfflineFactKey(entry.country, entry.lang);
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            if (!offlineFactsByKey.TryGetValue(key, out var list) || list == null)
            {
                list = new List<FactResponse>();
                offlineFactsByKey[key] = list;
            }

            list.Add(new FactResponse
            {
                title = entry.fact.title,
                description = entry.fact.description,
                image = entry.fact.image
            });
        }

        Debug.Log($"[DEBUG] Loaded offline facts groups: {offlineFactsByKey.Count} keys.");
    }

    private static string BuildOfflineFactKey(string country, string lang)
    {
        if (string.IsNullOrWhiteSpace(lang))
        {
            return null;
        }

        string normalizedLang = lang.Trim().ToLowerInvariant();
        string normalizedCountry = string.IsNullOrWhiteSpace(country)
            ? "*"
            : country.Trim().ToLowerInvariant();

        return $"{normalizedCountry}|{normalizedLang}";
    }

    private bool TryGetOfflineFact(string country, string lang, out FactResponse fact)
    {
        fact = null;
        EnsureOfflineFactsLoaded();

        if (offlineFactsByKey.Count == 0)
        {
            return false;
        }

        string specificKey = BuildOfflineFactKey(country, lang);
        if (!string.IsNullOrEmpty(specificKey) && TryPickOfflineFactFromKey(specificKey, out fact))
        {
            return true;
        }

        string wildcardKey = BuildOfflineFactKey("*", lang);
        if (!string.IsNullOrEmpty(wildcardKey) && TryPickOfflineFactFromKey(wildcardKey, out fact))
        {
            return true;
        }

        return false;
    }

    private bool TryPickOfflineFactFromKey(string key, out FactResponse fact)
    {
        fact = null;
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        if (!offlineFactsByKey.TryGetValue(key, out var list) || list == null || list.Count == 0)
        {
            return false;
        }

        List<FactResponse> withImages = new List<FactResponse>();
        List<FactResponse> others = new List<FactResponse>();

        foreach (var item in list)
        {
            if (item == null) continue;

            if (!string.IsNullOrWhiteSpace(item.image) && CanResolveOfflineSprite(item.image))
            {
                withImages.Add(item);
            }
            else
            {
                others.Add(item);
            }
        }

        FactResponse PickRandom(List<FactResponse> src)
        {
            if (src == null || src.Count == 0) return null;
            int idx = UnityEngine.Random.Range(0, src.Count);
            return src[idx];
        }

        fact = PickRandom(withImages) ?? PickRandom(others);
        return fact != null;
    }

    private bool TryHandleOfflineFact(string country, string lang)
    {
        if (!TryGetOfflineFact(country, lang, out FactResponse fact))
        {
            return false;
        }

        Debug.Log($"[DEBUG] Using offline fact for country '{country}' and language '{lang}'.");
        HandleFactResponse(lang, fact);
        return true;
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

    public void ShowRandomQuestionPanel()
    {
        if (questionEntries == null || questionEntries.Count == 0)
        {
            Debug.LogWarning("[DEBUG] ShowRandomQuestionPanel called but no questions configured.");
            return;
        }

        if (PhotonNetwork.IsConnected)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                int questionIndex = GetRandomQuestionIndex();
                ApplyQuestion(questionIndex);
                photonView.RPC(nameof(RPC_ShowQuestion), RpcTarget.Others, questionIndex);
            }
            else
            {
                photonView.RPC(nameof(RPC_RequestQuestion), RpcTarget.MasterClient, PhotonNetwork.LocalPlayer.ActorNumber);
            }
        }
        else
        {
            int questionIndex = GetRandomQuestionIndex();
            ApplyQuestion(questionIndex);
        }
    }

    private void StartFactFetch(string country)
    {
        bool shouldUseOfflineOnly = useOfflineWhenNoInternet &&
                                    Application.internetReachability == NetworkReachability.NotReachable;

        if (shouldUseOfflineOnly)
        {
            string preferredLang = isVietnamese ? "vi" : "en";
            string fallbackLang = isVietnamese ? "en" : "vi";

            if (TryGetOfflineFact(country, preferredLang, out var preferredFact))
            {
                HandleFactResponse(preferredLang, preferredFact);
            }
            else if (TryGetOfflineFact(country, fallbackLang, out var fallbackFact))
            {
                HandleFactResponse(fallbackLang, fallbackFact);
            }
            else
            {
                Debug.LogWarning($"[DEBUG] Offline fallback did not contain facts for country '{country}'.");
            }

            return;
        }

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
                Debug.LogError($"[DEBUG] API Error ({lang}): {request.error}");
                if (!TryHandleOfflineFact(country, lang))
                {
                    Debug.LogWarning($"[DEBUG] No offline fact available for country '{country}' and language '{lang}'.");
                }
                yield break;
            }

            string responseText = request.downloadHandler.text;
            Debug.Log($"[DEBUG] API Response ({lang}): {responseText}");

            FactResponse fact = null;
            try
            {
                fact = JsonUtility.FromJson<FactResponse>(responseText);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DEBUG] Failed to parse API response for language '{lang}'. Exception: {ex.Message}");
            }

            if (fact == null)
            {
                if (!TryHandleOfflineFact(country, lang))
                {
                    Debug.LogWarning($"[DEBUG] API response for country '{country}' and language '{lang}' was invalid and no offline fallback was found.");
                }
                yield break;
            }

            HandleFactResponse(lang, fact);
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
        else
        {
            // If preferred language data is missing, show the available one to avoid blank UI
            bool preferredMissing = isVietnamese ? (factVi == null) : (factEn == null);
            if (preferredMissing)
            {
                UpdateUI(fact);
            }
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
        else
        {
            // If preferred language data is missing, show the available one to avoid blank UI
            bool preferredMissing = isVietnamese ? (factVi == null) : (factEn == null);
            if (preferredMissing)
            {
                UpdateUI(fact);
            }
        }
    }

    [PunRPC]
    private void RPC_RequestQuestion(int requestingActorNumber)
    {
        Debug.Log($"[DEBUG] RPC_RequestQuestion from actor {requestingActorNumber}. IsMasterClient: {PhotonNetwork.IsMasterClient}");

        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }

        if (questionEntries == null || questionEntries.Count == 0)
        {
            Debug.LogWarning("[DEBUG] Master received question request but no questions configured.");
            return;
        }

        int questionIndex = GetRandomQuestionIndex();
        ApplyQuestion(questionIndex);
        photonView.RPC(nameof(RPC_ShowQuestion), RpcTarget.Others, questionIndex);
    }

    [PunRPC]
    private void RPC_ShowQuestion(int questionIndex)
    {
        Debug.Log($"[DEBUG] RPC_ShowQuestion index:{questionIndex}");
        ApplyQuestion(questionIndex);
    }

    private int GetRandomQuestionIndex()
    {
        if (questionEntries == null || questionEntries.Count == 0)
        {
            return -1;
        }

        if (questionEntries.Count == 1)
        {
            return 0;
        }

        int index = UnityEngine.Random.Range(0, questionEntries.Count);

        int safety = 0;
        while (index == currentQuestionIndex && safety < 5)
        {
            index = UnityEngine.Random.Range(0, questionEntries.Count);
            safety++;
        }

        return index;
    }

    private void ApplyQuestion(int questionIndex)
    {
        if (questionIndex < 0 || questionEntries == null || questionIndex >= questionEntries.Count)
        {
            Debug.LogWarning($"[DEBUG] ApplyQuestion received invalid index: {questionIndex}");
            return;
        }

        ResetQuestionUIState();

        currentQuestionIndex = questionIndex;
        currentQuestion = questionEntries[questionIndex];
        UpdateQuestionUI(currentQuestion);
    }

    public void OnQuestionAnswerSelected(int answerIndex)
    {
        if (currentPanelMode != PanelMode.Question || currentQuestion == null)
        {
            Debug.LogWarning("[DEBUG] OnQuestionAnswerSelected called but no active question panel.");
            return;
        }

        if (isQuestionAnswered)
        {
            Debug.Log("[DEBUG] Question already answered. Ignoring additional input.");
            return;
        }

        int clampedIndex = Mathf.Clamp(answerIndex, 0, 2);
        bool isCorrect = currentQuestion.correctAnswerIndex == clampedIndex;

        isQuestionAnswered = true;
        lastAnswerWasCorrect = isCorrect;
        DisableAnswerButtons();
        DisplayQuestionFeedback(isCorrect);

        if (questionAutoHideDelay <= 0f)
        {
            HideQuestionPanelImmediate();
            ResetQuestionUIState();
        }
        else
        {
            questionHideCoroutine = StartCoroutine(HideQuestionPanelAfterDelay(questionAutoHideDelay));
        }
    }

    private void DisableAnswerButtons()
    {
        if (answerButtons == null)
        {
            return;
        }

        foreach (Button button in answerButtons)
        {
            if (button == null) continue;
            button.interactable = false;
            button.gameObject.SetActive(false);
        }
    }

    private void DisplayQuestionFeedback(bool isCorrect)
    {
        if (questionFeedbackText == null)
        {
            return;
        }

        questionFeedbackText.text = GetFeedbackMessage(isCorrect);
        questionFeedbackText.gameObject.SetActive(true);
    }

    private void RefreshQuestionFeedback()
    {
        if (!isQuestionAnswered || questionFeedbackText == null)
        {
            return;
        }

        questionFeedbackText.text = GetFeedbackMessage(lastAnswerWasCorrect);
        questionFeedbackText.gameObject.SetActive(true);
    }

    private string GetFeedbackMessage(bool isCorrect)
    {
        if (isVietnamese)
        {
            return isCorrect ? ValueOrFallback(correctMessageVietnamese, "Bạn trả lời đúng!") :
                               ValueOrFallback(incorrectMessageVietnamese, "Bạn trả lời sai.");
        }

        return isCorrect ? ValueOrFallback(correctMessageEnglish, "You answered correctly!") :
                           ValueOrFallback(incorrectMessageEnglish, "That is not correct.");
    }

    private string ValueOrFallback(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private IEnumerator HideQuestionPanelAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideQuestionPanelImmediate();
        ResetQuestionUIState(false);
        currentQuestion = null;
        questionHideCoroutine = null;
    }

    private void HideQuestionPanelImmediate()
    {
        SetPanelActive(questionPanel, false);
        currentPanelMode = PanelMode.None;
        currentQuestion = null;
    }

    private QuestionLocalization GetLocalizedQuestion(QuestionEntry entry)
    {
        if (entry == null)
        {
            return null;
        }

        QuestionLocalization preferred = isVietnamese ? entry.vietnamese : entry.english;
        QuestionLocalization fallback = isVietnamese ? entry.english : entry.vietnamese;

        if (preferred != null && !string.IsNullOrWhiteSpace(preferred.question))
        {
            return preferred;
        }

        return preferred ?? fallback;
    }

    private void EnsureAnswerCapacity(QuestionLocalization localization)
    {
        if (localization == null)
        {
            return;
        }

        if (localization.answers == null)
        {
            localization.answers = new string[3];
            return;
        }

        if (localization.answers.Length != 3)
        {
            string[] resized = new string[3];
            int copyLength = Mathf.Min(localization.answers.Length, 3);
            for (int i = 0; i < copyLength; i++)
            {
                resized[i] = localization.answers[i];
            }
            localization.answers = resized;
        }
    }

    private void UpdateQuestionUI(QuestionEntry question)
    {
        if (question == null)
        {
            Debug.LogWarning("[DEBUG] UpdateQuestionUI called with null question.");
            return;
        }

        QuestionLocalization localization = GetLocalizedQuestion(question);
        if (localization == null)
        {
            Debug.LogWarning("[DEBUG] No localization found for question.");
            return;
        }

        EnsureAnswerCapacity(localization);

        if (questionText != null)
        {
            questionText.text = localization.question ?? string.Empty;
        }
        else
        {
            Debug.LogWarning("[DEBUG] questionText reference is missing.");
        }

        for (int i = 0; i < 3; i++)
        {
            string answer = (localization.answers != null && i < localization.answers.Length) ? localization.answers[i] : string.Empty;
            ApplyAnswerText(i, answer);
        }

        ShowQuestionPanel();

        if (isQuestionAnswered)
        {
            RefreshQuestionFeedback();
        }
    }

    private void ApplyAnswerText(int index, string answer)
    {
        string safeAnswer = answer ?? string.Empty;

        if (answerTexts != null && index < answerTexts.Length && answerTexts[index] != null)
        {
            answerTexts[index].text = safeAnswer;
        }
        else if (answerButtons != null && index < answerButtons.Length && answerButtons[index] != null)
        {
            TextMeshProUGUI textComponent = answerButtons[index].GetComponentInChildren<TextMeshProUGUI>();
            if (textComponent != null)
            {
                textComponent.text = safeAnswer;
            }
        }

        if (answerButtons != null && index < answerButtons.Length && answerButtons[index] != null)
        {
            if (!isQuestionAnswered)
            {
                answerButtons[index].gameObject.SetActive(true);
                answerButtons[index].interactable = true;
            }
        }
    }

    private void ShowFactPanel()
    {
        CancelQuestionHideRoutine();
        ResetQuestionUIState(false);

        currentPanelMode = PanelMode.Fact;

        if (factPanel != null)
        {
            factPanel.SetActive(true);
        }

        if (questionPanel != null)
        {
            questionPanel.SetActive(false);
        }

        currentQuestion = null;
    }

    private void ShowQuestionPanel()
    {
        currentPanelMode = PanelMode.Question;

        if (factPanel != null)
        {
            factPanel.SetActive(false);
        }

        if (questionPanel != null)
        {
            questionPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[DEBUG] questionPanel reference is missing.");
        }
    }

    private void UpdateUI(FactResponse fact)
    {
        Debug.Log($"[DEBUG] UpdateUI called. fact is null: {fact == null}");
        if (fact == null) return;

        ShowFactPanel();

        Debug.Log($"[DEBUG] Updating UI with title: {fact.title}");
        Debug.Log($"[DEBUG] titleText is null: {titleText == null}");
        Debug.Log($"[DEBUG] descriptionText is null: {descriptionText == null}");
        Debug.Log($"[DEBUG] factImage is null: {factImage == null}");

        if (titleText != null) titleText.text = fact.title;
        if (descriptionText != null) descriptionText.text = fact.description;
        if (factImage != null) StartCoroutine(LoadImage(fact.image));
    }

    IEnumerator LoadImage(string imageSource)
    {
        if (factImage == null)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(imageSource))
        {
            factImage.sprite = null;
            yield break;
        }

        if (!IsRemoteImageSource(imageSource))
        {
            Sprite resourceSprite = LoadSpriteFromResources(imageSource);
            if (resourceSprite != null)
            {
                factImage.sprite = resourceSprite;
            }
            else
            {
                factImage.sprite = null;
                Debug.LogWarning($"[DEBUG] Failed to load offline fact image from Resources path '{imageSource}'.");
            }
            yield break;
        }

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageSource))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D tex = DownloadHandlerTexture.GetContent(request);
                factImage.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
            }
            else
            {
                factImage.sprite = null;
                Debug.LogError($"[DEBUG] Image load error for source '{imageSource}': {request.error}");
            }
        }
    }

    private static bool IsRemoteImageSource(string imageSource)
    {
        if (string.IsNullOrWhiteSpace(imageSource))
        {
            return false;
        }

        if (imageSource.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            imageSource.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private Sprite LoadSpriteFromResources(string resourcePath)
    {
        string normalizedPath = NormalizeResourcePath(resourcePath);
        if (string.IsNullOrEmpty(normalizedPath))
        {
            return null;
        }

        if (resourceSpriteCache.TryGetValue(normalizedPath, out Sprite cachedSprite) && cachedSprite != null)
        {
            return cachedSprite;
        }

        // Try exact sprite path first
        Sprite loadedSprite = Resources.Load<Sprite>(normalizedPath);
        if (loadedSprite != null)
        {
            resourceSpriteCache[normalizedPath] = loadedSprite;
            return loadedSprite;
        }

        // Fallback: try exact texture and create a sprite
        Texture2D tex = Resources.Load<Texture2D>(normalizedPath);
        if (tex != null)
        {
            var created = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
            return created;
        }

        // If not found, treat the path as a folder and try to load any sprites from it
        Sprite[] spritesInFolder = Resources.LoadAll<Sprite>(normalizedPath);
        if (spritesInFolder != null && spritesInFolder.Length > 0)
        {
            int idx = UnityEngine.Random.Range(0, spritesInFolder.Length);
            return spritesInFolder[idx];
        }

        // Fallback: try textures in folder and create a sprite
        Texture2D[] texturesInFolder = Resources.LoadAll<Texture2D>(normalizedPath);
        if (texturesInFolder != null && texturesInFolder.Length > 0)
        {
            int idx = UnityEngine.Random.Range(0, texturesInFolder.Length);
            var tex2 = texturesInFolder[idx];
            var created = Sprite.Create(tex2, new Rect(0, 0, tex2.width, tex2.height), Vector2.one * 0.5f);
            return created;
        }

        Debug.LogWarning($"[DEBUG] Resources could not find sprite/texture at '{normalizedPath}'.");
        return null;
    }

    private static string NormalizeResourcePath(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return null;
        }

        string trimmed = rawPath.Trim().Replace('\\', '/');

        // Remove trailing slash so we can treat the value as a folder path if needed
        while (trimmed.EndsWith("/"))
        {
            trimmed = trimmed.Substring(0, trimmed.Length - 1);
        }

        const string resourcesPrefix = "Resources/";
        if (trimmed.StartsWith(resourcesPrefix, StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(resourcesPrefix.Length);
        }

        if (trimmed.StartsWith("/"))
        {
            trimmed = trimmed.Substring(1);
        }

        string withoutExtension = Path.ChangeExtension(trimmed, null);
        return withoutExtension;
    }

    private bool CanResolveOfflineSprite(string imageSource)
    {
        if (string.IsNullOrWhiteSpace(imageSource))
        {
            return false;
        }

        if (IsRemoteImageSource(imageSource))
        {
            // Remote images are not considered resolvable in offline mode
            return false;
        }

        string normalizedPath = NormalizeResourcePath(imageSource);
        if (string.IsNullOrEmpty(normalizedPath))
        {
            return false;
        }

        // First try direct sprite or texture
        var direct = Resources.Load<Sprite>(normalizedPath);
        if (direct != null)
        {
            return true;
        }
        var tex = Resources.Load<Texture2D>(normalizedPath);
        if (tex != null)
        {
            return true;
        }

        // Then try as a folder containing sprites or textures
        var allSprites = Resources.LoadAll<Sprite>(normalizedPath);
        if (allSprites != null && allSprites.Length > 0)
        {
            return true;
        }

        var allTextures = Resources.LoadAll<Texture2D>(normalizedPath);
        return allTextures != null && allTextures.Length > 0;
    }

    // Hàm gọi từ Button để đổi ngôn ngữ
    public void ToggleLanguage()
    {
        isVietnamese = !isVietnamese;

        if (currentPanelMode == PanelMode.Question)
        {
            if (currentQuestion != null)
            {
                UpdateQuestionUI(currentQuestion);
                RefreshQuestionFeedback();
            }
            return;
        }

        if (isVietnamese && factVi != null)
        {
            UpdateUI(factVi);
        }
        else if (!isVietnamese && factEn != null)
        {
            UpdateUI(factEn);
        }
        else if (factVi != null)
        {
            UpdateUI(factVi);
        }
        else if (factEn != null)
        {
            UpdateUI(factEn);
        }
    }
}
