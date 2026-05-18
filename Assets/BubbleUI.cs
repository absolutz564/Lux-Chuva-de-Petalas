using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;


public class BubbleUI : MonoBehaviour
{
    [Header("Refs (assign no prefab)")]
    public Image bubbleImage;       // imagem da bolha (raiz) - opcional
    public Image productImage;      // filho que acompanha a bolha (produto)

    [Header("Config")]
    public int scoreValue = 1;
    public float bubbleSpeed = 180f; // pixels/s

    [Header("Prefabs")]
    public GameObject scorePopupPrefab; // prefab do +1/+2 (UI Image/Text)
    public GameObject explosionPrefab;  // opcional: animaÿÿo de explosÿo (UI/particle)
    public Sprite spriteScoreMore1;
    public Sprite spriteScoreMore2;
    public Sprite spriteScoreMore3;
    public Sprite spriteScoreLess1;
    public Sprite spriteScoreLess2;
    public Sprite spriteScoreLess3;
    // Runtime
    private RectTransform rt;
    private RectTransform spawnParent; // painel/canvas onde as bolhas sÿo criadas

    [Header("Sprites")]
    public Sprite sprite1;
    public Sprite sprite2;

    [Header("UI Image")]
    public Image targetImage;

    // Chame esta funÿÿo para trocar o sprite aleatoriamente
    public void AssignRandomSprite()
    {
        if (targetImage == null)
        {
            Debug.LogWarning("Target Image nÿo atribuÿda!");
            return;
        }

        // Escolhe aleatoriamente entre 0 e 1
        int randomIndex = Random.Range(0, 2);

        targetImage.sprite = (randomIndex == 0) ? sprite1 : sprite2;
    }

    void Awake()
    {
        rt = GetComponent<RectTransform>();

        if (spawnParent == null && rt.parent is RectTransform p) spawnParent = p;

        // se productImage nÿo foi setado, tenta achar um filho Image (diferente da bolha)
        if (productImage == null)
        {
            var imgs = GetComponentsInChildren<Image>(true);
            foreach (var img in imgs)
            {
                if (img.gameObject != gameObject) { productImage = img; break; }
            }
        }

        AssignRandomSprite();

        // importante: enquanto o produto estiver preso ? bolha, ele N?O deve bloquear cliques
        if (productImage != null)
            productImage.raycastTarget = false;

        // a imagem da bolha precisa aceitar o raycast (para receber o clique)
        if (bubbleImage == null)
            bubbleImage = GetComponent<Image>();
    }

    void Update()
    {
        // sobe a bolha
        rt.anchoredPosition += Vector2.up * bubbleSpeed * Time.deltaTime;

        // destruir se sair da tela
        if (spawnParent != null)
        {
            float topLimit = spawnParent.rect.height / 2f + 1800f;
            if (rt.anchoredPosition.y > topLimit) Destroy(gameObject);
        }
        else
        {
            if (rt.anchoredPosition.y > Screen.height + 300f) Destroy(gameObject);
        }
    }

