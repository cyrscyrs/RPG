using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeadZone : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.GetComponent<Enemy>() != null)
        {
            collision.GetComponent<EnemyStats>().KillEntity();
        }
        else if(collision.GetComponent<Player>() != null) 
        {
            PlayerStats playerStats = collision.GetComponent<PlayerStats>();
            playerStats.TakeDamage(Mathf.RoundToInt(playerStats.GetMaxHealthValue() * .1f));
            //PlayerManager.instance.player.transform.position.x = PlayerManager.instance.lastGroundPosX;
            PlayerManager.instance.player.transform.position = PlayerManager.instance.lastGrounedPos;
            PlayerManager.instance.player.SetZeroVelocity();
        }
        else
            Destroy(collision.gameObject);
    }
}
