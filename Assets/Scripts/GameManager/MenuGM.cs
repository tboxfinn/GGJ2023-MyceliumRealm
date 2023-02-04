using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuGM : MonoBehaviour
{
    public GameObject optionsMenu, agradecimentosMenu;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void PlayGame(){
        SceneManager.LoadScene("Lvl1");
    }
    
    public void QuitGame(){
        Application.Quit();
    }

    public void Options(){
        optionsMenu.SetActive(true);
    }

    public void Back(){
        optionsMenu.SetActive(false);
    }

    public void Agradecimentos(){
        agradecimentosMenu.SetActive(true);
    }

    public void BackAgradecimentos(){
        agradecimentosMenu.SetActive(false);
    }
}
