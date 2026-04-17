namespace Assets.Scripts.Sensors
{
    public interface ISensorController<T>
    {
        public void Read();
        public T GetState();
    }
}