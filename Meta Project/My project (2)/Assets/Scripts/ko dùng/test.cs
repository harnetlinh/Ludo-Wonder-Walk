using UnityEngine;

public class test : MonoBehaviour
{
    //private void OnCollisionEnter(Collision collision)
    //{
    //    Debug.Log("Collision detected with: " + collision.gameObject.name);
    //}

    //private void OnCollisionExit(Collision collision)
    //{
    //    Debug.Log("Collision ended with: " + collision.gameObject.name);
    //}

    private void OnTriggerEnter(Collider other)
    {
        //Debug.Log("Trigger entered with: " + other.gameObject.name);
    }
}
