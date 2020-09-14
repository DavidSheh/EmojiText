#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        var count = atlas.frames.Count;
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
                // 填充单个图片的像素到 Atlas 中
                for (int k = 0; k < frameInfos[j].texture.width; k++)
                {
                    for (int l = 0; l < frameInfos[j].texture.height; l++)
                    {
                        atlas.SetPixel(k + textureWidthCounter, l + textureHeightCounter, frameInfos[j].texture.GetPixel(k, l));
                    }
                }

                textureWidthCounter += frameInfos[j].texture.width;
                if (textureWidthCounter >= atlas.width)
                {
                    textureWidthCounter = 0;
                    textureHeightCounter += frameInfos[j].texture.height;
                }
            }
        }

        var tex = SaveSpriteToEditorPath(atlas, path);
        emojiAtlas.atlas = tex;
        return emojiAtlas;
    }

    static EmojiAtlas GenEmojiAtlas(Texture2D[] textures)
    {
        Dictionary<string, List<FrameInfo>> dic = new Dictionary<string, List<FrameInfo>>();
        // get all select textures
        int width = 0;
        int totalSize = 0;
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
            if (texture.width > width)
            {
                width = texture.width;
            }
            totalSize += texture.width * texture.height;
        }

        // sort frames
        List<List<FrameInfo>> frameInfos = new List<List<FrameInfo>>();
        foreach (var f in dic.Values)
        {
            f.Sort((l, r) => l.index.CompareTo(r.index));
            frameInfos.Add(f);
        }

        frameInfos.Sort((a, b) => a[0].name.CompareTo(b[0].name));

        int atlasWidth = CalcAtlasWidth(totalSize);
        if(atlasWidth < 2)
        {
            Debug.LogError("计算表情图集宽度出错");
            return null;
        }
        int column = atlasWidth / width;
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

    static int CalcAtlasWidth(int totalSize)
    {
        // 计算最终生成的整张图的最小长宽，默认图集为最小2的N次方正方形
        int power = Mathf.NextPowerOfTwo(totalSize);
        int w = Mathf.CeilToInt(Mathf.Sqrt(power));
        w = Mathf.NextPowerOfTwo(w);
        return w;
    }

    static Texture2D SaveSpriteToEditorPath(Texture2D sp, string path)
    {
        string dir = Path.GetDirectoryName(path);

        Directory.CreateDirectory(dir);

        File.WriteAllBytes(path, sp.EncodeToPNG());
        AssetDatabase.Refresh();
        AssetDatabase.SaveAssets();

        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        ti.textureType = TextureImporterType.Default;
        ti.alphaIsTransparency = true;
        ti.npotScale = TextureImporterNPOTScale.ToNearest;
        ti.isReadable = false;
        ti.mipmapEnabled = false;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        EditorUtility.SetDirty(ti);
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D)) as Texture2D;
    }

    /// <summary>
    /// Create and add new default material to emoji asset.
    /// </summary>
    /// <param name="asset"></param>
    static void AddDefaultMaterial(EmojiAsset asset)
    {
        Material material = new Material(Shader.Find("UI/EmojiFont"));
        material.SetTexture("_EmojiTex", asset.spriteSheet);
        material.SetFloat("_EmojiSize", asset.emojiSize);
        material.SetFloat("_Column", asset.column);
        asset.material = material;
        material.hideFlags = HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(material, asset);
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
