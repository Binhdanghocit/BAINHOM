using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Checks playback state and UI callbacks; does not measure audible/device output.
public static class TestAudioSettingsPlayMode
{
    private const string Flag = "BAINHOM.AudioSettingsPlayMode";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> results = new List<string>();
    private static SettingsManager fixture;
    private static AudioManager owner;
    private static AudioClip a, b, c, selected;
    private static AudioClip[] menuClips;
    private static int stage, notificationCount, sampleMarker;
    private static double startedAt, nextStepAt;

    public static void Begin()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        SessionState.SetBool(Flag, true);
        ResumeIfRequested();
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void ResumeIfRequested()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static object Call(object target, string method, params object[] args)
        => target.GetType().GetMethod(method, Private).Invoke(target, args);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        results.Add("PASS: " + message);
    }

    private static T UI<T>(Transform parent) where T : Component
    {
        var go = new GameObject(typeof(T).Name, typeof(RectTransform), typeof(T));
        go.transform.SetParent(parent, false);
        return go.GetComponent<T>();
    }

    private static void CheckVolumes(SettingsManager settings)
    {
        Check(Mathf.Approximately(AudioListener.volume, 0.2f) && Mathf.Approximately(owner.bgmSource.volume, 0.5f),
            "Master 0.2 at listener and BGM 0.5 at source; effective gain 0.1 without a second master multiplication");
        Check(Mathf.Approximately(owner.sfxSource.volume, 0.35f) && Mathf.Approximately(owner.voiceSource.volume, 1f),
            "SFX 0.35 and voice 1.0 retained independently of master");
        Check(Mathf.Approximately(settings.masterSlider.value, 0.2f) && Mathf.Approximately(settings.bgmSlider.value, 0.5f)
            && Mathf.Approximately(settings.sfxSlider.value, 0.35f), "All sliders display shared session values");
    }

    private static void CheckScene(string name)
    {
        Check(SceneManager.GetActiveScene().name == name, "Loaded " + name);
        Check(AudioManager.Instance == owner && owner.bgmSource.isPlaying && owner.bgmSource.clip == selected,
            "Same persistent manager/source keeps selected clip playing across scene change");
        Check(UnityEngine.Object.FindObjectsByType<AudioManager>().Length == 1,
            "Exactly one active AudioManager after duplicate scene manager cleanup");
        var playing = UnityEngine.Object.FindObjectsByType<AudioSource>().Where(s => s.isPlaying).ToArray();
        Check(playing.Length == 1 && playing[0] == owner.bgmSource, "Exactly one AudioSource is playing BGM in saved scene");
        var settings = UnityEngine.Object.FindAnyObjectByType<SettingsManager>();
        CheckVolumes(settings);
        var clips = (List<AudioClip>)typeof(SettingsManager).GetField("dropdownClips", Private).GetValue(settings);
        Check(clips[settings.bgmDropdown.value] == selected, "Dropdown selection maps to actual current clip, including a clip absent from local list");
        if (settings.bgmAudioSource != null)
            Check(!settings.bgmAudioSource.enabled && !settings.bgmAudioSource.isPlaying && !settings.bgmAudioSource.playOnAwake,
                "Serialized menu BGM source is retired without editing its scene");
        notificationCount = 0;
        settings.masterSlider.onValueChanged.AddListener(_ => notificationCount++);
        settings.bgmSlider.onValueChanged.AddListener(_ => notificationCount++);
        settings.sfxSlider.onValueChanged.AddListener(_ => notificationCount++);
        settings.bgmDropdown.onValueChanged.AddListener(_ => notificationCount++);
        sampleMarker = Mathf.Min(owner.bgmSource.clip.frequency * 5, owner.bgmSource.clip.samples / 4);
        owner.bgmSource.timeSamples = sampleMarker;
        settings.OpenPanel();
        settings.ClosePanel();
        settings.OpenPanel();
        settings.ClosePanel();
        Check(notificationCount == 0, "Opening/syncing UI dispatches no slider or dropdown callbacks");
        Check(owner.bgmSource.timeSamples >= sampleMarker, "UI sync preserves playback sample position instead of restarting BGM");
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (startedAt == 0) { startedAt = EditorApplication.timeSinceStartup; nextStepAt = startedAt + 1; }
        if (EditorApplication.timeSinceStartup - startedAt > 150) { Finish(new TimeoutException("Audio settings test timed out.")); return; }
        if (EditorApplication.timeSinceStartup < nextStepAt) return;
        nextStepAt = EditorApplication.timeSinceStartup + 0.5;
        try
        {
            switch (stage)
            {
                case 0:
                    Check(AudioManager.Instance == null, "No singleton exists before menu-style settings callbacks");
                    new GameObject("Test audio listener", typeof(AudioListener));
                    a = AudioClip.Create("Fixture A", 44100 * 60, 1, 44100, false);
                    b = AudioClip.Create("Fixture B", 44100 * 60, 1, 44100, false);
                    c = AudioClip.Create("Fixture C", 44100 * 60, 1, 44100, false);
                    fixture = new GameObject("Pre-manager Settings").AddComponent<SettingsManager>();
                    fixture.enabled = false;
                    fixture.settingsPanel = new GameObject("Fixture panel");
                    fixture.settingsPanel.transform.SetParent(fixture.transform);
                    fixture.masterSlider = UI<Slider>(fixture.transform);
                    fixture.bgmSlider = UI<Slider>(fixture.transform);
                    fixture.sfxSlider = UI<Slider>(fixture.transform);
                    fixture.bgmDropdown = UI<TMP_Dropdown>(fixture.transform);
                    fixture.bgmAudioSource = fixture.gameObject.AddComponent<AudioSource>();
                    fixture.localBGMList = new List<AudioClip> { b, a, null };
                    Call(fixture, "OnMasterVolumeChanged", 0.2f);
                    Call(fixture, "OnBGMVolumeChanged", 0.5f);
                    Call(fixture, "OnSFXVolumeChanged", 0.35f);
                    Call(fixture, "SetupBGMDropdown");
                    Call(fixture, "OnBGMSelected", 0);
                    Check(AudioManager.Instance == null && AudioManager.SelectedBGM == b && AudioManager.BGMVolume == 0.5f
                        && AudioManager.SFXVolume == 0.35f && AudioListener.volume == 0.2f,
                        "Volume and selected clip persist before singleton creation, with local clip order B,A");
                    var root = new GameObject("First audio owner");
                    root.SetActive(false);
                    owner = root.AddComponent<AudioManager>();
                    owner.bgmSource = root.AddComponent<AudioSource>();
                    owner.sfxSource = root.AddComponent<AudioSource>();
                    owner.voiceSource = root.AddComponent<AudioSource>();
                    owner.bgmClips = new[] { a, b, (AudioClip)null };
                    owner.bgmSource.clip = a;
                    root.SetActive(true);
                    break;
                case 1:
                    Check(owner.bgmSource.clip == b && owner.bgmSource.isPlaying, "Pending clip B overrides serialized/default clip A on first startup");
                    owner.bgmSource.timeSamples = 44100 * 20;
                    fixture.masterSlider.onValueChanged.AddListener(_ => notificationCount++);
                    fixture.bgmSlider.onValueChanged.AddListener(_ => notificationCount++);
                    fixture.sfxSlider.onValueChanged.AddListener(_ => notificationCount++);
                    fixture.bgmDropdown.onValueChanged.AddListener(_ => notificationCount++);
                    notificationCount = 0;
                    Call(fixture, "Start");
                    Call(fixture, "Start");
                    CheckVolumes(fixture);
                    Check(notificationCount == 0 && owner.bgmSource.timeSamples >= 44100 * 20,
                        "Repeated Settings initialization silently syncs values and keeps playback position");
                    Check(!fixture.bgmAudioSource.enabled, "Legacy source disabled during runtime registration");
                    fixture.bgmDropdown.value = 1;
                    Check(owner.bgmSource.clip == a, "Actual dropdown event selects local A at index 1, not manager B at index 1");
                    fixture.bgmDropdown.value = 0;
                    owner.bgmSource.timeSamples = 44100 * 20;
                    Call(fixture, "OnBGMSelected", 0);
                    Call(fixture, "OnBGMSelected", 0);
                    Check(owner.bgmSource.clip == b && owner.bgmSource.timeSamples >= 44100 * 20,
                        "Repeated selection/callback of the playing clip is idempotent");
                    Call(fixture, "OnBGMSelected", -1);
                    Call(fixture, "OnBGMSelected", 999);
                    Call(fixture, "OnBGMSelected", 2);
                    owner.ChangeBGM(-1);
                    owner.ChangeBGM(999);
                    owner.ChangeBGM(2);
                    AudioManager.SelectBGM(null);
                    Check(owner.bgmSource.clip == b && fixture.bgmDropdown.value == 0 && owner.bgmSource.isPlaying,
                        "Negative/out-of-range/null selection leaves playback and displayed choice unchanged");
                    var duplicateRoot = new GameObject("Duplicate AudioManager");
                    duplicateRoot.SetActive(false);
                    var duplicate = duplicateRoot.AddComponent<AudioManager>();
                    duplicate.bgmSource = duplicateRoot.AddComponent<AudioSource>();
                    duplicate.bgmSource.clip = c;
                    duplicate.bgmClips = new[] { c };
                    duplicate.footstepClip = c;
                    duplicate.jumpClip = c;
                    duplicate.inspectClip = c;
                    duplicateRoot.SetActive(true);
                    Check(AudioManager.Instance == owner && !duplicate.enabled && !duplicate.bgmSource.enabled,
                        "Duplicate manager stops/disables its sources immediately before deferred destruction");
                    Check(owner.bgmClips.Contains(c) && owner.footstepClip == c && owner.jumpClip == c && owner.inspectClip == c,
                        "Scene duplicate contributes clip catalog and missing SFX metadata to persistent owner");
                    Check(owner.bgmSource.timeSamples >= 44100 * 20, "Duplicate manager cannot restart current BGM");
                    owner.footstepClip = owner.jumpClip = owner.inspectClip = null;
                    selected = b;
                    SceneManager.LoadScene("MainMenu");
                    break;
                case 2:
                    Check(owner.bgmSource.timeSamples >= 44100 * 20, "Scene load did not restart the pending selected clip");
                    CheckScene("MainMenu");
                    var menuSettings = UnityEngine.Object.FindAnyObjectByType<SettingsManager>();
                    menuClips = menuSettings.localBGMList.ToArray();
                    menuSettings.masterSlider.value = 0.7f;
                    menuSettings.masterSlider.value = 0.2f;
                    menuSettings.bgmSlider.value = 0.8f;
                    menuSettings.bgmSlider.value = 0.5f;
                    menuSettings.sfxSlider.value = 0.9f;
                    menuSettings.sfxSlider.value = 0.35f;
                    CheckVolumes(menuSettings);
                    menuSettings.bgmDropdown.value = 1;
                    selected = menuSettings.localBGMList[1];
                    Check(owner.bgmSource.clip == selected, "Saved menu dropdown selects its actual second local clip through shared owner");
                    owner.bgmSource.timeSamples = sampleMarker = Mathf.Min(selected.frequency * 5, selected.samples / 4);
                    SceneManager.LoadScene("SampleScene");
                    break;
                case 3:
                    Check(owner.bgmSource.timeSamples >= sampleMarker, "Menu-selected track carries its sample position into gallery");
                    CheckScene("SampleScene");
                    UnityEngine.Object.FindAnyObjectByType<SettingsManager>().GoToMainMenu();
                    break;
                case 4:
                    Check(owner.bgmSource.timeSamples >= sampleMarker, "Return to menu preserves playback sample position");
                    CheckScene("MainMenu");
                    SceneManager.LoadScene("SampleScene");
                    break;
                case 5:
                    Check(owner.bgmSource.timeSamples >= sampleMarker, "Second gallery load preserves playback sample position");
                    CheckScene("SampleScene");
                    Check(owner.footstepClip != null && owner.jumpClip != null && owner.inspectClip != null,
                        "Actual saved gallery SFX metadata reaches the manager created before gallery load");
                    var gallerySettings = UnityEngine.Object.FindAnyObjectByType<SettingsManager>();
                    var galleryClips = (List<AudioClip>)typeof(SettingsManager).GetField("dropdownClips", Private).GetValue(gallerySettings);
                    int other = galleryClips.FindIndex(clip => clip != null && !menuClips.Contains(clip)
                        && clip != a && clip != b && clip != c);
                    Check(other >= 0, "Gallery catalog includes actual saved gallery music from duplicate manager");
                    gallerySettings.bgmDropdown.value = other;
                    selected = galleryClips[other];
                    Check(owner.bgmSource.clip == selected, "Gallery dropdown selects actual gallery clip by reference");
                    gallerySettings.GoToMainMenu();
                    break;
                case 6:
                    CheckScene("MainMenu");
                    Check(!UnityEngine.Object.FindAnyObjectByType<SettingsManager>().localBGMList.Contains(selected),
                        "Menu displays preserved gallery selection even when absent from menu local list");
                    SceneManager.LoadScene("SampleScene");
                    break;
                case 7:
                    CheckScene("SampleScene");
                    Finish(null);
                    return;
            }
            stage++;
        }
        catch (Exception error) { Finish(error); }
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Flag, false);
        results.Add(error == null
            ? "RESULT: PASS, 8 Play Mode stages. Pre-singleton settings, local clip mapping, silent UI sync, playback continuity, invalid selection, duplicate manager and menu/gallery round trips. Playback state/callbacks checked; audible output, device audio and physical UI input not verified."
            : "RESULT: FAIL at stage " + stage + "\n" + error);
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/AudioSettingsPlayModeResults.txt", results);
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
