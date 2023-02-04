using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowEnemyBullet : MonoBehaviour
{
    private GameObject[] multipleEnemys;
    public Transform closestEnemy;
    public bool enemyContact;

    void Start()
    {
        
        closestEnemy = null;
        enemyContact = false;
    }

    void Update()
    {

    }

}
