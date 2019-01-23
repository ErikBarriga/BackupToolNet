using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ToolBackupDataBaseNet
{
    class Config
    {
        public static bool GetBoolProperty(string prop)
        {
            if (bool.TryParse(GetstringProperty(prop), out bool vlBln))
                return vlBln;
            else
                return false;
        }
        public static string GetstringProperty(string prop)
        {
            string vlVal = string.Empty;
            try
            {
                vlVal = ConfigurationManager.AppSettings[prop];
            }
            catch (Exception)
            {
            }
            return vlVal;
        }
        public static int? GetIntNullableProperty(string prop)
        {
            if (int.TryParse(ConfigurationManager.AppSettings[prop], out int vlVal))
                return vlVal;
            else
                return null;
        }
    }
}
