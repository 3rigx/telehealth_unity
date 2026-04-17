using System.Collections;
using Assets.Scripts.Util;
using UnityEngine;

namespace Assets.Scripts
{
    public class VideoTest : MonoBehaviour
    {
        private IEnumerator ConvertCoroutine()
        {
            // escape  C:\Users\mzozi\Desktop
            VideoFileConverter.SVOtoMP4("C:\\Users\\mzozi\\Desktop\\test.svo", "C:\\Users\\mzozi\\Desktop\\test.mp4");
            yield break;
        }

        private void LStart()
        {
            Debug.Log("Starting conversion");
            StartCoroutine(ConvertCoroutine());
        }

        private void Start()
        {
            Invoke(nameof(LStart), 1f);
        }
    }
}