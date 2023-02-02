using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealthBuff : PowerupEffect
{
    public float amount;
    public override void ApplyEffect(GameObject target)
    {
        //target.GetComponent<PlayerController>().health += 1;
    }
}
