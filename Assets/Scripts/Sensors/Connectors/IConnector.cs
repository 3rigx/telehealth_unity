using System.Collections.Generic;

namespace Assets.Scripts.Sensors.Connectors
{
    /// <summary>
    ///     Interface for sensor connectors.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IConnector<T> where T : new()
    {
        public List<SensorState<T>> Sensors { get; }

        /// <summary>
        ///     Connect Sensor
        /// </summary>
        /// <returns>Whether connection was successful</returns>
        public void Connect();

        /// <summary>
        ///     Disconnect Sensor
        /// </summary>
        /// <returns>Whether disconnect was successful</returns>
        public void Disconnect();

        /// <summary>
        ///     Execute Read operation
        /// </summary>
        public void Read();
    }
}