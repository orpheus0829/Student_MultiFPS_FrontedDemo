using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.TextCore;

public static class KillFeedSpriteAssetBuilder
{
    private const string WeaponIconFolder = "Assets/Resource/UI/WeaponSilhouettes";
    private const string KillIconFolder = "Assets/Resource/UI/KillIcons";
    private const string OutputFolder = "Assets/Resource/UI/KillFeed";
    private const string AtlasPath = OutputFolder + "/KillFeedAtlas.png";
    private const string SpriteAssetPath = OutputFolder + "/KillFeedSpriteAsset.asset";

    private const int AtlasColumns = 4;
    private const int MinimumAtlasRows = 8;
    private const int CellWidth = 512;
    private const int CellHeight = 256;
    private const int WeaponWidth = 448;
    private const int WeaponHeight = 192;
    private const int SkullSize = 192;
    private const int SpritePadding = 8;
    private const byte AlphaThreshold = 2;

    [MenuItem("工具/Gun Demo/UI/重建击杀图标 TMP 图集")]
    public static void RebuildFromMenu()
    {
        KillFeedSpriteBuildReport report = Rebuild();
        EditorUtility.DisplayDialog(
            report.Success ? "击杀图标图集已更新" : "击杀图标图集更新失败",
            report.Message,
            "确定");
    }

    public static KillFeedSpriteBuildReport Rebuild()
    {
        KillFeedSpriteBuildReport report = new KillFeedSpriteBuildReport();
        try
        {
            List<IconSource> sources = CollectSources(report);
            if (!report.Success)
            {
                Debug.LogError(report.Message);
                return report;
            }

            EnsureFolder(OutputFolder);
            List<AtlasSlice> slices = BuildAtlas(sources, report);
            ConfigureAtlasImporter(slices, report.AtlasWidth, report.AtlasHeight);
            CreateOrUpdateSpriteAsset(slices);
            AssetDatabase.SaveAssets();

            report.Success = true;
            report.IconCount = sources.Count;
            report.Message = "已生成 " + report.WeaponIconCount + " 个武器图标和 " +
                report.KillIconCount + " 个击杀图标，共 " + report.IconCount +
                " 个 TMP Sprite。\n图集：" + AtlasPath +
                "\nTMP Sprite Asset：" + SpriteAssetPath;
            Debug.Log("[KillFeedSpriteAssetBuilder] " + report.Message);
        }
        catch (Exception exception)
        {
            report.Success = false;
            report.Message = "重建击杀图标图集失败：" + exception.Message;
            Debug.LogException(exception);
        }

        return report;
    }

