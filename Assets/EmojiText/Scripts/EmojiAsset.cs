using System.Collections.Generic;
using UnityEngine;

public class EmojiAsset : ScriptableObject
{
    public Texture spriteSheet;
    public Material material;
    public float emojiSize;
    public int column;
    public List<SpriteInfo> spriteInfoList;
}

[System.Serializable]
public class SpriteInfo
{
    public string name;
    public int index;
    public int frameCount;

    public SpriteInfo(string name, int index, int frameCount)
    {
        this.name = name;
        this.index = index;
        this.frameCount = frameCount;
    }
}
