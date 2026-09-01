using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Blackhole_HotKey_Controller : MonoBehaviour
{
    private SpriteRenderer sr;
    private KeyCode myHotKry;
    private TextMeshProUGUI myText;
    private Transform myEnemy;
    private Blackhole_Skill_Controller myBlackhole;

    public void SetupHotKey(KeyCode _myHotKey, Transform _myEnemy, Blackhole_Skill_Controller _myBlackhole)
    {
        sr = GetComponent<SpriteRenderer>();
        myText = GetComponentInChildren<TextMeshProUGUI>();
        myEnemy = _myEnemy;
        myBlackhole = _myBlackhole;

        myHotKry = _myHotKey;
        myText.text = myHotKry.ToString();
    }

    private void Update()
    {
        if (Input.GetKeyDown(myHotKry))
        {
            //if(myBlackhole.targets.)
            myBlackhole.AddTargetToList(myEnemy);

            myText.color = Color.clear;
            sr.color = Color.clear;
        }
    }
}
