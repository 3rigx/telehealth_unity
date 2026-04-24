using System;
using System.Linq;
using TMPro;
using UnityEngine;
#if UNITY_STANDALONE
using System.IO.Ports;
#endif


namespace Assets.Scripts.UI
{
    public class SettingsMenuController : MonoBehaviour, MenuScreenController
    {


        public TMP_InputField FSRHostAPIKey;

        public TMP_InputField FSRHostURI;
        public TMP_Dropdown FSRTypeDropdown;

        public TMP_Dropdown FSRUSBDropdown;

        public void SetFSRType(int option)
        {
            PlayerPrefs.SetString("FSRConntype", FSRTypeDropdown.options[option].text);
            PlayerPrefs.Save();
        }

        public void SetFSRHost(string fsrHost)
        {
            PlayerPrefs.SetString("FSRUri", fsrHost.Trim());
            Debug.Log("host " + fsrHost.Trim());
            PlayerPrefs.Save();
        }

        public void SetFSRAPIKey(string fsrAPIKey)
        {
            PlayerPrefs.SetString("FSRApiKey", fsrAPIKey.Trim());
            Debug.Log("key " + fsrAPIKey.Trim());
            PlayerPrefs.Save();
        }

        public void SetFSRUSB(int option)
        {
            PlayerPrefs.SetString("FSRUsbPort", FSRUSBDropdown.options[option].text);
            PlayerPrefs.Save();
        }


        private void Start()
        {
            FSRTypeDropdown.options.Add(new TMP_Dropdown.OptionData("Select"));
            FSRTypeDropdown.options.Add(new TMP_Dropdown.OptionData("Mock"));
            FSRTypeDropdown.options.Add(new TMP_Dropdown.OptionData("USB"));
            FSRTypeDropdown.options.Add(new TMP_Dropdown.OptionData("WebSocket"));
            FSRTypeDropdown.options.Add(new TMP_Dropdown.OptionData("TCP"));

            FSRTypeDropdown.value = Array.IndexOf(FSRTypeDropdown.options.Select(e => e.text).ToArray(),
                PlayerPrefs.GetString("FSRConntype", "Select"));
            FSRTypeDropdown.RefreshShownValue();

            FSRHostURI.text = PlayerPrefs.GetString("FSRUri", "");
            FSRHostAPIKey.text = PlayerPrefs.GetString("FSRApiKey", "");


#if UNITY_STANDALONE

            FSRUSBDropdown.options.Add(new TMP_Dropdown.OptionData("Select"));
            foreach (var port in SerialPort.GetPortNames().Reverse())
            {
                FSRUSBDropdown.options.Add(new TMP_Dropdown.OptionData(port));
            }
            FSRUSBDropdown.value = Array.IndexOf(FSRUSBDropdown.options.Select(e => e.text).ToArray(),
                PlayerPrefs.GetString("FSRUsbPort", "Select"));
            FSRUSBDropdown.RefreshShownValue();
#else
            FSRUSBDropdown.options.Add(new TMP_Dropdown.OptionData("Unavailable"));
#endif
        }
    }
}