using System;
using System.IO;
using UnityEngine;

namespace Assets.Scripts.SaveSystem
{
    /// <summary>
    ///     File manager for reading and writing files.
    /// </summary>
    internal static class FileManager
    {
        /// <summary>
        ///     Writes data to a file in the persistent data path.
        /// </summary>
        /// <param name="path">Path to write data to</param>
        /// <param name="data"> Data to write</param>
        /// <returns>Whether write operation was successful</returns>
        public static bool WriteFile(string path, string data)
        {
            try
            {
                var combinedPath = Path.Combine(Application.persistentDataPath, path);
                File.WriteAllText(combinedPath, data);
                Debug.Log("Saved file to " + combinedPath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                return false;
            }
        }

        /// <summary>
        ///     Reads data from a file at path in the persistent data path.
        /// </summary>
        /// <param name="path">Path to read data from</param>
        /// <returns>Data read from file</returns>
        public static string LoadFromFile(string path)
        {
            try
            {
                return File.ReadAllText(
                    Path.Combine(Application.persistentDataPath, path)
                );
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                return "";
            }
        }

        public static byte[] LoadFromFileBinary(string path)
        {
            try
            {
                return File.ReadAllBytes(
                    Path.Combine(Application.persistentDataPath, path)
                );
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                return Array.Empty<byte>();
            }
        }


        public static void DeleteFile(string path)
        {
            try
            {
                File.Delete(Path.Combine(Application.persistentDataPath, path));
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }
        }
    }
}