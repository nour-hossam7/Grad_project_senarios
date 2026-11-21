
using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.XR;

public class ExperimentManager : MonoBehaviour
{
    [Header("References")]
    public LetterSequenceManager letterManager;
    public TextMeshProUGUI statusText;
    public GameObject instructionBackground;

    [Header("Mode")]
    public bool shortTestMode = true;

    [Header("Participant Instructions")]
    [TextArea(5, 8)]
    public string readyMessage =
        "LETTER TASK\n\n" +
        "Watch the letters carefully.\n\n" +
        "Press the A button on the right controller only when K appears immediately after A.\n\n" +
        "Press A when you are ready to begin.";

    private bool waitingForStart;
    private bool experimentFinished;

    private void Start()
    {
        if (letterManager == null)
        {
            Debug.LogError(
                "Assign LetterSequenceManager in the Inspector."
            );

            enabled = false;
            return;
        }

        // Show instructions and background at the beginning
        if (instructionBackground != null)
            instructionBackground.SetActive(true);

        letterManager.ConfigureMode(shortTestMode);
        StartCoroutine(RunExperiment());
    }

    private IEnumerator RunExperiment()
    {
        letterManager.EndPhase();

        waitingForStart = true;

        if (statusText != null)
            statusText.text = readyMessage;

        if (instructionBackground != null)
            instructionBackground.SetActive(true);

        Debug.Log(
            shortTestMode
                ? "Starting Short Test Mode."
                : "Starting Research Mode."
        );

        bool previousButtonState = false;

        while (waitingForStart)
        {
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

            // Detect a new press of the A button
            bool buttonDown =
                currentButtonState && !previousButtonState;

            previousButtonState = currentButtonState;

            if (buttonDown)
            {
                waitingForStart = false;
            }

            yield return null;
        }

        // Hide instructions and background together
        if (statusText != null)
            statusText.text = "";

        if (instructionBackground != null)
            instructionBackground.SetActive(false);

        for (int block = 1; block <= 3; block++)
        {
            yield return StartBlock(block);
        }

        letterManager.EndPhase();
        experimentFinished = true;

        if (statusText != null)
            statusText.text = "";

        Debug.Log("Experiment session completed.");
    }

    private IEnumerator StartBlock(int blockNumber)
    {
        Debug.Log("Starting block " + blockNumber);

        letterManager.BeginPhase(
            "Block " + blockNumber,
            blockNumber
        );

        while (!letterManager.IsPhaseComplete)
            yield return null;

        Debug.Log("Block " + blockNumber + " completed.");
    }

    public bool IsExperimentFinished()
    {
        return experimentFinished;
    }
}
