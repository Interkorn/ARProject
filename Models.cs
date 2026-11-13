using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Процедурные 3D-модели (собираются из примитивов в реальных размерах, метры).
/// Начало координат модели — центр основания на полу, «лицо» смотрит в +Z.
/// </summary>
public static class Models
{
    public class Def
    {
        public string name;
        public Color swatch;
        public Func<GameObject> build;
    }

    static readonly Dictionary<Color, Material> mats = new();

    public static Color C(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);

    public static Material Mat(Color c)
    {
        if (mats.TryGetValue(c, out var m) && m != null) return m;
        m = new Material(Shader.Find("Standard")) { color = c };
        m.SetFloat("_Glossiness", 0.25f);
        mats[c] = m;
        return m;
    }

    /// <summary>
    /// Добавляет примитив. size: для куба/шара — размеры, для цилиндра — (диаметр, высота, диаметр).
    /// </summary>
    public static GameObject Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 size, Color c, Vector3 euler = default)
    {
        var g = GameObject.CreatePrimitive(type);
        var col = g.GetComponent<Collider>();
        if (col != null) UnityEngine.Object.DestroyImmediate(col);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localEulerAngles = euler;
        g.transform.localScale = type == PrimitiveType.Cylinder ? new Vector3(size.x, size.y * 0.5f, size.z) : size;
        g.GetComponent<Renderer>().sharedMaterial = Mat(c);
        return g;
    }

    static GameObject Box(Transform p, float x, float y, float z, float w, float h, float d, Color c) =>
        Part(p, PrimitiveType.Cube, new Vector3(x, y, z), new Vector3(w, h, d), c);

    static GameObject Cyl(Transform p, float x, float y, float z, float dia, float h, Color c) =>
        Part(p, PrimitiveType.Cylinder, new Vector3(x, y, z), new Vector3(dia, h, dia), c);

    static void Legs4(Transform p, float hx, float hz, float h, float dia, Color c)
    {
        foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
                Cyl(p, sx * hx, h / 2f, sz * hz, dia, h, c);
    }

    static Transform Root(string name) => new GameObject(name).transform;

    // ---------------------------------------------------------------- модели

    static GameObject Sofa()
    {
        var r = Root("Диван");
        var fabric = C(70, 110, 160); var dark = C(50, 80, 120); var wood = C(60, 40, 30);
        Legs4(r, 0.85f, 0.35f, 0.1f, 0.06f, wood);
        Box(r, 0, 0.24f, 0, 1.9f, 0.28f, 0.85f, dark);
        Box(r, -0.45f, 0.45f, 0.08f, 0.88f, 0.14f, 0.68f, fabric);
        Box(r, 0.45f, 0.45f, 0.08f, 0.88f, 0.14f, 0.68f, fabric);
        Box(r, 0, 0.65f, -0.34f, 1.9f, 0.55f, 0.18f, fabric);
        Box(r, -0.93f, 0.42f, 0, 0.16f, 0.46f, 0.85f, dark);
        Box(r, 0.93f, 0.42f, 0, 0.16f, 0.46f, 0.85f, dark);
        return r.gameObject;
    }

    static GameObject Armchair()
    {
        var r = Root("Кресло");
        var fabric = C(190, 110, 70); var dark = C(150, 80, 50); var wood = C(60, 40, 30);
        Legs4(r, 0.35f, 0.3f, 0.1f, 0.05f, wood);
        Box(r, 0, 0.24f, 0, 0.8f, 0.26f, 0.8f, dark);
        Box(r, 0, 0.42f, 0.06f, 0.55f, 0.12f, 0.62f, fabric);
        Box(r, 0, 0.62f, -0.33f, 0.8f, 0.5f, 0.15f, fabric);
        Box(r, -0.37f, 0.42f, 0, 0.12f, 0.35f, 0.8f, dark);
        Box(r, 0.37f, 0.42f, 0, 0.12f, 0.35f, 0.8f, dark);
        return r.gameObject;
    }

    static GameObject CoffeeTable()
    {
        var r = Root("Журнальный столик");
        var wood = C(150, 105, 65);
        Box(r, 0, 0.4f, 0, 1.0f, 0.05f, 0.6f, wood);
        Box(r, 0, 0.15f, 0, 0.8f, 0.03f, 0.4f, C(120, 80, 50));
        Legs4(r, 0.42f, 0.24f, 0.38f, 0.05f, C(90, 60, 40));
        Cyl(r, 0.1f, 0.44f, 0, 0.2f, 0.04f, C(230, 230, 235));
        return r.gameObject;
    }

    static GameObject DiningTable()
    {
        var r = Root("Обеденный стол");
        var wood = C(175, 130, 85);
        Box(r, 0, 0.74f, 0, 1.6f, 0.05f, 0.9f, wood);
        Legs4(r, 0.72f, 0.38f, 0.72f, 0.07f, C(110, 75, 45));
        Box(r, 0, 0.66f, 0, 1.3f, 0.08f, 0.04f, C(110, 75, 45));
        return r.gameObject;
    }

    static GameObject Chair()
    {
        var r = Root("Стул");
        var wood = C(200, 160, 110);
        Box(r, 0, 0.45f, 0, 0.44f, 0.05f, 0.44f, wood);
        Legs4(r, 0.19f, 0.19f, 0.43f, 0.04f, C(120, 85, 55));
        Box(r, 0, 0.74f, -0.2f, 0.44f, 0.5f, 0.04f, wood);
        Box(r, 0, 0.72f, -0.2f, 0.34f, 0.2f, 0.05f, C(225, 190, 140));
        return r.gameObject;
    }

    static GameObject Bed()
    {
        var r = Root("Кровать");
        var wood = C(120, 85, 55);
        Box(r, 0, 0.2f, 0, 1.6f, 0.25f, 2.05f, wood);
        Box(r, 0, 0.42f, 0.02f, 1.5f, 0.2f, 1.95f, C(240, 240, 245));
        Box(r, 0, 0.53f, 0.3f, 1.5f, 0.03f, 1.4f, C(100, 140, 190));
        Box(r, 0, 0.7f, -1.04f, 1.65f, 1.0f, 0.08f, wood);
        Box(r, -0.38f, 0.58f, -0.78f, 0.55f, 0.12f, 0.35f, Color.white);
        Box(r, 0.38f, 0.58f, -0.78f, 0.55f, 0.12f, 0.35f, Color.white);
        Box(r, 0, 0.38f, 1.03f, 1.65f, 0.4f, 0.06f, wood);
        return r.gameObject;
    }

    static GameObject Bookshelf()
    {
        var r = Root("Книжный шкаф");
        var wood = C(140, 95, 60);
        Box(r, 0, 0.9f, -0.14f, 0.9f, 1.8f, 0.03f, wood);
        Box(r, -0.435f, 0.9f, 0, 0.03f, 1.8f, 0.3f, wood);
        Box(r, 0.435f, 0.9f, 0, 0.03f, 1.8f, 0.3f, wood);
        for (int i = 0; i < 6; i++) Box(r, 0, 0.015f + i * 0.36f, 0, 0.9f, 0.03f, 0.3f, wood);
        var cols = new[] { C(200, 60, 60), C(60, 120, 200), C(80, 170, 90), C(230, 190, 60), C(150, 80, 170) };
        for (int s = 0; s < 4; s++)
        {
            float x = -0.38f;
            for (int k = 0; k < 7; k++)
            {
                float w = 0.04f + 0.01f * ((k + s) % 3);
                float h = 0.22f + 0.03f * ((k * 2 + s) % 4);
                Box(r, x + w / 2, 0.03f + s * 0.36f + h / 2 + 0.015f, 0, w, h, 0.2f, cols[(k + s) % cols.Length]);
                x += w + 0.01f;
                if (x > 0.35f) break;
            }
        }
        return r.gameObject;
    }

    static GameObject FloorLamp()
    {
        var r = Root("Торшер");
        Cyl(r, 0, 0.015f, 0, 0.3f, 0.03f, C(40, 40, 45));
        Cyl(r, 0, 0.8f, 0, 0.03f, 1.55f, C(60, 60, 65));
        Cyl(r, 0, 1.6f, 0, 0.38f, 0.28f, C(255, 235, 170));
        Part(r, PrimitiveType.Sphere, new Vector3(0, 1.6f, 0), Vector3.one * 0.12f, C(255, 250, 220));
        return r.gameObject;
    }

    static GameObject Plant()
    {
        var r = Root("Растение");
        Cyl(r, 0, 0.15f, 0, 0.32f, 0.3f, C(170, 90, 60));
        Cyl(r, 0, 0.3f, 0, 0.28f, 0.02f, C(70, 50, 35));
        Cyl(r, 0, 0.55f, 0, 0.03f, 0.5f, C(70, 120, 50));
        var g1 = C(60, 150, 70); var g2 = C(40, 120, 55);
        Part(r, PrimitiveType.Sphere, new Vector3(0, 0.9f, 0), new Vector3(0.5f, 0.42f, 0.5f), g1);
        Part(r, PrimitiveType.Sphere, new Vector3(0.17f, 0.72f, 0.06f), new Vector3(0.3f, 0.26f, 0.3f), g2);
        Part(r, PrimitiveType.Sphere, new Vector3(-0.16f, 0.75f, -0.05f), new Vector3(0.3f, 0.26f, 0.3f), g2);
        return r.gameObject;
    }

    static GameObject TvStand()
    {
        var r = Root("Тумба с ТВ");
        var wood = C(90, 70, 55);
        Box(r, 0, 0.25f, 0, 1.3f, 0.5f, 0.4f, wood);
        Box(r, -0.32f, 0.25f, 0.205f, 0.6f, 0.4f, 0.01f, C(70, 52, 40));
        Box(r, 0.32f, 0.25f, 0.205f, 0.6f, 0.4f, 0.01f, C(70, 52, 40));
        Box(r, 0, 0.52f, 0, 0.35f, 0.03f, 0.2f, C(30, 30, 30));
        Box(r, 0, 0.88f, 0, 1.15f, 0.65f, 0.04f, C(20, 20, 22));
        Box(r, 0, 0.88f, 0.022f, 1.09f, 0.59f, 0.005f, C(40, 90, 150));
        return r.gameObject;
    }

    static GameObject Wardrobe()
    {
        var r = Root("Шкаф");
        var body = C(235, 232, 225); var line = C(190, 185, 175);
        Box(r, 0, 1.0f, 0, 1.1f, 2.0f, 0.55f, body);
        Box(r, 0, 1.0f, 0.279f, 0.01f, 1.9f, 0.01f, line);
        Box(r, -0.1f, 1.0f, 0.285f, 0.025f, 0.3f, 0.025f, C(60, 60, 60));
        Box(r, 0.1f, 1.0f, 0.285f, 0.025f, 0.3f, 0.025f, C(60, 60, 60));
        Box(r, 0, 0.04f, 0.0f, 1.0f, 0.08f, 0.5f, C(90, 80, 70));
        return r.gameObject;
    }

    static GameObject Desk()
    {
        var r = Root("Письменный стол");
        var wood = C(215, 195, 160);
        Box(r, 0, 0.74f, 0, 1.3f, 0.04f, 0.65f, wood);
        Box(r, -0.55f, 0.37f, 0, 0.04f, 0.72f, 0.6f, C(180, 160, 125));
        Box(r, 0.4f, 0.37f, 0, 0.4f, 0.72f, 0.6f, C(180, 160, 125));
        Box(r, 0.4f, 0.6f, 0.305f, 0.36f, 0.12f, 0.01f, C(150, 130, 100));
        Box(r, 0.4f, 0.4f, 0.305f, 0.36f, 0.12f, 0.01f, C(150, 130, 100));
        // монитор
        Box(r, -0.15f, 0.78f, -0.1f, 0.12f, 0.02f, 0.1f, C(30, 30, 30));
        Box(r, -0.15f, 0.9f, -0.1f, 0.03f, 0.2f, 0.03f, C(30, 30, 30));
        Box(r, -0.15f, 1.06f, -0.1f, 0.55f, 0.32f, 0.02f, C(25, 25, 28));
        Box(r, -0.15f, 1.06f, -0.088f, 0.52f, 0.29f, 0.005f, C(70, 140, 200));
        return r.gameObject;
    }

    static GameObject Rug()
    {
        var r = Root("Ковёр");
        Cyl(r, 0, 0.01f, 0, 1.8f, 0.02f, C(170, 60, 70));
        Cyl(r, 0, 0.0125f, 0, 1.4f, 0.02f, C(235, 215, 180));
        Cyl(r, 0, 0.015f, 0, 0.9f, 0.02f, C(170, 60, 70));
        return r.gameObject;
    }

    static GameObject Pouf()
    {
        var r = Root("Пуф");
        Cyl(r, 0, 0.19f, 0, 0.5f, 0.38f, C(240, 180, 60));
        Cyl(r, 0, 0.385f, 0, 0.44f, 0.04f, C(250, 205, 100));
        return r.gameObject;
    }

    // ---------------------------------------------------------------- каталог

    public static readonly List<Def> Catalog = new()
    {
        new Def { name = "Диван", swatch = C(70, 110, 160), build = Sofa },
        new Def { name = "Кресло", swatch = C(190, 110, 70), build = Armchair },
        new Def { name = "Журнальный\nстолик", swatch = C(150, 105, 65), build = CoffeeTable },
        new Def { name = "Обеденный\nстол", swatch = C(175, 130, 85), build = DiningTable },
        new Def { name = "Стул", swatch = C(200, 160, 110), build = Chair },
        new Def { name = "Кровать", swatch = C(100, 140, 190), build = Bed },
        new Def { name = "Книжный\nшкаф", swatch = C(140, 95, 60), build = Bookshelf },
        new Def { name = "Торшер", swatch = C(255, 235, 170), build = FloorLamp },
        new Def { name = "Растение", swatch = C(60, 150, 70), build = Plant },
        new Def { name = "Тумба\nс ТВ", swatch = C(90, 70, 55), build = TvStand },
        new Def { name = "Шкаф", swatch = C(235, 232, 225), build = Wardrobe },
        new Def { name = "Письменный\nстол", swatch = C(215, 195, 160), build = Desk },
        new Def { name = "Ковёр", swatch = C(170, 60, 70), build = Rug },
        new Def { name = "Пуф", swatch = C(240, 180, 60), build = Pouf },
    };

    /// <summary>Собирает модель и добавляет коллайдер для выбора касанием.</summary>
    public static GameObject Build(int index, out float radius)
    {
        var root = Catalog[index].build();
        var rs = root.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        var col = root.AddComponent<BoxCollider>();
        col.center = b.center;
        col.size = b.size;
        radius = Mathf.Max(b.extents.x, b.extents.z);
        return root;
    }
}
