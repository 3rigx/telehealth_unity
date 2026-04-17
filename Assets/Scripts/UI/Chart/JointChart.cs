using System.Collections.Generic;
using Assets.Scripts.AvatarRenderer;
using Assets.Scripts.Sensors.Zed;
using UnityEngine;

namespace Assets.Scripts.UI.Chart
{
    [DisallowMultipleComponent]
    public class JointChart : MonoBehaviour
    {
        private readonly BodyFormat bodyFormat = BodyFormat.BODY_34;
        private List<GameObject> lines = new();
        private DD_DataDiagram m_diagram;

        private void CreateLines()
        {
            for (var i = 0; i < 34; i++)
            {
                var jointName = bodyFormat.GetJointTypeName(i);
                var jointColor = new Color(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f),
                    Random.Range(0.0f, 1.0f));

                for (var d = 0; d < 3; d++)
                {
                    var line = m_diagram.AddLine(jointName + "_" + d, jointColor);
                    if (line != null) lines.Add(line);
                    else Debug.LogError("Line is null");
                    line.GetComponent<DD_Lines>().IsShow = false;
                }
            }
        }

        private void Start()
        {
            m_diagram = GetComponent<DD_DataDiagram>();
            if (!m_diagram)
            {
                Debug.LogError("No DataDiagram found");
                return;
            }

            m_diagram.PreDestroyLineEvent += (s, e) => { lines.Remove(e.line); };
            CreateLines();
        }

        public void AddJoints(Vector3[] joints)
        {
            for (var i = 0; i < joints.Length; i++)
            for (var d = 0; d < 3; d++)
            {
                Vector2 newpoint = new(1, joints[i][d]);
                m_diagram.InputPoint(lines[i], newpoint);
            }
        }

        public void AddState(SkeletonState st)
        {
            AddJoints(st.JointPos);
        }

        public void Clear()
        {
            foreach (var line in lines) m_diagram.DestroyLine(line);
            lines = new List<GameObject>();
            CreateLines();
        }
    }
}