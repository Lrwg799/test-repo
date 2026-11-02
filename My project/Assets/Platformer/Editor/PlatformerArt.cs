using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 程序化像素美术生成器：把角色 / 地块 / 金币 / 敌人 / 背景等 PNG 画出来并设置为 Sprite。
/// 菜单：Platformer / 1 - Generate Art
/// </summary>
public static class PlatformerArt
{
    public const string Folder = "Assets/Platformer/Art";
    public const int PixelsPerUnit = 32;

    [MenuItem("Platformer/1 - Generate Art")]
    public static void GenerateFromMenu()
    {
        GenerateAll();
        EditorUtility.DisplayDialog("Platformer", "像素美术已生成到 " + Folder, "好");
    }

    public static void GenerateAll()
    {
        Directory.CreateDirectory(Folder);

        Save(DrawPlayer(), "player");
        Save(DrawSlime(), "slime");
        Save(DrawCoin(), "coin");
        Save(DrawSpike(), "spike");
        Save(DrawDirt(), "dirt");
        Save(DrawGrassCap(), "grass");
        Save(DrawFlag(), "flag");
        Save(DrawCheckpoint(), "checkpoint");
        Save(DrawCloud(), "cloud");
        Save(DrawBush(), "bush");
        Save(DrawHillFar(), "hill_far");
        Save(DrawHillNear(), "hill_near");
        Save(DrawSky(), "sky");

        AssetDatabase.Refresh();
    }

    // ------------------------------------------------------------------ 调色板

    static Color32 Hex(string hex)
    {
        Color color;
        if (!ColorUtility.TryParseHtmlString(hex, out color)) return new Color32(255, 0, 255, 255);
        return color;
    }

    static Color32 Lerp(Color32 a, Color32 b, float t)
    {
        t = Mathf.Clamp01(t);
        return new Color32(
            (byte)Mathf.RoundToInt(Mathf.Lerp(a.r, b.r, t)),
            (byte)Mathf.RoundToInt(Mathf.Lerp(a.g, b.g, t)),
            (byte)Mathf.RoundToInt(Mathf.Lerp(a.b, b.b, t)),
            (byte)Mathf.RoundToInt(Mathf.Lerp(a.a, b.a, t)));
    }

    // ------------------------------------------------------------------ 画布

    class Canvas
    {
        public readonly int W;
        public readonly int H;
        public readonly Color32[] Px;

        public Canvas(int width, int height)
        {
            W = width;
            H = height;
            Px = new Color32[width * height];
        }

        public bool In(int x, int y) { return x >= 0 && y >= 0 && x < W && y < H; }

        public Color32 Get(int x, int y)
        {
            return In(x, y) ? Px[y * W + x] : new Color32(0, 0, 0, 0);
        }

        public void Set(int x, int y, Color32 color)
        {
            if (In(x, y)) Px[y * W + x] = color;
        }

