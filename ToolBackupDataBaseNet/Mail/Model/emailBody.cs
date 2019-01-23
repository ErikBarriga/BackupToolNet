using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ToolBackupDataBaseNet.Mail.Model
{
    internal class emailBody
    {
        public long Id { get; set; }
        public short SenderGroup;
        public string System;
        public bool ConfirmReading;
        public bool ConfirmDelivery;
        public string To;
        public string CC;
        public string CCO;
        public string Subjet;
        public string Body;
        public bool IsHTML;

        public emailBody()
        {
            Id = 0;
            SenderGroup = 0;
            System = string.Empty;
            ConfirmReading = false;
            ConfirmDelivery = false;
            To = string.Empty;
            CC= string.Empty;
            CCO = string.Empty;
            Subjet = string.Empty;
            Body = string.Empty;
            IsHTML = true;
        }
    }
}
