using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Particles")]
    public ParticleSystem dust;

    [SerializeField] private float moveSpeed;

    public Rigidbody2D playerRb;

    [SerializeField] private Vector2 moveDirection;

    [Header("ProyectilesToSpawn")]
    public GameObject proyectil1Prefab;


    [SerializeField] private float activeMoveSpeed;
    public float dashSpeed;

    public float dashLenght, dashCooldown, shootingCooldown;

    [SerializeField] private float dashCounter;
    [SerializeField] private float dashCoolCounter;

    public float ProjectileDistance;

    

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
                activeMoveSpeed = dashSpeed;
                dashCounter = dashLenght;
            }
        }

        if (dashCounter > 0)
        {
            dashCounter -= Time.deltaTime;

            if(dashCounter <= 0)
            {
                CreateDust();
                activeMoveSpeed = moveSpeed;
                dashCoolCounter = dashCooldown;
            }
        }

        if(dashCoolCounter > 0)
        {
            dashCoolCounter -= Time.deltaTime;
        }

        //on button stay down shoot
        if (Input.GetMouseButton(0) && shootingCooldown <= 0)
        {
            Shoot();
        }

        if (shootingCooldown > 0)
        {
            shootingCooldown -= Time.deltaTime;
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
        //Instantiate a prefab at a certain distance from the player
        Instantiate(proyectil1Prefab, transform.position + (transform.right * ProjectileDistance), transform.rotation);
        Instantiate(proyectil1Prefab, transform.position + (transform.right * -ProjectileDistance), transform.rotation);
        shootingCooldown = 3f;
        
    }

    void CreateDust()
    {
        dust.Play();
    }
    
}
