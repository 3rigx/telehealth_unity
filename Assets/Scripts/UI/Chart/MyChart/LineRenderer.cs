using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Random = System.Random;

public class LineRenderer : Graphic
{
    private int _cursor = 0;

    public List<float> values;
    
    public float thickness = 0.01f;
    public int windowSize = 10;
    public bool sliding = true;
    public float maxUnitWidth = 1;

    public int cursor
    {
        get { return _cursor; }
        set
        {
            _cursor = value;
            SetVerticesDirty();
        }
    }


    protected void populateValues(List<float> values, VertexHelper vh)
    {

        if (values.Count < 2)
            return;

        float width = rectTransform.rect.width;
        float height = rectTransform.rect.height;
        float unitWidth = Mathf.Min(maxUnitWidth, rectTransform.rect.width / values.Count);
        float unitHeight = rectTransform.rect.height / 256 ;

        unitHeight *= values.Max() / values.Min();

        for (int i = 0; i < values.Count; i++)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = new Vector3(unitWidth * i - thickness / 2, unitHeight * values[i]);
            vh.AddVert(vertex);
            vertex.position = new Vector3(unitWidth * i + thickness / 2, unitHeight * values[i]);
            vh.AddVert(vertex);
        }

        for (int i = 0; i < values.Count - 1; i++)
        {
            int index = i * 2;
            vh.AddTriangle(index, index + 1, index + 3);
            vh.AddTriangle(index + 3, index + 2, index);
        }
    }

    protected List<float> windowValues
    {
        get
        {
            List<float> consideredValues = new List<float>();

            int end = Mathf.Min(_cursor + windowSize, values.Count);
            for (int i = _cursor; i < end; i++)
            {
                consideredValues.Add(values[i]);
            }

            return consideredValues;
        }
    }   
    
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        populateValues(values,vh);

    }

    public void AddValue(float value)
    {
        values.Add(value);
        if (sliding && _cursor + windowSize < values.Count) ++_cursor;
        SetVerticesDirty();
    }

    public void AddValues(List<float> values)
    {
        this.values.AddRange(values);
        if (sliding && _cursor + windowSize < values.Count) _cursor += values.Count;
        SetVerticesDirty();
    }


}
