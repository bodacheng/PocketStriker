using UnityEngine;

public class Rotate_the_object : MonoBehaviour
{
    public float Rotation_X = 0.0f;
    public float Rotation_Y = 20.0f;
    public float Rotation_Z = 0.0f;

    void Update()
    {
        transform.Rotate(new Vector3(Rotation_X, Rotation_Y, Rotation_Z) * Time.deltaTime);
    }
}
