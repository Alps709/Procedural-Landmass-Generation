using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class TextureData : UpdatableData
{
    private float savedMinHeight;
    private float savedMaxHeight;
    public void ApplyToMaterial(Material material)
    {
        UpdateMeshHeights(material, savedMinHeight, savedMaxHeight);
    }

    public void UpdateMeshHeights(Material material, float minHeight, float maxHeight)
    {
        savedMaxHeight = maxHeight;
        savedMinHeight = minHeight;
        
        material.SetFloat("minHeight", minHeight);
        material.SetFloat("maxHeight", maxHeight);
        
    }
}
