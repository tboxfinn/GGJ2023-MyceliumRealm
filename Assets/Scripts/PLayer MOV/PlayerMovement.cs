using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed;

    public Rigidbody2D playerRb;

    private Vector2 moveDirection;

    [Header("ProyectilesToSpawn")]
    public GameObject proyectil1Prefab;


    [SerializeField] private float activeMoveSpeed;
    public float dashSpeed;

    public float dashLenght, dashCooldown;

    [SerializeField] private float dashCounter;
    [SerializeField] private float dashCoolCounter;

    void Start()
    {
        activeMoveSpeed = moveSpeed;
        playerRb = GetComponent<Rigidbody2D>();
    }

    // Update is called once per frame
    void Update()
    {
        //INPUTS
        ProcessInputs();

        if (Input.GetMouseButtonDown(1))
        {
            Debug.Log("HOLA");

            if (dashCoolCounter <= 0 && dashCounter <= 0)
            {
                activeMoveSpeed= dashSpeed;
                dashCounter = dashLenght;
            }
        }

        if (dashCounter > 0)
        {
            dashCounter -= Time.deltaTime;

            if(dashCounter <= 0)
            {
                activeMoveSpeed = moveSpeed;
                dashCoolCounter = dashCooldown;
            }
        }

        if(dashCoolCounter > 0)
        {
            dashCoolCounter -= Time.deltaTime;
        }

        if (Input.GetMouseButtonDown(0))
        {
            //Intancia Loot, para elk enemigo
            //GetComponent<LootBag>().InstantiateLoot(transform.position);

            Shoot();
        }

    }

    void ProcessInputs()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        moveDirection = new Vector2(moveX, moveY).normalized;
        
        moveDirection.Normalize();
    }

    void FixedUpdate()
    {
        //CALCULAR FISICAS
        Move();

         
    }

    void Move()
    {
        playerRb.velocity = new Vector2(moveDirection.x * activeMoveSpeed, moveDirection.y * activeMoveSpeed);
    }
    
    public void Shoot()
    {
        
    }
    
}
