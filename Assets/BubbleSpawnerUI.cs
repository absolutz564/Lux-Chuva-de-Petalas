using UnityEngine;

[System.Serializable]
public class ProductUI
{
    public string name;
    public Sprite sprite;
    public int score;
}

public class BubbleSpawnerUI : MonoBehaviour
{
    public ProductUI[] products;
    public GameObject bubblePrefab;
    public RectTransform spawnArea;
    public GameManagerUI gameManager;

    public float spawnInterval = 1f;
    private float timer;

    void Update()
    {
        if (GameManagerUI.Instance == null || !GameManagerUI.Instance.gameActive) return;

        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer = 0f;
            SpawnBubble();
        }
    }

    void SpawnBubble()
    {
        ProductUI product = products[Random.Range(0, products.Length)];

        GameObject bubble = Instantiate(bubblePrefab, spawnArea);
        RectTransform rt = bubble.GetComponent<RectTransform>();

        // posi��o aleat�ria na largura da tela
        float randomX = Random.Range(-spawnArea.rect.width / 2f, spawnArea.rect.width / 2f);
        rt.anchoredPosition = new Vector2(randomX, -spawnArea.rect.height / 2f - 100f);

        if (gameManager.score >= 30)
        {
            bubble.GetComponent<BubbleUI>().Init(product.score, product.sprite, Random.Range(650f, 950f));

        }
        else
        {
            bubble.GetComponent<BubbleUI>().Init(product.score, product.sprite, Random.Range(250f, 750f));
        }
    }
}
