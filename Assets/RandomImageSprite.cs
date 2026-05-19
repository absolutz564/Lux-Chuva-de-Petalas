using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RandomImageSprite : MonoBehaviour
{
    public Image BgImage;
    public Sprite[] RandomSprites;

    // Start is called before the first frame update
    void Start()
    {
        int RandomIndex = Random.Range(0, RandomSprites.Length);
        BgImage.sprite = RandomSprites[RandomIndex];
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
