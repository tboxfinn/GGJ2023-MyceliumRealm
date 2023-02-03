using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private Transform player, targetEnemy;
    [SerializeField] float followRange = 6f;
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] Vector3 followOffset;
    Rigidbody2D rb;
    Vector2 moveDirection;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
    }

    private void Update()
    {
        float distanceToPlayer = Vector3.Distance(player.position, transform.position);
        float distanceToEnemy = float.MaxValue;

        if (targetEnemy)
        {
            distanceToEnemy = Vector3.Distance(targetEnemy.position, transform.position);
        }

        if (distanceToEnemy < followRange)
        {
            Vector3 direction = (targetEnemy.position - transform.position).normalized;
            moveDirection = direction;
        }
        else if (distanceToPlayer < followRange)
        {
            player = GameObject.FindGameObjectWithTag("Player").transform;
            Vector3 direction = (player.position + followOffset - transform.position).normalized;
            moveDirection = direction;
        }
        else
        {
            moveDirection = Vector2.zero;
        }
    }

    void FixedUpdate()
    {
        rb.velocity = new Vector2(moveDirection.x * moveSpeed, moveDirection.y * moveSpeed);
    }

    public void SetTargetEnemy(Transform enemy)
    {
        targetEnemy = enemy;
    }
}