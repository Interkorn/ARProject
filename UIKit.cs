using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Мини-библиотека для построения интерфейса (uGUI) кодом.</summary>
public static class UIKit
{
    public static readonly Color Accent = new Color(0.13f, 0.55f, 0.95f, 0.95f);
    public static readonly Color Green = new Color(0.18f, 0.7f, 0.35f, 0.95f);
    public static readonly Color Red = new Color(0.85f, 0.25f, 0.25f, 0.95f);
    public static readonly Color Gray = new Color(0.2f, 0.22f, 0.27f, 0.9f);
    public static readonly Color Dim = new Color(0f, 0f, 0f, 0.62f);

    static Font font;
    public static Font F
    {
        get
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }
    }

    public static Canvas CreateCanvas(out RectTransform safeRoot)
    {
        var go = new GameObject("Canvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();

        if (Object.FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var safe = new GameObject("SafeArea", typeof(RectTransform));
        safe.transform.SetParent(go.transform, false);
        safeRoot = (RectTransform)safe.transform;
        var sa = Screen.safeArea;
        safeRoot.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
        safeRoot.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
        safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
        return canvas;
    }

    public static bool OverUI(int fingerId)
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(fingerId);
    }

    public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static RectTransform Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    /// <summary>Полоса во всю ширину у нижнего края.</summary>
    public static RectTransform BottomStrip(RectTransform rt, float side, float bottom, float height)
    {
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.offsetMin = new Vector2(side, bottom);
        rt.offsetMax = new Vector2(-side, bottom + height);
        return rt;
    }

    /// <summary>Полоса во всю ширину у верхнего края.</summary>
    public static RectTransform TopStrip(RectTransform rt, float side, float top, float height)
    {
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.offsetMin = new Vector2(side, -top - height);
        rt.offsetMax = new Vector2(-side, -top);
        return rt;
    }

    public static GameObject Panel(Transform parent, string name, Color? color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform);
        if (color.HasValue)
        {
            var img = go.AddComponent<Image>();
            img.color = color.Value;
        }
        return go;
    }

    public static Text Label(Transform parent, string text, int size, TextAnchor align = TextAnchor.MiddleCenter, Color? color = null)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = F;
        t.fontSize = size;
        t.text = text;
        t.alignment = align;
        t.color = color ?? Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var sh = go.AddComponent<Shadow>();
        sh.effectColor = new Color(0, 0, 0, 0.8f);
        sh.effectDistance = new Vector2(2, -2);
        return t;
    }

    public static Text Label(Transform parent, string text, int size, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 sz,
        TextAnchor align = TextAnchor.MiddleCenter, Color? color = null)
    {
        var t = Label(parent, text, size, align, color);
        Place(t.rectTransform, anchor, pivot, pos, sz);
        return t;
    }

    public static Button Btn(Transform parent, string text, int size, Color color, UnityAction onClick)
    {
        var go = new GameObject("Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        var b = go.AddComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(onClick);
        var l = Label(go.transform, text, size);
        Stretch(l.rectTransform);
        return b;
    }

    public static Button Btn(Transform parent, string text, int size, Color color, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 sz, UnityAction onClick)
    {
        var b = Btn(parent, text, size, color, onClick);
        Place((RectTransform)b.transform, anchor, pivot, pos, sz);
        return b;
    }

    /// <summary>Горизонтальная прокручиваемая лента (каталог). Возвращает content для добавления кнопок.</summary>
    public static RectTransform HScroll(Transform parent, out RectTransform content)
    {
        var view = new GameObject("Scroll", typeof(RectTransform));
        view.transform.SetParent(parent, false);
        var bg = view.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.45f);
        view.AddComponent<RectMask2D>();
        var scroll = view.AddComponent<ScrollRect>();

        var c = new GameObject("Content", typeof(RectTransform));
        c.transform.SetParent(view.transform, false);
        content = (RectTransform)c.transform;
        content.anchorMin = new Vector2(0, 0);
        content.anchorMax = new Vector2(0, 1);
        content.pivot = new Vector2(0, 0.5f);
        content.offsetMin = content.offsetMax = Vector2.zero;
        var hl = c.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 14;
        hl.padding = new RectOffset(14, 14, 12, 12);
        hl.childControlWidth = false;
        hl.childControlHeight = false;
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = false;
        hl.childAlignment = TextAnchor.MiddleLeft;
        var fit = c.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = (RectTransform)view.transform;
        scroll.content = content;
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        return (RectTransform)view.transform;
    }
}

/// <summary>Всплывающее сообщение, плавно исчезающее.</summary>
public class Toast : MonoBehaviour
{
    Text text;
    float until;

    public static Toast Create(Transform parent)
    {
        var t = UIKit.Label(parent, "", 44, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880, 140));
        var toast = t.gameObject.AddComponent<Toast>();
        toast.text = t;
        return toast;
    }

    public void Show(string msg, float seconds = 1.6f)
    {
        text.text = msg;
        until = Time.unscaledTime + seconds;
        text.color = Color.white;
    }

    void Update()
    {
        float left = until - Time.unscaledTime;
        var c = text.color;
        c.a = Mathf.Clamp01(left / 0.4f);
        text.color = c;
    }
}
