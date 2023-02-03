using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Particles")]
    public ParticleSystem dust;

    [SerializeField] private float moveSpeed;

    public Rigidbody2D playerRb;

    public Animator anim;
    [SerializeField] private Vector2 moveDirection;
    public bool dashing;

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
        dashing = false;
        anim = GetComponent<Animator>();
        activeMoveSpeed = moveSpeed;
        playerRb = GetComponent<Rigidbody2D>();
        
    }

    // Update is called once per frame
    void Update()
    {
        //INPUTS
        ProcessInputs();
        Animate();

        if (Input.GetMouseButtonDown(1))
        {
            Debug.Log("HOLA");

            Dash();

        }

        if (dashCounter > 0)
        {
            dashCounter -= Time.deltaTime;

            if(dashCounter <= 0)
            {
                activeMoveSpeed = moveSpeed;
                dashCoolCounter = dashCooldown;
                anim.SetBool("dash", false);
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

    void Dash()
    {
        if (dashCoolCounter <= 0 && dashCounter <= 0)
        {
            activeMoveSpeed = dashSpeed;
            dashCounter = dashLenght;
            anim.SetBool("dash", true);

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

    void Animate()
    {
        anim.SetFloat("AnimMoveX", moveDirection.x);
        anim.SetFloat("AnimMoveY", moveDirection.y);
        
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
