namespace AgileInspect
{
    public class MachineName
    {
        #region Singleton
        private static readonly MachineName _instance = new MachineName();

        public static MachineName Instance => _instance;

        private MachineName()
        {

        }
        #endregion

        public string Name { get; private set; }
        public int ID { get; private set; }

        public void UpdateName()
        {
            Name = Environment.MachineName.ToString() + ":" + Environment.UserName.ToString();
            ID = 0; //Default is allwhite
        }
    }
}
