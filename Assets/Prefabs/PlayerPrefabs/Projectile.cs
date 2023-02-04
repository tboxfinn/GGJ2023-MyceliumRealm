using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private float speed;
    public void Init(Vector2 direction)
    {
        rb.velocity = direction * speed;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        other.transform.GetComponent<Enemy>()?.TakeDamage(1);
        
        if (other.gameObject.tag == "Enemy")
        {
            Destroy(gameObject);
        }
    }
}
