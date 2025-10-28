using System.Collections;
using System.Collections.Generic;
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
    public List<QuestionEntry> questionEntries = new List<QuestionEntry>();

    private string apiUrl = "https://ludo-mr.sapca.ai.vn/api/fact";

    private FactResponse factVi;
    private FactResponse factEn;

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
