using System;
using System.Collections;
using OpenCvSharp;
using sl;
using UnityEngine;
using Rect = OpenCvSharp.Rect;

namespace Assets.Scripts.Util
{
    public class VideoFileConverter
    {
        /// <summary>
        ///     Returns the OpenCV type that corresponds to a given ZED Mat type.
        /// </summary>
        private static MatType SLMatType2CVMatType(ZEDMat.MAT_TYPE zedmattype)
        {
            switch (zedmattype)
            {
                case ZEDMat.MAT_TYPE.MAT_32F_C1:
                    return MatType.CV_32FC1;
                case ZEDMat.MAT_TYPE.MAT_32F_C2:
                    return MatType.CV_32FC2;
                case ZEDMat.MAT_TYPE.MAT_32F_C3:
                    return MatType.CV_32FC3;
                case ZEDMat.MAT_TYPE.MAT_32F_C4:
                    return MatType.CV_32FC4;
                case ZEDMat.MAT_TYPE.MAT_8U_C1:
                    return MatType.CV_8UC1;
                case ZEDMat.MAT_TYPE.MAT_8U_C2:
                    return MatType.CV_8UC2;
                case ZEDMat.MAT_TYPE.MAT_8U_C3:
                    return MatType.CV_8UC3;
                case ZEDMat.MAT_TYPE.MAT_8U_C4:
                    return MatType.CV_8UC4;
                default:
                    throw new Exception("Unknown ZEDMat type");
            }
        }


        /// <summary>
        ///     Creates an OpenCV version of a ZED Mat.
        /// </summary>
        /// <param name="zedmat">Source ZED Mat.</param>
        /// <param name="zedmattype">
        ///     Type of ZED Mat - data type and channel number.
        ///     <returns></returns>
        private static Mat SLMat2CVMat(ref ZEDMat zedmat, ZEDMat.MAT_TYPE zedmattype)
        {
            return new Mat(zedmat.GetHeight(), zedmat.GetWidth(), SLMatType2CVMatType(zedmattype), zedmat.GetPtr());
        }

        public static string SVOtoAnything(string src, string dest, int fourcc)
        {
            ZEDCamera zed = new();
            InitParameters initParameters = new()
            {
                inputType = INPUT_TYPE.SVO,
                pathSVO = src,
                svoRealTimeMode = true,
                coordinateUnit = UNIT.MILLIMETER
            };
            if (zed.Open(ref initParameters) != ERROR_CODE.SUCCESS)
            {
                Debug.LogError("Failed to open SVO file");
                return "";
            }

            var resolution = zed.GetCalibrationParameters().leftCam.resolution;

            ZEDMat leftImage = new();
            leftImage.Create(resolution, ZEDMat.MAT_TYPE.MAT_8U_C4);
            var leftImageOCV = SLMat2CVMat(ref leftImage, ZEDMat.MAT_TYPE.MAT_8U_C4);


            var outputImageOCV = new Mat((int)resolution.height, (int)resolution.width, MatType.CV_8UC3);

            var frameRate = Math.Max(initParameters.cameraFPS, 25);

            VideoWriter videoWriter = new();
            videoWriter.Open(dest, fourcc, frameRate, new Size((int)resolution.width, (int)resolution.height));
            if (!videoWriter.IsOpened())
            {
                Console.WriteLine(
                    "Error: OpenCV video writer cannot be opened. Please check the .avi file path and write permissions.");
                zed.Destroy();
                Environment.Exit(-1);
            }

            RuntimeParameters rtParams = new();
            // Start SVO conversion to AVI/SEQUENCE

            var nbFrames = zed.GetSVONumberOfFrames();
            var svoPosition = 0;
            zed.SetSVOPosition(svoPosition);

            var targetFrameTime = 1.0f / frameRate;
            while (zed.Grab(ref rtParams) == ERROR_CODE.SUCCESS)
            {
                svoPosition = zed.GetSVOPosition();
                zed.RetrieveImage(leftImage, VIEW.LEFT);
                Cv2.CvtColor(leftImageOCV,
                    outputImageOCV[new Rect(0, 0, (int)resolution.width, (int)resolution.height)],
                    ColorConversionCodes.BGRA2BGR);
                var frameTime = 1.0f / zed.GetCameraFPS();
                for (var i = 0; i < (int)(frameTime / targetFrameTime); i++) videoWriter.Write(outputImageOCV);
            }

            if (zed.GetSVOPosition() >= nbFrames - (initParameters.svoRealTimeMode ? 2 : 1))
                Debug.Log("SVO end has been reached.");
            else
                Debug.LogError("Failed to grab frame");
            videoWriter.Release();
            zed.Destroy();
            return "";
        }

        public static IEnumerator SVOtoAVI(string src, string dest)
        {
            SVOtoAnything(src, dest, VideoWriter.FourCC('M', '4', 'S', '2')); // MPEG-4 part 2 codec
            yield break;
        }

        public static IEnumerator SVOtoMP4(string src, string dest)
        {
            SVOtoAnything(src, dest, VideoWriter.FourCC('a', 'v', 'c', '1')); // H.264 codec
            yield break;
        }
    }
}