    private static List<IconSource> CollectSources(KillFeedSpriteBuildReport report)
    {
        List<IconSource> sources = new List<IconSource>();
        Dictionary<string, string> weaponTokens = CollectWeaponTokens();
        string[] weaponGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { WeaponIconFolder });
        List<string> weaponPaths = ConvertToSortedPaths(weaponGuids);
        HashSet<string> tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string path in weaponPaths)
        {
            if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string token;
            if (!weaponTokens.TryGetValue(path, out token))
            {
                token = "weapon." + NormalizeSlug(Path.GetFileNameWithoutExtension(path)) + ".silhouette";
            }

            AddSource(sources, tokens, path, token, true);
            report.WeaponIconCount++;
        }

        string whiteSkullPath = KillIconFolder + "/KillSkull_White.png";
        string redSkullPath = KillIconFolder + "/KillSkull_Red.png";
        AddRequiredKillIcon(sources, tokens, whiteSkullPath, "skull_white", report);
        AddRequiredKillIcon(sources, tokens, redSkullPath, "skull_red", report);

        if (report.WeaponIconCount == 0)
        {
            report.Success = false;
            report.Message = "没有在 " + WeaponIconFolder + " 找到武器剪影 PNG。";
            return sources;
        }

        if (report.KillIconCount != 2)
        {
            report.Success = false;
            report.Message = "击杀图标不完整，需要 KillSkull_White.png 和 KillSkull_Red.png。";
            return sources;
        }

        report.Success = true;
        return sources;
    }

    private static Dictionary<string, string> CollectWeaponTokens()
    {
        Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string[] dataGuids = AssetDatabase.FindAssets("t:WeaponDefinitionSO", new[] { "Assets/SO/Weapons" });
        foreach (string guid in dataGuids)
        {
            string dataPath = AssetDatabase.GUIDToAssetPath(guid);
            UnityEngine.Object data = AssetDatabase.LoadMainAssetAtPath(dataPath);
            if (!data)
            {
                continue;
            }

            SerializedObject serializedData = new SerializedObject(data);
            SerializedProperty silhouetteId = serializedData.FindProperty("_basic._silhouetteResourceId");
            if (silhouetteId == null || string.IsNullOrWhiteSpace(silhouetteId.stringValue))
            {
                continue;
            }

            string fileName = Path.GetFileNameWithoutExtension(dataPath);
            string iconPath = WeaponIconFolder + "/" + fileName + ".png";
            result[iconPath] = silhouetteId.stringValue;
        }

        return result;
    }

    private static List<string> ConvertToSortedPaths(string[] guids)
    {
        List<string> paths = new List<string>(guids.Length);
        foreach (string guid in guids)
        {
            paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        }

        paths.Sort(StringComparer.OrdinalIgnoreCase);
        return paths;
    }

    private static void AddRequiredKillIcon(
        List<IconSource> sources,
        HashSet<string> tokens,
        string path,
        string token,
        KillFeedSpriteBuildReport report)
    {
        if (!File.Exists(GetAbsolutePath(path)))
        {
            return;
        }

        AddSource(sources, tokens, path, token, false);
        report.KillIconCount++;
    }

    private static void AddSource(
        List<IconSource> sources,
        HashSet<string> tokens,
        string path,
        string token,
        bool isWeapon)
    {
        if (string.IsNullOrWhiteSpace(token) || !tokens.Add(token))
        {
            throw new InvalidOperationException("击杀图标名称为空或重复：" + token);
        }

        sources.Add(new IconSource(path, token, isWeapon));
    }

    private static List<AtlasSlice> BuildAtlas(
        List<IconSource> sources,
        KillFeedSpriteBuildReport report)
    {
        int requiredRows = Mathf.CeilToInt(sources.Count / (float)AtlasColumns);
        int atlasRows = Mathf.NextPowerOfTwo(Mathf.Max(MinimumAtlasRows, requiredRows));
        int atlasWidth = AtlasColumns * CellWidth;
        int atlasHeight = atlasRows * CellHeight;
        Color32[] atlasPixels = new Color32[atlasWidth * atlasHeight];
        List<AtlasSlice> slices = new List<AtlasSlice>(sources.Count);

        for (int i = 0; i < sources.Count; i++)
        {
            IconSource source = sources[i];
            Texture2D texture = LoadTexture(source.Path);
            try
            {
                RectInt sourceBounds = FindVisibleBounds(texture);
                int maximumWidth = source.IsWeapon ? WeaponWidth : SkullSize;
                int maximumHeight = source.IsWeapon ? WeaponHeight : SkullSize;
                Vector2Int targetSize = GetFittedSize(sourceBounds.size, maximumWidth, maximumHeight);
                int column = i % AtlasColumns;
                int rowFromTop = i / AtlasColumns;
                int cellX = column * CellWidth;
                int cellY = atlasHeight - ((rowFromTop + 1) * CellHeight);
                int targetX = cellX + ((CellWidth - targetSize.x) / 2);
                int targetY = cellY + ((CellHeight - targetSize.y) / 2);

                CopyScaled(texture, sourceBounds, atlasPixels, atlasWidth, targetX, targetY, targetSize);
                Rect spriteRect = new Rect(
                    targetX - SpritePadding,
                    targetY - SpritePadding,
                    targetSize.x + (SpritePadding * 2),
                    targetSize.y + (SpritePadding * 2));
                slices.Add(new AtlasSlice(source.Token, spriteRect));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        Texture2D atlas = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, false, false);
        atlas.name = "KillFeedAtlas";
        atlas.SetPixels32(atlasPixels);
        atlas.Apply(false, false);
        File.WriteAllBytes(GetAbsolutePath(AtlasPath), atlas.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(atlas);
        AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        report.AtlasWidth = atlasWidth;
        report.AtlasHeight = atlasHeight;
        return slices;
    }

    private static Texture2D LoadTexture(string path)
    {
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
        texture.name = Path.GetFileNameWithoutExtension(path);
        if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(GetAbsolutePath(path)), false))
        {
            UnityEngine.Object.DestroyImmediate(texture);
            throw new InvalidOperationException("无法读取图标：" + path);
        }

        return texture;
    }

    private static RectInt FindVisibleBounds(Texture2D texture)
    {
        Color32[] pixels = texture.GetPixels32();
        int minimumX = texture.width;
        int minimumY = texture.height;
        int maximumX = -1;
        int maximumY = -1;

        for (int y = 0; y < texture.height; y++)
        {
            int row = y * texture.width;
            for (int x = 0; x < texture.width; x++)
            {
                if (pixels[row + x].a <= AlphaThreshold)
                {
                    continue;
                }

                minimumX = Mathf.Min(minimumX, x);
                minimumY = Mathf.Min(minimumY, y);
                maximumX = Mathf.Max(maximumX, x);
                maximumY = Mathf.Max(maximumY, y);
            }
        }

        if (maximumX < minimumX || maximumY < minimumY)
        {
            throw new InvalidOperationException("图标没有可见像素：" + texture.name);
        }

        return new RectInt(
            minimumX,
            minimumY,
            maximumX - minimumX + 1,
            maximumY - minimumY + 1);
    }

    private static Vector2Int GetFittedSize(Vector2Int sourceSize, int maximumWidth, int maximumHeight)
    {
        float scale = Mathf.Min(
            maximumWidth / (float)sourceSize.x,
            maximumHeight / (float)sourceSize.y);
        return new Vector2Int(
            Mathf.Max(1, Mathf.RoundToInt(sourceSize.x * scale)),
            Mathf.Max(1, Mathf.RoundToInt(sourceSize.y * scale)));
    }

    private static void CopyScaled(
        Texture2D source,
        RectInt sourceBounds,
        Color32[] destination,
        int destinationWidth,
        int destinationX,
        int destinationY,
        Vector2Int destinationSize)
    {
        Color32[] sourcePixels = source.GetPixels32();
        for (int y = 0; y < destinationSize.y; y++)
        {
            float sourceY = sourceBounds.y + (((y + 0.5f) / destinationSize.y) * sourceBounds.height) - 0.5f;
            for (int x = 0; x < destinationSize.x; x++)
            {
                float sourceX = sourceBounds.x + (((x + 0.5f) / destinationSize.x) * sourceBounds.width) - 0.5f;
                Color32 color = SampleBilinear(sourcePixels, source.width, source.height, sourceX, sourceY);
                int targetIndex = (destinationY + y) * destinationWidth + destinationX + x;
                destination[targetIndex] = color;
            }
        }
    }

    private static Color32 SampleBilinear(
        Color32[] pixels,
        int width,
        int height,
        float x,
        float y)
    {
        int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, width - 1);
        int y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, height - 1);
        int x1 = Mathf.Min(x0 + 1, width - 1);
        int y1 = Mathf.Min(y0 + 1, height - 1);
        float tx = Mathf.Clamp01(x - x0);
        float ty = Mathf.Clamp01(y - y0);
        Color bottom = Color.Lerp(pixels[(y0 * width) + x0], pixels[(y0 * width) + x1], tx);
        Color top = Color.Lerp(pixels[(y1 * width) + x0], pixels[(y1 * width) + x1], tx);
        return Color.Lerp(bottom, top, ty);
    }

    private static void ConfigureAtlasImporter(List<AtlasSlice> slices, int atlasWidth, int atlasHeight)
    {
        TextureImporter importer = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
        if (!importer)
        {
            throw new InvalidOperationException("无法取得图集导入设置：" + AtlasPath);
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.sRGBTexture = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(atlasWidth, atlasHeight));
        importer.SaveAndReimport();

        SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null)
        {
            throw new InvalidOperationException("当前 Unity 版本无法编辑图集 Sprite 数据。");
        }

        provider.InitSpriteEditorDataProvider();
        Dictionary<string, GUID> existingIds = new Dictionary<string, GUID>(StringComparer.Ordinal);
        SpriteRect[] existingRects = provider.GetSpriteRects();
        foreach (SpriteRect existingRect in existingRects)
        {
            existingIds[existingRect.name] = existingRect.spriteID;
        }

        SpriteRect[] spriteRects = new SpriteRect[slices.Count];
        List<SpriteNameFileIdPair> namePairs = new List<SpriteNameFileIdPair>(slices.Count);
        for (int i = 0; i < slices.Count; i++)
        {
            AtlasSlice slice = slices[i];
            GUID spriteId;
            if (!existingIds.TryGetValue(slice.Token, out spriteId))
            {
                spriteId = GUID.Generate();
            }

            SpriteRect spriteRect = new SpriteRect
            {
                name = slice.Token,
                rect = slice.Rect,
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                spriteID = spriteId
            };
            spriteRects[i] = spriteRect;
            namePairs.Add(new SpriteNameFileIdPair(slice.Token, spriteId));
        }

        provider.SetSpriteRects(spriteRects);
        ISpriteNameFileIdDataProvider nameProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (nameProvider != null)
        {
            nameProvider.SetNameFileIdPairs(namePairs);
        }
        provider.Apply();
        AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
    }

    private static void CreateOrUpdateSpriteAsset(List<AtlasSlice> slices)
    {
        Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        if (!atlas)
        {
            throw new InvalidOperationException("图集导入后无法加载：" + AtlasPath);
        }

        Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        UnityEngine.Object[] importedAssets = AssetDatabase.LoadAllAssetsAtPath(AtlasPath);
        foreach (UnityEngine.Object importedAsset in importedAssets)
        {
            Sprite sprite = importedAsset as Sprite;
            if (sprite)
            {
                sprites[sprite.name] = sprite;
            }
        }

        TMP_SpriteAsset spriteAsset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(SpriteAssetPath);
        if (!spriteAsset)
        {
            spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            spriteAsset.name = "KillFeedSpriteAsset";
            AssetDatabase.CreateAsset(spriteAsset, SpriteAssetPath);
        }

        spriteAsset.spriteSheet = atlas;
        spriteAsset.hashCode = TMP_TextUtilities.GetSimpleHashCode(spriteAsset.name);
        spriteAsset.spriteGlyphTable.Clear();
        spriteAsset.spriteCharacterTable.Clear();

        for (int i = 0; i < slices.Count; i++)
        {
            AtlasSlice slice = slices[i];
            Sprite sprite;
            if (!sprites.TryGetValue(slice.Token, out sprite))
            {
                throw new InvalidOperationException("图集中缺少 Sprite：" + slice.Token);
            }

            TMP_SpriteGlyph glyph = new TMP_SpriteGlyph(
                (uint)i,
                new GlyphMetrics(
                    sprite.rect.width,
                    sprite.rect.height,
                    0f,
                    sprite.rect.height - sprite.pivot.y,
                    sprite.rect.width),
                new GlyphRect(sprite.rect),
                1f,
                0,
                sprite);
            TMP_SpriteCharacter character = new TMP_SpriteCharacter(0xFFFE, spriteAsset, glyph);
            character.name = slice.Token;
            character.scale = 1f;
            spriteAsset.spriteGlyphTable.Add(glyph);
            spriteAsset.spriteCharacterTable.Add(character);
        }

        ConfigureSpriteMaterial(spriteAsset, atlas);
        SerializedObject serializedAsset = new SerializedObject(spriteAsset);
        SerializedProperty version = serializedAsset.FindProperty("m_Version");
        if (version != null)
        {
            version.stringValue = "1.1.0";
            serializedAsset.ApplyModifiedPropertiesWithoutUndo();
        }

        spriteAsset.UpdateLookupTables();
        EditorUtility.SetDirty(spriteAsset);
        TMPro_EventManager.ON_SPRITE_ASSET_PROPERTY_CHANGED(true, spriteAsset);
    }

    private static void ConfigureSpriteMaterial(TMP_SpriteAsset spriteAsset, Texture2D atlas)
    {
        ShaderUtilities.GetShaderPropertyIDs();
        Material material = spriteAsset.material;
        if (!material)
        {
            Shader shader = Shader.Find("TextMeshPro/Sprite");
            if (!shader)
            {
                throw new InvalidOperationException("找不到 TextMeshPro/Sprite Shader，请确认 TMP 基础资源可用。");
            }

            material = new Material(shader);
            material.name = spriteAsset.name + " Material";
            AssetDatabase.AddObjectToAsset(material, spriteAsset);
            spriteAsset.material = material;
        }

        material.SetTexture(ShaderUtilities.ID_MainTex, atlas);
        spriteAsset.materialHashCode = TMP_TextUtilities.GetSimpleHashCode(material.name);
        EditorUtility.SetDirty(material);
    }

    private static string NormalizeSlug(string value)
    {
        value = value.Trim().ToLowerInvariant();
        char[] result = new char[value.Length];
        int count = 0;
        bool lastWasSeparator = false;
        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            if (char.IsLetterOrDigit(character))
            {
                result[count++] = character;
                lastWasSeparator = false;
            }
            else if (count > 0 && !lastWasSeparator)
            {
                result[count++] = '-';
                lastWasSeparator = true;
            }
        }

        if (count > 0 && result[count - 1] == '-')
        {
            count--;
        }

        return new string(result, 0, count);
    }

    private static void EnsureFolder(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }

    private static string GetAbsolutePath(string assetPath)
    {
        string projectFolder = Path.GetDirectoryName(Application.dataPath);
        return Path.Combine(projectFolder, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }

    private sealed class IconSource
    {
        public string Path { get; private set; }
        public string Token { get; private set; }
        public bool IsWeapon { get; private set; }

        public IconSource(string path, string token, bool isWeapon)
        {
            Path = path;
            Token = token;
            IsWeapon = isWeapon;
        }
    }

    private sealed class AtlasSlice
    {
        public string Token { get; private set; }
        public Rect Rect { get; private set; }

        public AtlasSlice(string token, Rect rect)
        {
            Token = token;
            Rect = rect;
        }
    }
}

[Serializable]
public sealed class KillFeedSpriteBuildReport
{
    public bool Success;
    public int IconCount;
    public int WeaponIconCount;
    public int KillIconCount;
    public int AtlasWidth;
    public int AtlasHeight;
    public string Message;
}