        public void Blend(int x, int y, Color32 color)
        {
            if (!In(x, y)) return;
            Color32 dst = Px[y * W + x];
            float srcA = color.a / 255f;
            if (srcA <= 0f) return;
            if (srcA >= 1f) { Px[y * W + x] = color; return; }

            float outA = srcA + (dst.a / 255f) * (1f - srcA);
            if (outA <= 0.0001f) return;

            float r = (color.r * srcA + dst.r * (dst.a / 255f) * (1f - srcA)) / outA;
            float g = (color.g * srcA + dst.g * (dst.a / 255f) * (1f - srcA)) / outA;
            float b = (color.b * srcA + dst.b * (dst.a / 255f) * (1f - srcA)) / outA;

            Px[y * W + x] = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(r), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(g), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(b), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(outA * 255f), 0, 255));
        }

        public void Fill(Color32 color)
        {
            for (int i = 0; i < Px.Length; i++) Px[i] = color;
        }

        public void Rect(int x0, int y0, int x1, int y1, Color32 color)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    Set(x, y, color);
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color32 color, bool blend = false)
        {
            if (rx <= 0f || ry <= 0f) return;
            int x0 = Mathf.FloorToInt(cx - rx);
            int x1 = Mathf.CeilToInt(cx + rx);
            int y0 = Mathf.FloorToInt(cy - ry);
            int y1 = Mathf.CeilToInt(cy + ry);

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + 0.5f - cx) / rx;
                    float dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy > 1f) continue;
                    if (blend) Blend(x, y, color);
                    else Set(x, y, color);
                }
            }
        }

        public void Triangle(Vector2 a, Vector2 b, Vector2 c, Color32 color)
        {
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)));
            int x1 = Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)));
            int y0 = Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)));
            int y1 = Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)));

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float w0 = Cross(b - a, p - a);
                    float w1 = Cross(c - b, p - b);
                    float w2 = Cross(a - c, p - c);
                    if ((w0 >= 0f && w1 >= 0f && w2 >= 0f) || (w0 <= 0f && w1 <= 0f && w2 <= 0f))
                        Set(x, y, color);
                }
            }
        }

        static float Cross(Vector2 u, Vector2 v) { return u.x * v.y - u.y * v.x; }

        public void Line(int x0, int y0, int x1, int y1, Color32 color)
        {
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                Set(x0, y0, color);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }
            }
        }

        public void Outline(Color32 color)
        {
            Color32[] copy = (Color32[])Px.Clone();
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    if (copy[y * W + x].a != 0) continue;

                    bool near = false;
                    if (x > 0 && copy[y * W + x - 1].a > 0) near = true;
                    else if (x < W - 1 && copy[y * W + x + 1].a > 0) near = true;
                    else if (y > 0 && copy[(y - 1) * W + x].a > 0) near = true;
                    else if (y < H - 1 && copy[(y + 1) * W + x].a > 0) near = true;

                    if (near) Px[y * W + x] = color;
                }
            }
        }

        public Texture2D ToTexture(string name)
        {
            Texture2D texture = new Texture2D(W, H, TextureFormat.RGBA32, false);
            texture.name = name;
            texture.SetPixels32(Px);
            texture.Apply();
            return texture;
        }
    }

    // ------------------------------------------------------------------ 输出

    static void Save(Canvas canvas, string fileName)
    {
        string path = Folder + "/" + fileName + ".png";
        Texture2D texture = canvas.ToTexture(fileName);
        byte[] png = texture.EncodeToPNG();
        File.WriteAllBytes(path, png);
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        ConfigureImporter(path);
    }

    static void ConfigureImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;   // 平铺 / 九宫格需要 FullRect
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);

        importer.SaveAndReimport();
    }

    // ------------------------------------------------------------------ 角色

    static Canvas DrawPlayer()
    {
        Canvas c = new Canvas(24, 32);

        Color32 outline = Hex("#4A3623");
        Color32 body = Hex("#FFD166");
        Color32 shade = Hex("#EFA53C");
        Color32 light = Hex("#FFE9AE");
        Color32 leaf = Hex("#5FCB63");
        Color32 leafDark = Hex("#3D9E45");
        Color32 white = Hex("#FFFFFF");
        Color32 pupil = Hex("#35261A");
        Color32 blush = Hex("#FF9AA8");

        // 脚
        c.Ellipse(8.5f, 3.2f, 3.3f, 2.1f, shade);
        c.Ellipse(15.5f, 3.2f, 3.3f, 2.1f, shade);

        // 身体
        c.Ellipse(12f, 14.5f, 9.0f, 11.0f, body);

        // 底部阴影
        for (int y = 0; y < 9; y++)
            for (int x = 0; x < 24; x++)
                if (c.Get(x, y).a > 0) c.Set(x, y, shade);

        // 左上高光
        c.Ellipse(8.6f, 21.5f, 3.8f, 4.4f, new Color32(light.r, light.g, light.b, 115), true);
        c.Ellipse(5.6f, 18.0f, 1.2f, 1.6f, new Color32(light.r, light.g, light.b, 90), true);

        // 眼睛
        c.Ellipse(8.4f, 17.8f, 2.8f, 3.2f, white);
        c.Ellipse(15.6f, 17.8f, 2.8f, 3.2f, white);
        c.Ellipse(9.2f, 17.4f, 1.5f, 2.1f, pupil);
        c.Ellipse(16.4f, 17.4f, 1.5f, 2.1f, pupil);
        c.Set(8, 19, white);
        c.Set(15, 19, white);

        // 腮红
        c.Ellipse(4.6f, 13.0f, 1.9f, 1.2f, new Color32(blush.r, blush.g, blush.b, 185), true);
        c.Ellipse(19.4f, 13.0f, 1.9f, 1.2f, new Color32(blush.r, blush.g, blush.b, 185), true);

        // 嘴巴
        c.Set(11, 10, pupil);
        c.Set(12, 10, pupil);
        c.Set(13, 10, pupil);

        // 头顶小叶子
        c.Line(12, 25, 12, 28, leafDark);
        c.Ellipse(14.2f, 28.4f, 3.1f, 2.0f, leaf);
        c.Line(11, 28, 17, 28, leafDark);

        c.Outline(outline);
        return c;
    }

    static Canvas DrawSlime()
    {
        Canvas c = new Canvas(24, 18);

        Color32 outline = Hex("#5E1F3C");
        Color32 body = Hex("#FF6B9D");
        Color32 shade = Hex("#D94C7E");
        Color32 light = Hex("#FFA8C4");
        Color32 white = Hex("#FFFFFF");
        Color32 pupil = Hex("#3A1526");

        c.Ellipse(12f, 7.4f, 10f, 6.6f, body);

        // 下半部阴影
        for (int y = 2; y < 6; y++)
            for (int x = 0; x < 24; x++)
                if (c.Get(x, y).a > 0) c.Set(x, y, shade);

        c.Ellipse(8.6f, 11.2f, 3.6f, 1.8f, new Color32(light.r, light.g, light.b, 130), true);
        c.Ellipse(6.4f, 9.2f, 1.6f, 1.1f, new Color32(light.r, light.g, light.b, 90), true);

        c.Ellipse(8.6f, 8.4f, 2.6f, 2.9f, white);
        c.Ellipse(15.4f, 8.4f, 2.6f, 2.9f, white);
        c.Ellipse(9.3f, 8.1f, 1.4f, 1.9f, pupil);
        c.Ellipse(16.1f, 8.1f, 1.4f, 1.9f, pupil);
        c.Set(8, 9, white);
        c.Set(15, 9, white);

        // 小嘴
        c.Set(11, 4, pupil);
        c.Set(12, 4, pupil);
        c.Set(13, 4, pupil);

        c.Outline(outline);
        return c;
    }

    // ------------------------------------------------------------------ 道具

    static Canvas DrawCoin()
    {
        Canvas c = new Canvas(16, 16);

        Color32 rim = Hex("#E9A400");
        Color32 body = Hex("#FFD93D");
        Color32 light = Hex("#FFF6C4");
        Color32 outline = Hex("#8A5B00");

        c.Ellipse(8f, 8f, 7f, 7f, rim);
        c.Ellipse(8f, 8f, 5.6f, 5.6f, body);
        c.Ellipse(5.6f, 10.4f, 1.7f, 2.4f, light);
        c.Ellipse(5.2f, 11.6f, 0.9f, 1.0f, new Color32(255, 255, 255, 230));

        // 中间的星形花纹
        c.Set(8, 10, rim);
        c.Set(8, 9, rim);
        c.Set(7, 8, rim);
        c.Set(8, 8, rim);
        c.Set(9, 8, rim);
        c.Set(8, 7, rim);
        c.Set(8, 6, rim);

        c.Outline(outline);
        return c;
    }

    static Canvas DrawSpike()
    {
        Canvas c = new Canvas(16, 16);

        Color32 light = Hex("#DCE5F0");
        Color32 shade = Hex("#A9B6C6");
        Color32 dark = Hex("#7C8B9E");
        Color32 outline = Hex("#3E4A5C");

        c.Triangle(new Vector2(8f, 16f), new Vector2(0f, 0f), new Vector2(9f, 0f), light);
        c.Triangle(new Vector2(8f, 16f), new Vector2(9f, 0f), new Vector2(16f, 0f), shade);

        // 底部暗边
        c.Rect(0, 0, 15, 1, dark);
        c.Rect(0, 2, 15, 2, dark);

        // 高光
        c.Line(7, 14, 3, 3, new Color32(255, 255, 255, 210));
        c.Set(8, 14, new Color32(255, 255, 255, 200));

        c.Outline(outline);
        return c;
    }

    static Canvas DrawFlag()
    {
        Canvas c = new Canvas(24, 48);

        Color32 pole = Hex("#8B6B4A");
        Color32 poleDark = Hex("#6E5238");
        Color32 cloth = Hex("#FF5A5F");
        Color32 clothDark = Hex("#D93B45");
        Color32 stone = Hex("#C7CDD6");
        Color32 stoneDark = Hex("#A8AFB9");
        Color32 outline = Hex("#332A22");

        // 底座
        c.Ellipse(12f, 4.5f, 8f, 3.6f, stone);
        c.Rect(4, 1, 20, 4, stone);
        c.Ellipse(12f, 2.6f, 6f, 1.8f, stoneDark);

        // 旗杆
        c.Rect(10, 3, 12, 45, pole);
        c.Rect(12, 3, 12, 45, poleDark);

        // 旗面
        c.Triangle(new Vector2(13f, 45f), new Vector2(23f, 39f), new Vector2(13f, 33f), cloth);
        c.Triangle(new Vector2(13f, 39f), new Vector2(23f, 39f), new Vector2(13f, 33f), clothDark);
        c.Line(13, 39, 22, 39, cloth);

        // 星星
        c.Set(16, 40, Hex("#FFF4C2"));
        c.Set(17, 39, Hex("#FFF4C2"));
        c.Set(16, 39, Hex("#FFF4C2"));
        c.Set(16, 38, Hex("#FFF4C2"));
        c.Set(18, 39, Hex("#FFF4C2"));
        c.Set(15, 39, Hex("#FFF4C2"));

        c.Outline(outline);
        return c;
    }

    static Canvas DrawCheckpoint()
    {
        Canvas c = new Canvas(16, 40);

        Color32 pole = Hex("#C3CAD4");
        Color32 poleDark = Hex("#9AA3AF");
        Color32 cloth = Hex("#FFFFFF");
        Color32 baseColor = Hex("#8E97A3");
        Color32 outline = Hex("#333A44");

        c.Ellipse(8f, 4f, 6f, 3f, baseColor);
        c.Rect(2, 1, 14, 4, baseColor);

        c.Rect(7, 4, 8, 37, pole);
        c.Rect(8, 4, 8, 37, poleDark);

        c.Rect(9, 27, 14, 35, cloth);
        c.Triangle(new Vector2(14f, 35f), new Vector2(9f, 31f), new Vector2(14f, 27f), cloth);
        c.Triangle(new Vector2(14f, 35f), new Vector2(9f, 35f), new Vector2(14f, 27f), cloth);

        c.Outline(outline);
        return c;
    }

    static Canvas DrawCloud()
    {
        Canvas c = new Canvas(128, 44);

        Color32 white = Hex("#FFFFFF");
        Color32 shade = Hex("#DDEEFF");

        c.Ellipse(24f, 20f, 12f, 9f, white);
        c.Ellipse(48f, 24f, 16f, 12f, white);
        c.Ellipse(76f, 21f, 14f, 10f, white);
        c.Ellipse(102f, 18f, 11f, 8f, white);
        c.Ellipse(116f, 15f, 7f, 5f, white);

        for (int x = 0; x < 128; x++)
        {
            for (int y = 0; y < 12; y++)
            {
                if (c.Get(x, y).a > 0) c.Set(x, y, shade);
            }
        }

        return c;
    }

    static Canvas DrawBush()
    {
        Canvas c = new Canvas(32, 20);

        Color32 body = Hex("#4FB05C");
        Color32 dark = Hex("#3A8C46");
        Color32 light = Hex("#6BD178");
        Color32 outline = Hex("#2C6B36");

        c.Ellipse(9f, 8f, 8f, 6.5f, body);
        c.Ellipse(17f, 10f, 9f, 8f, body);
        c.Ellipse(25f, 8f, 7f, 6f, body);
        c.Rect(2, 1, 30, 6, body);

        c.Ellipse(13f, 12f, 4f, 3f, light);
        c.Ellipse(23f, 10f, 2.6f, 2f, light);
        c.Rect(2, 1, 30, 2, dark);

        c.Outline(outline);
        return c;
    }

    // ------------------------------------------------------------------ 地形

    static Canvas DrawDirt()
    {
        Canvas c = new Canvas(32, 32);

        Color32 baseColor = Hex("#A5764F");
        Color32 dark = Hex("#8A5E3B");
        Color32 light = Hex("#BE8C63");

        c.Fill(baseColor);

        // 每 16 像素一层土纹，平铺时形成自然的层理
        for (int y = 0; y < 32; y++)
        {
            if (y % 16 >= 14)
                for (int x = 0; x < 32; x++) c.Set(x, y, Lerp(baseColor, dark, 0.55f));
            if (y % 16 == 0)
                for (int x = 0; x < 32; x++) c.Set(x, y, Lerp(baseColor, dark, 0.35f));
        }

        System.Random random = new System.Random(1337);
        for (int i = 0; i < 90; i++)
        {
            int x = random.Next(0, 32);
            int y = random.Next(0, 32);
            Color32 color = random.Next(0, 2) == 0 ? dark : light;
            if (c.Get(x, y).a > 0) c.Set(x, y, color);
        }

        // 几块小石子
        for (int i = 0; i < 6; i++)
        {
            int x = random.Next(2, 29);
            int y = random.Next(10, 28);
            c.Rect(x, y, x + 2, y + 1, light);
            c.Rect(x, y - 1, x + 2, y - 1, dark);
        }

        return c;
    }

    static Canvas DrawGrassCap()
    {
        Canvas c = new Canvas(32, 12);

        Color32 grass = Hex("#5FCB63");
        Color32 dark = Hex("#3D9E45");
        Color32 light = Hex("#8AE88D");

        int[] heights = new int[32];
        System.Random random = new System.Random(42);
        for (int x = 0; x < 32; x++)
        {
            heights[x] = 6 + random.Next(0, 4);
            if (x % 11 == 3) heights[x] = 10;
        }
        heights[0] = 8;
        heights[31] = 8;

        for (int x = 0; x < 32; x++)
        {
            int top = heights[x];
            for (int y = 0; y < top; y++)
            {
                Color32 color = y < 3 ? dark : grass;
                if (y == top - 1) color = light;
                c.Set(x, y, color);
            }
            // 草叶尖
            if (x % 5 == 0 && top < 11) c.Set(x, top, light);
        }

        return c;
    }

    // ------------------------------------------------------------------ 背景

    static Canvas DrawSky()
    {
        Canvas c = new Canvas(8, 256);

        Color32 top = Hex("#6FB7EE");
        Color32 bottom = Hex("#E9F8FF");

        for (int y = 0; y < 256; y++)
        {
            float k = Mathf.Pow(y / 255f, 0.8f);
            Color32 color = Lerp(bottom, top, k);
            for (int x = 0; x < 8; x++) c.Set(x, y, color);
        }

        return c;
    }

    static Canvas DrawHillFar()
    {
        return DrawHillBand(512, 288, -3.2f, 3.4f, 1.1f, 0.6f, 0.35f, 0.0f,
            Hex("#B7E3CC"), Hex("#93CDB2"), Hex("#D3F2E2"));
    }

    static Canvas DrawHillNear()
    {
        return DrawHillBand(512, 208, -3.2f, 1.5f, 0.8f, 0.45f, 0.3f, 0.7f,
            Hex("#6FC49B"), Hex("#4C9C77"), Hex("#8FDCB6"));
    }

    /// <summary>按世界坐标高度生成可水平平铺的山丘剪影。</summary>
    static Canvas DrawHillBand(int width, int height, float bottomWorldY, float baseWorldY,
                               float a1, float a2, float a3, float phase,
                               Color32 top, Color32 bottom, Color32 rim)
    {
        Canvas c = new Canvas(width, height);

        for (int x = 0; x < width; x++)
        {
            float px = x / (float)width;
            float crest = baseWorldY
                          + a1 * Mathf.Sin(px * Mathf.PI * 2f + phase)
                          + a2 * Mathf.Sin(px * Mathf.PI * 4f + phase * 2.3f + 1.1f)
                          + a3 * Mathf.Sin(px * Mathf.PI * 6f + phase + 2.7f);

            int heightPx = Mathf.Clamp(Mathf.RoundToInt((crest - bottomWorldY) * PixelsPerUnit), 4, height - 2);
            for (int y = 0; y < heightPx; y++)
            {
                float k = y / (float)heightPx;
                c.Set(x, y, Lerp(bottom, top, k));
            }
            c.Set(x, heightPx - 1, rim);
            if (heightPx > 2) c.Set(x, heightPx - 2, rim);
        }

        return c;
    }
}
