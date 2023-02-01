using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour
{
    [SerializeField] private Transform target;
    
    private NavMeshAgent navMeshAgent;

    private void Start()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        navMeshAgent.updateRotation = false;
        navMeshAgent.updateUpAxis = false;
    }

    private void Update()
    {
        var distance = Vector3.Distance(target.position, transform.position);

        if(distance<5f)
        {
            navMeshAgent.enabled=true;
            navMeshAgent.SetDestination(target.position);
        }
        else{
            navMeshAgent.enabled=false;
        }

        
    }

    private void Die()
    {
        //Intancia Loot, para elk enemigo
        GetComponent<LootBag>().InstantiateLoot(transform.position);
        
        Destroy(gameObject);
    }
}
