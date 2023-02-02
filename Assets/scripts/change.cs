using System.Collections;
using System.Collections.Generic;
using System.Net.Security;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class change : MonoBehaviour
{   public GameObject menuPausa;
    public GameObject botonPausa;
    public GameObject texto;
    // Start is called before the first frame update
    public void Scene1()
    {
        SceneManager.LoadScene("Menu");
    }
    public void Scene2()
    {
        SceneManager.LoadScene("Main");
    }
 

    public void pausa()
    {
        Time.timeScale = 0;
        botonPausa.SetActive(false);
        menuPausa.SetActive(true);
    }
    public void despausa()
    {
        menuPausa.SetActive(false);
        botonPausa.SetActive(true);
        Time.timeScale = 1;
    }
    public void popUp()
    {
        StartCoroutine(waiter());
    }

    IEnumerator waiter()
    {
        texto.SetActive(true);

        yield return new WaitForSecondsRealtime(2);

        texto.SetActive(false);
 
    }

}

