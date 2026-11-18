using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Monta toda a interface por codigo no Awake (uGUI, Screen Space Overlay):
/// tela inicial (operacao, dificuldade, Comecar), HUD (pontos/granadas) e painel de fim.
/// Nao decide nada do fluxo: so mostra telas e dispara eventos para o GameFlow.
/// </summary>
public class GameUI : MonoBehaviour
{
    public event Action<Operation, Difficulty> StartRequested;
    public event Action PlayAgainRequested;

    static readonly Color DimColor = new Color(0f, 0f, 0f, 0.55f);
    static readonly Color BoxColor = new Color(0.07f, 0.10f, 0.17f, 0.94f);
    static readonly Color OptionColor = new Color(0.20f, 0.30f, 0.50f, 1f);
    static readonly Color SelectedColor = new Color(1.00f, 0.62f, 0.15f, 1f);
    static readonly Color StartColor = new Color(0.20f, 0.72f, 0.40f, 1f);
    static readonly Color TitleColor = new Color(1f, 0.85f, 0.35f);
    static readonly Color TextColor = new Color(0.95f, 0.96f, 1f);

    Font font;
    GameObject menuPanel, hudPanel, gameOverPanel;
    Text scoreLabel, grenadeLabel, chanceLabel, gameOverTitle, hitsText, errorsText, grenadesText, finalScoreText;

    readonly Dictionary<Operation, Image> opButtons = new Dictionary<Operation, Image>();
    readonly Dictionary<Difficulty, Image> diffButtons = new Dictionary<Difficulty, Image>();

    public Operation SelectedOperation { get; private set; } = Operation.Multiplication;
    public Difficulty SelectedDifficulty { get; private set; } = Difficulty.Medium;

