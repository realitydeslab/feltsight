using UnityEngine;

public class ChangeMaterialRandomColor : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        var color=Random.ColorHSV();
        color.a = (float)0.6;
        this.gameObject.GetComponent<MeshRenderer>().material.color = color;
    }
}
