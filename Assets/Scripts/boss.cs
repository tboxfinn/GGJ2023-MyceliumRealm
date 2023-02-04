using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class boss : MonoBehaviour
{
    int senal = 1;

    public float speed;
    public float stoppingDistance;
    public float retreaDistance;

    private float timeBtwShots;
    public float startTimeBtwShots;




    public Transform player;
    public GameObject projectile;
    public GameObject projectile2;

    // Start is called before the first frame update
    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        timeBtwShots = startTimeBtwShots;
    }

    // Update is called once per frame
    void Update()
    {
        if (senal == 1)
        {
            StartCoroutine(Waiter());
        }
        if (Vector2.Distance(transform.position, player.position) > stoppingDistance)
        {
            transform.position = Vector2.MoveTowards(transform.position, player.position, speed * Time.deltaTime);
        }
        else if (Vector2.Distance(transform.position, player.position) > stoppingDistance && Vector2.Distance(transform.position, player.position) > retreaDistance)
        {
            transform.position = this.transform.position;
        }
        else if (Vector2.Distance(transform.position, player.position) > retreaDistance)
        {
            transform.position = Vector2.MoveTowards(transform.position, player.position, -speed * Time.deltaTime);
        }

        if (timeBtwShots <= 0)
        {
            Instantiate(projectile, transform.position, Quaternion.identity);
            timeBtwShots = startTimeBtwShots;
        }
        else
        {
            timeBtwShots -= Time.deltaTime;
        }
    }

    private IEnumerator Waiter()
    {
        senal = 2;
        Debug.Log("lmao");


        yield return new WaitForSecondsRealtime(3);
        Debug.Log("lmao1");
        timeBtwShots = 1;
        Instantiate(projectile2, transform.position, Quaternion.identity);
        yield return new WaitForSecondsRealtime(0.1F);
        Instantiate(projectile2, transform.position, Quaternion.identity);
        yield return new WaitForSecondsRealtime(0.1F);
        Instantiate(projectile2, transform.position, Quaternion.identity);
        yield return new WaitForSecondsRealtime(0.1F);
        Instantiate(projectile2, transform.position, Quaternion.identity);
        yield return new WaitForSecondsRealtime(5);

        Debug.Log("lmao2");

        yield return new WaitForSecondsRealtime(5);

        senal = 1;
    }
}
