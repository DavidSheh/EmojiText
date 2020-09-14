using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Example : MonoBehaviour
{
    public EmojiText emojiText;

    // Start is called before the first frame update
    void Start()
    {
        emojiText.OnHrefClick.AddListener(OnHrefClick);
    }
    
    void OnHrefClick(string msg)
    {
        Debug.Log(msg);
    }
}
