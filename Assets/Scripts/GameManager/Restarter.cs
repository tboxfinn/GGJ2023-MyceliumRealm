using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Restarter : MonoBehaviour {

	public GameObject gameOverMenu, pauseMenu;

	void Start(){
		gameOverMenu.SetActive(false);
		pauseMenu.SetActive(false);
		Time.timeScale = 1;
	}

	public void EnableGameOverMenu(){
		gameOverMenu.SetActive(true);
	}
	void Update(){
		if (Input.GetKeyDown("r")){
			SceneManager.LoadScene("Lvl1");
		}
		if (Input.GetKeyDown("escape")){
			if (pauseMenu.activeSelf){
				pauseMenu.SetActive(false);
				Time.timeScale = 1;
			}
			else{
				pauseMenu.SetActive(true);
				Time.timeScale = 0;
			}
		}
	}

	public void Resume(){
		pauseMenu.SetActive(false);
		Time.timeScale = 1;
	}

	public void Restart(){
		SceneManager.LoadScene("Lvl1");
	}

	public void Quit(){
		Application.Quit();
	}

	public void MainMenu(){
		SceneManager.LoadScene("MainMenu");
		Time.timeScale = 1;
	}
}