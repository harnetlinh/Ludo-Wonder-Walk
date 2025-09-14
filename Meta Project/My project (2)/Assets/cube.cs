using UnityEngine;

public class cube : MonoBehaviour
{

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Table"))
        {
            // Thêm hành động khác tại đây
        }
    }


}
