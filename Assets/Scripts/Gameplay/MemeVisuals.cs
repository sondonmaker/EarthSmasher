using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>밈 무기용 절차적 비주얼 — 외부 텍스처 없이 런타임 생성.</summary>
public static class MemeVisuals
{
    static Texture2D dogeCoinTex;
    static Texture2D elonDogeTex;
    static Texture2D penguCoinTex;
    static Texture2D penguHeroTex;
    static Texture2D trumpPoseTex;
    static Texture2D tariffTex;
    static Texture2D dogeTex;
    static Texture2D pepeTex;
    static Texture2D pepePoseTex;
    static Texture2D sneakerSharkTex;
    static Texture2D cowTex;
    static Texture2D catTex;
    static Texture2D catPoseTex;
    static Mesh coinMesh;
    static Mesh sharedBillboardMesh;
    static Material coinRimMat;
    static Mesh sphereMesh;
    static Mesh sharedClawMesh;
    static Material sharedClawMat;
    static Material sharedMilkMat;

    public static GameObject CreateDoge(float scale)
    {
        var coinTex = LoadDogeCoinTexture();
        if (coinTex != null)
            return CreateDogeCoin(scale, coinTex);

        EnsureSphere();
        var go = Body("MemeDoge", scale, BuildDogeTexture(), new Color(0.92f, 0.78f, 0.55f));
        return go;
    }

    static Texture2D LoadDogeCoinTexture()
    {
        if (dogeCoinTex != null)
            return dogeCoinTex;
        var src = Resources.Load<Texture2D>("Meme/doge_coin");
        if (src == null)
            return null;
        dogeCoinTex = PrepareCoinTexture(src);
        return dogeCoinTex;
    }

