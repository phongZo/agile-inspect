using System;

namespace AgileInspect
{
    public class MachineName
    {
        #region Singleton
        private static MachineName _instance;
        public static MachineName Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new MachineName();
                    _instance.UpdateName(); // Ensure name is always populated on first access
                }
                return _instance;
            }
        }

        public MachineName()
        {
            UpdateName();
        }

        #endregion

        public string Name { get; private set; }
        public int ID { get; private set; }

        public void UpdateName()
        {
            try
            {
                Name = Environment.MachineName.ToString() + ":" + Environment.UserName.ToString();
                ID = 0; //Default is allwhite
            }
            catch
            {
                Name = "UnknownDevice";
            }
        }
    }
}
