using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Практическое задание №2: AR-игра «Охота за монетами».
/// Игрок закрепляет игровое поле на полу/столе, управляет роботом касанием экрана,
/// собирает монеты, кристаллы и бонус-звезду, избегая красных шипов. 3 уровня, таймер, жизни.
/// 3D-объекты (7 типов): робот, монета, кристалл, звезда-бонус, платформа-поле, шипы-препятствия, угловые столбы.
/// </summary>
public class CoinGame : MonoBehaviour
{
    enum State { Menu, Placing, Playing, Paused, Result }
    enum Kind { Coin, Crystal, Bonus }

    class Pickup { public GameObject go; public Kind kind; public float phase; public Vector3 basePos; }
    class Hazard { public GameObject go; public Vector3 a, b; public float t, speed; public int dir = 1; }
    struct LevelCfg { public int coins, crystals, hazards, target; public float time, hazardSpeed; }

    static readonly LevelCfg[] Levels =
    {
        new LevelCfg { coins = 10, crystals = 1, hazards = 1, target = 10, time = 60, hazardSpeed = 0.22f },
        new LevelCfg { coins = 14, crystals = 2, hazards = 3, target = 18, time = 55, hazardSpeed = 0.32f },
        new LevelCfg { coins = 18, crystals = 3, hazards = 5, target = 28, time = 50, hazardSpeed = 0.42f },
    };

    const float Half = 0.55f;          // половина стороны поля (м)
    const float RobotSpeed = 0.55f;    // м/с
    const float PickRadius = 0.085f;
    const float HitRadius = 0.095f;
    const int StartLives = 3;

    ARRig rig;
    State state;

    // игровое состояние
    int level, score, totalScore, pendingScore, lives;
    float timeLeft, elapsed, invul;
    bool bonusSpawned, touchOverUi;

    // мир
    GameObject anchorGo, board, robot, targetMarker, reticle;
    Transform boardT;
    Renderer[] robotRenderers;
    Vector3 robotTarget;
    readonly List<Pickup> pickups = new();
    readonly List<Hazard> hazards = new();
    Pose lastPose;
    bool hasPose;

    // UI
    GameObject menuPanel, placingPanel, hudPanel, pausePanel, resultPanel;
    Text hintT, scoreT, timeT, levelT, livesT, resultTitle, resultBody;
    Button placeBtn, nextBtn;
    Toast toast;

    // ----------------------------------------------------------------- старт

    void Start()
    {
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Application.targetFrameRate = 60;
        rig = ARRig.Create();
        Sfx.Init(gameObject);
        BuildReticle();
        BuildUI();
        SetState(State.Menu);
    }

