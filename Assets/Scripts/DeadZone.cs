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
            // 回到最新存档点；还没存过档就退回「最后站立的位置」
            Vector3 respawn = PlayerManager.instance.lastGrounedPos;

            if (GameManager.instance != null &&
                GameManager.instance.TryGetLatestCheckpointPosition(out Vector3 checkpointPosition))
                respawn = checkpointPosition;

            PlayerManager.instance.player.transform.position = respawn;
            PlayerManager.instance.player.SetZeroVelocity();
        }
        else
            Destroy(collision.gameObject);
    }
}
