using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.UI
{

    public class MenuController: MonoBehaviour
    {

#nullable enable
        private string? _currentScreen;
#nullable disable
        private GameObject _currentScreenObject;
        private Dictionary<String, GameObject> _screens;
        private List<String> _screenNames;
        public string initialScreen = "MainMenu";
        private static string getScreenName(System.Object screen)
        {
            return screen.GetType().Name.Replace("Controller", "");
        }

        private static string getScreenName<T> ()
        {
            return typeof(T).Name.Replace("Controller", "");
        }


        private void addScreen<T>() where T : MonoBehaviour, MenuScreenController
        {
            string name = getScreenName<T>();
            Debug.Log("Adding Screen "+name);
            _screens.Add(name, gameObject.GetComponentInChildren<T>().gameObject);
            _screenNames.Add(name);
        }

        private void addScreen(MonoBehaviour controller)
        {
            string name = getScreenName(controller);
            Debug.Log("Adding Screen " + name);
            _screens.Add(name, controller.gameObject);
            _screenNames.Add(name);
        }

        public MenuController()
        {
            _screens = new Dictionary<string, GameObject>();
            _screenNames = new List<string>();
        }

        private List<MonoBehaviour> discoverScreens()
        {
            return gameObject.GetComponentsInChildren<MonoBehaviour>().Where(e =>
            {
                return e.GetType().GetInterfaces().Any(i => i == typeof(MenuScreenController));
            }).ToList();
        }

        private void Start()
        {
            discoverScreens().ForEach(addScreen);
            foreach(GameObject screen in _screens.Values)
            {
                screen.SetActive(false);
            }
            SetScreen(initialScreen);
        }

        public void SetScreen(String newScreen)
        {
            if (!_screenNames.Contains(newScreen))
            {
                Debug.LogError("Menu Screen " + newScreen + " does not exist");
                return;
            }
            if (_currentScreen == newScreen) return;
            if(_currentScreen!= null) _screens[_currentScreen].SetActive(false);
            _screens[newScreen].SetActive(true);
            _currentScreen = newScreen;
        }


        public void SetScreen<T>() where T : MonoBehaviour
        {
            SetScreen(getScreenName<T>());
        }

    }
}