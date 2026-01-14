using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks how many trivia answers each player has handled correctly/incorrectly
/// and exposes the pending step modifier that should be applied to the next dice roll.
/// Created on demand to avoid touching scene objects.
/// </summary>
public class QuestionTurnEffectManager : MonoBehaviour
{
    private class PlayerQuestionState
    {
        public int correctAnswers;
        public int wrongAnswers;
        public int pendingStepModifier;
    }

    private static QuestionTurnEffectManager _instance;

    [Header("Correct Answer Bonus")]
    [Min(0)]
    [Tooltip("Number of consecutive correct answers required before awarding bonus steps. Set to 0 to disable bonuses.")]
    public int correctAnswersRequired = 2;

    [Tooltip("How many steps are granted once the correct-answer threshold is reached.")]
    public int bonusStepAmount = 2;

    [Tooltip("If enabled, bonus steps are randomized between the provided range instead of using the fixed bonusStepAmount.")]
    public bool useRandomBonusRange = false;
    public Vector2Int randomBonusRange = new Vector2Int(1, 3);

    [Tooltip("Automatically clear the wrong-answer streak when a correct answer arrives.")]
    public bool resetWrongStreakOnCorrect = true;

    [Header("Wrong Answer Penalty")]
    [Min(0)]
    [Tooltip("Number of wrong answers required before deducting steps. Set to 0 to disable penalties.")]
    public int wrongAnswersRequired = 2;

    [Tooltip("How many steps are removed from the next roll when the wrong-answer threshold is met.")]
    public int penaltyStepAmount = 2;

    [Tooltip("Automatically clear the correct-answer streak when a wrong answer arrives.")]
    public bool resetCorrectStreakOnWrong = true;

    [Header("Debug")]
    public bool verboseLogs = false;

    private readonly Dictionary<PlayerColor, PlayerQuestionState> playerStates =
        new Dictionary<PlayerColor, PlayerQuestionState>();
    private readonly Dictionary<PlayerColor, QuestionRollBreakdown> lastRollBreakdowns =
        new Dictionary<PlayerColor, QuestionRollBreakdown>();

