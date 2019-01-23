using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ToolBackupDataBaseNet.Mail.Model
{
    internal class emailAttachment
    {
        public string FilePath  { get; set; }
        public string DeleteAfterSendingEmail  { get; set; }
    }
}
