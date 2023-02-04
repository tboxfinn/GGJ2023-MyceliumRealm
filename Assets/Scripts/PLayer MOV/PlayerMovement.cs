using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] Camera cam;

    [Header("Particles")]
    public ParticleSystem dust;
    public ParticleSystem WalkingDust;

    public bool Corrutina = false;

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
    [SerializeField] Transform SpawnPoint1, SpawnPoint2;

    Vector3 mousePos;
    Vector3 targetPos;
    [SerializeField] float threshold;

    
    void OnEnable(){
        PlayerHealth.OnPlayerDeath += DisablePlayerMovement;
    }

    void OnDisable(){
        PlayerHealth.OnPlayerDeath -= DisablePlayerMovement;
    }

    
    void Start()
    {
        dashing = false;
        anim = GetComponent<Animator>();
        activeMoveSpeed = moveSpeed;
        playerRb = GetComponent<Rigidbody2D>();
        EnablePlayerMovement();
        
    }

    // Update is called once per frame
    void Update()
    {
        

        targetPos = (transform.position + mousePos)/2f;
        mousePos = cam.ScreenToWorldPoint(Input.mousePosition);

        

        targetPos.x = Mathf.Clamp(targetPos.x, -threshold + transform.position.x, threshold + transform.position.x); //limita la distancia
        targetPos.y = Mathf.Clamp(targetPos.y, -threshold + transform.position.y, threshold + transform.position.y);

        if (moveDirection != Vector2.zero&& !WalkingDust.isPlaying)
        {
            WalkingDust.Play();
        }
        else if(WalkingDust.isPlaying && moveDirection == Vector2.zero)
        {
            WalkingDust.Stop();
        }

        if (Corrutina==false)
        {
            StopAllCoroutines();
            
        }

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
            StartCoroutine(Shoot());
            shootingCooldown = 3f;
            Corrutina = true;
            DisablePlayerMovement();
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
        //activate dust particle
        
    }

    void Animate()
    {
        anim.SetFloat("AnimMoveX", moveDirection.x);
        anim.SetFloat("AnimMoveY", moveDirection.y);
        
    }
    


    IEnumerator Shoot()
    {
        //instantiate dust particle then destroy it
        
        yield return new WaitForSeconds(1f);
        Instantiate(proyectil1Prefab, SpawnPoint1.position, SpawnPoint1.rotation);
        Instantiate(proyectil1Prefab, SpawnPoint2.position, SpawnPoint2.rotation); 
        EnablePlayerMovement();
        Corrutina = false;

    }

    private void EnablePlayerMovement(){
        anim.enabled = true;
        playerRb.bodyType = RigidbodyType2D.Dynamic;
        Debug.Log("Player movement enabled");
    }

    private void DisablePlayerMovement(){
        anim.enabled = false;
        playerRb.bodyType = RigidbodyType2D.Static;
        Debug.Log("Player movement disabled");
    }
    
    
}
