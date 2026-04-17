using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

namespace Assets.Scripts.UI.Chart.MyChart
{

    class LineButton: MonoBehaviour
    {
        public string label;
        public GameObject[] targets;

        public void OnClick()
        {
            foreach(var line in targets)
            {
                line.gameObject.SetActive(!line.gameObject.activeSelf);
            }
        }

        public void Initialise(string label, LineRenderer[] targets)
        {
            this.label = label;
            this.targets = targets.Select(e => e.gameObject).ToArray();
        }

        void OnDestroy()
        {
            
        }
    }

    public class JointLineGroup : MonoBehaviour
    {
        public LineRenderer x, y, z;
        public string jointName;
        public void Initialise(string jointName)
        {
            this.jointName = jointName;
        }

    }

    public class LineSelector : MonoBehaviour
    {
        private List<LineButton> buttons;
        

        public void Clear() {
            buttons.ForEach(e => Destroy(e.gameObject));
            buttons.Clear();
        }

        public void addGroup(string groupName, LineRenderer[] lines)
        {
            LineButton newButton = Instantiate(Resources.Load<GameObject>("Prefabs/LineButton")).GetComponent<LineButton>();
            newButton.Initialise(groupName, lines);
            buttons.Add(newButton);

        }

        public void addLine(string name, LineRenderer line)
        {
            addGroup(name,new []{line});
        }



    }
}