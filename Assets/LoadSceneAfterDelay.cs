using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class LoadSceneAfterDelay : MonoBehaviour
{
    [Header("Config")]
    public float delaySeconds = 8f; // tempo de espera
    public int sceneIndex = 0;      // índice da cena a ser carregada

    private void Start()
    {
        StartCoroutine(LoadSceneCoroutine());
    }

    private IEnumerator LoadSceneCoroutine()
    {
        yield return new WaitForSeconds(delaySeconds); // espera
        SceneManager.LoadScene(sceneIndex);            // carrega a cena
    }
}
