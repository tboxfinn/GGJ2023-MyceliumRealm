using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour
{
    [SerializeField] private Transform target;
    public static event System.Action<Enemy> OnEnemyKilled;
    [SerializeField] float health, maxHealth=3f;

    [SerializeField] float moveSpeed = 5f;
    Rigidbody2D rb;
    Vector2 moveDirection;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }
    private void Start()
    {
        health = maxHealth;
        target = GameObject.FindGameObjectWithTag("Player").transform; 
    }

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
    private void Update()
    {
        var distance = Vector3.Distance(target.position, transform.position);

        if (distance < 7f)
        {
            Vector3 direction = (target.position - transform.position).normalized;
            //float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            //rb.rotation = angle;
            moveDirection = direction;
        }
        else
        {
            moveDirection = Vector2.zero;
        }

    }

    void FixedUpdate()
    {
        if (target)
        {
            rb.velocity = new Vector2(moveDirection.x * moveSpeed, moveDirection.y * moveSpeed);
        }
    }

    

   
}
