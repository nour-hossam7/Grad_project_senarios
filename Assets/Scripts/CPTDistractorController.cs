
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CPTDistractorController : MonoBehaviour
{
    [Header("Glitch Screens")]
    [Tooltip("RawImages allowed to show glitches. Do NOT add Screen (6).")]
    public RawImage[] glitchScreens;

    [Header("Glitch Frames")]
    public Texture2D[] glitchFrames;

    [Header("Glitch Audio")]
    public AudioSource audioSource;
    public AudioClip glitchClip;

    [Range(0f, 1f)]
    public float glitchVolume = 0.8f;

    [Header("Background Clock Sound")]
    public AudioSource clockAudioSource;
    public AudioClip clockClip;

    [Range(0f, 1f)]
    public float normalClockVolume = 0.15f;

    [Range(0f, 1f)]
    public float dpClockVolume = 0.8f;

    [Min(0.1f)]
    public float clockFadeDuration = 5f;

    [Header("Glitch Settings")]
    [Min(0.1f)]
    public float glitchDuration = 2f;

    [Min(1f)]
    public float glitchFrameRate = 12f;

    private Texture[] originalTextures;
    private RawImage currentScreen;

    private Coroutine glitchCoroutine;
    private Coroutine clockFadeCoroutine;

    private bool isRunning;
    private int previousScreenIndex = -1;

    private void Awake()
    {
        SaveOriginalTextures();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (clockAudioSource != null && clockClip == null)
            clockClip = clockAudioSource.clip;
    }

    private void Start()
    {
        StartClockSound();
    }

    private void SaveOriginalTextures()
    {
        if (glitchScreens == null)
            return;

        originalTextures = new Texture[glitchScreens.Length];

        for (int i = 0; i < glitchScreens.Length; i++)
        {
            if (glitchScreens[i] != null)
                originalTextures[i] = glitchScreens[i].texture;
        }
    }

    private void StartClockSound()
    {
        if (clockAudioSource == null)
        {
            Debug.LogWarning("Clock AudioSource is not assigned.");
            return;
        }

        if (clockClip != null)
            clockAudioSource.clip = clockClip;

        if (clockAudioSource.clip == null)
        {
            Debug.LogWarning("Clock audio clip is missing.");
            return;
        }

        clockAudioSource.loop = true;
        clockAudioSource.volume = normalClockVolume;

        if (!clockAudioSource.isPlaying)
            clockAudioSource.Play();
    }

    private void FadeClockVolume(float targetVolume)
    {
        if (clockAudioSource == null)
            return;

        if (!clockAudioSource.isPlaying)
            StartClockSound();

        if (!clockAudioSource.isPlaying)
            return;

        if (clockFadeCoroutine != null)
            StopCoroutine(clockFadeCoroutine);

        clockFadeCoroutine =
            StartCoroutine(FadeClockRoutine(targetVolume));
    }

    private IEnumerator FadeClockRoutine(float targetVolume)
    {
        float startVolume = clockAudioSource.volume;
        float elapsed = 0f;

        while (elapsed < clockFadeDuration)
        {
            if (clockAudioSource == null)
                yield break;

            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / clockFadeDuration);

            clockAudioSource.volume =
                Mathf.Lerp(startVolume, targetVolume, t);

            yield return null;
        }

        if (clockAudioSource != null)
            clockAudioSource.volume = targetVolume;

        clockFadeCoroutine = null;
    }

    // Connect to LetterSequenceManager.onDistractorStart.
    public void StartDistractors()
    {
        isRunning = true;

        FadeClockVolume(dpClockVolume);

        if (glitchCoroutine != null)
        {
            StopCoroutine(glitchCoroutine);
            glitchCoroutine = null;
        }

        RestoreCurrentScreen();

        RawImage selectedScreen = ChooseRandomScreen();

        if (selectedScreen == null)
        {
            Debug.LogWarning(
                "No glitch screen available. Check Glitch Screens."
            );
            return;
        }

        currentScreen = selectedScreen;
        glitchCoroutine = StartCoroutine(GlitchRoutine(currentScreen));

        Debug.Log("DP started. Glitch screen: " + currentScreen.name);
    }

    private RawImage ChooseRandomScreen()
    {
        if (glitchScreens == null || glitchScreens.Length == 0)
            return null;

        int validCount = 0;

        for (int i = 0; i < glitchScreens.Length; i++)
        {
            if (glitchScreens[i] != null && i != previousScreenIndex)
                validCount++;
        }

        bool excludePrevious = validCount > 0;

        if (!excludePrevious)
        {
            for (int i = 0; i < glitchScreens.Length; i++)
            {
                if (glitchScreens[i] != null)
                    validCount++;
            }
        }

        if (validCount == 0)
            return null;

        int randomChoice = Random.Range(0, validCount);
        int chosenIndex = -1;

        for (int i = 0; i < glitchScreens.Length; i++)
        {
            if (glitchScreens[i] == null)
                continue;

            if (excludePrevious && i == previousScreenIndex)
                continue;

            if (randomChoice == 0)
            {
                chosenIndex = i;
                break;
            }

            randomChoice--;
        }

        if (chosenIndex == -1)
        {
            for (int i = 0; i < glitchScreens.Length; i++)
            {
                if (glitchScreens[i] != null)
                {
                    chosenIndex = i;
                    break;
                }
            }
        }

        if (chosenIndex == -1)
            return null;

        previousScreenIndex = chosenIndex;
        return glitchScreens[chosenIndex];
    }

    private IEnumerator GlitchRoutine(RawImage targetScreen)
    {
        if (targetScreen == null ||
            glitchFrames == null ||
            glitchFrames.Length == 0)
        {
            Debug.LogWarning("Glitch screen or frames are missing.");
            RestoreCurrentScreen();
            glitchCoroutine = null;
            yield break;
        }

        // Glitch volume is independent of the clock volume.
        if (audioSource != null && glitchClip != null)
            audioSource.PlayOneShot(glitchClip, glitchVolume);

        float elapsed = 0f;
        float frameInterval = 1f / glitchFrameRate;
        int previousFrameIndex = -1;

        while (elapsed < glitchDuration && isRunning)
        {
            if (targetScreen == null)
                break;

            int frameIndex = Random.Range(0, glitchFrames.Length);

            if (glitchFrames.Length > 1)
            {
                while (frameIndex == previousFrameIndex)
                    frameIndex = Random.Range(0, glitchFrames.Length);
            }

            Texture2D frame = glitchFrames[frameIndex];

            if (frame != null)
                targetScreen.texture = frame;

            previousFrameIndex = frameIndex;

            yield return new WaitForSeconds(frameInterval);
            elapsed += frameInterval;
        }

        RestoreCurrentScreen();
        glitchCoroutine = null;

        Debug.Log("Glitch finished. Original picture restored.");
    }

    // Connect to onDistractorRepeat only if repeated glitches
    // are wanted during the same DP.
    public void PlayNextDistractor()
    {
        if (!isRunning)
            return;

        if (glitchCoroutine != null)
        {
            StopCoroutine(glitchCoroutine);
            glitchCoroutine = null;
        }

        RestoreCurrentScreen();

        RawImage selectedScreen = ChooseRandomScreen();

        if (selectedScreen == null)
            return;

        currentScreen = selectedScreen;
        glitchCoroutine = StartCoroutine(GlitchRoutine(currentScreen));
    }

    // Connect to LetterSequenceManager.onDistractorStop.
    public void StopDistractors()
    {
        isRunning = false;

        if (glitchCoroutine != null)
        {
            StopCoroutine(glitchCoroutine);
            glitchCoroutine = null;
        }

        if (audioSource != null)
            audioSource.Stop();

        RestoreCurrentScreen();

        FadeClockVolume(normalClockVolume);

        Debug.Log("DP ended. Clock continues at normal volume.");
    }

    private void RestoreCurrentScreen()
    {
        if (currentScreen == null ||
            glitchScreens == null ||
            originalTextures == null)
            return;

        for (int i = 0; i < glitchScreens.Length; i++)
        {
            if (glitchScreens[i] == currentScreen)
            {
                currentScreen.texture = originalTextures[i];
                break;
            }
        }

        currentScreen = null;
    }

    private void OnDisable()
    {
        isRunning = false;

        if (glitchCoroutine != null)
        {
            StopCoroutine(glitchCoroutine);
            glitchCoroutine = null;
        }

        if (clockFadeCoroutine != null)
        {
            StopCoroutine(clockFadeCoroutine);
            clockFadeCoroutine = null;
        }

        if (audioSource != null)
            audioSource.Stop();

        if (clockAudioSource != null)
            clockAudioSource.Stop();

        RestoreCurrentScreen();
    }
}
