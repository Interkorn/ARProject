using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Метка на размещённом предмете: нужна, чтобы выбрать его касанием.</summary>
public class PlacedFurniture : MonoBehaviour
{
    public int modelIndex;
    public float baseRadius;
    public float scale = 1f;
}

/// <summary>
/// Практическое задание №1: AR-приложение расстановки мебели (аналог IKEA Place).
/// Каталог 14 моделей → касание пола → размещение; перемещение, масштаб, поворот,
/// удаление, несколько объектов одновременно, скриншот в галерею.
/// </summary>
public class FurnitureApp : MonoBehaviour
{
    const float MinScale = 0.3f, MaxScale = 3f;

    ARRig rig;
    Canvas canvas;
    Text hintT;
    Toast toast;
    GameObject ring;

    readonly List<PlacedFurniture> placed = new();
    PlacedFurniture selected;
    int chosen = -1;
    readonly List<Image> catalogImgs = new();

    bool dragging, uiTouch;
    float prevDist, prevAngle;

    void Start()
    {
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Application.targetFrameRate = 60;
        rig = ARRig.Create();
        Sfx.Init(gameObject);
        BuildUI();

        ring = Models.Part(null, PrimitiveType.Cylinder, Vector3.zero, new Vector3(1, 0.004f, 1), new Color(1f, 0.85f, 0.2f, 0.55f));
        ring.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = new Color(1f, 0.85f, 0.2f, 0.55f) };
        ring.SetActive(false);
    }

    // ------------------------------------------------------------------ UI

    void BuildUI()
    {
        canvas = UIKit.CreateCanvas(out var safe);
        toast = Toast.Create(safe);

        hintT = UIKit.Label(safe, "", 40, TextAnchor.MiddleCenter);
        UIKit.TopStrip(hintT.rectTransform, 20, 20, 150);

        // Каталог моделей (прокручиваемая лента внизу)
        var scroll = UIKit.HScroll(safe, out var content);
        UIKit.BottomStrip(scroll, 0, 10, 270);
        for (int i = 0; i < Models.Catalog.Count; i++)
        {
            int idx = i;
            var def = Models.Catalog[i];
            var b = UIKit.Btn(content, "", 28, def.swatch, () => Choose(idx));
            ((RectTransform)b.transform).sizeDelta = new Vector2(210, 240);
            catalogImgs.Add(b.GetComponent<Image>());

            // «Миниатюра»: тёмная плашка с названием и цветом материала
            var name = UIKit.Label(b.transform, def.name, 30, TextAnchor.LowerCenter, Color.white);
            UIKit.Stretch(name.rectTransform);
            name.rectTransform.offsetMin = new Vector2(4, 6);
            var shade = UIKit.Panel(b.transform, "Shade", new Color(0, 0, 0, 0.35f));
            shade.transform.SetAsFirstSibling();
            shade.GetComponent<Image>().raycastTarget = false;
            var num = UIKit.Label(b.transform, (i + 1).ToString(), 80, TextAnchor.UpperCenter, new Color(1, 1, 1, 0.9f));
            UIKit.Stretch(num.rectTransform);
            num.rectTransform.offsetMax = new Vector2(0, -16);
        }

        // Кнопки управления выбранным объектом (справа)
        var right = new (string, Color, Action)[]
        {
            ("Крупнее", UIKit.Gray, () => ScaleSelected(1.12f)),
            ("Мельче", UIKit.Gray, () => ScaleSelected(1f / 1.12f)),
            ("Повернуть ←", UIKit.Gray, () => RotateSelected(-15f)),
            ("Повернуть →", UIKit.Gray, () => RotateSelected(15f)),
            ("Удалить", UIKit.Red, DeleteSelected),
            ("Очистить всё", UIKit.Red, ClearAll),
            ("Фото", UIKit.Green, TakeShot),
        };
        float y = 330;
        foreach (var (text, col, act) in right)
        {
            var cb = act;
            UIKit.Btn(safe, text, 32, col, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, y), new Vector2(240, 105),
                () => { Sfx.Play(Sfx.Click); cb(); });
            y -= 120;
        }
    }

    void Choose(int i)
    {
        Sfx.Play(Sfx.Click);
        chosen = chosen == i ? -1 : i;
        for (int k = 0; k < catalogImgs.Count; k++)
        {
            var c = Models.Catalog[k].swatch;
            c.a = (chosen == k) ? 1f : 0.78f;
            catalogImgs[k].color = c;
            catalogImgs[k].transform.localScale = Vector3.one * (chosen == k ? 1.07f : 1f);
        }
        if (chosen >= 0) Select(null);
    }

    void UpdateHint()
    {
        string s;
        if (rig.planes.trackables.count == 0)
            s = "Медленно двигайте телефон над полом или столом, чтобы найти поверхность";
        else if (chosen >= 0)
            s = "Коснитесь поверхности, чтобы поставить: " + Models.Catalog[chosen].name.Replace("\n", " ");
        else if (selected != null)
            s = "Тяните — переместить\nЩипок — размер, два пальца — поворот";
        else if (placed.Count == 0)
            s = "Поверхность найдена. Выберите предмет в каталоге внизу";
        else
            s = "Коснитесь предмета, чтобы выбрать его, или выберите новый";
        hintT.text = s;
    }

    // ------------------------------------------------------------- управление

    void Update()
    {
        UpdateHint();
        int n = Input.touchCount;

        if (n >= 2)
        {
            dragging = false;
            if (selected == null) return;
            var a = Input.GetTouch(0);
            var b = Input.GetTouch(1);
            Vector2 d = b.position - a.position;
            float dist = d.magnitude;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            if (a.phase == TouchPhase.Began || b.phase == TouchPhase.Began)
            {
                prevDist = dist;
                prevAngle = ang;
                return;
            }
            if (prevDist > 1f) SetScale(selected, selected.scale * dist / prevDist);
            selected.transform.Rotate(0, -Mathf.DeltaAngle(prevAngle, ang), 0, Space.World);
            prevDist = dist;
            prevAngle = ang;
            return;
        }

        if (n == 1)
        {
            var t = Input.GetTouch(0);
            switch (t.phase)
            {
                case TouchPhase.Began:
                    uiTouch = UIKit.OverUI(t.fingerId);
                    dragging = false;
                    if (!uiTouch) TapDown(t.position);
                    break;
                case TouchPhase.Moved:
                    if (!uiTouch && dragging && selected != null && rig.RaycastFloor(t.position, out var p))
                        selected.transform.position = p.position;
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    dragging = false;
                    break;
            }
        }
    }

    void TapDown(Vector2 screenPos)
    {
        // 1) попали по уже размещённому предмету — выбираем его
        var ray = rig.cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out var hit, 30f))
        {
            var pf = hit.collider.GetComponentInParent<PlacedFurniture>();
            if (pf != null && chosen < 0)
            {
                Select(pf);
                dragging = true;
                return;
            }
        }
        // 2) иначе, если в каталоге выбрана модель — ставим на поверхность
        if (chosen >= 0 && rig.RaycastFloor(screenPos, out var pose))
        {
            Place(chosen, pose);
            Choose(chosen); // снять выбор в каталоге
        }
        else if (chosen < 0)
        {
            Select(null);
        }
    }

    void Place(int index, Pose pose)
    {
        var model = Models.Build(index, out float radius);
        var toCam = rig.cam.transform.position - pose.position;
        toCam.y = 0;
        var rot = toCam.sqrMagnitude > 0.001f ? Quaternion.LookRotation(toCam.normalized) : Quaternion.identity;

        var anchor = rig.CreateAnchor(pose.position, Quaternion.identity);
        model.transform.SetParent(anchor.transform, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.rotation = rot;

        var pf = model.AddComponent<PlacedFurniture>();
        pf.modelIndex = index;
        pf.baseRadius = radius;
        placed.Add(pf);
        Select(pf);
        Sfx.Play(Sfx.Place);
        toast.Show(Models.Catalog[index].name.Replace("\n", " ") + " добавлен(а)");
    }

    void Select(PlacedFurniture pf)
    {
        selected = pf;
        if (pf == null)
        {
            ring.transform.SetParent(null);
            ring.SetActive(false);
            return;
        }
        ring.transform.SetParent(pf.transform, false);
        ring.transform.localPosition = new Vector3(0, 0.004f, 0);
        ring.transform.localRotation = Quaternion.identity;
        float d = pf.baseRadius * 2.3f;
        ring.transform.localScale = new Vector3(d, 0.002f, d);
        ring.SetActive(true);
    }

    void SetScale(PlacedFurniture pf, float s)
    {
        pf.scale = Mathf.Clamp(s, MinScale, MaxScale);
        pf.transform.localScale = Vector3.one * pf.scale;
    }

    void ScaleSelected(float k)
    {
        if (selected == null) { toast.Show("Сначала выберите предмет"); return; }
        SetScale(selected, selected.scale * k);
    }

    void RotateSelected(float deg)
    {
        if (selected == null) { toast.Show("Сначала выберите предмет"); return; }
        selected.transform.Rotate(0, deg, 0, Space.World);
    }

    void DeleteSelected()
    {
        if (selected == null) { toast.Show("Сначала выберите предмет"); return; }
        var pf = selected;
        Select(null);
        placed.Remove(pf);
        Destroy(pf.transform.parent != null ? pf.transform.parent.gameObject : pf.gameObject);
        toast.Show("Удалено");
    }

    void ClearAll()
    {
        Select(null);
        foreach (var pf in placed)
            if (pf != null) Destroy(pf.transform.parent != null ? pf.transform.parent.gameObject : pf.gameObject);
        placed.Clear();
        toast.Show("Сцена очищена");
    }

    // ------------------------------------------------------------- скриншот

    void TakeShot() => StartCoroutine(ShotRoutine());

    IEnumerator ShotRoutine()
    {
        // Прячем кольцо выбора и интерфейс на один кадр.
        bool ringWas = ring.activeSelf;
        ring.SetActive(false);
        canvas.enabled = false;
        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        canvas.enabled = true;
        ring.SetActive(ringWas);

        byte[] png = tex.EncodeToPNG();
        Destroy(tex);
        string file = "AR_Mebel_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
        string where = Gallery.Save(png, file);
        Sfx.Play(Sfx.Place);
        toast.Show("Снимок сохранён в " + where, 2.5f);
    }
}