    /// <summary>체커보드/회색 배경 제거 + 가장자리 flood-fill — 진짜 투명 PNG 전까지.</summary>
    static Texture2D PrepareCoinTexture(Texture2D src)
    {
        var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(src, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        int w = tex.width;
        int h = tex.height;
        var px = tex.GetPixels();
        var bg = new bool[px.Length];
        for (int i = 0; i < px.Length; i++)
            bg[i] = IsMemeBackgroundPixel(px[i]);

        var q = new Queue<int>();
        void TrySeed(int x, int y)
        {
            int i = y * w + x;
            if (bg[i])
                q.Enqueue(i);
        }

        for (int x = 0; x < w; x++)
        {
            TrySeed(x, 0);
            TrySeed(x, h - 1);
        }
        for (int y = 0; y < h; y++)
        {
            TrySeed(0, y);
            TrySeed(w - 1, y);
        }

        while (q.Count > 0)
        {
            int i = q.Dequeue();
            if (!bg[i])
                continue;
            bg[i] = false;
            px[i] = new Color(0f, 0f, 0f, 0f);

            int x = i % w;
            int y = i / w;
            if (x > 0) TryEnqueue(x - 1, y);
            if (x < w - 1) TryEnqueue(x + 1, y);
            if (y > 0) TryEnqueue(x, y - 1);
            if (y < h - 1) TryEnqueue(x, y + 1);
        }

        void TryEnqueue(int x, int y)
        {
            int j = y * w + x;
            if (bg[j])
                q.Enqueue(j);
        }

        tex.SetPixels(px);
        tex.Apply(false, false);
        FinalizeMemeTexture(tex);
        return tex;
    }

    static void FinalizeMemeTexture(Texture2D tex)
    {
        if (tex == null)
            return;
        tex.filterMode = FilterMode.Bilinear;
        tex.anisoLevel = 8;
        tex.wrapMode = TextureWrapMode.Clamp;
    }

    static bool IsMemeBackgroundPixel(Color c)
    {
        if (c.a < 0.05f)
            return true;
        if (c.r > 0.93f && c.g > 0.93f && c.b > 0.93f)
            return true;

        float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
        float sat = max - min;
        if (sat < 0.1f)
        {
            float gray = (c.r + c.g + c.b) / 3f;
            if (gray > 0.32f && gray < 0.92f)
                return true;
        }

        return IsCheckerboardPixel(c);
    }

    static bool IsCheckerboardPixel(Color c)
    {
        if (c.a < 0.05f)
            return false;
        float dRG = Mathf.Abs(c.r - c.g);
        float dGB = Mathf.Abs(c.g - c.b);
        if (dRG > 0.06f || dGB > 0.06f)
            return false;
        float gray = (c.r + c.g + c.b) / 3f;
        return gray > 0.38f && gray < 0.88f;
    }

    /// <summary>도지코인 PNG — 납작 동전(앞뒤 면 + 금 림).</summary>
    static GameObject CreateDogeCoin(float diameter, Texture2D tex) =>
        CreateTexturedCoin(diameter, tex, "MemeDogeCoin");

    public static GameObject CreateTexturedCoin(float diameter, Texture2D tex, string name)
    {
        EnsureCoinMesh();
        var go = new GameObject(name);
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = coinMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = new[] { CoinFaceMat(tex ?? BuildPenguCoinTexture()), CoinRimMat() };
        mr.shadowCastingMode = ShadowCastingMode.Off;
        go.transform.localScale = Vector3.one * diameter;
        return go;
    }

    public static GameObject CreatePenguCoin(float diameter)
    {
        return CreateTexturedCoin(diameter, LoadPenguCoinTexture(), "MemePenguCoin");
    }

    public static GameObject CreateElonDogeRide(float size)
    {
        return CreateBillboard("MemeElonDoge", size, 1.62f, LoadElonDogeTexture(), new Color(0.55f, 0.75f, 1f));
    }

    public static GameObject CreatePenguHero(float size)
    {
        return CreateBillboard("MemePenguHero", size, 1f, LoadPenguHeroTexture(), new Color(0.55f, 0.82f, 1f));
    }

    public static GameObject CreateTrumpBillboard(float size)
    {
        return CreateBillboard("MemeTrump", size, 0.72f, LoadTrumpTexture(), new Color(1f, 0.62f, 0.32f));
    }

    public static GameObject CreateTariffCoin(float diameter)
    {
        return CreateTexturedCoin(diameter, LoadTariffTexture(), "TariffCoin");
    }

    static Texture2D LoadTrumpTexture()
    {
        if (trumpPoseTex != null)
            return trumpPoseTex;
        var src = Resources.Load<Texture2D>("Meme/trump_pose");
        if (src == null)
            return null;
        trumpPoseTex = PrepareCoinTexture(src);
        return trumpPoseTex;
    }

    static Texture2D LoadTariffTexture()
    {
        if (tariffTex != null)
            return tariffTex;
        tariffTex = BuildTariffTexture();
        return tariffTex;
    }

    static Texture2D BuildTariffTexture()
    {
        var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        var px = new Color[128 * 128];
        for (int y = 0; y < 128; y++)
        {
            for (int x = 0; x < 128; x++)
            {
                float dx = (x - 64f) / 58f;
                float dy = (y - 64f) / 58f;
                px[y * 128 + x] = dx * dx + dy * dy > 1f
                    ? new Color(0f, 0f, 0f, 0f)
                    : new Color(0.92f, 0.72f, 0.18f, 1f);
            }
        }
        StampEllipse(px, 128, 64, 64, 46f, 46f, new Color(1f, 0.86f, 0.35f));
        tex.SetPixels(px);
        tex.Apply(false, true);
        return tex;
    }

    public static void AddFireTrail(GameObject go, float width)
    {
        var trail = go.AddComponent<TrailRenderer>();
        trail.time = 0.28f;
        trail.startWidth = width;
        trail.endWidth = width * 0.06f;
        trail.material = RuntimeMaterial.UnlitTransparent(new Color(1f, 0.45f, 0.08f, 0.8f));
        trail.startColor = new Color(1f, 0.65f, 0.15f, 0.85f);
        trail.endColor = new Color(0.8f, 0.1f, 0.02f, 0f);
        trail.minVertexDistance = 0.04f;
        trail.shadowCastingMode = ShadowCastingMode.Off;
    }

    static GameObject CreateBillboard(string name, float size, float aspect, Texture2D tex, Color fallback)
    {
        EnsureBillboardMesh();
        var go = new GameObject(name);
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = sharedBillboardMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = CoinFaceMat(tex != null ? tex : BuildSolidTex(fallback));
        mr.shadowCastingMode = ShadowCastingMode.Off;
        go.transform.localScale = new Vector3(size * aspect, size, 1f);
        go.AddComponent<MemeBillboard>();
        return go;
    }

    static Texture2D LoadElonDogeTexture()
    {
        if (elonDogeTex != null)
            return elonDogeTex;
        var src = Resources.Load<Texture2D>("Meme/elon_doge_ride");
        elonDogeTex = src != null ? PrepareCoinTexture(src) : null;
        return elonDogeTex;
    }

    static Texture2D LoadPenguCoinTexture()
    {
        if (penguCoinTex != null)
            return penguCoinTex;
        var src = Resources.Load<Texture2D>("Meme/pengu_coin");
        penguCoinTex = src != null ? PrepareCoinTexture(src) : BuildPenguCoinTexture();
        return penguCoinTex;
    }

    static Texture2D LoadPenguHeroTexture()
    {
        if (penguHeroTex != null)
            return penguHeroTex;
        var src = Resources.Load<Texture2D>("Meme/pengu_hero");
        if (src != null)
            penguHeroTex = PrepareCoinTexture(src);
        else
            penguHeroTex = LoadPenguCoinTexture();
        return penguHeroTex;
    }

    static Texture2D BuildPenguCoinTexture()
    {
        var tex = new Texture2D(128, 128, TextureFormat.RGB24, false);
        var px = new Color[128 * 128];
        for (int i = 0; i < px.Length; i++)
            px[i] = new Color(0.72f, 0.88f, 1f);
        StampEllipse(px, 128, 64, 64, 52f, 52f, Color.white);
        StampEllipse(px, 128, 48, 58, 8f, 10f, Color.black);
        StampEllipse(px, 128, 80, 58, 8f, 10f, Color.black);
        StampEllipse(px, 128, 64, 72, 7f, 5f, new Color(1f, 0.55f, 0.35f));
        tex.SetPixels(px);
        tex.Apply(false, true);
        return tex;
    }

    static Texture2D BuildSolidTex(Color col)
    {
        var tex = new Texture2D(4, 4, TextureFormat.RGB24, false);
        var px = new Color[16];
        for (int i = 0; i < px.Length; i++)
            px[i] = col;
        tex.SetPixels(px);
        tex.Apply(false, true);
        return tex;
    }

    static void EnsureBillboardMesh()
    {
        if (sharedBillboardMesh != null)
            return;
        sharedBillboardMesh = new Mesh { name = "MemeBillboard" };
        sharedBillboardMesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f)
        };
        sharedBillboardMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        sharedBillboardMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        sharedBillboardMesh.RecalculateNormals();
    }

    public static void AddRainbowTrail(GameObject go, float earthRadius)
    {
        float w = Mathf.Clamp(earthRadius * 0.04f, 0.06f, 0.18f);
        var trail = go.AddComponent<TrailRenderer>();
        trail.time = 0.45f;
        trail.startWidth = w;
        trail.endWidth = w * 0.05f;
        trail.material = RuntimeMaterial.UnlitTransparent(new Color(1f, 1f, 1f, 0.85f));
        trail.startColor = new Color(1f, 0.2f, 0.2f, 0.9f);
        trail.endColor = new Color(0.4f, 0.1f, 1f, 0f);
        trail.minVertexDistance = 0.05f;
        trail.numCapVertices = 2;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.2f, 0.2f), 0f),
                new GradientColorKey(new Color(1f, 0.85f, 0.1f), 0.25f),
                new GradientColorKey(new Color(0.2f, 1f, 0.3f), 0.5f),
                new GradientColorKey(new Color(0.2f, 0.5f, 1f), 0.75f),
                new GradientColorKey(new Color(0.7f, 0.2f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.85f, 0f),
                new GradientAlphaKey(0.5f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        trail.colorGradient = g;
    }

    public static void AddIceTrail(GameObject go, float width)
    {
        var trail = go.AddComponent<TrailRenderer>();
        trail.time = 0.3f;
        trail.startWidth = width;
        trail.endWidth = width * 0.08f;
        trail.material = RuntimeMaterial.UnlitTransparent(new Color(0.75f, 0.92f, 1f, 0.7f));
        trail.startColor = new Color(0.85f, 0.95f, 1f, 0.75f);
        trail.endColor = new Color(0.5f, 0.75f, 1f, 0f);
        trail.minVertexDistance = 0.04f;
        trail.shadowCastingMode = ShadowCastingMode.Off;
    }

    static void EnsureCoinMesh()
    {
        if (coinMesh != null)
            return;
        coinMesh = BuildCoinMesh(48);
    }

    static Material CoinFaceMat(Texture2D tex) =>
        RuntimeMaterial.TexturedTransparent(tex);

    static Material CoinRimMat()
    {
        if (coinRimMat == null)
            coinRimMat = RuntimeMaterial.Opaque(new Color(0.82f, 0.62f, 0.14f), 0.35f);
        return coinRimMat;
    }

    /// <summary>+Z/-Z 면에 텍스처, 옆면은 금색 림.</summary>
    static Mesh BuildCoinMesh(int segments)
    {
        var mesh = new Mesh { name = "MemeCoinDisc" };
        float radius = 0.5f;
        float halfH = 0.045f;
        segments = Mathf.Max(12, segments);

        int topCount = segments + 1;
        int vertCount = topCount * 2 + segments * 2;
        var verts = new Vector3[vertCount];
        var uvs = new Vector2[vertCount];
        var trisFace = new int[segments * 6];
        var trisRim = new int[segments * 6];

        verts[0] = new Vector3(0f, 0f, halfH);
        uvs[0] = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < segments; i++)
        {
            float ang = i / (float)segments * Mathf.PI * 2f;
            float cx = Mathf.Cos(ang) * radius;
            float cy = Mathf.Sin(ang) * radius;
            int ti = 1 + i;
            verts[ti] = new Vector3(cx, cy, halfH);
            uvs[ti] = new Vector2(cx + 0.5f, cy + 0.5f);
        }

        int botBase = topCount;
        verts[botBase] = new Vector3(0f, 0f, -halfH);
        uvs[botBase] = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < segments; i++)
        {
            float ang = i / (float)segments * Mathf.PI * 2f;
            float cx = Mathf.Cos(ang) * radius;
            float cy = Mathf.Sin(ang) * radius;
            int bi = botBase + 1 + i;
            verts[bi] = new Vector3(cx, cy, -halfH);
            uvs[bi] = new Vector2(cx + 0.5f, cy + 0.5f);
        }

        int rimBase = topCount * 2;
        for (int i = 0; i < segments; i++)
        {
            float ang = i / (float)segments * Mathf.PI * 2f;
            float cx = Mathf.Cos(ang) * radius;
            float cy = Mathf.Sin(ang) * radius;
            int ri = rimBase + i * 2;
            verts[ri] = new Vector3(cx, cy, halfH);
            uvs[ri] = new Vector2(i / (float)segments, 1f);
            verts[ri + 1] = new Vector3(cx, cy, -halfH);
            uvs[ri + 1] = new Vector2(i / (float)segments, 0f);
        }

        int tiFace = 0;
        for (int i = 0; i < segments; i++)
        {
            int a = 1 + i;
            int b = 1 + (i + 1) % segments;
            trisFace[tiFace++] = 0;
            trisFace[tiFace++] = a;
            trisFace[tiFace++] = b;

            int ba = botBase + 1 + i;
            int bb = botBase + 1 + (i + 1) % segments;
            trisFace[tiFace++] = botBase;
            trisFace[tiFace++] = bb;
            trisFace[tiFace++] = ba;
        }

        int tiRim = 0;
        for (int i = 0; i < segments; i++)
        {
            int ri = rimBase + i * 2;
            int rj = rimBase + ((i + 1) % segments) * 2;
            trisRim[tiRim++] = ri;
            trisRim[tiRim++] = rj;
            trisRim[tiRim++] = ri + 1;
            trisRim[tiRim++] = rj;
            trisRim[tiRim++] = rj + 1;
            trisRim[tiRim++] = ri + 1;
        }

        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.subMeshCount = 2;
        mesh.SetTriangles(trisFace, 0);
        mesh.SetTriangles(trisRim, 1);
        mesh.RecalculateNormals();
        return mesh;
    }

    /// <summary>BigMeteorStrike와 동일한 화염 꼬리 + 글로우.</summary>
    public static void AddMeteorFallTrail(GameObject go, float earthRadius)
    {
        float w = Mathf.Clamp(earthRadius * 0.05f, 0.08f, 0.22f);
        var trail = go.AddComponent<TrailRenderer>();
        trail.time = 0.35f;
        trail.startWidth = w;
        trail.endWidth = w * 0.08f;
        trail.material = RuntimeMaterial.UnlitTransparent(new Color(1f, 0.6f, 0.2f, 0.85f));
        trail.startColor = new Color(1f, 0.75f, 0.3f, 0.9f);
        trail.endColor = new Color(1f, 0.15f, 0.05f, 0f);
        trail.minVertexDistance = 0.04f;
        trail.numCapVertices = 1;
        trail.shadowCastingMode = ShadowCastingMode.Off;

        var glowGo = new GameObject("MeteorGlow");
        glowGo.transform.SetParent(go.transform, false);
        var glow = glowGo.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.55f, 0.2f);
        glow.intensity = 2.2f;
        glow.range = earthRadius * 1.2f;
    }

    public static GameObject CreatePepe(float scale)
    {
        var tex = LoadPepePoseTexture();
        if (tex != null)
            return CreateBillboard("MemePepe", scale, 0.78f, tex, new Color(0.35f, 0.78f, 0.28f));

        EnsureSphere();
        var go = Body("MemePepe", scale, BuildPepeTexture(), new Color(0.35f, 0.78f, 0.28f));
        return go;
    }

    public static GameObject CreateEarthCow(float scale)
    {
        EnsureSphere();
        var go = Body("MemeEarthCow", scale, BuildCowTexture(), Color.white);
        return go;
    }

    public static GameObject CreateSneakerShark(float scale)
    {
        var tex = LoadSneakerSharkTexture();
        if (tex != null)
            return CreateBillboard("MemeShark", scale, 0.66f, tex, new Color(0.55f, 0.65f, 0.78f));

        var root = new GameObject("MemeShark");
        root.transform.localScale = Vector3.one * scale;

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = new Vector3(1.6f, 0.55f, 0.75f);
        Object.Destroy(body.GetComponent<Collider>());
        body.GetComponent<Renderer>().material = RuntimeMaterial.Opaque(new Color(0.55f, 0.58f, 0.62f), 0f);

        var fin = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fin.name = "Fin";
        fin.transform.SetParent(root.transform, false);
        fin.transform.localPosition = new Vector3(0f, 0.42f, 0f);
        fin.transform.localScale = new Vector3(0.08f, 0.35f, 0.45f);
        Object.Destroy(fin.GetComponent<Collider>());
        fin.GetComponent<Renderer>().material = RuntimeMaterial.Opaque(new Color(0.48f, 0.5f, 0.54f));

        var snout = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        snout.name = "Snout";
        snout.transform.SetParent(root.transform, false);
        snout.transform.localPosition = new Vector3(0.95f, 0f, 0f);
        snout.transform.localScale = new Vector3(0.55f, 0.42f, 0.38f);
        Object.Destroy(snout.GetComponent<Collider>());
        snout.GetComponent<Renderer>().material = RuntimeMaterial.Opaque(new Color(0.62f, 0.64f, 0.68f));

        for (int i = 0; i < 2; i++)
        {
            var leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leg.name = i == 0 ? "LegL" : "LegR";
            leg.transform.SetParent(root.transform, false);
            leg.transform.localPosition = new Vector3(0.15f, -0.55f, i == 0 ? 0.22f : -0.22f);
            leg.transform.localScale = new Vector3(0.12f, 0.28f, 0.12f);
            Object.Destroy(leg.GetComponent<Collider>());
            leg.GetComponent<Renderer>().material = RuntimeMaterial.Opaque(new Color(0.5f, 0.52f, 0.56f));

            var shoe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shoe.name = i == 0 ? "ShoeL" : "ShoeR";
            shoe.transform.SetParent(leg.transform, false);
            shoe.transform.localPosition = new Vector3(0f, -0.55f, 0.08f);
            shoe.transform.localScale = new Vector3(1.8f, 0.45f, 2.4f);
            Object.Destroy(shoe.GetComponent<Collider>());
            shoe.GetComponent<Renderer>().material = RuntimeMaterial.Opaque(new Color(0.18f, 0.45f, 0.92f), 0.15f);
        }

        return root;
    }

    public static GameObject CreateClawSwipe(float length, float width)
    {
        if (sharedClawMesh == null)
            sharedClawMesh = BuildClawQuad(1f, 1f);
        if (sharedClawMat == null)
            sharedClawMat = RuntimeMaterial.UnlitTransparent(new Color(1f, 0.95f, 0.88f, 0.5f));

        var go = new GameObject("ClawSwipe");
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = sharedClawMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = sharedClawMat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        go.transform.localScale = new Vector3(width, length, 1f);
        go.AddComponent<ClawSwipeFade>().Init(0.28f);
        return go;
    }

    public static Material SharedMilkMat()
    {
        if (sharedMilkMat == null)
            sharedMilkMat = RuntimeMaterial.Opaque(new Color(0.95f, 0.95f, 0.9f), 0.15f);
        return sharedMilkMat;
    }

    static GameObject Body(string name, float scale, Texture2D tex, Color tint)
    {
        var go = new GameObject(name);
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = sphereMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = Lit(tex, tint);
        mr.shadowCastingMode = ShadowCastingMode.On;
        go.transform.localScale = Vector3.one * scale;
        return go;
    }

    static Material Lit(Texture2D tex, Color tint)
    {
        var shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = new Material(shader);
        if (tex != null)
        {
            mat.mainTexture = tex;
            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", tex);
        }
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", tint);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", tint);
        return mat;
    }

    static void EnsureSphere()
    {
        if (sphereMesh != null)
            return;
        sphereMesh = BuildUvSphere(32, 24);
    }

    static Mesh BuildUvSphere(int lonSeg, int latSeg)
    {
        var mesh = new Mesh { name = "MemeSphere" };
        int vertCount = (lonSeg + 1) * (latSeg + 1);
        var verts = new Vector3[vertCount];
        var uvs = new Vector2[vertCount];
        int vi = 0;
        for (int lat = 0; lat <= latSeg; lat++)
        {
            float v = lat / (float)latSeg;
            float theta = v * Mathf.PI;
            for (int lon = 0; lon <= lonSeg; lon++)
            {
                float u = lon / (float)lonSeg;
                float phi = u * Mathf.PI * 2f;
                float sinT = Mathf.Sin(theta);
                verts[vi] = new Vector3(sinT * Mathf.Sin(phi), Mathf.Cos(theta), sinT * Mathf.Cos(phi));
                uvs[vi] = new Vector2(u, v);
                vi++;
            }
        }
        var tris = new int[lonSeg * latSeg * 6];
        int ti = 0;
        for (int lat = 0; lat < latSeg; lat++)
        {
            for (int lon = 0; lon < lonSeg; lon++)
            {
                int i0 = lat * (lonSeg + 1) + lon;
                int i1 = i0 + 1;
                int i2 = i0 + (lonSeg + 1);
                int i3 = i2 + 1;
                tris[ti++] = i0; tris[ti++] = i2; tris[ti++] = i1;
                tris[ti++] = i1; tris[ti++] = i2; tris[ti++] = i3;
            }
        }
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        return mesh;
    }

    public static GameObject CreateCatOrb(float scale)
    {
        var tex = LoadCatPoseTexture();
        if (tex != null)
            return CreateBillboard("MemeCat", scale, 0.82f, tex, new Color(0.92f, 0.78f, 0.55f));

        EnsureSphere();
        var go = Body("MemeCat", scale, BuildCatTexture(), new Color(0.92f, 0.78f, 0.55f));
        return go;
    }

    static Texture2D BuildDogeTexture()
    {
        if (dogeTex != null)
            return dogeTex;
        dogeTex = new Texture2D(128, 128, TextureFormat.RGB24, false);
        var px = new Color[128 * 128];
        for (int i = 0; i < px.Length; i++)
            px[i] = new Color(0.86f, 0.72f, 0.48f);
        StampEllipse(px, 128, 64, 52, 34f, 28f, new Color(0.95f, 0.82f, 0.58f));
        StampEllipse(px, 128, 44, 68, 10f, 12f, Color.white);
        StampEllipse(px, 128, 84, 68, 10f, 12f, Color.white);
        StampEllipse(px, 128, 44, 68, 5f, 7f, Color.black);
        StampEllipse(px, 128, 84, 68, 5f, 7f, Color.black);
        StampEllipse(px, 128, 64, 52, 8f, 6f, new Color(0.35f, 0.22f, 0.12f));
        dogeTex.SetPixels(px);
        dogeTex.Apply(false, true);
        return dogeTex;
    }

    static Texture2D LoadCatPoseTexture()
    {
        if (catPoseTex != null)
            return catPoseTex;
        var src = Resources.Load<Texture2D>("Meme/giant_cat");
        if (src == null)
            return null;
        catPoseTex = PrepareCoinTexture(src);
        return catPoseTex;
    }

    static Texture2D BuildCatTexture()
    {
        if (catTex != null)
            return catTex;
        catTex = new Texture2D(128, 128, TextureFormat.RGB24, false);
        var px = new Color[128 * 128];
        for (int i = 0; i < px.Length; i++)
            px[i] = new Color(0.78f, 0.62f, 0.42f);
        StampEllipse(px, 128, 64, 58, 36f, 30f, new Color(0.88f, 0.72f, 0.5f));
        StampEllipse(px, 128, 38, 78, 9f, 11f, Color.white);
        StampEllipse(px, 128, 90, 78, 9f, 11f, Color.white);
        StampEllipse(px, 128, 38, 78, 4f, 6f, new Color(0.15f, 0.55f, 0.2f));
        StampEllipse(px, 128, 90, 78, 4f, 6f, new Color(0.15f, 0.55f, 0.2f));
        StampEllipse(px, 128, 64, 48, 7f, 5f, new Color(0.85f, 0.45f, 0.55f));
        catTex.SetPixels(px);
        catTex.Apply(false, true);
        return catTex;
    }

    static Texture2D LoadPepePoseTexture()
    {
        if (pepePoseTex != null)
            return pepePoseTex;
        var src = Resources.Load<Texture2D>("Meme/pepe_punch");
        if (src == null)
            return null;
        pepePoseTex = PrepareCoinTexture(src);
        return pepePoseTex;
    }

    static Texture2D LoadSneakerSharkTexture()
    {
        if (sneakerSharkTex != null)
            return sneakerSharkTex;
        var src = Resources.Load<Texture2D>("Meme/sneaker_shark");
        if (src == null)
            return null;
        sneakerSharkTex = PrepareCoinTexture(src);
        return sneakerSharkTex;
    }

    static Texture2D BuildPepeTexture()
    {
        if (pepeTex != null)
            return pepeTex;
        pepeTex = new Texture2D(128, 128, TextureFormat.RGB24, false);
        var px = new Color[128 * 128];
        for (int i = 0; i < px.Length; i++)
            px[i] = new Color(0.42f, 0.78f, 0.32f);
        StampEllipse(px, 128, 64, 70, 38f, 34f, new Color(0.38f, 0.72f, 0.28f));
        StampEllipse(px, 128, 46, 78, 11f, 13f, Color.white);
        StampEllipse(px, 128, 82, 78, 11f, 13f, Color.white);
        StampEllipse(px, 128, 46, 78, 5f, 7f, Color.black);
        StampEllipse(px, 128, 82, 78, 5f, 7f, Color.black);
        StampEllipse(px, 128, 64, 58, 16f, 8f, new Color(0.72f, 0.35f, 0.28f));
        pepeTex.SetPixels(px);
        pepeTex.Apply(false, true);
        return pepeTex;
    }

    static Texture2D BuildCowTexture()
    {
        if (cowTex != null)
            return cowTex;
        cowTex = new Texture2D(128, 128, TextureFormat.RGB24, false);
        var px = new Color[128 * 128];
        for (int y = 0; y < 128; y++)
        {
            for (int x = 0; x < 128; x++)
            {
                float v = y / 127f;
                Color baseCol = Color.Lerp(new Color(0.12f, 0.35f, 0.72f), new Color(0.08f, 0.55f, 0.22f), v);
                if ((x + y * 3) % 17 < 5 && v > 0.35f)
                    baseCol = Color.Lerp(baseCol, Color.white, 0.85f);
                px[y * 128 + x] = baseCol;
            }
        }
        StampEllipse(px, 128, 64, 92, 28f, 18f, new Color(0.92f, 0.88f, 0.82f));
        StampEllipse(px, 128, 48, 96, 6f, 8f, Color.black);
        StampEllipse(px, 128, 80, 96, 6f, 8f, Color.black);
        cowTex.SetPixels(px);
        cowTex.Apply(false, true);
        return cowTex;
    }

    static void StampEllipse(Color[] px, int w, float cx, float cy, float rx, float ry, Color col)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - rx - 1f));
        int x1 = Mathf.Min(w - 1, Mathf.CeilToInt(cx + rx + 1f));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - ry - 1f));
        int y1 = Mathf.Min(w - 1, Mathf.CeilToInt(cy + ry + 1f));
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                float dx = (x - cx) / rx;
                float dy = (y - cy) / ry;
                if (dx * dx + dy * dy > 1f)
                    continue;
                px[y * w + x] = col;
            }
        }
    }

    static Mesh BuildClawQuad(float length, float width)
    {
        var mesh = new Mesh { name = "ClawQuad" };
        float h = length * 0.5f;
        float w = width * 0.5f;
        mesh.vertices = new[]
        {
            new Vector3(-w, -h, 0f),
            new Vector3(w, -h, 0f),
            new Vector3(w, h, 0f),
            new Vector3(-w, h, 0f)
        };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        return mesh;
    }
}

public class ClawSwipeFade : MonoBehaviour
{
    Material mat;
    float life;
    float t;

    public void Init(float sec)
    {
        life = Mathf.Max(0.1f, sec);
        var mr = GetComponent<MeshRenderer>();
        if (mr != null)
            mat = mr.material;
    }

    void Update()
    {
        t += Time.deltaTime;
        float u = Mathf.Clamp01(t / life);
        if (mat != null)
        {
            Color c = mat.color;
            c.a = Mathf.Lerp(0.55f, 0f, u);
            mat.color = c;
        }
        transform.localScale *= 1f + Time.deltaTime * 0.8f;
        if (u >= 1f)
            Destroy(gameObject);
    }
}
