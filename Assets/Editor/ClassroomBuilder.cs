using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Monta a cena Game do zero: sala de aula 3D, professor robo, quadro, prefabs de esfera e
/// granada, gameplay (pontuacao, lancador, lamina, fluxo) e UI.
/// Menu: MathNinja/Build Classroom. Batch: -executeMethod ClassroomBuilder.BuildFromCommandLine
///
/// DESATIVADO (2026-11-21): a sala antiga foi substituida pelo ambiente modelado no Blender
/// (Assets/Art/Ambiente/MathNinja-Ambiente.fbx). NAO rode este builder: ele recria Game.unity do zero
/// com a sala antiga e apagaria o ambiente novo. O item de menu foi removido e Build/BuildFromCommandLine
/// so avisam e saem, a menos que LegacyBuilderEnabled seja ligado de proposito.
/// </summary>
public static class ClassroomBuilder
{
    // ---------------------------------------------------------------- dimensoes (tudo deriva daqui)
    const float RoomW = 16f;    // largura (x)
    const float RoomD = 14f;    // profundidade (z); quadro na parede +z
    const float RoomH = 4.2f;   // altura (y)
    const float WallT = 0.2f;   // espessura das paredes

    const float AisleW = 4.4f;          // corredor central (tapete) onde as esferas voam
    const int DeskColsPerSide = 3;
    const int DeskRows = 5;
    const float DeskColSpacing = 1.4f;
    const float DeskRowSpacing = 1.6f;
    const float DeskFrontGap = 3.8f;    // distancia da parede do quadro ate a 1a fileira

    const float BoardW = 6.0f;
    const float BoardH = 2.0f;
    const float BoardCenterY = 2.15f;

    const int WindowCount = 4;
    const int PosterCount = 4;
    const int LightCols = 3;
    const int LightRows = 3;

    const float CamY = 1.75f;
    const float CamZ = -0.5f;
    const float SpawnDistance = 2.6f;   // distancia camera -> linha de lancamento
    const float SpawnY = 0.35f;

    const string MatDir = "Assets/Materials";
    const string PrefabDir = "Assets/Prefabs";
    const string ScenePath = "Assets/Scenes/Game.unity";

    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static Font font;

    /// <summary>Ligue so se quiser MESMO recriar a sala antiga (substitui a Game.unity inteira).</summary>
    static readonly bool LegacyBuilderEnabled = false;

