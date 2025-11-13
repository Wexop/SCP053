using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SCP053.Scripts;

public class Scp053Actions : MonoBehaviour
{
    public Canvas canvas;
    public Volume volume;

    private void Start()
    {
        Enable(false);
    }

    public void Enable(bool enable)
    {
        canvas.gameObject.SetActive(enable);
        volume.gameObject.SetActive(enable);
        if (!enable)
        {
            SetVolumeWeight(0);
        }
    }

    public void SetVolumeWeight(float weight)
    {
        volume.weight = weight;
    }
    
}