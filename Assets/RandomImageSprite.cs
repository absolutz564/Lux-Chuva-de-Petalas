using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RandomImageSprite : MonoBehaviour
{
    public Image BgImage;
    public Sprite[] RandomSprites;
    public Image BgImage2;
    public Sprite[] RandomSprites2;
    public Image BgImage3;
    public Sprite[] RandomSprites3;

    // Start is called before the first frame update
    void Start()
    {
        int RandomIndex = Random.Range(0, RandomSprites.Length);
        BgImage.sprite = RandomSprites[RandomIndex];
        BgImage2.sprite = RandomSprites2[RandomIndex];
        BgImage3.sprite = RandomSprites3[RandomIndex];
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
