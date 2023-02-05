using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowEnemyBullet : MonoBehaviour
{

    void Start()
    {
        
    }
   /*void Update()
    {
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = Camera.main.transform.position.z + Camera.main.nearClipPlane;
        transform.position = mousePosition;

        

        Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        transform.position = Vector2.MoveTowards(transform.position, mousePosition, moveSpeed * Time.deltaTime);
    }*/

    public float maxMoveSpeed = 3f;
    public float smoothTime = .1f;
    Vector2 currentVelocity;
    void Update()
    {
        Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        transform.position = Vector2.SmoothDamp(transform.position, mousePosition, ref currentVelocity, smoothTime, maxMoveSpeed); 
    }

}
