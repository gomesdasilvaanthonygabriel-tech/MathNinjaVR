using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Monta a sala de aula 3D com o professor robo, o quadro e o lancador de esferas.
/// Menu: MathNinja/Build Classroom (pode ser rodado de novo: reconstroi tudo).
/// </summary>
public static class ClassroomBuilder
{
    const float RoomW = 10f;   // x
    const float RoomD = 10f;   // z
    const float RoomH = 3.5f;  // y
    const string MatDir = "Assets/Materials";
    const string PrefabDir = "Assets/Prefabs";
    const string ScenePath = "Assets/Scenes/Game.unity";

    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static Font font;

    [MenuItem("MathNinja/Build Classroom")]
    public static void Build()
    {
        mats.Clear();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureFolder("Assets/Materials");
        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Scenes");

        foreach (string n in new[] { "Classroom", "Gameplay" })
        {
            var old = GameObject.Find(n);
            if (old != null) Object.DestroyImmediate(old);
        }

        var root = new GameObject("Classroom").transform;
        var structure = Group("Structure", root);
        var furniture = Group("Furniture", root);
        var decor = Group("Decor", root);
        var lights = Group("Lights", root);

        BuildStructure(structure);
        BuildBoard(decor, out TextMesh questionText, out TextMesh scoreText, out TextMesh feedbackText);
        BuildFurniture(furniture);
        BuildDecor(decor);
        BuildLights(lights);
        BuildRobot(root);

        AnswerTarget prefab = BuildAnswerPrefab();
        BuildSpawner(prefab, questionText, scoreText, feedbackText);
        SetupCameraAndAmbient();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[MathNinja] Sala de aula construida e salva em " + ScenePath);
    }

    // ---------------------------------------------------------------- estrutura

    static void BuildStructure(Transform p)
    {
        var floorM = Mat("Floor_Wood", new Color(0.55f, 0.40f, 0.27f), 0f, 0.35f);
        var carpetM = Mat("Floor_Carpet", new Color(0.22f, 0.30f, 0.48f), 0f, 0.1f);
        var wallM = Mat("Wall_Cream", new Color(0.90f, 0.87f, 0.78f), 0f, 0.1f);
        var bandM = Mat("Wall_Band", new Color(0.18f, 0.45f, 0.42f), 0f, 0.15f);
        var ceilM = Mat("Ceiling", new Color(0.95f, 0.95f, 0.95f), 0f, 0.05f);

        float hw = RoomW / 2f, hd = RoomD / 2f;

        Box("Floor", p, new Vector3(0, -0.05f, 0), new Vector3(RoomW + 0.4f, 0.1f, RoomD + 0.4f), floorM);
        Box("Carpet", p, new Vector3(0, 0.006f, 0), new Vector3(3.6f, 0.012f, RoomD - 0.4f), carpetM, false);
        Box("Ceiling", p, new Vector3(0, RoomH + 0.05f, 0), new Vector3(RoomW + 0.4f, 0.1f, RoomD + 0.4f), ceilM);

        Box("Wall_Front", p, new Vector3(0, RoomH / 2f, hd + 0.1f), new Vector3(RoomW + 0.4f, RoomH, 0.2f), wallM);
        Box("Wall_Back", p, new Vector3(0, RoomH / 2f, -hd - 0.1f), new Vector3(RoomW + 0.4f, RoomH, 0.2f), wallM);
        Box("Wall_Left", p, new Vector3(-hw - 0.1f, RoomH / 2f, 0), new Vector3(0.2f, RoomH, RoomD + 0.4f), wallM);
        Box("Wall_Right", p, new Vector3(hw + 0.1f, RoomH / 2f, 0), new Vector3(0.2f, RoomH, RoomD + 0.4f), wallM);

        float bandH = 1.0f;
        Box("Band_Front", p, new Vector3(0, bandH / 2f, hd - 0.02f), new Vector3(RoomW, bandH, 0.04f), bandM, false);
        Box("Band_Back", p, new Vector3(0, bandH / 2f, -hd + 0.02f), new Vector3(RoomW, bandH, 0.04f), bandM, false);
        Box("Band_Left", p, new Vector3(-hw + 0.02f, bandH / 2f, 0), new Vector3(0.04f, bandH, RoomD), bandM, false);
        Box("Band_Right", p, new Vector3(hw - 0.02f, bandH / 2f, 0), new Vector3(0.04f, bandH, RoomD), bandM, false);
    }

