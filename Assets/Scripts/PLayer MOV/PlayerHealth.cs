using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public static event Action OnPLayerDamaged;
    public static event Action OnPlayerDeath;

    public float health, maxHealth;

    public Restarter restarter;

    public static object Instance { get; internal set; }

    private void Start(){
        health = maxHealth;
    }

    public void TakeDamage(float amount){
        health -= amount;
        OnPLayerDamaged?.Invoke();

        if(health <= 0){
            health = 0;
            restarter.EnableGameOverMenu();
            Debug.Log("Player is dead");
            OnPlayerDeath?.Invoke();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.tag == "Loot"&& health < maxHealth)
        {
            health += .5f;
        }
    }
}