    public static QuestionTurnEffectManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<QuestionTurnEffectManager>();
                if (_instance == null)
                {
                    GameObject managerRoot = new GameObject(nameof(QuestionTurnEffectManager));
                    _instance = managerRoot.AddComponent<QuestionTurnEffectManager>();
                }
            }

            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private bool BonusEnabled => correctAnswersRequired > 0 && bonusStepAmount != 0;
    private bool PenaltyEnabled => wrongAnswersRequired > 0 && penaltyStepAmount != 0;

    private PlayerQuestionState GetOrCreateState(PlayerColor color)
    {
        if (!playerStates.TryGetValue(color, out PlayerQuestionState state))
        {
            state = new PlayerQuestionState();
            playerStates[color] = state;
        }

        return state;
    }

    /// <summary>
    /// Registers an authoritative answer result and returns the updated streak/modifier state.
    /// </summary>
    public QuestionAnswerResolution RegisterAnswer(PlayerColor color, bool isCorrect)
    {
        if (color == PlayerColor.None)
        {
            return QuestionAnswerResolution.Empty;
        }

        PlayerQuestionState state = GetOrCreateState(color);
        int appliedModifier = 0;

        if (isCorrect)
        {
            state.correctAnswers++;
            if (resetWrongStreakOnCorrect)
            {
                state.wrongAnswers = 0;
            }

            if (BonusEnabled && state.correctAnswers >= correctAnswersRequired)
            {
                int bonusAmount = ResolveBonusStepAmount();
                state.pendingStepModifier += bonusAmount;
                appliedModifier = bonusAmount;
                state.correctAnswers = 0;
            }
        }
        else
        {
            state.wrongAnswers++;
            if (resetCorrectStreakOnWrong)
            {
                state.correctAnswers = 0;
            }

            if (PenaltyEnabled && state.wrongAnswers >= wrongAnswersRequired)
            {
                int penalty = -Mathf.Abs(penaltyStepAmount);
                state.pendingStepModifier += penalty;
                appliedModifier = penalty;
                state.wrongAnswers = 0;
            }
        }

        var resolution = new QuestionAnswerResolution
        {
            WasCorrect = isCorrect,
            AppliedModifier = appliedModifier,
            PendingModifier = state.pendingStepModifier,
            CorrectCount = state.correctAnswers,
            WrongCount = state.wrongAnswers
        };

        if (verboseLogs)
        {
            Debug.Log($"[QuestionTurnEffectManager] {color} answered {(isCorrect ? "correct" : "wrong")} | " +
                      $"Applied {appliedModifier}, Pending {state.pendingStepModifier}, " +
                      $"Correct streak {state.correctAnswers}, Wrong streak {state.wrongAnswers}");
        }

        return resolution;
    }

    /// <summary>
    /// Applies a snapshot broadcast from the master client so every peer stays in sync.
    /// </summary>
    public void ApplyResolutionSnapshot(PlayerColor color, QuestionAnswerResolution snapshot)
    {
        if (color == PlayerColor.None)
        {
            return;
        }

        PlayerQuestionState state = GetOrCreateState(color);
        state.correctAnswers = Mathf.Max(0, snapshot.CorrectCount);
        state.wrongAnswers = Mathf.Max(0, snapshot.WrongCount);
        state.pendingStepModifier = snapshot.PendingModifier;
    }

    /// <summary>
    /// Consumes the pending modifier when a dice result is finalized.
    /// </summary>
    public QuestionDiceAdjustment ConsumeModifier(PlayerColor color, int rolledValue)
    {
        PlayerQuestionState state = GetOrCreateState(color);
        QuestionDiceAdjustment adjustment = new QuestionDiceAdjustment
        {
            OriginalValue = rolledValue,
            AppliedModifier = state.pendingStepModifier,
            FinalValue = rolledValue + state.pendingStepModifier,
            ForcedSkip = false,
            PendingModifierAfterConsumption = 0
        };

        state.pendingStepModifier = 0;
        StoreRollBreakdown(color, adjustment.OriginalValue, adjustment.AppliedModifier);
        adjustment.PendingModifierAfterConsumption = state.pendingStepModifier;

        if (adjustment.FinalValue <= 0)
        {
            adjustment.ForcedSkip = true;
            adjustment.FinalValue = 0;
        }

        if (verboseLogs && adjustment.AppliedModifier != 0)
        {
            Debug.Log($"[QuestionTurnEffectManager] Consumed modifier {adjustment.AppliedModifier} for {color}. " +
                      $"Final steps {adjustment.FinalValue}, forcedSkip={adjustment.ForcedSkip}");
        }

        return adjustment;
    }

    /// <summary>
    /// Overwrites the pending modifier with the authoritative value (usually called after RPCs).
    /// </summary>
    public void ApplyPendingModifierSnapshot(PlayerColor color, int pendingModifier)
    {
        if (color == PlayerColor.None)
        {
            return;
        }

        PlayerQuestionState state = GetOrCreateState(color);
        state.pendingStepModifier = pendingModifier;
    }

    private int ResolveBonusStepAmount()
    {
        if (!useRandomBonusRange)
        {
            return Mathf.Max(1, bonusStepAmount);
        }

        int min = Mathf.Min(randomBonusRange.x, randomBonusRange.y);
        int max = Mathf.Max(randomBonusRange.x, randomBonusRange.y);
        return Mathf.Max(1, Random.Range(min, max + 1));
    }

    private void StoreRollBreakdown(PlayerColor color, int baseValue, int modifier)
    {
        if (color == PlayerColor.None)
        {
            return;
        }

        lastRollBreakdowns[color] = new QuestionRollBreakdown
        {
            BaseValue = baseValue,
            Modifier = modifier
        };
    }

    public bool TryGetRollBreakdown(PlayerColor color, out QuestionRollBreakdown breakdown)
    {
        return lastRollBreakdowns.TryGetValue(color, out breakdown);
    }

    public void ApplyRollBreakdownSnapshot(PlayerColor color, int originalValue, int appliedModifier)
    {
        StoreRollBreakdown(color, originalValue, appliedModifier);
    }
}

[System.Serializable]
public struct QuestionAnswerResolution
{
    public static readonly QuestionAnswerResolution Empty = new QuestionAnswerResolution
    {
        WasCorrect = false,
        AppliedModifier = 0,
        PendingModifier = 0,
        CorrectCount = 0,
        WrongCount = 0
    };

    public bool WasCorrect;
    public int AppliedModifier;
    public int PendingModifier;
    public int CorrectCount;
    public int WrongCount;
}

public struct QuestionDiceAdjustment
{
    public int OriginalValue;
    public int AppliedModifier;
    public int FinalValue;
    public bool ForcedSkip;
    public int PendingModifierAfterConsumption;
}

public struct QuestionRollBreakdown
{
    public int BaseValue;
    public int Modifier;
}