    // ---------------------------------------------------------------- quadro

    static void BuildBoard(Transform p, out TextMesh questionText, out TextMesh scoreText, out TextMesh feedbackText)
    {
        var frameM = Mat("Board_Frame", new Color(0.42f, 0.27f, 0.14f), 0f, 0.3f);
        var boardM = Mat("Board_Green", new Color(0.09f, 0.24f, 0.19f), 0f, 0.05f);
        var chalkM = Mat("Chalk_White", new Color(0.95f, 0.95f, 0.92f), 0f, 0.05f);

        float z = RoomD / 2f;
        var board = Group("Blackboard", p);
        Box("Frame", board, new Vector3(0, 1.75f, z - 0.04f), new Vector3(4.7f, 1.7f, 0.08f), frameM);
        Box("Surface", board, new Vector3(0, 1.75f, z - 0.085f), new Vector3(4.5f, 1.5f, 0.02f), boardM, false);
        Box("ChalkTray", board, new Vector3(0, 0.88f, z - 0.1f), new Vector3(4.5f, 0.05f, 0.12f), frameM, false);
        Box("Chalk_A", board, new Vector3(-1.2f, 0.92f, z - 0.1f), new Vector3(0.1f, 0.02f, 0.02f), chalkM, false);
        Box("Chalk_B", board, new Vector3(-1.0f, 0.92f, z - 0.1f), new Vector3(0.08f, 0.02f, 0.02f), chalkM, false);

        Text("BoardTitle", board, new Vector3(0.45f, 2.28f, z - 0.1f), Vector3.zero, "Math Ninja", 0.17f,
            new Color(0.9f, 0.95f, 0.8f), TextAnchor.MiddleCenter);
        questionText = Text("QuestionText", board, new Vector3(0.45f, 1.7f, z - 0.1f), Vector3.zero, "7 × 8 = ?", 0.5f,
            Color.white, TextAnchor.MiddleCenter);
        scoreText = Text("ScoreText", board, new Vector3(1.7f, 2.28f, z - 0.1f), Vector3.zero, "Pontos: 0", 0.15f,
            new Color(1f, 0.9f, 0.4f), TextAnchor.MiddleCenter);
        feedbackText = Text("FeedbackText", board, new Vector3(0.45f, 1.2f, z - 0.1f), Vector3.zero, "", 0.17f,
            new Color(0.7f, 1f, 0.7f), TextAnchor.MiddleCenter);
    }

    // ---------------------------------------------------------------- moveis

    static void BuildFurniture(Transform p)
    {
        var woodM = Mat("Desk_Wood", new Color(0.72f, 0.52f, 0.30f), 0f, 0.3f);
        var metalM = Mat("Desk_Metal", new Color(0.35f, 0.37f, 0.40f), 0.6f, 0.5f);
        var chairColors = new[]
        {
            Mat("Chair_Blue", new Color(0.25f, 0.45f, 0.85f), 0f, 0.3f),
            Mat("Chair_Orange", new Color(0.95f, 0.55f, 0.15f), 0f, 0.3f)
        };

        float[] cols = { -3.9f, -2.7f, 2.7f, 3.9f };
        float[] rows = { -3.4f, -2.1f, -0.8f, 0.5f };
        int idx = 0;
        foreach (float x in cols)
            foreach (float z in rows)
            {
                Desk(p, "StudentDesk_" + idx, new Vector3(x, 0, z), 0f, woodM, metalM, chairColors[idx % 2], true);
                idx++;
            }

        Desk(p, "TeacherDesk", new Vector3(3.6f, 0, 4.0f), 180f, woodM, metalM, chairColors[0], false, 1.8f);
    }

