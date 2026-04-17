using System.Collections;
using System.Windows.Forms;
using Assets.Scripts.Exercises;
using Assets.Scripts.SaveSystem;
using Assets.Scripts.Statistics;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.UI
{
    public class FeatureMenuController: MonoBehaviour, MenuScreenController
    {
        private string fileLoc;
        private string csvloc;
        public TMP_Text progressTracker;
        private bool isRunning = false;
#nullable enable
        public PopupController? popupController;
#nullable disable
        public void SelectFile()
        {
            string[] selectedLocs;
            do
            {
                selectedLocs = StandaloneFileBrowser.StandaloneFileBrowser.OpenFilePanel("Select File", "", "json", false);
            }while(selectedLocs.Length != 1);
            fileLoc = selectedLocs[0];

            do
            {
                csvloc = StandaloneFileBrowser.StandaloneFileBrowser.SaveFilePanel("CSV location", "", "save", "csv");
            } while (csvloc == null);
        }

        public void StartCalc()
        {
            if(!isRunning) StartCoroutine(CalcCoroutine());
            else Debug.Log("Calculation already running");
        }

        void OnProgress(int progress)
        {
            progressTracker.text = progress.ToString() + "%";
        }

        public IEnumerator CalcCoroutine()
        {
            if (fileLoc == null || csvloc == null)
            {
                Debug.LogError("No file selected");
                popupController.Show("No file selected");
                yield break;
            }

            isRunning = true;

            Exercise exercise = SaveManager.LoadExercise(fileLoc);
            StatisticsController.CalcExercise(exercise);
            SaveManager.Save(fileLoc,exercise);
            
            CSVCreator csvCreator = new CSVCreator();

            FileManager.WriteFile(csvloc, csvCreator.CSVString(exercise));


            isRunning = false;

            if (popupController != null) popupController.Show("Calculation finished");

        }



    }
}