    void BuildReticle()
    {
        reticle = new GameObject("Reticle");
        var mat = new Material(Shader.Find("Sprites/Default")) { color = new Color(1f, 0.9f, 0.2f, 0.85f) };
        var outer = Models.Part(reticle.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.5f, 0.004f, 0.5f), Color.white);
        outer.GetComponent<Renderer>().sharedMaterial = mat;
        var hole = Models.Part(reticle.transform, PrimitiveType.Cylinder, new Vector3(0, 0.001f, 0), new Vector3(0.44f, 0.004f, 0.44f), Color.white);
        hole.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = new Color(1f, 0.9f, 0.2f, 0.18f) };
        reticle.SetActive(false);
    }

    // -------------------------------------------------------------------- UI

    void BuildUI()
    {
        var canvas = UIKit.CreateCanvas(out var safe);
        var mid = new Vector2(0.5f, 0.5f);

        // ---- Меню
        menuPanel = UIKit.Panel(safe, "Menu", UIKit.Dim);
        UIKit.Label(menuPanel.transform, "AR ОХОТА\nЗА МОНЕТАМИ", 84, mid, mid, new Vector2(0, 520), new Vector2(900, 260));
        UIKit.Label(menuPanel.transform,
            "1. Наведите камеру на пол или стол и закрепите игровое поле.\n" +
            "2. Касайтесь экрана — робот поедет в эту точку.\n" +
            "3. Собирайте монеты (+1), кристаллы (+5) и звезду (+10 очков, +8 сек).\n" +
            "4. Избегайте красных шипов — у вас 3 жизни.\n" +
            "5. Наберите нужные очки до конца времени. Всего 3 уровня.",
            38, mid, mid, new Vector2(0, 130), new Vector2(880, 460), TextAnchor.MiddleLeft);
        UIKit.Btn(menuPanel.transform, "ИГРАТЬ", 64, UIKit.Green, mid, mid, new Vector2(0, -300), new Vector2(560, 160), () =>
        {
            Sfx.Play(Sfx.Click);
            level = 0; totalScore = 0;
            SetState(State.Placing);
        });

        // ---- Закрепление поля
        placingPanel = UIKit.Panel(safe, "Placing", null);
        hintT = UIKit.Label(placingPanel.transform, "", 42, TextAnchor.MiddleCenter);
        UIKit.TopStrip(hintT.rectTransform, 20, 30, 170);
        placeBtn = UIKit.Btn(placingPanel.transform, "ЗАКРЕПИТЬ ПОЛЕ", 52, UIKit.Green,
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(720, 150), OnPlaceClicked);
        UIKit.Btn(placingPanel.transform, "Меню", 36, UIKit.Gray,
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 150), new Vector2(200, 100), () => { Sfx.Play(Sfx.Click); ToMenu(); });

        // ---- HUD
        hudPanel = UIKit.Panel(safe, "HUD", null);
        scoreT = UIKit.Label(hudPanel.transform, "", 44, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -20), new Vector2(430, 70), TextAnchor.MiddleLeft);
        timeT = UIKit.Label(hudPanel.transform, "", 60, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(40, -14), new Vector2(240, 80));
        levelT = UIKit.Label(hudPanel.transform, "", 38, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -90), new Vector2(430, 60), TextAnchor.MiddleLeft);
        livesT = UIKit.Label(hudPanel.transform, "", 38, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(40, -92), new Vector2(300, 60));
        UIKit.Btn(hudPanel.transform, "II", 56, UIKit.Gray, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -14), new Vector2(130, 120), () =>
        {
            Sfx.Play(Sfx.Click);
            SetState(State.Paused);
        });
        toast = Toast.Create(safe);

        // ---- Пауза
        pausePanel = UIKit.Panel(safe, "Pause", UIKit.Dim);
        UIKit.Label(pausePanel.transform, "ПАУЗА", 90, mid, mid, new Vector2(0, 420), new Vector2(800, 140));
        UIKit.Btn(pausePanel.transform, "Продолжить", 52, UIKit.Green, mid, mid, new Vector2(0, 200), new Vector2(640, 130), () => { Sfx.Play(Sfx.Click); SetState(State.Playing); });
        UIKit.Btn(pausePanel.transform, "Начать уровень заново", 44, UIKit.Accent, mid, mid, new Vector2(0, 50), new Vector2(640, 130), () => { Sfx.Play(Sfx.Click); StartLevel(level); });
        UIKit.Btn(pausePanel.transform, "Переместить поле", 44, UIKit.Accent, mid, mid, new Vector2(0, -100), new Vector2(640, 130), () => { Sfx.Play(Sfx.Click); SetState(State.Placing); });
        UIKit.Btn(pausePanel.transform, "В меню", 44, UIKit.Gray, mid, mid, new Vector2(0, -250), new Vector2(640, 130), () => { Sfx.Play(Sfx.Click); ToMenu(); });

        // ---- Результат
        resultPanel = UIKit.Panel(safe, "Result", UIKit.Dim);
        resultTitle = UIKit.Label(resultPanel.transform, "", 76, mid, mid, new Vector2(0, 470), new Vector2(900, 200));
        resultBody = UIKit.Label(resultPanel.transform, "", 44, mid, mid, new Vector2(0, 270), new Vector2(880, 220));
        nextBtn = UIKit.Btn(resultPanel.transform, "Следующий уровень", 48, UIKit.Green, mid, mid, new Vector2(0, 90), new Vector2(660, 130), () =>
        {
            Sfx.Play(Sfx.Click);
            totalScore += pendingScore;
            pendingScore = 0;
            StartLevel(level + 1);
        });
        UIKit.Btn(resultPanel.transform, "Повторить уровень", 44, UIKit.Accent, mid, mid, new Vector2(0, -60), new Vector2(660, 130), () => { Sfx.Play(Sfx.Click); StartLevel(level); });
        UIKit.Btn(resultPanel.transform, "Играть заново (с 1 уровня)", 40, UIKit.Accent, mid, mid, new Vector2(0, -210), new Vector2(660, 130), () =>
        {
            Sfx.Play(Sfx.Click);
            totalScore = 0; pendingScore = 0;
            StartLevel(0);
        });
        UIKit.Btn(resultPanel.transform, "В меню", 44, UIKit.Gray, mid, mid, new Vector2(0, -360), new Vector2(660, 130), () => { Sfx.Play(Sfx.Click); ToMenu(); });
    }

    void SetState(State s)
    {
        state = s;
        menuPanel.SetActive(s == State.Menu);
        placingPanel.SetActive(s == State.Placing);
        hudPanel.SetActive(s == State.Playing || s == State.Paused);
        pausePanel.SetActive(s == State.Paused);
        resultPanel.SetActive(s == State.Result);

        bool scanning = s == State.Menu || s == State.Placing;
        rig.ShowPlanes(scanning);
        reticle.SetActive(false);
        if (board != null) board.SetActive(s != State.Placing && s != State.Menu);
        if (s == State.Placing) hasPose = false;
    }

    void ToMenu()
    {
        ClearWorld();
        level = 0; totalScore = 0; pendingScore = 0;
        SetState(State.Menu);
    }

    // ----------------------------------------------------------------- цикл

    void Update()
    {
        switch (state)
        {
            case State.Placing: UpdatePlacing(); break;
            case State.Playing: UpdatePlaying(Time.deltaTime); break;
        }
    }

    // ------------------------------------------------- закрепление поля

    void UpdatePlacing()
    {
        var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        hasPose = rig.RaycastFloor(center, out lastPose);
        reticle.SetActive(hasPose);
        if (hasPose)
        {
            reticle.transform.SetPositionAndRotation(lastPose.position, Quaternion.identity);
            float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 4f);
            reticle.transform.localScale = new Vector3(pulse, 1, pulse);
        }
        placeBtn.interactable = hasPose;
        hintT.text = hasPose
            ? "Поверхность найдена!\nНаведите прицел на место для игры и нажмите «Закрепить поле»"
            : "Медленно двигайте телефон над полом или столом, чтобы найти поверхность...";

        if (hasPose && Input.touchCount == 1)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began && !UIKit.OverUI(t.fingerId)) OnPlaceClicked();
        }
    }

    void OnPlaceClicked()
    {
        if (!hasPose) return;
        Sfx.Play(Sfx.Place);
        ClearWorld();

        var toCam = rig.cam.transform.position - lastPose.position;
        toCam.y = 0;
        var rot = toCam.sqrMagnitude > 0.001f ? Quaternion.LookRotation(toCam.normalized) : Quaternion.identity;

        anchorGo = rig.CreateAnchor(lastPose.position, rot);
        BuildBoard(anchorGo.transform);
        StartLevel(level);
    }

    void BuildBoard(Transform parent)
    {
        board = new GameObject("Board");
        board.transform.SetParent(parent, false);
        boardT = board.transform;

        // платформа: шахматные плитки 6x6 по 0.2 м
        var a = Models.C(44, 52, 78);
        var b = Models.C(66, 78, 110);
        for (int i = 0; i < 6; i++)
            for (int j = 0; j < 6; j++)
                Models.Part(boardT, PrimitiveType.Cube, new Vector3(-0.5f + i * 0.2f, -0.01f, -0.5f + j * 0.2f),
                    new Vector3(0.2f, 0.02f, 0.2f), (i + j) % 2 == 0 ? a : b);

        // бортики и угловые столбы
        var orange = Models.C(240, 150, 40);
        Models.Part(boardT, PrimitiveType.Cube, new Vector3(0, 0.015f, 0.61f), new Vector3(1.24f, 0.04f, 0.025f), orange);
        Models.Part(boardT, PrimitiveType.Cube, new Vector3(0, 0.015f, -0.61f), new Vector3(1.24f, 0.04f, 0.025f), orange);
        Models.Part(boardT, PrimitiveType.Cube, new Vector3(0.61f, 0.015f, 0), new Vector3(0.025f, 0.04f, 1.24f), orange);
        Models.Part(boardT, PrimitiveType.Cube, new Vector3(-0.61f, 0.015f, 0), new Vector3(0.025f, 0.04f, 1.24f), orange);
        foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
            {
                Models.Part(boardT, PrimitiveType.Cylinder, new Vector3(sx * 0.61f, 0.05f, sz * 0.61f), new Vector3(0.05f, 0.1f, 0.05f), Models.C(250, 210, 60));
                Models.Part(boardT, PrimitiveType.Sphere, new Vector3(sx * 0.61f, 0.11f, sz * 0.61f), Vector3.one * 0.05f, Models.C(255, 240, 150));
            }

        robot = BuildRobot(boardT);
        robotRenderers = robot.GetComponentsInChildren<Renderer>();

        targetMarker = Models.Part(boardT, PrimitiveType.Cylinder, new Vector3(0, 0.003f, 0), new Vector3(0.09f, 0.004f, 0.09f), Color.white);
        targetMarker.GetComponent<Renderer>().sharedMaterial =
            new Material(Shader.Find("Sprites/Default")) { color = new Color(0.3f, 1f, 0.5f, 0.6f) };
        targetMarker.SetActive(false);
    }

    // ---------------------------------------------------------- 3D-объекты

    static GameObject BuildRobot(Transform parent)
    {
        var r = new GameObject("Robot").transform;
        r.SetParent(parent, false);
        var blue = Models.C(60, 130, 230); var light = Models.C(225, 235, 245); var dark = Models.C(30, 35, 45);
        Models.Part(r, PrimitiveType.Cube, new Vector3(0, 0.055f, 0), new Vector3(0.085f, 0.06f, 0.065f), blue);
        Models.Part(r, PrimitiveType.Sphere, new Vector3(0, 0.115f, 0), new Vector3(0.065f, 0.06f, 0.06f), light);
        Models.Part(r, PrimitiveType.Sphere, new Vector3(-0.015f, 0.12f, 0.024f), Vector3.one * 0.014f, dark);
        Models.Part(r, PrimitiveType.Sphere, new Vector3(0.015f, 0.12f, 0.024f), Vector3.one * 0.014f, dark);
        Models.Part(r, PrimitiveType.Cylinder, new Vector3(0, 0.16f, 0), new Vector3(0.005f, 0.035f, 0.005f), dark);
        Models.Part(r, PrimitiveType.Sphere, new Vector3(0, 0.18f, 0), Vector3.one * 0.016f, Models.C(235, 60, 60));
        foreach (int sx in new[] { -1, 1 })
            Models.Part(r, PrimitiveType.Cylinder, new Vector3(sx * 0.05f, 0.026f, 0), new Vector3(0.05f, 0.016f, 0.05f), dark, new Vector3(0, 0, 90));
        return r.gameObject;
    }

    static GameObject BuildCoin(Transform parent)
    {
        var g = new GameObject("Coin").transform;
        g.SetParent(parent, false);
        Models.Part(g, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.07f, 0.012f, 0.07f), Models.C(235, 180, 30), new Vector3(90, 0, 0));
        Models.Part(g, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.05f, 0.016f, 0.05f), Models.C(255, 215, 80), new Vector3(90, 0, 0));
        return g.gameObject;
    }

    static GameObject BuildCrystal(Transform parent)
    {
        var g = new GameObject("Crystal").transform;
        g.SetParent(parent, false);
        Models.Part(g, PrimitiveType.Cube, Vector3.zero, Vector3.one * 0.055f, Models.C(60, 210, 240), new Vector3(35, 45, 0));
        Models.Part(g, PrimitiveType.Cube, Vector3.zero, Vector3.one * 0.04f, Models.C(190, 245, 255), new Vector3(-30, 10, 45));
        return g.gameObject;
    }

    static GameObject BuildStar(Transform parent)
    {
        var g = new GameObject("Bonus").transform;
        g.SetParent(parent, false);
        var y = Models.C(255, 200, 40);
        Models.Part(g, PrimitiveType.Cube, Vector3.zero, new Vector3(0.1f, 0.1f, 0.02f), y);
        Models.Part(g, PrimitiveType.Cube, Vector3.zero, new Vector3(0.1f, 0.1f, 0.02f), y, new Vector3(0, 0, 45));
        Models.Part(g, PrimitiveType.Sphere, new Vector3(0, 0, 0.012f), Vector3.one * 0.04f, Models.C(255, 245, 160));
        return g.gameObject;
    }

    static GameObject BuildSpike(Transform parent)
    {
        var g = new GameObject("Spike").transform;
        g.SetParent(parent, false);
        Models.Part(g, PrimitiveType.Cylinder, new Vector3(0, 0.01f, 0), new Vector3(0.1f, 0.02f, 0.1f), Models.C(60, 20, 20));
        Models.Part(g, PrimitiveType.Cube, new Vector3(0, 0.07f, 0), Vector3.one * 0.06f, Models.C(230, 40, 40), new Vector3(45, 45, 0));
        Models.Part(g, PrimitiveType.Cube, new Vector3(0, 0.07f, 0), Vector3.one * 0.035f, Models.C(255, 120, 100), new Vector3(0, 45, 45));
        return g.gameObject;
    }

    // --------------------------------------------------------------- уровень

    void ClearWorld()
    {
        pickups.Clear();
        hazards.Clear();
        if (anchorGo != null) Destroy(anchorGo);
        anchorGo = null; board = null; boardT = null; robot = null; targetMarker = null;
    }

    void StartLevel(int index)
    {
        level = Mathf.Clamp(index, 0, Levels.Length - 1);
        if (board == null) { SetState(State.Placing); return; }

        // убрать старые предметы
        foreach (var p in pickups) if (p.go != null) Destroy(p.go);
        foreach (var h in hazards) if (h.go != null) Destroy(h.go);
        pickups.Clear();
        hazards.Clear();

        var cfg = Levels[level];
        score = 0;
        lives = StartLives;
        timeLeft = cfg.time;
        elapsed = 0;
        invul = 0;
        bonusSpawned = false;
        robot.transform.localPosition = Vector3.zero;
        robotTarget = Vector3.zero;
        foreach (var r in robotRenderers) r.enabled = true;

        var used = new List<Vector3> { Vector3.zero };
        for (int i = 0; i < cfg.coins; i++) SpawnPickup(Kind.Coin, used);
        for (int i = 0; i < cfg.crystals; i++) SpawnPickup(Kind.Crystal, used);
        for (int i = 0; i < cfg.hazards; i++) SpawnHazard(cfg.hazardSpeed, used);

        SetState(State.Playing);
        RefreshHud();
        toast.Show("Уровень " + (level + 1) + ": наберите " + cfg.target + " очков!", 2.2f);
    }

    Vector3 RandomSpot(List<Vector3> used, float minDist)
    {
        for (int tries = 0; tries < 60; tries++)
        {
            var p = new Vector3(Random.Range(-Half + 0.07f, Half - 0.07f), 0, Random.Range(-Half + 0.07f, Half - 0.07f));
            bool ok = true;
            foreach (var u in used)
                if ((u - p).sqrMagnitude < minDist * minDist) { ok = false; break; }
            if (ok) { used.Add(p); return p; }
        }
        var f = new Vector3(Random.Range(-Half + 0.07f, Half - 0.07f), 0, Random.Range(-Half + 0.07f, Half - 0.07f));
        used.Add(f);
        return f;
    }

    void SpawnPickup(Kind kind, List<Vector3> used)
    {
        var pos = RandomSpot(used, 0.16f);
        var go = kind == Kind.Coin ? BuildCoin(boardT) : kind == Kind.Crystal ? BuildCrystal(boardT) : BuildStar(boardT);
        pos.y = kind == Kind.Bonus ? 0.09f : 0.065f;
        go.transform.localPosition = pos;
        pickups.Add(new Pickup { go = go, kind = kind, phase = Random.value * 6f, basePos = pos });
    }

    void SpawnHazard(float speed, List<Vector3> used)
    {
        var a = RandomSpot(used, 0.25f);
        Vector3 b = a;
        for (int i = 0; i < 20; i++)
        {
            b = new Vector3(Random.Range(-Half + 0.08f, Half - 0.08f), 0, Random.Range(-Half + 0.08f, Half - 0.08f));
            if ((b - a).magnitude > 0.4f) break;
        }
        var go = BuildSpike(boardT);
        go.transform.localPosition = a;
        hazards.Add(new Hazard { go = go, a = a, b = b, t = Random.value, speed = speed });
    }

    void RefreshHud()
    {
        var cfg = Levels[level];
        scoreT.text = "Очки: " + score + " / " + cfg.target;
        levelT.text = "Уровень " + (level + 1) + " из " + Levels.Length;
        livesT.text = "Жизни: " + lives;
        int sec = Mathf.CeilToInt(Mathf.Max(0, timeLeft));
        timeT.text = (sec / 60) + ":" + (sec % 60).ToString("00");
        timeT.color = timeLeft < 10 ? new Color(1f, 0.4f, 0.4f) : Color.white;
    }

    // ------------------------------------------------------------ игровой цикл

    void UpdatePlaying(float dt)
    {
        if (board == null) return;
        var cfg = Levels[level];
        elapsed += dt;
        timeLeft -= dt;
        if (timeLeft <= 0) { timeLeft = 0; RefreshHud(); EndLevel(false, "Время вышло!"); return; }

        // бонус-звезда появляется через 8 секунд
        if (!bonusSpawned && elapsed >= 8f)
        {
            bonusSpawned = true;
            var used = new List<Vector3> { robot.transform.localPosition };
            foreach (var p in pickups) used.Add(p.basePos);
            SpawnPickup(Kind.Bonus, used);
            Sfx.Play(Sfx.Place);
            toast.Show("Появилась бонус-звезда!");
        }

        HandleTouch();
        MoveRobot(dt);
        AnimateWorld(dt);
        CheckPickups();
        CheckHazards(dt);
        RefreshHud();

        if (state == State.Playing && score >= cfg.target) EndLevel(true, "Уровень пройден!");
    }

    void HandleTouch()
    {
        if (Input.touchCount == 0) return;
        var t = Input.GetTouch(0);
        if (t.phase == TouchPhase.Began) touchOverUi = UIKit.OverUI(t.fingerId);
        if (touchOverUi) return;
        if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) return;

        // Луч от пальца до плоскости игрового поля
        var ray = rig.cam.ScreenPointToRay(t.position);
        var plane = new Plane(boardT.up, boardT.position);
        if (plane.Raycast(ray, out float d))
        {
            var local = boardT.InverseTransformPoint(ray.GetPoint(d));
            local.y = 0;
            local.x = Mathf.Clamp(local.x, -Half + 0.04f, Half - 0.04f);
            local.z = Mathf.Clamp(local.z, -Half + 0.04f, Half - 0.04f);
            robotTarget = local;
            targetMarker.SetActive(true);
            targetMarker.transform.localPosition = new Vector3(local.x, 0.003f, local.z);
        }
    }

    void MoveRobot(float dt)
    {
        var rt = robot.transform;
        var p = rt.localPosition;
        var to = robotTarget - p;
        to.y = 0;
        float dist = to.magnitude;
        if (dist > 0.006f)
        {
            var dir = to / dist;
            p += dir * Mathf.Min(dist, RobotSpeed * dt);
            rt.localPosition = p;
            rt.localRotation = Quaternion.Slerp(rt.localRotation, Quaternion.LookRotation(dir, Vector3.up), 12f * dt);
        }
        else if (targetMarker.activeSelf)
        {
            targetMarker.SetActive(false);
        }
    }

    void AnimateWorld(float dt)
    {
        foreach (var p in pickups)
        {
            if (p.go == null) continue;
            p.go.transform.Rotate(0, 150f * dt, 0, Space.Self);
            p.go.transform.localPosition = p.basePos + Vector3.up * (Mathf.Sin(Time.time * 3f + p.phase) * 0.012f);
        }
        foreach (var h in hazards)
        {
            h.t += h.dir * h.speed * dt / Mathf.Max(0.2f, (h.b - h.a).magnitude);
            if (h.t > 1f) { h.t = 1f; h.dir = -1; }
            if (h.t < 0f) { h.t = 0f; h.dir = 1; }
            h.go.transform.localPosition = Vector3.Lerp(h.a, h.b, h.t);
            h.go.transform.Rotate(0, 90f * dt, 0, Space.Self);
        }
        if (invul > 0)
        {
            invul -= dt;
            bool vis = invul <= 0 || ((int)(invul * 10f) % 2 == 0);
            foreach (var r in robotRenderers) r.enabled = vis;
        }
    }

    void CheckPickups()
    {
        var rp = robot.transform.localPosition;
        for (int i = pickups.Count - 1; i >= 0; i--)
        {
            var p = pickups[i];
            var d = p.basePos - rp;
            d.y = 0;
            if (d.magnitude > PickRadius) continue;

            switch (p.kind)
            {
                case Kind.Coin: score += 1; Sfx.Play(Sfx.Coin); break;
                case Kind.Crystal: score += 5; Sfx.Play(Sfx.Crystal); toast.Show("+5 кристалл!"); break;
                case Kind.Bonus: score += 10; timeLeft += 8f; Sfx.Play(Sfx.Bonus); toast.Show("БОНУС! +10 очков, +8 сек"); break;
            }
            Destroy(p.go);
            pickups.RemoveAt(i);
        }
    }

    void CheckHazards(float dt)
    {
        if (invul > 0) return;
        var rp = robot.transform.localPosition;
        foreach (var h in hazards)
        {
            var d = h.go.transform.localPosition - rp;
            d.y = 0;
            if (d.magnitude > HitRadius) continue;

            lives--;
            invul = 1.6f;
            Sfx.Play(Sfx.Hit);
#if UNITY_ANDROID && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
            toast.Show("Ай! Осталось жизней: " + lives);
            if (lives <= 0) { RefreshHud(); EndLevel(false, "Жизни закончились!"); }
            return;
        }
    }

    // -------------------------------------------------------------- итоги

    void EndLevel(bool win, string title)
    {
        bool last = level >= Levels.Length - 1;
        pendingScore = win ? score : 0;
        int gross = totalScore + score;
        int best = PlayerPrefs.GetInt("coin_best", 0);
        bool record = gross > best;
        if (record) { best = gross; PlayerPrefs.SetInt("coin_best", best); PlayerPrefs.Save(); }

        Sfx.Play(win ? Sfx.Win : Sfx.Lose);
        resultTitle.text = win && last ? "ВЫ ПРОШЛИ ВСЕ УРОВНИ!" : title;
        resultTitle.color = win ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.5f, 0.5f);
        resultBody.text = "Очки на уровне: " + score + " / " + Levels[level].target +
                          "\nВсего очков: " + gross +
                          "\nРекорд: " + best + (record ? "  (новый!)" : "");
        nextBtn.gameObject.SetActive(win && !last);
        SetState(State.Result);
    }
}