    static void Desk(Transform parent, string name, Vector3 pos, float yaw, Material wood, Material metal,
        Material chairM, bool withChair, float width = 1.0f)
    {
        var d = Group(name, parent);
        d.localPosition = pos;
        d.localRotation = Quaternion.Euler(0, yaw, 0);

        Box("Top", d, new Vector3(0, 0.74f, 0), new Vector3(width, 0.05f, 0.6f), wood);
        float lx = width / 2f - 0.05f;
        foreach (float sx in new[] { -lx, lx })
            foreach (float sz in new[] { -0.24f, 0.24f })
                Cyl("Leg", d, new Vector3(sx, 0.36f, sz), new Vector3(0.04f, 0.36f, 0.04f), metal, false);

        if (!withChair) return;
        var c = Group("Chair", d);
        c.localPosition = new Vector3(0, 0, -0.62f);
        Box("Seat", c, new Vector3(0, 0.45f, 0), new Vector3(0.42f, 0.05f, 0.42f), chairM, false);
        Box("Back", c, new Vector3(0, 0.72f, -0.2f), new Vector3(0.42f, 0.38f, 0.04f), chairM, false);
        foreach (float sx in new[] { -0.17f, 0.17f })
            foreach (float sz in new[] { -0.17f, 0.17f })
                Cyl("Leg", c, new Vector3(sx, 0.22f, sz), new Vector3(0.03f, 0.22f, 0.03f), metal, false);
    }

    // ---------------------------------------------------------------- decoracao

    static void BuildDecor(Transform p)
    {
        var glassM = Mat("Window_Glass", new Color(0.6f, 0.8f, 1f), 0f, 0.9f, new Color(0.45f, 0.65f, 0.95f), 0.8f);
        var frameM = Mat("Window_Frame", new Color(0.95f, 0.95f, 0.95f), 0f, 0.3f);
        var doorM = Mat("Door_Wood", new Color(0.50f, 0.32f, 0.18f), 0f, 0.3f);
        var darkM = Mat("Dark_Metal", new Color(0.12f, 0.12f, 0.14f), 0.5f, 0.4f);
        var whiteM = Mat("Clock_Face", new Color(0.97f, 0.97f, 0.95f), 0f, 0.5f);

        float hw = RoomW / 2f, hd = RoomD / 2f;

        foreach (float z in new[] { -2.8f, 0f, 2.8f })
        {
            var w = Group("Window", p);
            w.localPosition = new Vector3(-hw + 0.04f, 1.9f, z);
            Box("Frame", w, Vector3.zero, new Vector3(0.06f, 1.5f, 1.7f), frameM, false);
            Box("Glass", w, new Vector3(0.02f, 0, 0), new Vector3(0.04f, 1.36f, 1.56f), glassM, false);
            Box("BarV", w, new Vector3(0.04f, 0, 0), new Vector3(0.04f, 1.4f, 0.05f), frameM, false);
            Box("BarH", w, new Vector3(0.04f, 0, 0), new Vector3(0.04f, 0.05f, 1.6f), frameM, false);
            Box("Sill", w, new Vector3(0.08f, -0.78f, 0), new Vector3(0.2f, 0.05f, 1.85f), frameM, false);
        }

        var door = Group("Door", p);
        door.localPosition = new Vector3(3.5f, 1.05f, -hd + 0.05f);
        Box("Leaf", door, Vector3.zero, new Vector3(1.0f, 2.1f, 0.08f), doorM, false);
        Box("Handle", door, new Vector3(-0.38f, -0.05f, 0.07f), new Vector3(0.05f, 0.05f, 0.1f), darkM, false);
        Box("Window", door, new Vector3(0, 0.5f, 0.045f), new Vector3(0.45f, 0.5f, 0.02f), glassM, false);

        var clock = Group("Clock", p);
        clock.localPosition = new Vector3(0, 2.7f, -hd + 0.05f);
        var face = Cyl("Face", clock, Vector3.zero, new Vector3(0.45f, 0.02f, 0.45f), whiteM, false);
        face.transform.localRotation = Quaternion.Euler(90, 0, 0);
        Box("HandH", clock, new Vector3(0, 0.06f, 0.025f), new Vector3(0.02f, 0.14f, 0.01f), darkM, false);
        Box("HandM", clock, new Vector3(0.09f, 0, 0.03f), new Vector3(0.2f, 0.015f, 0.01f), darkM, false);

        string[] symbols = { "+", "-", "×", "÷" };
        Color[] cols =
        {
            new Color(0.95f, 0.45f, 0.35f), new Color(0.30f, 0.60f, 0.95f),
            new Color(0.35f, 0.80f, 0.50f), new Color(0.95f, 0.75f, 0.25f)
        };
        for (int i = 0; i < 4; i++)
        {
            var m = Mat("Poster_" + i, cols[i], 0f, 0.2f);
            float z = -3.3f + i * 2.2f;
            Box("Poster_" + symbols[i], p, new Vector3(hw - 0.03f, 1.9f, z), new Vector3(0.04f, 1.1f, 1.1f), m, false);
            Text("PosterSymbol", p, new Vector3(hw - 0.06f, 1.9f, z), new Vector3(0, -90, 0), symbols[i], 0.9f,
                Color.white, TextAnchor.MiddleCenter);
        }
    }