    void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureEventSystem();
        Transform canvas = BuildCanvas();
        BuildHud(canvas);
        BuildMenu(canvas);
        BuildGameOver(canvas);
        menuPanel.SetActive(false);
        hudPanel.SetActive(false);
        gameOverPanel.SetActive(false);
    }

    // ---------------------------------------------------------------- API

    public void ShowMenu(Operation op, Difficulty diff)
    {
        SelectOperation(op);
        SelectDifficulty(diff);
        menuPanel.SetActive(true);
        hudPanel.SetActive(false);
        gameOverPanel.SetActive(false);
    }

    public void ShowHud()
    {
        menuPanel.SetActive(false);
        gameOverPanel.SetActive(false);
        hudPanel.SetActive(true);
    }

    public void UpdateHud(MatchStats s)
    {
        if (s == null || scoreLabel == null) return;
        scoreLabel.text = "Pontos: " + s.Score + " / " + s.TargetScore;
        grenadeLabel.text = "Granadas: " + s.GrenadesSliced + " / " + s.MaxGrenades;
        if (chanceLabel != null) chanceLabel.text = "Chance: " + s.Chance + " / " + s.MaxChances;
    }

    public void ShowGameOver(bool won, MatchStats s)
    {
        gameOverTitle.text = won ? "Você venceu!" : "Fim de jogo";
        gameOverTitle.color = won ? TitleColor : new Color(1f, 0.45f, 0.4f);
        if (s != null)
        {
            hitsText.text = "Acertos: " + s.Hits;
            errorsText.text = "Erros: " + s.Errors;
            grenadesText.text = "Granadas cortadas: " + s.GrenadesSliced;
            finalScoreText.text = "Pontuação: " + s.Score;
        }
        menuPanel.SetActive(false);
        hudPanel.SetActive(false);
        gameOverPanel.SetActive(true);
    }

    // ---------------------------------------------------------------- selecao

    void SelectOperation(Operation op)
    {
        SelectedOperation = op;
        foreach (var kv in opButtons) kv.Value.color = kv.Key == op ? SelectedColor : OptionColor;
    }

    void SelectDifficulty(Difficulty d)
    {
        SelectedDifficulty = d;
        foreach (var kv in diffButtons) kv.Value.color = kv.Key == d ? SelectedColor : OptionColor;
    }

    static string Label(Operation op)
    {
        switch (op)
        {
            case Operation.Addition: return "Adição";
            case Operation.Subtraction: return "Subtração";
            case Operation.Multiplication: return "Multiplicação";
            case Operation.Division: return "Divisão";
            default: return op.ToString();
        }
    }

    static string Label(Difficulty d)
    {
        switch (d)
        {
            case Difficulty.Easy: return "Fácil";
            case Difficulty.Medium: return "Médio";
            case Difficulty.Hard: return "Difícil";
            default: return d.ToString();
        }
    }

    // ---------------------------------------------------------------- montagem

    static void EnsureEventSystem()
    {
        var es = FindFirstObjectByType<EventSystem>();
        if (es == null) es = new GameObject("EventSystem").AddComponent<EventSystem>();
        if (es.GetComponent<InputSystemUIInputModule>() == null)
        {
            var module = es.gameObject.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }
    }

    Transform BuildCanvas()
    {
        var go = new GameObject("UICanvas", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return go.transform;
    }

    void BuildHud(Transform canvas)
    {
        var hud = NewRect("HUD", canvas);
        Stretch(hud);
        hudPanel = hud.gameObject;

        scoreLabel = HudLabel("ScoreLabel", hud, new Vector2(0f, 1f), new Vector2(40f, -30f), TextAnchor.UpperLeft);
        grenadeLabel = HudLabel("GrenadeLabel", hud, new Vector2(1f, 1f), new Vector2(-40f, -30f), TextAnchor.UpperRight);
        scoreLabel.text = "Pontos: 0 / 20";
        grenadeLabel.text = "Granadas: 0 / 3";

        // linha discreta abaixo dos pontos
        chanceLabel = HudLabel("ChanceLabel", hud, new Vector2(0f, 1f), new Vector2(40f, -105f), TextAnchor.UpperLeft);
        chanceLabel.fontSize = 36;
        chanceLabel.fontStyle = FontStyle.Normal;
        chanceLabel.color = new Color(TextColor.r, TextColor.g, TextColor.b, 0.8f);
        chanceLabel.text = "Chance: 1 / 3";
    }

    Text HudLabel(string name, Transform parent, Vector2 corner, Vector2 offset, TextAnchor anchor)
    {
        var rt = NewRect(name, parent);
        rt.anchorMin = rt.anchorMax = corner;
        rt.pivot = corner;
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(700f, 80f);
        var t = AddText(rt.gameObject, "", 54, TextColor, anchor, FontStyle.Bold);
        var outline = rt.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(3f, -3f);
        return t;
    }

    void BuildMenu(Transform canvas)
    {
        var dim = NewRect("Menu", canvas);
        Stretch(dim);
        dim.gameObject.AddComponent<Image>().color = DimColor;
        menuPanel = dim.gameObject;

        var box = Box("MenuBox", dim, new Vector2(1400f, 900f));

        Line(box, "Title", "MathNinja", 120, TitleColor, 150f, FontStyle.Bold);
        Line(box, "OperationLabel", "Operação", 48, TextColor, 60f, FontStyle.Normal);
        var opRow = Row(box, "OperationRow", 110f);
        foreach (Operation op in (Operation[])Enum.GetValues(typeof(Operation)))
        {
            Operation captured = op;
            opButtons[op] = MakeButton(opRow, Label(op), 290f, 100f, 34,OptionColor, () => SelectOperation(captured));
        }

        Line(box, "DifficultyLabel", "Dificuldade", 48, TextColor, 60f, FontStyle.Normal);
        var diffRow = Row(box, "DifficultyRow", 110f);
        foreach (Difficulty d in (Difficulty[])Enum.GetValues(typeof(Difficulty)))
        {
            Difficulty captured = d;
            diffButtons[d] = MakeButton(diffRow, Label(d), 290f, 100f, 38,OptionColor, () => SelectDifficulty(captured));
        }

        Spacer(box, 20f);
        MakeButton(box, "Começar", 460f, 130f, 60, StartColor,
            () => StartRequested?.Invoke(SelectedOperation, SelectedDifficulty));
    }

    void BuildGameOver(Transform canvas)
    {
        var dim = NewRect("GameOver", canvas);
        Stretch(dim);
        dim.gameObject.AddComponent<Image>().color = DimColor;
        gameOverPanel = dim.gameObject;

        var box = Box("GameOverBox", dim, new Vector2(1100f, 820f));

        gameOverTitle = Line(box, "Title", "Fim de jogo", 100, TitleColor, 150f, FontStyle.Bold);
        MakeButton(box, "Jogar Novamente", 520f, 130f, 54, StartColor, () => PlayAgainRequested?.Invoke());
        Spacer(box, 20f);
        hitsText = Line(box, "Hits", "Acertos: 0", 46, TextColor, 64f, FontStyle.Normal);
        errorsText = Line(box, "Errors", "Erros: 0", 46, TextColor, 64f, FontStyle.Normal);
        grenadesText = Line(box, "Grenades", "Granadas cortadas: 0", 46, TextColor, 64f, FontStyle.Normal);
        finalScoreText = Line(box, "Score", "Pontuação: 0", 52, TitleColor, 70f, FontStyle.Bold);
    }

    // ---------------------------------------------------------------- helpers

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static RectTransform Box(string name, Transform parent, Vector2 size)
    {
        var rt = NewRect(name, parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        rt.gameObject.AddComponent<Image>().color = BoxColor;

        var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(40, 40, 40, 40);
        v.spacing = 22f;
        v.childAlignment = TextAnchor.MiddleCenter;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = false;
        v.childForceExpandHeight = false;
        return rt;
    }

    static RectTransform Row(Transform parent, string name, float height)
    {
        var rt = NewRect(name, parent);
        var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 20f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;
        return rt;
    }

    static void Spacer(Transform parent, float height)
    {
        var rt = NewRect("Spacer", parent);
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;
    }

    Text Line(Transform parent, string name, string text, int size, Color color, float height, FontStyle style)
    {
        var rt = NewRect(name, parent);
        var t = AddText(rt.gameObject, text, size, color, TextAnchor.MiddleCenter, style);
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 1000f;
        le.preferredHeight = height;
        le.minHeight = height;
        return t;
    }

    Image MakeButton(Transform parent, string label, float w, float h, int fontSize, Color color, Action onClick)
    {
        var rt = NewRect("Button_" + label, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;

        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.88f, 0.92f, 1f);
        cb.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        cb.selectedColor = Color.white;
        cb.fadeDuration = 0.08f;
        btn.colors = cb;
        btn.onClick.AddListener(() => onClick());

        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = w;
        le.preferredHeight = h;
        le.minWidth = w;
        le.minHeight = h;

        var textRt = NewRect("Text", rt);
        Stretch(textRt);
        AddText(textRt.gameObject, label, fontSize, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        return img;
    }

    Text AddText(GameObject go, string text, int size, Color color, TextAnchor anchor, FontStyle style)
    {
        var t = go.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }
}
