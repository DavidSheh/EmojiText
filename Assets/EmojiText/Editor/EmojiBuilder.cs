#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public class EmojiBuilder
{
    [MenuItem("Tools/Emoji Build")]
    static void Build()
    {
        var textures = Selection.GetFiltered<Texture2D>(SelectionMode.DeepAssets);
        if(textures == null || textures.Length < 1)
        {
            EditorUtility.DisplayDialog("警告", "未选择目录或者所选目录中没有表情图片！", "确定");
            return;
        }
        
        string cachePath = EditorPrefs.GetString("EmojiPath");
        string path = EditorUtility.SaveFilePanelInProject("Select Save Path", "Emoji", "asset", "aaaaaaa", cachePath);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }
        EditorPrefs.SetString("EmojiPath", path);
        CreateEmojiAsset(path, textures);
    }

    static void CreateEmojiAsset(string path, Texture2D[] textures)
    {
        EmojiAsset emojiAsset = AssetDatabase.LoadAssetAtPath(path, typeof(EmojiAsset)) as EmojiAsset;
        if (null == emojiAsset)
        {
            emojiAsset = ScriptableObject.CreateInstance<EmojiAsset>();
            AssetDatabase.CreateAsset(emojiAsset, path);
        }

        string ext = Path.GetExtension(path);
        string atlasPath = path.Replace(ext, ".png");
        var atlas = CreateAtlas(textures, atlasPath);
        List<SpriteInfo> spriteInfoList = new List<SpriteInfo>();
        var totalFrame = 0;
        for (int i = 0; i < atlas.frames.Count; i++)
        {
            var frames = atlas.frames[i];
            var name = frames[0].name;
            var index = totalFrame;
            var frameCount = frames.Count;
            spriteInfoList.Add(new SpriteInfo(name, index, frameCount));
            totalFrame += frameCount;
        }
        emojiAsset.spriteInfoList = spriteInfoList;
        emojiAsset.spriteSheet = atlas.atlas;
        emojiAsset.emojiSize = atlas.emojiSize;
        emojiAsset.column = atlas.column;
        AddDefaultMaterial(emojiAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static EmojiAtlas CreateAtlas(Texture2D[] textures, string path)
    {
        var emojiAtlas = GenEmojiAtlas(textures);
        int w = emojiAtlas.atlasWidth;
        // make your new texture
        var atlas = new Texture2D(w, w, TextureFormat.RGBA32, false);
        // clear pixel
        Color32[] fillColor = atlas.GetPixels32();
        for (int i = 0; i < fillColor.Length; ++i)
        {
            fillColor[i] = Color.clear;
        }
        atlas.SetPixels32(fillColor);

        int textureWidthCounter = 0;
        int textureHeightCounter = 0;
        for (int i = 0; i < emojiAtlas.frames.Count; i++)
        {
            var frameInfos = emojiAtlas.frames[i];
            for (int j = 0; j < frameInfos.Count; j++)
            {
                var frame = frameInfos[j].texture;
                // 填充单个图片的像素到 Atlas 中
                for (int k = 0; k < frame.width; k++)
                {
                    for (int l = 0; l < frame.height; l++)
                    {
                        atlas.SetPixel(k + textureWidthCounter, l + textureHeightCounter, frame.GetPixel(k, l));
                    }
                }

                textureWidthCounter += frame.width;
                if (textureWidthCounter > atlas.width - frame.width)
                {
                    textureWidthCounter = 0;
                    textureHeightCounter += frame.height;
                }
            }
        }
        atlas.Apply();
        var tex = SaveSpriteToEditorPath(atlas, path);
        emojiAtlas.atlas = tex;
        return emojiAtlas;
    }

    static EmojiAtlas GenEmojiAtlas(Texture2D[] textures)
    {
        Dictionary<string, List<FrameInfo>> dic = new Dictionary<string, List<FrameInfo>>();
        // get all select textures
        int width = 0;
        int count = textures.Length;
        foreach (var texture in textures)
        {
            Match match = Regex.Match(texture.name, "^([a-zA-Z0-9]+)(_([0-9]+))?$");//name_idx; name
            if (!match.Success)
            {
                Debug.LogWarning(texture.name + " 不匹配命名规则，跳过.");
                continue;
            }
            string name = match.Groups[1].Value;
            if (!dic.TryGetValue(name, out List<FrameInfo> frames))
            {
                frames = frames = new List<FrameInfo>();
                dic.Add(name, frames);
            }

            if (!int.TryParse(match.Groups[3].Value, out int index))
            {
                index = 1;
            }
            frames.Add(new FrameInfo() { name = name, index = index, texture = texture });
            if(0 == width)
            {
                width = texture.width;
            }
            else if (texture.width != width)
            {
                Debug.LogError($"单个表情的大小不一致！第一个表情的大小为: {width}, 当前表情 {texture.name} 的大小为：{texture.width}");
            }
        }

        // sort frames
        List<List<FrameInfo>> frameInfos = new List<List<FrameInfo>>();
        foreach (var f in dic.Values)
        {
            f.Sort((l, r) => l.index.CompareTo(r.index));
            frameInfos.Add(f);
        }

        frameInfos.Sort((a, b) => a[0].name.CompareTo(b[0].name));
        int column = Mathf.CeilToInt(Mathf.Sqrt(count));
        int atlasWidth = column * width;
        float emojiSize = ((float)width) / atlasWidth;
        var atlas = new EmojiAtlas()
        {
            atlasWidth = atlasWidth,
            emojiSize = emojiSize,
            column = column,
            frames = frameInfos,
        };

        return atlas;
    }

    static Texture2D SaveSpriteToEditorPath(Texture2D sp, string path)
    {
        string dir = Path.GetDirectoryName(path);

        Directory.CreateDirectory(dir);

        File.WriteAllBytes(path, sp.EncodeToPNG());
        AssetDatabase.Refresh();
        AssetDatabase.SaveAssets();

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        importer.textureType = TextureImporterType.Default;
        importer.textureShape = TextureImporterShape.Texture2D;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.isReadable = false;
        importer.mipmapEnabled = false;
        
        var settingsDefault = importer.GetDefaultPlatformTextureSettings();
        settingsDefault.textureCompression = TextureImporterCompression.Uncompressed;
        settingsDefault.maxTextureSize = 2048;
        settingsDefault.format = TextureImporterFormat.RGBA32;

        var settingsAndroid = importer.GetPlatformTextureSettings("Android");
        settingsAndroid.overridden = true;
        settingsAndroid.maxTextureSize = settingsDefault.maxTextureSize;
        settingsAndroid.format = TextureImporterFormat.ASTC_RGBA_8x8;
        importer.SetPlatformTextureSettings(settingsAndroid);

        var settingsiOS = importer.GetPlatformTextureSettings("iPhone");
        settingsiOS.overridden = true;
        settingsiOS.maxTextureSize = settingsDefault.maxTextureSize;
        settingsiOS.format = TextureImporterFormat.ASTC_RGBA_8x8;
        importer.SetPlatformTextureSettings(settingsiOS);

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D)) as Texture2D;
    }

    /// <summary>
    /// Create and add new default material to emoji asset.
    /// </summary>
    /// <param name="asset"></param>
    static void AddDefaultMaterial(EmojiAsset asset)
    {
        if(null == asset)
        {
            return;
        }

        var material = asset.material;
        if (null == material)
        {
            material = new Material(Shader.Find("UI/EmojiFont"));
            asset.material = material;
            AssetDatabase.AddObjectToAsset(material, asset);
        }
        
        material.SetTexture("_EmojiTex", asset.spriteSheet);
        material.SetFloat("_EmojiSize", asset.emojiSize);
        material.SetFloat("_Column", asset.column);
        material.hideFlags = HideFlags.HideInHierarchy;
    }

    [MenuItem("GameObject/UI/EmojiText")]
    static void Create()
    {
        GameObject select = Selection.activeGameObject;
        if (select == null)
        {
            return;
        }
        RectTransform transform = select.GetComponent<RectTransform>();
        if (transform == null)
        {
            return;
        }

        GameObject obj = new GameObject("EmojiText");
        obj.transform.SetParent(transform);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(160, 30);

        obj.AddComponent<CanvasRenderer>();
        EmojiText text = obj.AddComponent<EmojiText>();
        text.text = "New EmojiText";

        Selection.activeGameObject = obj;
    }

    class EmojiAtlas
    {
        public int atlasWidth;
        public float emojiSize;
        public int column;
        public Texture2D atlas;
        public List<List<FrameInfo>> frames;
    }

    class FrameInfo
    {
        public string name;
        public int index;
        public Texture2D texture;
    }
}
#endif