    // ---------------------------------------------------------------- luzes

    static void BuildLights(Transform p)
    {
        var panelM = Mat("Ceiling_Light", Color.white, 0f, 0.2f, new Color(1f, 0.96f, 0.85f), 2.2f);
        Vector3[] pos =
        {
            new Vector3(-2.5f, RoomH - 0.02f, -2f), new Vector3(2.5f, RoomH - 0.02f, -2f),
            new Vector3(-2.5f, RoomH - 0.02f, 2f), new Vector3(2.5f, RoomH - 0.02f, 2f)
        };
        int i = 0;
        foreach (var v in pos)
        {
            Box("LightPanel_" + i, p, v, new Vector3(1.4f, 0.04f, 0.5f), panelM, false);
            var l = new GameObject("PointLight_" + i).AddComponent<Light>();
            l.transform.SetParent(p, false);
            l.transform.localPosition = v + Vector3.down * 0.4f;
            l.type = LightType.Point;
            l.color = new Color(1f, 0.95f, 0.85f);
            l.intensity = 6f;
            l.range = 9f;
            l.shadows = LightShadows.None;
            i++;
        }

        var spot = new GameObject("BoardLight").AddComponent<Light>();
        spot.transform.SetParent(p, false);
        spot.transform.localPosition = new Vector3(0.4f, RoomH - 0.3f, 3.2f);
        spot.type = LightType.Point;
        spot.color = new Color(1f, 0.97f, 0.9f);
        spot.intensity = 5f;
        spot.range = 6f;
        spot.shadows = LightShadows.None;
    }

    // ---------------------------------------------------------------- robo

