
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization;
using UnityEngine;
using TMPro;
using UnityEngine.XR;
using UnityEngine.Events;

public class LetterSequenceManager : MonoBehaviour
{
    [Header("Letter Display")]
    public TextMeshProUGUI letterText;

    [Header("Display Timing (seconds)")]
    public float stimulusDuration = 0.7f;

    [Header("Distractor Events")]
    public UnityEvent onDistractorStart;
    public UnityEvent onDistractorStop;
    public UnityEvent onDistractorRepeat;

    [Header("Distractor Settings")]
    public float researchDistractorInterval = 30f;
    public float shortTestDistractorInterval = 8f;

    private string csvPath;
    private int trialNumber;
    private int blockNumber;
    private int pairsCompleted;
    private int pairsPerBlock;
    private bool shortTestMode;

    private string currentPhase = "NotStarted";
    private bool experimentRunning;
    private bool trialActive;
    private bool responded;
    private bool currentIsTarget;
    private bool isDistractorPhase;

    private float secondLetterStartTime;
    private float reactionTime = -1f;
    private float nextDistractorTime;

    private Coroutine sequenceCoroutine;
    private List<LetterPair> pairs = new List<LetterPair>();

    // Controller input tracking
    private bool previousPrimaryButtonState;

    public bool IsPhaseComplete =>
        !experimentRunning && pairsCompleted >= pairsPerBlock;

    private class LetterPair
    {
        public string first;
        public string second;
        public bool target;
        public bool pseudoTarget;

        public LetterPair(
            string first,
            string second,
            bool target,
            bool pseudoTarget)
        {
            this.first = first;
            this.second = second;
            this.target = target;
            this.pseudoTarget = pseudoTarget;
        }
    }

    private void Start()
    {
        csvPath = Path.Combine(
            Application.persistentDataPath,
            "CPT_Results.csv"
        );

        try
        {
            using (StreamWriter writer = new StreamWriter(
                csvPath, false, Encoding.UTF8))
            {
                writer.WriteLine(
                    "TrialNumber,Phase,BlockNumber,FirstLetter," +
                    "SecondLetter,IsTarget,IsPseudoTarget," +
                    "Response,ReactionTimeSeconds,ErrorType"
                );
            }

            Debug.Log("CPT CSV created: " + csvPath);
        }
        catch (IOException ex)
        {
            Debug.LogError("Could not create CSV: " + ex.Message);
        }
    }

    public void ConfigureMode(bool useShortTestMode)
    {
        shortTestMode = useShortTestMode;
        pairsPerBlock = shortTestMode ? 40 : 450;

        Debug.Log(
            shortTestMode
                ? "SHORT TEST MODE: 40 pairs per block."
                : "RESEARCH MODE: 450 pairs per block."
        );
    }

    private void Update()
    {
        // Read the Right Controller's A button.
        InputDevice rightController =
            InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        bool currentButtonState = false;

        if (rightController.isValid)
        {
            rightController.TryGetFeatureValue(
                CommonUsages.primaryButton,
                out currentButtonState
            );
        }

        // Detect a new press, not a button being held.
        bool buttonDown =
            currentButtonState && !previousPrimaryButtonState;

        previousPrimaryButtonState = currentButtonState;

        if (!experimentRunning || !trialActive || responded)
            return;

        if (buttonDown)
        {
            responded = true;

            if (currentIsTarget)
            {
                reactionTime = Time.time - secondLetterStartTime;

                Debug.Log(
                    "Target response | RT: " +
                    reactionTime.ToString("F3") + " seconds"
                );
            }
            else
            {
                Debug.Log("Commission response.");
            }
        }
    }

    public void BeginPhase(string newPhase, int newBlockNumber)
    {
        EndPhase();

        currentPhase = newPhase;
        blockNumber = newBlockNumber;
        pairsCompleted = 0;

        if (pairsPerBlock <= 0)
            pairsPerBlock = shortTestMode ? 40 : 450;

        pairs = GeneratePairs(pairsPerBlock);

        experimentRunning = true;
        sequenceCoroutine = StartCoroutine(RunBlock());

        Debug.Log(
            "CPT started | Block " + blockNumber +
            " | Pairs: " + pairsPerBlock
        );
    }

    public void EndPhase()
    {
        experimentRunning = false;

        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = null;
        }

        trialActive = false;

        if (isDistractorPhase)
        {
            onDistractorStop?.Invoke();
            isDistractorPhase = false;
        }

