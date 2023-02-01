using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class change : MonoBehaviour
{
    // Start is called before the first frame update
    public void Scene1()
    {
        SceneManager.LoadScene("Menu");
    }
    public void Scene2()
    {
        SceneManager.LoadScene("Main");
    }
    public GameObject lol;

    public void pausa()
    {
        Time.timeScale = 0;
        lol.SetActive(true);
    }
    public void despausa()
    {
        lol.SetActive(false);
        Time.timeScale = 1;
    }

}