    static void BuildRobot(Transform parent)
    {
        var white = Mat("Robot_White", new Color(0.88f, 0.90f, 0.94f), 0.3f, 0.7f);
        var accent = Mat("Robot_Accent", new Color(0.95f, 0.50f, 0.12f), 0.2f, 0.6f);
        var dark = Mat("Robot_Dark", new Color(0.07f, 0.08f, 0.10f), 0.6f, 0.8f);
        var metal = Mat("Robot_Metal", new Color(0.50f, 0.53f, 0.58f), 0.9f, 0.6f);
        var eye = Mat("Robot_Eye", new Color(0.2f, 0.9f, 1f), 0f, 0.5f, new Color(0.2f, 0.9f, 1f), 3f);
        var bulb = Mat("Robot_Bulb", new Color(1f, 0.4f, 0.25f), 0f, 0.5f, new Color(1f, 0.35f, 0.2f), 2.5f);
        var chalk = Mat("Chalk_White", new Color(0.95f, 0.95f, 0.92f), 0f, 0.05f);

        var robot = Group("RobotTeacher", parent);
        robot.localPosition = new Vector3(-1.7f, 0, 4.4f); // de frente para o quadro (olha para +z)
        robot.localRotation = Quaternion.identity;

        foreach (float sx in new[] { -0.14f, 0.14f })
        {
            Cyl("Leg", robot, new Vector3(sx, 0.36f, 0), new Vector3(0.12f, 0.34f, 0.12f), metal, false);
            Box("Foot", robot, new Vector3(sx, 0.045f, 0.05f), new Vector3(0.2f, 0.09f, 0.32f), accent, false);
            Sphere("Knee", robot, new Vector3(sx, 0.42f, 0), new Vector3(0.16f, 0.16f, 0.16f), dark, false);
        }

        var upper = Group("UpperBody", robot);
        Box("Pelvis", upper, new Vector3(0, 0.76f, 0), new Vector3(0.44f, 0.12f, 0.28f), dark, false);
        Box("Torso", upper, new Vector3(0, 1.1f, 0), new Vector3(0.58f, 0.62f, 0.34f), white, false);
        Box("ChestBand", upper, new Vector3(0, 1.22f, 0), new Vector3(0.6f, 0.08f, 0.36f), accent, false);

        // costas (voltadas para o jogador): painel com luzes
        Box("BackPanel", upper, new Vector3(0, 1.08f, -0.175f), new Vector3(0.40f, 0.40f, 0.02f), dark, false);
        for (int i = 0; i < 3; i++)
            Box("BackLED_" + i, upper, new Vector3(-0.1f + i * 0.1f, 1.18f, -0.19f), new Vector3(0.05f, 0.05f, 0.01f),
                i == 1 ? bulb : eye, false);
        for (int i = 0; i < 4; i++)
            Box("Vent_" + i, upper, new Vector3(0, 0.96f + i * 0.04f, -0.19f), new Vector3(0.28f, 0.015f, 0.01f), metal, false);

        Cyl("Neck", upper, new Vector3(0, 1.45f, 0), new Vector3(0.12f, 0.06f, 0.12f), metal, false);

        var head = Group("Head", upper);
        head.localPosition = new Vector3(0, 1.66f, 0);
        Box("Skull", head, Vector3.zero, new Vector3(0.40f, 0.32f, 0.34f), white, false);
        Box("Visor", head, new Vector3(0, 0.01f, 0.175f), new Vector3(0.32f, 0.17f, 0.02f), dark, false);
        Box("EyeL", head, new Vector3(-0.07f, 0.02f, 0.19f), new Vector3(0.07f, 0.045f, 0.01f), eye, false);
        Box("EyeR", head, new Vector3(0.07f, 0.02f, 0.19f), new Vector3(0.07f, 0.045f, 0.01f), eye, false);
        foreach (float sx in new[] { -0.215f, 0.215f })
        {
            var ear = Cyl("Ear", head, new Vector3(sx, 0, 0), new Vector3(0.14f, 0.03f, 0.14f), accent, false);
            ear.transform.localRotation = Quaternion.Euler(0, 0, 90);
        }
        Cyl("AntennaRod", head, new Vector3(0, 0.22f, 0), new Vector3(0.02f, 0.06f, 0.02f), metal, false);
        var bulbGo = Sphere("AntennaBulb", head, new Vector3(0, 0.3f, 0), new Vector3(0.07f, 0.07f, 0.07f), bulb, false);
        Box("GlassesBridge", head, new Vector3(0, 0.03f, 0.2f), new Vector3(0.05f, 0.012f, 0.01f), metal, false);
        foreach (float sx in new[] { -0.07f, 0.07f })
            Box("GlassesRim", head, new Vector3(sx, 0.025f, 0.2f), new Vector3(0.1f, 0.075f, 0.008f), metal, false);

        Transform rightArm = Arm(upper, "RightArm", new Vector3(0.40f, 1.3f, 0), -120f, white, accent, chalk, true);
        Transform leftArm = Arm(upper, "LeftArm", new Vector3(-0.40f, 1.3f, 0), 8f, white, accent, chalk, false);

        var rt = robot.gameObject.AddComponent<RobotTeacher>();
        rt.upperBody = upper;
        rt.head = head;
        rt.rightArm = rightArm;
        rt.leftArm = leftArm;
        rt.antennaBulb = bulbGo.GetComponent<Renderer>();
    }

    static Transform Arm(Transform parent, string name, Vector3 shoulder, float xRot, Material white, Material accent,
        Material chalk, bool holdChalk)
    {
        var pivot = Group(name, parent);
        pivot.localPosition = shoulder;
        pivot.localRotation = Quaternion.Euler(xRot, 0, 0);
        Sphere("Shoulder", pivot, Vector3.zero, new Vector3(0.15f, 0.15f, 0.15f), accent, false);
        Cyl("UpperArm", pivot, new Vector3(0, -0.22f, 0), new Vector3(0.09f, 0.22f, 0.09f), white, false);
        Sphere("Hand", pivot, new Vector3(0, -0.47f, 0), new Vector3(0.13f, 0.13f, 0.13f), accent, false);
        if (holdChalk)
            Cyl("Chalk", pivot, new Vector3(0, -0.55f, 0), new Vector3(0.02f, 0.045f, 0.02f), chalk, false);
        return pivot;
    }

    // ---------------------------------------------------------------- esfera + lancador

