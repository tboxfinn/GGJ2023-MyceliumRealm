using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class followBullet : MonoBehaviour
{
    public float speed;


    private Transform player;
    private Vector2 target;
    int senal = 0;

    void Start()
    {

        player = GameObject.FindGameObjectWithTag("Player").transform;

        target = new Vector2(player.position.x, player.position.y);
    }


    void Update()
    {
        StartCoroutine(Waiter());
        transform.position = Vector2.MoveTowards(transform.position, player.position, speed * Time.deltaTime);

        if (transform.position.x == target.x && transform.position.y == target.y)
        {
            DestroyProjectile();
        }
        

            
    }
    public GameObject particle;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            DestroyProjectile();

        }
    }

    private IEnumerator Waiter()
    {
        

        yield return new WaitForSecondsRealtime(3);
        DestroyProjectile();
    }
        void DestroyProjectile()
    {
        Instantiate(particle, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }
}
