using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ToolBackupDataBaseNet.Mail.Model
{
    internal class EmailSender
    {
        public long Id;
        public short SenderGroup;

        public string Server;
        public string User;
        public string Pass;
        public int? SmtpPort;

        public string EmailAddres;
        public string EmailMask;
        public string EmailCCO;
        public string ReplyTo;

        public int? TimeOut;
        public bool UseSsl;

        public EmailSender()
        {
            Id = 0;
            SenderGroup = 0;
            Server = string.Empty;
            User = string.Empty;
            Pass = string.Empty;

            EmailAddres = string.Empty;
            EmailMask = string.Empty;
            EmailCCO = string.Empty;
            ReplyTo = string.Empty;

            UseSsl = false;
        }
    }
}
