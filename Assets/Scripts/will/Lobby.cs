using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Lobby : MonoBehaviour
{
    [SerializeField] private Image black;

    public void StartGame()
    {
        StartCoroutine(StartGameCoroutine());
    }

    private IEnumerator StartGameCoroutine()
    {

        while (black.color.a < 1)
        {
            Color color = black.color;
            color.a += 0.05f;
            black.color = color;
            yield return new WaitForSeconds(0.05f);
        }
        SceneManager.LoadScene("FightScene");
    }

    public void Exit()
    {
        Application.Quit();
    }
}