    static AnswerTarget BuildAnswerPrefab()
    {
        var bounce = new PhysicsMaterial("SphereBounce")
        {
            bounciness = 0.4f,
            dynamicFriction = 0.5f,
            staticFriction = 0.5f,
            bounceCombine = PhysicsMaterialCombine.Maximum
        };
        string matPath = MatDir + "/SphereBounce.asset";
        AssetDatabase.DeleteAsset(matPath);
        AssetDatabase.CreateAsset(bounce, matPath);

        var ballM = Mat("AnswerBall", Color.white, 0.1f, 0.85f);

        var go = new GameObject("AnswerTarget");
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.3f;
        rb.useGravity = false;
        rb.angularDamping = 0.05f;
        rb.linearDamping = 0.02f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        var col = go.AddComponent<SphereCollider>();
        col.radius = 0.22f;
        col.sharedMaterial = bounce;

        var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "Visual";
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.transform.SetParent(go.transform, false);
        visual.transform.localScale = Vector3.one * 0.44f;
        visual.GetComponent<Renderer>().sharedMaterial = ballM;

        var label = Text("Label", go.transform, Vector3.zero, Vector3.zero, "0", 0.2f, Color.white, TextAnchor.MiddleCenter);
        label.fontStyle = FontStyle.Bold;

        var target = go.AddComponent<AnswerTarget>();
        var so = new SerializedObject(target);
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("bodyRenderer").objectReferenceValue = visual.GetComponent<Renderer>();
        so.ApplyModifiedPropertiesWithoutUndo();

        string path = PrefabDir + "/AnswerTarget.prefab";
        var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return saved.GetComponent<AnswerTarget>();
    }

    static void BuildSpawner(AnswerTarget prefab, TextMesh questionText, TextMesh scoreText, TextMesh feedbackText)
    {
        var gameplay = new GameObject("Gameplay").transform;

        var scoreGo = new GameObject("ScoreManager");
        scoreGo.transform.SetParent(gameplay, false);
        var score = scoreGo.AddComponent<ScoreManager>();
        score.scoreText = scoreText;

        var sp = new GameObject("AnswerSpawner");
        sp.transform.SetParent(gameplay, false);
        sp.transform.position = new Vector3(0, 0.35f, 1.3f);
        var spawner = sp.AddComponent<AnswerSpawner>();
        spawner.prefab = prefab;
        spawner.questionText = questionText;
        spawner.feedbackText = feedbackText;
        spawner.score = score;

        var bladeGo = new GameObject("Blade");
        bladeGo.transform.SetParent(gameplay, false);
        var blade = bladeGo.AddComponent<Blade>();
        blade.cam = Camera.main;
    }

    // ---------------------------------------------------------------- camera / ambiente

    static void SetupCameraAndAmbient()
    {
        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0, 1.6f, -1.2f);
            cam.transform.rotation = Quaternion.identity;
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.05f;
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.50f, 0.50f, 0.55f);

        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) l.intensity = 0.5f;
    }

    // ---------------------------------------------------------------- helpers

    static Transform Group(string name, Transform parent)
    {
        var g = new GameObject(name).transform;
        g.SetParent(parent, false);
        return g;
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material m,
        bool collider)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 scale, Material m, bool collider = true)
        => Prim(PrimitiveType.Cube, name, parent, pos, scale, m, collider);

    static GameObject Cyl(string name, Transform parent, Vector3 pos, Vector3 scale, Material m, bool collider = true)
        => Prim(PrimitiveType.Cylinder, name, parent, pos, scale, m, collider);

    static GameObject Sphere(string name, Transform parent, Vector3 pos, Vector3 scale, Material m, bool collider = true)
        => Prim(PrimitiveType.Sphere, name, parent, pos, scale, m, collider);

    /// <summary>TextMesh: altura do "em" = fontSize * characterSize * 0.1 (em metros).</summary>
    static TextMesh Text(string name, Transform parent, Vector3 pos, Vector3 euler, string text, float emMeters,
        Color color, TextAnchor anchor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        var tm = go.AddComponent<TextMesh>();
        var mr = go.GetComponent<MeshRenderer>();
        tm.font = font;
        mr.sharedMaterial = font.material;
        tm.text = text;
        tm.fontSize = 100;
        tm.characterSize = emMeters / 10f;
        tm.anchor = anchor;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        return tm;
    }

    static Material Mat(string name, Color color, float metallic, float smooth, Color? emission = null, float emissionIntensity = 1f)
    {
        if (mats.TryGetValue(name, out var cached)) return cached;

        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smooth);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value * emissionIntensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }
        EditorUtility.SetDirty(m);
        mats[name] = m;
        return m;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int i = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
    }
}
