using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProyectilMov : MonoBehaviour
{
    [SerializeField] private Transform target,player;
    
    [SerializeField] float health, maxHealth=3f;

    [SerializeField] float moveSpeed = 4f;
    Rigidbody2D rb;
    Vector2 moveDirection;
    public float PlayerDistance;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }
    private void Start()
    {
        target = GameObject.FindGameObjectWithTag("Enemy").transform; 
        player = GameObject.FindGameObjectWithTag("Player").transform;
    }

    
    private void Update()
    {
        var distance = Vector3.Distance(target.position, transform.position);
        var distance2 = Vector3.Distance(player.position, transform.position);

        if (distance < 4f)
        {
            Vector3 direction = (target.position - transform.position).normalized;
            //float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            //rb.rotation = angle;
            moveDirection = direction;
        }
        else if (distance2 < 6f)
        {
            Vector3 direction = (player.position - transform.position).normalized;
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
        }else if (player)
        {
            rb.velocity = new Vector2(moveDirection.x * moveSpeed, moveDirection.y * moveSpeed);
        }
        else
        {
            rb.velocity = Vector2.zero;
        }
    }
}
