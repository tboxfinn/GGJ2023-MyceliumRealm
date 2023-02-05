using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BosHealth : MonoBehaviour
{
    public static event System.Action<BosHealth> OnEnemyKilled;
    [SerializeField] float health, maxHealth=3f;
    // Start is called before the first frame update
    void Start()
    {
        health = maxHealth;
    }

    // Update is called once per frame
    public void TakeDamage(float damageAmount)
    {
        health -= damageAmount;
        if(health <= 0)
        {
            GetComponent<LootBag>().InstantiateLoot(transform.position);
            Destroy(gameObject);
            OnEnemyKilled?.Invoke(this);
        }
    }
}
