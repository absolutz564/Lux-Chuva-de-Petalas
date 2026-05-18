using UnityEngine;

public class FallUI : MonoBehaviour
{
    [Header("Dinâmica")]
    public Vector2 velocity = Vector2.zero;   // pixels/segundo
    public float gravity = 2000f;             // pixels/segundo^2
    public float lifetime = -1f;              // >0 = destrói depois de 'lifetime' segs
    public float angularVelocity = 0f;        // graus/segundo

    private RectTransform rt;
    private float timer;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    void Update()
    {
        // aplicar gravidade (aumenta a velocidade para baixo)
        velocity.y -= gravity * Time.deltaTime;

        // mover
        rt.anchoredPosition += velocity * Time.deltaTime;

        // rotação visual
        if (angularVelocity != 0f)
            rt.Rotate(0f, 0f, angularVelocity * Time.deltaTime);

        // lifetime
        if (lifetime > 0f)
        {
            timer += Time.deltaTime;
            if (timer >= lifetime)
                Destroy(gameObject);
        }

        // destruir quando sair muito abaixo do pai/tela
        RectTransform parentRt = rt.parent as RectTransform;
        if (parentRt != null)
        {
            if (rt.anchoredPosition.y < -parentRt.rect.height / 2f - 400f) Destroy(gameObject);
        }
        else
        {
            if (rt.anchoredPosition.y < -Screen.height - 400f) Destroy(gameObject);
        }
    }
}
