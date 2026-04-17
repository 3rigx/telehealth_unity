using UnityEngine;
using UnityEngine.Assertions;

namespace Assets.Scripts.Util
{
    public class KeyHoldEnabler : MonoBehaviour
    {
        // Start is called before the first frame update
        public KeyCode Trigger = KeyCode.Tab;
        public GameObject Ui;


        // Update is called once per frame


        private void Start()
        {
            Assert.IsNotNull(Ui);
            Ui.SetActive(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(Trigger))
                Ui.SetActive(true);
            else if (Input.GetKeyUp(Trigger)) Ui.SetActive(false);
        }
    }
}