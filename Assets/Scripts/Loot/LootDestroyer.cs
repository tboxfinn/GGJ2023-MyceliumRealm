using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LootDestroyer : MonoBehaviour
{
    public float LifeTime = 5f;
    private float timeAlive = 0f;
    public PlayerHealth PlayerVida;

    private void Update()
    {
        timeAlive += Time.deltaTime;
        if (timeAlive >= LifeTime)
        {
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.tag == "Player")
        {
            //give player health
            Destroy(gameObject);
        }
    }
}
