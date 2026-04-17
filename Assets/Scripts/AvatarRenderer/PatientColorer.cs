using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

[DisallowMultipleComponent]
public class PatientColorer : MonoBehaviour
{

    public SkinnedMeshRenderer Smr;
    public Color32 PatientColor = Color.black;
    public Color32 DefaultColor = Color.white;

    public void UpdateColor(Color32 newColor)
    {
        if(!Smr) return;
        var mesh = Smr.sharedMesh;
        var colors = new Color32[mesh.vertexCount];
        for (var i = 0; i < colors.Length; ++i)
        {
            colors[i] = newColor;
        }
        Smr.sharedMesh.colors32 = colors;
    }

    IEnumerator ColorChangeCoroutine()
    {
        while (true)
        {
            UpdateColor(PatientColor);
            yield return new WaitForSeconds(1);
            UpdateColor(DefaultColor);
            yield return new WaitForSeconds(1);
        }
    }
    void Start()
    {
        // Find smr if not given
        if (Smr == null) Smr = GetComponent<SkinnedMeshRenderer>();
        Assert.IsNotNull(Smr, "SkinnedMeshRenderer not found");
        // SkinnedMeshRenderer has only shared mesh. We should not modify it.
        // So we make a copy on startup, and work with it.
        Smr.sharedMesh = Instantiate(Smr.sharedMesh);
        //StartCoroutine(ColorChangeCoroutine());
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
