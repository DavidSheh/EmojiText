using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class EmojiText : Text, IPointerClickHandler
{
    [Header("Emoji")]
    [SerializeField]
    private EmojiAsset emojiAsset;

    static Dictionary<string, SpriteInfo> emojiData;
    readonly StringBuilder builder = new StringBuilder();
    readonly Dictionary<int, EmojiInfo> emojis = new Dictionary<int, EmojiInfo>();
    readonly UIVertex[] tempVerts = new UIVertex[4];
    readonly MatchResult matchResult = new MatchResult();
    static readonly string regexTag = "\\[([0-9A-Za-z]+)((\\|[0-9]+){0,2})(#[0-9a-f]{6})?(#[^=\\]]+)?(=[^\\]]+)?\\]";
    string outputText = "";

    #region 超链接
    readonly List<HrefInfo> hrefs = new List<HrefInfo>();
    [Serializable]
    public class HrefClickEvent : UnityEvent<string> { }
    public HrefClickEvent OnHrefClick { get; } = new HrefClickEvent();
    #endregion

    public override float preferredWidth
    {
        get
        {
            var settings = GetGenerationSettings(Vector2.zero);
            return cachedTextGeneratorForLayout.GetPreferredWidth(outputText, settings) / pixelsPerUnit;
        }
    }
    public override float preferredHeight
    {
        get
        {
            var settings = GetGenerationSettings(new Vector2(rectTransform.rect.size.x, 0.0f));
            return cachedTextGeneratorForLayout.GetPreferredHeight(outputText, settings) / pixelsPerUnit;
        }
    }
    public override string text
    {
        get { return m_Text; }

        set
        {
            ParseText(value);
            base.text = value;
        }
    }

    protected override void Awake()
    {
        base.Awake();

        // only run in playing mode
        if(!Application.isPlaying)
            return;

        if (null == emojiData && null != emojiAsset)
        {
            emojiData = new Dictionary<string, SpriteInfo>();
            foreach (var data in emojiAsset.spriteInfoList)
            {
                if (emojiData.TryGetValue(data.name, out SpriteInfo info))
                {
                    Debug.LogWarning($"key {data.name} has exist!");
                    continue;
                }
                emojiData.Add(data.name, data);
            }
        }
    }

    protected override void OnValidate()
    {
        if(null != emojiAsset)
        {
            material = emojiAsset.material;
        }
        else
        {
            material = null;
        }
    }

    protected override void OnPopulateMesh(VertexHelper toFill)
    {
        if (font == null)
            return;

        if(string.IsNullOrEmpty(m_Text))
        {
            base.OnPopulateMesh(toFill);
            return;
        }
        ParseText(m_Text);
        
        // We don't care if we the font Texture changes while we are doing our Update.
        // The end result of cachedTextGenerator will be valid for this instance.
        // Otherwise we can get issues like Case 619238.
        m_DisableFontTextureRebuiltCallback = true;

        Vector2 extents = rectTransform.rect.size;

        var settings = GetGenerationSettings(extents);
        cachedTextGenerator.Populate(outputText, settings);

        // Apply the offset to the vertices
        IList<UIVertex> verts = cachedTextGenerator.verts;
        float unitsPerPixel = 1 / pixelsPerUnit;
        //Last 4 verts are always a new line... (\n)
        int vertCount = verts.Count - 4;

        // We have no verts to process just return (case 1037923)
        if (vertCount <= 0)
        {
            toFill.Clear();
            return;
        }
        
        Vector3 repairVec = new Vector3(0, fontSize * 0.1f);
        Vector2 roundingOffset = new Vector2(verts[0].position.x, verts[0].position.y) * unitsPerPixel;
        roundingOffset = PixelAdjustPoint(roundingOffset) - roundingOffset;
        toFill.Clear();
        if (roundingOffset != Vector2.zero)
        {
            for (int i = 0; i < vertCount; ++i)
            {
                int tempVertsIndex = i & 3;
                tempVerts[tempVertsIndex] = verts[i];
                tempVerts[tempVertsIndex].position *= unitsPerPixel;
                tempVerts[tempVertsIndex].position.x += roundingOffset.x;
                tempVerts[tempVertsIndex].position.y += roundingOffset.y;
                if (tempVertsIndex == 3)
                {
                    toFill.AddUIVertexQuad(tempVerts);
                }
            }
        }
        else
        {
            Vector2 uv = Vector2.zero;
            for (int i = 0; i < vertCount; ++i)
            {
                int index = i / 4;
                int tempVertIndex = i & 3;

                if (emojis.TryGetValue(index, out EmojiInfo info))
                {
                    tempVerts[tempVertIndex] = verts[i];
                    tempVerts[tempVertIndex].position -= repairVec;
                    if (info.type == MatchType.Emoji)
                    {
                        uv.x = info.sprite.index;
                        uv.y = info.sprite.frameCount;
                        tempVerts[tempVertIndex].uv0 += uv * 10;
                    }
                    else
                    {
                        tempVerts[tempVertIndex].position = tempVerts[0].position;
                    }

                    tempVerts[tempVertIndex].position *= unitsPerPixel;
                    if (tempVertIndex == 3)
                    {
                        toFill.AddUIVertexQuad(tempVerts);
                    }
                }
                else
                {
                    tempVerts[tempVertIndex] = verts[i];
                    tempVerts[tempVertIndex].position *= unitsPerPixel;
                    if (tempVertIndex == 3)
                    {
                        toFill.AddUIVertexQuad(tempVerts);
                    }
                }
            }
            CalcBoundsInfo(toFill);
            DrawUnderline(toFill);
        }

        m_DisableFontTextureRebuiltCallback = false;
    }

    void ParseText(string mText)
    {
        if (emojiData == null || !Application.isPlaying)
        {
            outputText = mText;
            return;
        }

        builder.Length = 0;
        emojis.Clear();
        hrefs.Clear();

        MatchCollection matches = Regex.Matches(mText, regexTag);
        if (matches.Count > 0)
        {
            int textIndex = 0;
            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                matchResult.Parse(match, fontSize);

                switch (matchResult.type)
                {
                    case MatchType.Emoji:
                        {
                            if (emojiData.TryGetValue(matchResult.title, out SpriteInfo info))
                            {
                                builder.Append(mText.Substring(textIndex, match.Index - textIndex));
                                int temIndex = builder.Length;

                                builder.Append("<quad size=");
                                builder.Append(matchResult.height);
                                builder.Append(" width=");
                                builder.Append((matchResult.width * 1.0f / matchResult.height).ToString("f2"));
                                builder.Append(" />");

                                emojis.Add(temIndex, new EmojiInfo()
                                {
                                    type = MatchType.Emoji,
                                    sprite = info,
                                    width = matchResult.width,
                                    height = matchResult.height
                                });

                                if (matchResult.HasUrl)
                                {
                                    var hrefInfo = new HrefInfo()
                                    {
                                        show = false,
                                        startIndex = temIndex * 4,
                                        endIndex = temIndex * 4 + 3,
                                        url = matchResult.url,
                                        color = matchResult.GetColor(color)
                                    };
                                    hrefs.Add(hrefInfo);
                                }

                                textIndex = match.Index + match.Length;
                            }
                            break;
                        }
                    case MatchType.HyperLink:
                        {
                            builder.Append(mText.Substring(textIndex, match.Index - textIndex));
                            builder.Append("<color=");
                            builder.Append(matchResult.GetHexColor(color));
                            builder.Append(">");

                            var href = new HrefInfo
                            {
                                show = true,
                                startIndex = builder.Length * 4
                            };
                            builder.Append(matchResult.link);
                            href.endIndex = builder.Length * 4 - 1;
                            href.url = matchResult.url;
                            href.color = matchResult.GetColor(color);

                            hrefs.Add(href);
                            builder.Append("</color>");

                            textIndex = match.Index + match.Length;
                            break;
                        }
                }
            }
            builder.Append(mText.Substring(textIndex, mText.Length - textIndex));
            outputText = builder.ToString();
        }
        else
        {
            outputText = mText;
        }
    }

    /// <summary>
    /// 计算可点击的富文本部分的包围盒
    /// </summary>
    /// <param name="toFill"></param>
    void CalcBoundsInfo(VertexHelper toFill)
    {
        UIVertex vert = new UIVertex();
        for (int u = 0; u < hrefs.Count; u++)
        {
            var href = hrefs[u];
            href.boxes.Clear();
            if (href.startIndex >= toFill.currentVertCount)
                continue;

            // Add hyper text vector index to bounds
            toFill.PopulateUIVertex(ref vert, href.startIndex);
            var pos = vert.position;
            var bounds = new Bounds(pos, Vector3.zero);
            for (int i = href.startIndex, m = href.endIndex; i < m; i++)
            {
                if (i >= toFill.currentVertCount) break;

                toFill.PopulateUIVertex(ref vert, i);
                pos = vert.position;
                if (pos.x < bounds.min.x)
                {
                    //if in different lines
                    href.boxes.Add(new Rect(bounds.min, bounds.size));
                    bounds = new Bounds(pos, Vector3.zero);
                }
                else
                {
                    bounds.Encapsulate(pos); //expand bounds
                }

            }
            //add bound
            href.boxes.Add(new Rect(bounds.min, bounds.size));
        }
    }

    void DrawUnderline(VertexHelper toFill)
    {
        if (hrefs.Count <= 0)
        {
            return;
        }

        Vector2 extents = rectTransform.rect.size;
        var settings = GetGenerationSettings(extents);
        cachedTextGenerator.Populate("_", settings);
        IList<UIVertex> uList = cachedTextGenerator.verts;
        float h = uList[2].position.y - uList[1].position.y;
        Vector3[] temVecs = new Vector3[4];
        
        for (int i = 0; i < hrefs.Count; i++)
        {
            var info = hrefs[i];
            if (!info.show)
            {
                continue;
            }

            for (int j = 0; j < info.boxes.Count; j++)
            {
                if (info.boxes[j].width <= 0 || info.boxes[j].height <= 0)
                {
                    continue;
                }

                temVecs[0] = info.boxes[j].min;
                temVecs[1] = temVecs[0] + new Vector3(info.boxes[j].width, 0);
                temVecs[2] = temVecs[0] + new Vector3(info.boxes[j].width, -h);
                temVecs[3] = temVecs[0] + new Vector3(0, -h);

                for (int k = 0; k < 4; k++)
                {
                    tempVerts[k] = uList[k];
                    tempVerts[k].color = info.color;
                    tempVerts[k].position = temVecs[k];
                    tempVerts[k].uv0 = GetUnderlineCharUV();
                }

                toFill.AddUIVertexQuad(tempVerts);
            }
        }
    }

    private Vector2 GetUnderlineCharUV()
    {
        if (font.GetCharacterInfo('_', out CharacterInfo info, fontSize, fontStyle))
        {
            return (info.uvBottomLeft + info.uvBottomRight + info.uvTopLeft + info.uvTopRight) * 0.25f;
        }
        return Vector2.zero;
    }

    void IPointerClickHandler.OnPointerClick(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 lp);

        for (int h = 0; h < hrefs.Count; h++)
        {
            var hrefInfo = hrefs[h];
            var boxes = hrefInfo.boxes;
            for (var i = 0; i < boxes.Count; ++i)
            {
                if (boxes[i].Contains(lp))
                {
                    OnHrefClick.Invoke(hrefInfo.url);
                    return;
                }
            }
        }
    }

    class EmojiInfo
    {
        public MatchType type;
        public int width;
        public int height;
        public SpriteInfo sprite;
    }
    
    enum MatchType
    {
        None,
        Emoji,
        HyperLink,
    }

    class MatchResult
    {
        public MatchType type;
        public string title;
        public string url;
        public string link;
        public int height;
        public int width;
        private string strColor;
        private Color color;

        public bool HasUrl
        {
            get { return !string.IsNullOrEmpty(url); }
        }

        void Reset()
        {
            type = MatchType.None;
            title = String.Empty;
            width = 0;
            height = 0;
            strColor = string.Empty;
            url = string.Empty;
            link = string.Empty;
        }
        
        public void Parse(Match match, int fontSize)
        {
            Reset();
            if(!match.Success || match.Groups.Count != 7)
                return;
            title = match.Groups[1].Value;
            if (match.Groups[2].Success)
            {
                string v = match.Groups[2].Value;
                string[] sp = v.Split('|');
                height = sp.Length > 1 ? int.Parse(sp[1]) : fontSize;
                width = sp.Length == 3 ? int.Parse(sp[2]) : height;
            }
            else
            {
                height = fontSize;
                width = fontSize;
            }

            if (match.Groups[4].Success)
            {
                strColor = match.Groups[4].Value.Substring(1);
                strColor = "#" + strColor;
            }

            if (match.Groups[5].Success)
            {
                url = match.Groups[5].Value.Substring(1);
            }

            if (match.Groups[6].Success)
            {
                link = match.Groups[6].Value.Substring(1);
            }

            if (title.Equals("0x01")) //hyper link
            {
                if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(link))
                {
                    type = MatchType.HyperLink;
                }
            }

            if (type == MatchType.None)
            {
                type = MatchType.Emoji;
            }
        }

        public Color GetColor(Color fontColor)
        {
            if (string.IsNullOrEmpty(strColor))
                return fontColor;
            ColorUtility.TryParseHtmlString(strColor, out color);
            return color;
        }

        public string GetHexColor(Color fontColor)
        {
            if (!string.IsNullOrEmpty(strColor))
                return strColor;
            return ColorUtility.ToHtmlStringRGBA(fontColor);
        }
    }
    
    class HrefInfo
    {
        /// <summary>
        /// 是否绘制下划线
        /// </summary>
        public bool show;
        public int startIndex;
        public int endIndex;
        public Color color;
        public readonly List<Rect> boxes = new List<Rect>();
        public string url;
    }
}