    void HandleTouch(Vector2 screenPosition)
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        if (results.Count > 0)
        {
            // ordena pela ordem de renderiza??o (maior sortingOrder primeiro)
            results.Sort((a, b) => b.sortingOrder.CompareTo(a.sortingOrder));

            var hit = results[0]; // agora sim, o objeto mais "na frente"

            var bubble = hit.gameObject.GetComponent<BubbleUI>();
            if (bubble != null)
            {
                bubble.BurstAndRelease();
            }
        }
    }


    // compatibilidade para o spawner
    public void Init(int value, Sprite productSprite, float speed, RectTransform optionalSpawnParent = null)
    {
        scoreValue = value;
        bubbleSpeed = speed;

        scorePopupPrefab.GetComponent<Image>().sprite = GetScoreSprite(scoreValue);
        if (optionalSpawnParent != null) spawnParent = optionalSpawnParent;

        if (productImage == null)
        {
            var imgs = GetComponentsInChildren<Image>(true);
            foreach (var img in imgs)
            {
                if (img.gameObject != gameObject) { productImage = img; break; }
            }
        }

        if (productImage != null && productSprite != null)
            productImage.sprite = productSprite;

        // garante que product n?o bloqueie cliques enquanto preso
        if (productImage != null) productImage.raycastTarget = false;
        float randomScale = Random.Range(0.2f, 0.3f);
        transform.localScale = new Vector3(randomScale, randomScale, 1f);
    }

    Sprite GetScoreSprite(int value)
    {
        if (value > 0)
        {
            switch (value)
            {
                case 1: return spriteScoreMore1;
                case 2: return spriteScoreMore2;
                case 3: return spriteScoreMore3;
            }
        }
        else if (value < 0)
        {
            switch (value)
            {
                case -1: return spriteScoreLess1;
                case -2: return spriteScoreLess2;
                case -3: return spriteScoreLess3;
            }
        }

        return null; // ou algum sprite padrÿo
    }


    //public void OnPointerClick(PointerEventData eventData)
    //{
    //    BurstAndRelease();
    //}

    public void BurstAndRelease()
    {
        // 1) adicionar pontuaÿÿo
        if (GameManagerUI.Instance != null)
            GameManagerUI.Instance.AddScore(scoreValue);

        // 4) spawn popup de pontuaÿÿo na mesma posiÿÿo e faz ele sumir em 2s
        if (scorePopupPrefab != null)
        {
            RectTransform popupParent = spawnParent ? spawnParent : (rt.parent as RectTransform);
            GameObject popup = Instantiate(scorePopupPrefab, popupParent);

            // posiciona o popup no mesmo lugar visual da bolha
            Canvas canvas = popupParent.GetComponentInParent<Canvas>();
            Camera cam = (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera) ? canvas.worldCamera : null;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, rt.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(popupParent, screenPoint, cam, out Vector2 localPoint);
            var popupRt = popup.GetComponent<RectTransform>();
            popupRt.anchoredPosition = localPoint;

            // configura queda + duraÿÿo 2s
            popup.SetActive(true); // garante que estÿ ativo
            var popupFall = popup.AddComponent<FallUI>();
            popupFall.velocity = new Vector2(0f, 300f);
            popupFall.gravity = 0f;
            popupFall.lifetime = 1f;
            popupFall.angularVelocity = 0f;
        }

        // 2) opcional: spawn de explosÿo (visual) na posiÿÿo da bolha
        if (explosionPrefab != null && spawnParent != null)
        {
            var expl = Instantiate(explosionPrefab, spawnParent);
            var explRt = expl.GetComponent<RectTransform>();
            // posiciona na mesma posiÿÿo visual da bolha
            explRt.position = rt.position;
            Destroy(expl, 0.6f);
        }
        Destroy(productImage.gameObject);
        // 3) destacar o filho (productImage) e faz?-lo cair com FallUI
        if (productImage != null)
        {
            // garante que nÿo capture cliques apÿs soltar
            productImage.raycastTarget = false;

            // guarda posiÿÿo mundial atual
            Vector3 worldPos = productImage.transform.position;

            // reparent para spawnParent (ou para o pai atual se spawnParent nulo)
            RectTransform newParent = spawnParent ? spawnParent : (rt.parent as RectTransform);
            productImage.transform.SetParent(newParent, true); // preserva posiÿÿo mundial

            // corrigir anchoredPosition para o novo parent (mais robusto)
            Canvas canvas = newParent.GetComponentInParent<Canvas>();
            Camera cam = (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera) ? canvas.worldCamera : null;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(newParent, screenPoint, cam, out Vector2 localPoint);
            productImage.rectTransform.anchoredPosition = localPoint;

            // adiciona FallUI para simular queda com forÿa (objeto pesado)
            var fall = productImage.gameObject.AddComponent<FallUI>();
            fall.enabled = true;
            fall.velocity = new Vector2(Random.Range(-50f, 50f), -900f); // velocidade inicial forte para baixo
            fall.gravity = 3000f;   // aceleraÿÿo
            fall.lifetime = 5f;     // destrÿi depois se cair fora da tela
            fall.angularVelocity = Random.Range(-120f, 120f); // rotaÿÿo opcional
        }

        // 5) destruir a bolha (o filho jÿ foi reparentado, entÿo nÿo serÿ destruÿdo)
        Destroy(gameObject);
    }
}
