using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] Camera cam;
    [SerializeField] Transform player;
    [SerializeField] Transform SpawnPoint, SpawnPoint2; //distancia
    [SerializeField] float threshold; //distancia
    [SerializeField] private Projectile projectilePrefab;
    public bool CanShoot = true;
    

    void Update(){
        Vector3 mousePos = cam.ScreenToWorldPoint(Input.mousePosition); //checa posicion del mouse
        Vector3 targetPos = (player.position + mousePos)/2f; //calcula distancia del mouse al player

        targetPos.x = Mathf.Clamp(targetPos.x, -threshold + player.position.x, threshold + player.position.x); //limita la distancia
        targetPos.y = Mathf.Clamp(targetPos.y, -threshold + player.position.y, threshold + player.position.y);

        this.transform.position = targetPos;

        
    }

    
}
