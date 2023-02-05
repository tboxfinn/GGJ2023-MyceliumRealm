using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;

public class GameManagerScript : MonoBehaviour
{
    public AudioClip[] audioClips;
     public AudioSource audioSource;
     public AudioListener audioListener;
 
     // Start is called before the first frame update
     void Start()
     {
        Time.timeScale = 1;
        audioListener = GetComponent<AudioListener>();
        audioSource = gameObject.GetComponent<AudioSource>();
     }
 
     // Update is called once per frame
     void Update()
     {
         if (!audioSource.isPlaying)
         {
             PlayRandom();
         }
     }
     void PlayRandom()
     {
        int random = UnityEngine.Random.Range(0, audioClips.Length);
        audioSource.clip = audioClips[random];
        
        audioSource.Play();
        
     
     }
}