    // [MenuItem("MathNinja/Build Classroom")]  // desativado: ver comentario da classe
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildScene();
    }

    /// <summary>Para batch mode: reconstroi e salva Game.unity e encerra o Editor (0 = ok, 1 = erro).</summary>
    public static void BuildFromCommandLine()
    {
        int code = 0;
        try
        {
            BuildScene();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            code = 1;
        }
        if (Application.isBatchMode) EditorApplication.Exit(code);
    }

    static void BuildScene()
    {
        if (!LegacyBuilderEnabled)
        {
            Debug.LogWarning("[MathNinja] ClassroomBuilder desativado: Game.unity usa o ambiente do Blender (Assets/Art/Ambiente). Nada foi alterado.");
            return;
        }
        mats.Clear();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureFolder(MatDir);
        EnsureFolder(PrefabDir);
        EnsureFolder("Assets/Scenes");

        // cena nova e vazia: nada residual (tester, objetos vazios) e recriado
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var root = new GameObject("Classroom").transform;
        var structure = Group("Structure", root);
        var furniture = Group("Furniture", root);
        var decor = Group("Decor", root);
        var lights = Group("Lights", root);

        BuildStructure(structure);
        BuildBoard(decor, out TextMesh questionText, out TextMesh feedbackText);
        BuildFurniture(furniture);
        BuildDecor(decor);
        BuildLights(lights);
        BuildRobot(root);

        Camera cam = BuildCameraAndAmbient();

        var bounce = BuildBounceMaterial();
        AnswerTarget answerPrefab = BuildAnswerPrefab(bounce);
        AnswerTarget grenadePrefab = BuildGrenadePrefab(bounce);
        BuildGameplay(answerPrefab, grenadePrefab, questionText, feedbackText, cam);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new Exception("Falha ao salvar " + ScenePath);
        AssetDatabase.SaveAssets();
        EnsureSceneInBuild();
        Debug.Log("[MathNinja] Sala de aula construida e salva em " + ScenePath);
    }

    static void EnsureSceneInBuild()
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        int idx = list.FindIndex(s => s.path == ScenePath);
        if (idx == 0 && list[0].enabled) return;
        if (idx >= 0) list.RemoveAt(idx);
        list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
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
        float outerW = RoomW + 2f * WallT, outerD = RoomD + 2f * WallT;

        Box("Floor", p, new Vector3(0, -0.05f, 0), new Vector3(outerW, 0.1f, outerD), floorM);
        Box("Carpet", p, new Vector3(0, 0.006f, 0), new Vector3(AisleW, 0.012f, RoomD - 0.4f), carpetM, false);
        Box("Ceiling", p, new Vector3(0, RoomH + 0.05f, 0), new Vector3(outerW, 0.1f, outerD), ceilM);

        Box("Wall_Front", p, new Vector3(0, RoomH / 2f, hd + WallT / 2f), new Vector3(outerW, RoomH, WallT), wallM);
        Box("Wall_Back", p, new Vector3(0, RoomH / 2f, -hd - WallT / 2f), new Vector3(outerW, RoomH, WallT), wallM);
        Box("Wall_Left", p, new Vector3(-hw - WallT / 2f, RoomH / 2f, 0), new Vector3(WallT, RoomH, outerD), wallM);
        Box("Wall_Right", p, new Vector3(hw + WallT / 2f, RoomH / 2f, 0), new Vector3(WallT, RoomH, outerD), wallM);

        float bandH = 1.0f;
        Box("Band_Front", p, new Vector3(0, bandH / 2f, hd - 0.02f), new Vector3(RoomW, bandH, 0.04f), bandM, false);
        Box("Band_Back", p, new Vector3(0, bandH / 2f, -hd + 0.02f), new Vector3(RoomW, bandH, 0.04f), bandM, false);
        Box("Band_Left", p, new Vector3(-hw + 0.02f, bandH / 2f, 0), new Vector3(0.04f, bandH, RoomD), bandM, false);
        Box("Band_Right", p, new Vector3(hw - 0.02f, bandH / 2f, 0), new Vector3(0.04f, bandH, RoomD), bandM, false);
    }

    // ---------------------------------------------------------------- quadro

    static void BuildBoard(Transform p, out TextMesh questionText, out TextMesh feedbackText)
    {
        var frameM = Mat("Board_Frame", new Color(0.42f, 0.27f, 0.14f), 0f, 0.3f);
        var boardM = Mat("Board_Green", new Color(0.09f, 0.24f, 0.19f), 0f, 0.05f);
        var chalkM = Mat("Chalk_White", new Color(0.95f, 0.95f, 0.92f), 0f, 0.05f);

        float z = RoomD / 2f;
        float bottom = BoardCenterY - BoardH / 2f, top = BoardCenterY + BoardH / 2f;
        float textX = 0.5f; // levemente a direita: o robo fica a esquerda

        var board = Group("Blackboard", p);
        Box("Frame", board, new Vector3(0, BoardCenterY, z - 0.04f), new Vector3(BoardW + 0.2f, BoardH + 0.2f, 0.08f), frameM);
        Box("Surface", board, new Vector3(0, BoardCenterY, z - 0.085f), new Vector3(BoardW, BoardH, 0.02f), boardM, false);
        Box("ChalkTray", board, new Vector3(0, bottom - 0.12f, z - 0.1f), new Vector3(BoardW, 0.05f, 0.12f), frameM, false);
        Box("Chalk_A", board, new Vector3(-BoardW * 0.25f, bottom - 0.08f, z - 0.1f), new Vector3(0.1f, 0.02f, 0.02f), chalkM, false);
        Box("Chalk_B", board, new Vector3(-BoardW * 0.25f + 0.2f, bottom - 0.08f, z - 0.1f), new Vector3(0.08f, 0.02f, 0.02f), chalkM, false);

        Text("BoardTitle", board, new Vector3(textX, top - 0.28f, z - 0.1f), Vector3.zero, "Math Ninja", 0.22f,
            new Color(0.9f, 0.95f, 0.8f), TextAnchor.MiddleCenter);
        questionText = Text("QuestionText", board, new Vector3(textX, BoardCenterY - 0.05f, z - 0.1f), Vector3.zero, "", 0.65f,
            Color.white, TextAnchor.MiddleCenter);
        feedbackText = Text("FeedbackText", board, new Vector3(textX, bottom + 0.3f, z - 0.1f), Vector3.zero, "", 0.22f,
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

        // colunas dos dois lados do corredor; fileiras a partir da frente (quadro) para tras
        float firstX = AisleW / 2f + 0.7f;
        float frontZ = RoomD / 2f - DeskFrontGap;
        int idx = 0;
        for (int side = -1; side <= 1; side += 2)
            for (int c = 0; c < DeskColsPerSide; c++)
                for (int r = 0; r < DeskRows; r++)
                {
                    float x = side * (firstX + c * DeskColSpacing);
                    float z = frontZ - r * DeskRowSpacing;
                    Desk(p, "StudentDesk_" + idx, new Vector3(x, 0, z), 0f, woodM, metalM, chairColors[idx % 2], true);
                    idx++;
                }

        Desk(p, "TeacherDesk", new Vector3(BoardW / 2f + 1.0f, 0, RoomD / 2f - 1.2f), 180f, woodM, metalM,
            chairColors[0], false, 1.8f);
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
        float wallMidY = RoomH * 0.48f;

        // janelas: parede esquerda, distribuidas pela profundidade
        float winH = Mathf.Min(1.7f, RoomH * 0.4f), winW = Mathf.Min(2.0f, RoomD / WindowCount * 0.6f);
        for (int i = 0; i < WindowCount; i++)
        {
            float z = -hd + RoomD * (i + 0.5f) / WindowCount;
            var w = Group("Window_" + i, p);
            w.localPosition = new Vector3(-hw + 0.04f, wallMidY, z);
            Box("Frame", w, Vector3.zero, new Vector3(0.06f, winH, winW), frameM, false);
            Box("Glass", w, new Vector3(0.02f, 0, 0), new Vector3(0.04f, winH - 0.14f, winW - 0.14f), glassM, false);
            Box("BarV", w, new Vector3(0.04f, 0, 0), new Vector3(0.04f, winH - 0.1f, 0.05f), frameM, false);
            Box("BarH", w, new Vector3(0.04f, 0, 0), new Vector3(0.04f, 0.05f, winW - 0.1f), frameM, false);
            Box("Sill", w, new Vector3(0.08f, -winH / 2f - 0.03f, 0), new Vector3(0.2f, 0.05f, winW + 0.15f), frameM, false);
        }

        // porta: parede do fundo, perto do canto direito
        var door = Group("Door", p);
        door.localPosition = new Vector3(hw - 1.5f, 1.05f, -hd + 0.05f);
        Box("Leaf", door, Vector3.zero, new Vector3(1.0f, 2.1f, 0.08f), doorM, false);
        Box("Handle", door, new Vector3(-0.38f, -0.05f, 0.07f), new Vector3(0.05f, 0.05f, 0.1f), darkM, false);
        Box("Window", door, new Vector3(0, 0.5f, 0.045f), new Vector3(0.45f, 0.5f, 0.02f), glassM, false);

        // relogio: parede do quadro, acima e a direita do quadro (olha para -z)
        var clock = Group("Clock", p);
        clock.localPosition = new Vector3(BoardW / 2f + 1.6f, Mathf.Min(RoomH - 0.6f, BoardCenterY + BoardH / 2f + 0.3f), hd - 0.05f);
        var face = Cyl("Face", clock, Vector3.zero, new Vector3(0.5f, 0.02f, 0.5f), whiteM, false);
        face.transform.localRotation = Quaternion.Euler(90, 0, 0);
        Box("HandH", clock, new Vector3(0, 0.06f, -0.025f), new Vector3(0.02f, 0.14f, 0.01f), darkM, false);
        Box("HandM", clock, new Vector3(0.09f, 0, -0.03f), new Vector3(0.2f, 0.015f, 0.01f), darkM, false);

        // posteres: parede direita, distribuidos pela profundidade
        string[] symbols = { "+", "-", "×", "÷" };
        Color[] cols =
        {
            new Color(0.95f, 0.45f, 0.35f), new Color(0.30f, 0.60f, 0.95f),
            new Color(0.35f, 0.80f, 0.50f), new Color(0.95f, 0.75f, 0.25f)
        };
        for (int i = 0; i < PosterCount; i++)
        {
            var m = Mat("Poster_" + (i % 4), cols[i % 4], 0f, 0.2f);
            float z = -hd + RoomD * (i + 0.5f) / PosterCount;
            Box("Poster_" + symbols[i % 4], p, new Vector3(hw - 0.03f, wallMidY, z), new Vector3(0.04f, 1.2f, 1.2f), m, false);
            Text("PosterSymbol", p, new Vector3(hw - 0.06f, wallMidY, z), new Vector3(0, -90, 0), symbols[i % 4], 1.0f,
                Color.white, TextAnchor.MiddleCenter);
        }
    }

    // ---------------------------------------------------------------- luzes

    static void BuildLights(Transform p)
    {
        var panelM = Mat("Ceiling_Light", Color.white, 0f, 0.2f, new Color(1f, 0.96f, 0.85f), 2.2f);
        float cellW = RoomW / LightCols, cellD = RoomD / LightRows;
        float range = Mathf.Max(cellW, cellD) * 1.5f;
        int i = 0;
        for (int c = 0; c < LightCols; c++)
            for (int r = 0; r < LightRows; r++)
            {
                var v = new Vector3(-RoomW / 2f + cellW * (c + 0.5f), RoomH - 0.02f, -RoomD / 2f + cellD * (r + 0.5f));
                Box("LightPanel_" + i, p, v, new Vector3(1.4f, 0.04f, 0.5f), panelM, false);
                var l = new GameObject("PointLight_" + i).AddComponent<Light>();
                l.transform.SetParent(p, false);
                l.transform.localPosition = v + Vector3.down * 0.4f;
                l.type = LightType.Point;
                l.color = new Color(1f, 0.95f, 0.85f);
                l.intensity = 6f;
                l.range = range;
                l.shadows = LightShadows.None;
                i++;
            }

        var boardLight = new GameObject("BoardLight").AddComponent<Light>();
        boardLight.transform.SetParent(p, false);
        boardLight.transform.localPosition = new Vector3(0.4f, RoomH - 0.3f, RoomD / 2f - 1.8f);
        boardLight.type = LightType.Point;
        boardLight.color = new Color(1f, 0.97f, 0.9f);
        boardLight.intensity = 6f;
        boardLight.range = 7f;
        boardLight.shadows = LightShadows.None;
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
        robot.localPosition = new Vector3(-BoardW / 2f + 0.8f, 0, RoomD / 2f - 0.6f); // de frente para o quadro (+z)
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

    // ---------------------------------------------------------------- alvos (prefabs)

    static PhysicsMaterial BuildBounceMaterial()
    {
        string path = MatDir + "/SphereBounce.asset";
        var bounce = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (bounce == null)
        {
            bounce = new PhysicsMaterial("SphereBounce");
            AssetDatabase.CreateAsset(bounce, path);
        }
        bounce.bounciness = 0.4f;
        bounce.dynamicFriction = 0.5f;
        bounce.staticFriction = 0.5f;
        bounce.bounceCombine = PhysicsMaterialCombine.Maximum;
        EditorUtility.SetDirty(bounce);
        return bounce;
    }

    /// <summary>Raiz comum dos alvos: Rigidbody sem gravidade (o script aplica) + SphereCollider.</summary>
    static GameObject TargetRoot(string name, PhysicsMaterial bounce)
    {
        var go = new GameObject(name);
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
        return go;
    }

    static AnswerTarget SavePrefab(GameObject go, string fileName)
    {
        var saved = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/" + fileName);
        Object.DestroyImmediate(go);
        if (saved == null) throw new Exception("Falha ao salvar prefab " + fileName);
        return saved.GetComponent<AnswerTarget>();
    }

    static AnswerTarget BuildAnswerPrefab(PhysicsMaterial bounce)
    {
        var ballM = Mat("AnswerBall", Color.white, 0.1f, 0.85f);
        var go = TargetRoot("AnswerTarget", bounce);

        var visual = Sphere("Visual", go.transform, Vector3.zero, Vector3.one * 0.44f, ballM, false);

        var label = Text("Label", go.transform, Vector3.zero, Vector3.zero, "0", 0.2f, Color.white, TextAnchor.MiddleCenter);
        label.fontStyle = FontStyle.Bold;

        var target = go.AddComponent<AnswerTarget>();
        var so = new SerializedObject(target);
        so.FindProperty("kind").enumValueIndex = (int)TargetKind.Answer;
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("bodyRenderer").objectReferenceValue = visual.GetComponent<Renderer>();
        so.ApplyModifiedPropertiesWithoutUndo();

        return SavePrefab(go, "AnswerTarget.prefab");
    }

    static AnswerTarget BuildGrenadePrefab(PhysicsMaterial bounce)
    {
        var bodyM = Mat("Grenade_Body", new Color(0.13f, 0.14f, 0.15f), 0.6f, 0.45f);
        var metalM = Mat("Grenade_Metal", new Color(0.45f, 0.47f, 0.50f), 0.9f, 0.6f);
        var redM = Mat("Grenade_Red", new Color(0.9f, 0.08f, 0.05f), 0f, 0.6f, new Color(1f, 0.1f, 0.05f), 3f);
        var fxM = ParticleMat("Explosion_Particle", new Color(1f, 0.6f, 0.2f));

        var go = TargetRoot("Grenade", bounce);
        var t = go.transform;

        var body = Sphere("Visual", t, Vector3.zero, Vector3.one * 0.44f, bodyM, false);
        // faixa vermelha emissiva (disco fino que sobressai da esfera = anel)
        Cyl("RedBand", t, Vector3.zero, new Vector3(0.452f, 0.012f, 0.452f), redM, false);
        // tampa, pino e pavio no topo
        Cyl("Cap", t, new Vector3(0, 0.215f, 0), new Vector3(0.12f, 0.035f, 0.12f), metalM, false);
        var lever = Box("Lever", t, new Vector3(0.05f, 0.2f, 0), new Vector3(0.025f, 0.16f, 0.05f), metalM, false);
        lever.transform.localRotation = Quaternion.Euler(0, 0, -20f);
        var ring = Cyl("PinRing", t, new Vector3(-0.09f, 0.25f, 0), new Vector3(0.08f, 0.008f, 0.08f), metalM, false);
        ring.transform.localRotation = Quaternion.Euler(0, 0, 90f);
        Cyl("Fuse", t, new Vector3(0, 0.29f, 0), new Vector3(0.018f, 0.05f, 0.018f), bodyM, false);
        Sphere("FuseTip", t, new Vector3(0, 0.345f, 0), Vector3.one * 0.045f, redM, false);

        var target = go.AddComponent<AnswerTarget>();
        var so = new SerializedObject(target);
        so.FindProperty("kind").enumValueIndex = (int)TargetKind.Grenade;
        so.FindProperty("label").objectReferenceValue = null;
        so.FindProperty("bodyRenderer").objectReferenceValue = body.GetComponent<Renderer>();
        so.FindProperty("explosionMaterial").objectReferenceValue = fxM;
        so.ApplyModifiedPropertiesWithoutUndo();

        return SavePrefab(go, "Grenade.prefab");
    }

    // ---------------------------------------------------------------- gameplay + UI

    static void BuildGameplay(AnswerTarget answerPrefab, AnswerTarget grenadePrefab, TextMesh questionText,
        TextMesh feedbackText, Camera cam)
    {
        var gameplay = new GameObject("Gameplay").transform;

        var score = new GameObject("ScoreManager").AddComponent<ScoreManager>();
        score.transform.SetParent(gameplay, false);

        var spawner = new GameObject("AnswerSpawner").AddComponent<AnswerSpawner>();
        spawner.transform.SetParent(gameplay, false);
        // na frente da camera, no centro do corredor; apice abaixo do teto e dentro do campo de visao
        spawner.transform.position = new Vector3(0, SpawnY, CamZ + SpawnDistance);
        spawner.prefab = answerPrefab;
        spawner.grenadePrefab = grenadePrefab;
        spawner.questionText = questionText;
        spawner.feedbackText = feedbackText;
        spawner.score = score;
        spawner.apexHeight = Mathf.Min(2.6f, RoomH - SpawnY - 0.9f);
        spawner.spread = Mathf.Min(3f, AisleW - 0.8f);

        var blade = new GameObject("Blade").AddComponent<Blade>();
        blade.transform.SetParent(gameplay, false);
        blade.cam = cam;
        blade.enabled = false; // o GameFlow liga ao comecar

        var ui = new GameObject("GameUI").AddComponent<GameUI>();

        var flow = new GameObject("GameFlow").AddComponent<GameFlow>();
        flow.transform.SetParent(gameplay, false);
        flow.ui = ui;
        flow.spawner = spawner;
        flow.blade = blade;
        flow.score = score;
    }

    // ---------------------------------------------------------------- camera / ambiente

    static Camera BuildCameraAndAmbient()
    {
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<UniversalAdditionalCameraData>();
        cam.transform.position = new Vector3(0, CamY, CamZ);
        cam.transform.rotation = Quaternion.identity;
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 100f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.1f, 0.1f, 0.12f);

        var sun = new GameObject("Directional Light").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        sun.intensity = 0.35f;
        sun.shadows = LightShadows.None;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.52f, 0.52f, 0.57f);
        return cam;
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

    /// <summary>Material simples (opaco, sem textura) para as particulas da explosao.</summary>
    static Material ParticleMat(string name, Color color)
    {
        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
            m = new Material(s);
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int i = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
    }
}