        if (letterText != null)
            letterText.text = "";
    }

    private IEnumerator RunBlock()
    {
        float distractorInterval = shortTestMode
            ? shortTestDistractorInterval
            : researchDistractorInterval;

        const int phaseCount = 6;
        int activePhase = -1;

        for (int i = 0; i < pairs.Count; i++)
        {
            if (!experimentRunning)
                yield break;

            // Divide the block into six alternating phases.
            int phaseIndex = Mathf.Min(
                phaseCount - 1,
                i * phaseCount / pairs.Count
            );

            bool newDistractorPhase = phaseIndex % 2 == 1;

            if (phaseIndex != activePhase)
            {
                activePhase = phaseIndex;

                if (isDistractorPhase)
                    onDistractorStop?.Invoke();

                isDistractorPhase = newDistractorPhase;

                if (isDistractorPhase)
                {
                    nextDistractorTime =
                        Time.time + distractorInterval;

                    onDistractorStart?.Invoke();
                    Debug.Log("Distractor phase started.");
                }
                else
                {
                    Debug.Log("Non-distractor phase started.");
                }
            }

            if (isDistractorPhase &&
                Time.time >= nextDistractorTime)
            {
                onDistractorRepeat?.Invoke();

                nextDistractorTime =
                    Time.time + distractorInterval;
            }

            LetterPair pair = pairs[i];

            currentIsTarget = pair.target;
            responded = false;
            reactionTime = -1f;
            trialActive = false;

            // Show the first letter.
            if (letterText != null)
                letterText.text = pair.first;

            yield return new WaitForSeconds(stimulusDuration);

            if (!experimentRunning)
                yield break;

            // Replace it directly with the second letter.
            // No blank screen between the two letters.
            if (letterText != null)
                letterText.text = pair.second;

            secondLetterStartTime = Time.time;
            trialActive = true;

            // The response window is limited to the
            // time during which the second letter is shown.
            yield return new WaitForSeconds(stimulusDuration);

            if (!experimentRunning)
                yield break;

            trialActive = false;

            SavePair(pair);
            pairsCompleted++;

            // Do not clear the display here.
            // The next pair's first letter replaces
            // the current letter on the next iteration.

            if (pairsCompleted % 10 == 0)
            {
                Debug.Log(
                    "Block " + blockNumber + ": " +
                    pairsCompleted + "/" +
                    pairsPerBlock + " pairs."
                );
            }
        }

        trialActive = false;
        experimentRunning = false;
        sequenceCoroutine = null;

        if (isDistractorPhase)
            onDistractorStop?.Invoke();

        isDistractorPhase = false;

        if (letterText != null)
            letterText.text = "";

        Debug.Log(
            "Block " + blockNumber + " completed: " +
            pairsCompleted + "/" + pairsPerBlock + " pairs."
        );
    }

    private List<LetterPair> GeneratePairs(int count)
    {
        List<LetterPair> result = new List<LetterPair>();

        int targetCount = Mathf.RoundToInt(count * 0.30f);
        int nonTargetCount = count - targetCount;
        int pseudoCount = Mathf.RoundToInt(nonTargetCount * 0.5f);
        int ordinaryCount = nonTargetCount - pseudoCount;

        // Targets: A followed by K.
        for (int i = 0; i < targetCount; i++)
        {
            result.Add(
                new LetterPair("A", "K", true, false)
            );
        }

        // Pseudo-targets.
        for (int i = 0; i < pseudoCount; i++)
        {
            if (i % 2 == 0)
            {
                result.Add(
                    new LetterPair(
                        "A", RandomNonK(), false, true
                    )
                );
            }
            else
            {
                result.Add(
                    new LetterPair(
                        RandomNonA(), "K", false, true
                    )
                );
            }
        }

        // Ordinary non-targets.
        for (int i = 0; i < ordinaryCount; i++)
        {
            result.Add(
                new LetterPair(
                    RandomNonA(),
                    RandomNonK(),
                    false,
                    false
                )
            );
        }

        // Shuffle the pairs.
        for (int i = result.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);

            LetterPair temp = result[i];
            result[i] = result[j];
            result[j] = temp;
        }

        return result;
    }

    private string RandomNonA()
    {
        string[] options =
        {
            "B", "C", "D", "E", "F",
            "G", "H", "I", "J"
        };

        return options[
            UnityEngine.Random.Range(0, options.Length)
        ];
    }

    private string RandomNonK()
    {
        string[] options =
        {
            "A", "B", "C", "D", "E",
            "F", "G", "H", "I", "J"
        };

        return options[
            UnityEngine.Random.Range(0, options.Length)
        ];
    }

    private void SavePair(LetterPair pair)
    {
        trialNumber++;

        string response = responded ? "Yes" : "No";
        string errorType;

        if (pair.target && responded)
            errorType = "Correct";
        else if (pair.target && !responded)
            errorType = "Omission";
        else if (!pair.target && responded)
            errorType = "Commission";
        else
            errorType = "CorrectRejection";

        string rtValue = reactionTime >= 0f
            ? reactionTime.ToString(
                "F3", CultureInfo.InvariantCulture)
            : "";

        string row =
            trialNumber + "," +
            (isDistractorPhase ? "DP" : "NDP") + "," +
            blockNumber + "," +
            pair.first + "," +
            pair.second + "," +
            pair.target + "," +
            pair.pseudoTarget + "," +
            response + "," +
            rtValue + "," +
            errorType + "\n";

        try
        {
            using (StreamWriter writer = new StreamWriter(
                csvPath, true, Encoding.UTF8))
            {
                writer.Write(row);
            }
        }
        catch (IOException ex)
        {
            Debug.LogError("Could not save CSV row: " + ex.Message);
        }
    }
}
