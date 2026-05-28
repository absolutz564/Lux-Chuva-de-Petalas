using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class LoadSceneAfterDelay : MonoBehaviour
{
    [Header("Config")]
    public float delaySeconds = 8f; // tempo de espera
    public int sceneIndex = 0;      // �ndice da cena a ser carregada
    public bool isLoadAfterDelay = false;

    private void Start()
    {
        if (isLoadAfterDelay) {
            StartCoroutine(LoadSceneCoroutine());
        }
    }

    public void LoadNextScene() {
        SceneManager.LoadScene(sceneIndex);            // carrega a cena
    }

    private IEnumerator LoadSceneCoroutine()
    {
        yield return new WaitForSeconds(delaySeconds); // espera
        SceneManager.LoadScene(sceneIndex);            // carrega a cena
    }
}
