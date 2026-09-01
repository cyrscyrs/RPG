using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParallaxBackground : MonoBehaviour
{
    private GameObject cam;

    [SerializeField] private float parallaxEffect;

    private float xPosition;
    private float yPosition;
    private float length;
    // Start is called before the first frame update
    void Start()
    {
        cam = GameObject.Find("Main Camera");

        length = GetComponent<SpriteRenderer>().bounds.size.x;
        xPosition = transform.position.x;
        yPosition = transform.position.y;
    }

    // Update is called once per frame
    void Update()
    {
        float distanceMoved = cam.transform.position.x * (1 - parallaxEffect);
        float xDistanceToMove = cam.transform.position.x * parallaxEffect;
        float yDistanceToMove = cam.transform.position.y * parallaxEffect;

        transform.position = new Vector3(xPosition + xDistanceToMove, yPosition + yDistanceToMove);

        if(distanceMoved > xPosition + length)
            xPosition = xPosition + length;
        else if (distanceMoved < xPosition - length)
            xPosition = xPosition - length;
    }
}
