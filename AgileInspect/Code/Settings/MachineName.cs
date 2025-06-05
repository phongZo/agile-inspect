using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgileInspect
{
    public class MachineName
    {
         #region Singleton
        public static MachineName Instance { get; set; }
        public MachineName()
        {
            Instance = this;
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
