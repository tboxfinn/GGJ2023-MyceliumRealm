using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class change : MonoBehaviour
{
    // Start is called before the first frame update
    public void Scene1()
    {
        SceneManager.LoadScene("¨Menu");
    }
    public void Scene2()
    {
        SceneManager.LoadScene("Main");
    }
}
