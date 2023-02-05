using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LootBag : MonoBehaviour
{
    public GameObject droppedItemPrefab;
    public float LifeTime = 5f;
    public List<LootScriptable> lootList = new List<LootScriptable>();

    public Object lootGameObject { get; private set; }

    LootScriptable GetDroppeditem()
    {
        int randomNumber = Random.Range(1, 101);
        Debug.Log(randomNumber);
        List<LootScriptable> possibleItems = new List<LootScriptable>();
        foreach (LootScriptable item in lootList)
        {
            if (randomNumber <= item.dropChance)
            {
                possibleItems.Add(item);
                  
            }
        }

        if (possibleItems.Count > 0)
        {
            LootScriptable droppedItem = possibleItems[Random.Range(0, possibleItems.Count)];
            return droppedItem;
        }
        return null;
        
    }

    public void InstantiateLoot(Vector3 spawnPosition)
    {
        LootScriptable droppedItem = GetDroppeditem();
        if(droppedItem != null)
        {
            GameObject lootGameObject = Instantiate(droppedItemPrefab, spawnPosition, Quaternion.identity);
            lootGameObject.GetComponent<SpriteRenderer>().sprite = droppedItem.lootSprite;
        }

        
    }
    
}
