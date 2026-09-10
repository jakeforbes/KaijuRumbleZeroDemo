using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Exercises the actual runtime encounter and music routes in a fresh Play session.</summary>
public static class BossEncounterCheck
{
    static Enemy boss;
    static BackgroundMusicPlayer music;
    static PlayerProgress player;
    static double deadline;
    static int phase;
    static float previousSpeed;
    static float peak;
    static readonly float[] samples = new float[1024];

    [MenuItem("KRZ/Audio/Check Boss Encounter (Play mode) %&b")]
    static void Run()
    {
        if (!Application.isPlaying || CameraRig.IsBossIntroductionPlaying || Enemy.All.Any(x => x != null && x.IsBoss))
        {
            Debug.LogWarning("Start a fresh Play session without a boss before running this check.");
            return;
        }
        player = PlayerProgress.Instance;
        music = UnityEngine.Object.FindAnyObjectByType<BackgroundMusicPlayer>();
        if (player == null || music == null || music.settings == null || music.settings.bossFight.clip == null)
        {
            Debug.LogWarning("Boss check requires a player, music player and assigned fight track.");
            return;
        }
        previousSpeed = Time.timeScale;
        GameBootstrap.Instance.SpawnOne("Abomination");
        boss = Enemy.All.FirstOrDefault(x => x != null && x.IsBoss);
        if (boss == null) { Finish(false, "Boss failed to spawn at the perimeter."); return; }
        // Isolate test enemy settings; leave the Tuning asset and player untouched.
        boss.type = JsonUtility.FromJson<EnemyType>(JsonUtility.ToJson(boss.type));
        boss.type.moveSpeed = 0;
        boss.type.contactDamage = 0;
        boss.type.attackRange = 0;
        phase = 0;
        peak = 0;
        deadline = EditorApplication.timeSinceStartup + 20;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        try
        {
            if (!Application.isPlaying) { Finish(false, "Play mode stopped."); return; }
            if (boss == null || music == null) { Finish(false, "Test objects disappeared."); return; }
            if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Encounter phase timed out: " + phase); return; }
            if (phase == 0)
            {
                if (!CameraRig.IsBossIntroductionPlaying) { Finish(false, "Boss introduction cinematic did not start."); return; }
                if (!Mathf.Approximately(Time.timeScale, previousSpeed)) { Finish(false, "Gameplay speed changed when it should keep running normally."); return; }
                if (!player.Invulnerable) { Finish(false, "Player was not invulnerable during the boss introduction."); return; }
                if (music.CurrentState != MusicState.BossApproaching) return;
                phase = 1;
            }
            else if (phase == 1)
            {
                if (CameraRig.IsBossIntroductionPlaying)
                {
                    if (!Mathf.Approximately(Time.timeScale, previousSpeed)) { Finish(false, "Gameplay speed changed during the introduction."); return; }
                    if (!player.Invulnerable) { Finish(false, "Player lost invulnerability during the introduction."); return; }
                    if (music.CurrentState == MusicState.BossFight) Finish(false, "Fight triggered during the cinematic.");
                    return;
                }
                if (!Mathf.Approximately(Time.timeScale, previousSpeed)) { Finish(false, "Gameplay speed changed unexpectedly."); return; }
                if (player.Invulnerable) { Finish(false, "Player was still invulnerable after the introduction ended."); return; }
                if (CameraRig.InCombatView(Camera.main, boss.transform, music.settings.bossFightScreenMargin))
                { Finish(false, "Edge boss is already in the player view."); return; }
                if (music.CurrentState != MusicState.BossApproaching) { Finish(false, "Approach music stopped before boss arrival."); return; }
                // Move the test boss's gameplay position into view without requiring
                // the full tall sprite/shadow to fit in the frame.
                var cam = Camera.main;
                boss.GetComponent<Rigidbody2D>().position = new Vector2(player.transform.position.x,
                    cam.transform.position.y + cam.orthographicSize * 0.7f);
                Physics2D.SyncTransforms();
                phase = 2;
                deadline = EditorApplication.timeSinceStartup + Mathf.Max(3, music.settings.crossfadeSeconds + 3);
            }
            else
            {
                foreach (var source in music.GetComponentsInChildren<AudioSource>())
                {
                    if (source.clip != music.settings.bossFight.clip || !source.isPlaying) continue;
                    source.GetOutputData(samples, 0);
                    foreach (var value in samples) peak = Mathf.Max(peak, Mathf.Abs(value));
                }
                if (music.CurrentState == MusicState.BossFight && peak > 0.0001f)
                    Finish(true, "Edge spawn, uninterrupted gameplay with player invulnerability during the intro, approach music, on-screen fight trigger and fight-track audio output. Peak=" + peak);
            }
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }

    static void Finish(bool passed, string message)
    {
        EditorApplication.update -= Tick;
        CameraRig.CancelActiveIntroduction();
        Time.timeScale = previousSpeed;
        if (boss != null) UnityEngine.Object.Destroy(boss.gameObject);
        if (music != null) music.ResumeAutomaticMusic();
        Directory.CreateDirectory("Temp/SoundPlayerValidation");
        string result = (passed ? "PASS: " : "FAIL: ") + message;
        File.WriteAllText("Temp/SoundPlayerValidation/boss-encounter-result.txt", result);
        if (passed) Debug.Log(result); else Debug.LogError(result);
    }
}
