using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using ToolBackupDataBaseNet.Mail.Model;

namespace ToolBackupDataBaseNet.Mail
{
    class Email:IDisposable     
    {
        private static readonly Func<string> _Server = () => Config.GetstringProperty("Email_Sender_Server").Trim();
        private static readonly Func<string> _Port = () => Config.GetstringProperty("Email_Sender_Port").Trim();
        private static readonly Func<string> _User = () => Config.GetstringProperty("Email_Sender_User").Trim();
        private static readonly Func<string> _Pass = () => Config.GetstringProperty("Email_Sender_Pass").Trim();
        private static readonly Func<bool> _UseSsl = () => Config.GetBoolProperty("Email_Sender_UseSsl");
        private static readonly Func<string> _EmailSender = () => Config.GetstringProperty("Email_Sender_EmailAddres").Trim();
        private static readonly Func<string> _EmailMask = () => Config.GetstringProperty("Email_Sender_EmailMask").Trim();
        private static readonly Func<string> _To = () => Config.GetstringProperty("Email_To").Trim();
        private static readonly Func<string> _Cc = () => Config.GetstringProperty("Email_Cc").Trim();

        private static readonly Func<string> _SubjectFailure = () => Config.GetstringProperty("OnFailureEmail_Subject").Trim();
        private static readonly Func<string> _BodyFailure = () => Config.GetstringProperty("OnFailureEmail_Body").Trim();
        private static readonly Func<string> _SubjectSuccess = () => Config.GetstringProperty("OnSuccessEmail_Subject").Trim();
        private static readonly Func<string> _BodySuccess = () => Config.GetstringProperty("OnSuccessEmail_Body").Trim();

        internal static void CleanEmails(ref string emails)
        {
            //Depura la lista de cuentas de correo que se recupera de la BD
            if (!string.IsNullOrEmpty(emails))
            {
                List<string> vlLstEmails = emails.Replace(";", ",").Split(',').Where(_ => _.Trim().Length>0).Distinct().ToList(); //.Select(Function(i) Trim(i)).Distinct.ToList
                emails = string.Join(",", vlLstEmails.ToArray());
            }
            if (emails == null)
                emails = string.Empty;
        }
        internal static bool SendOneEmail(EmailSender config, emailBody mail, List<emailAttachment> attachments, ref string vOutStrError)
        {
            //Se genera el Mail y se envía...
            string vlStrAuxStep = string.Empty;
            vOutStrError = string.Empty;
            using (var vlMailMesage = new MailMessage()) {
                try
                {
                    CleanEmails(ref mail.To);
                    CleanEmails(ref mail.CC);
                    CleanEmails(ref mail.CCO);
                    if (string.IsNullOrEmpty(config.EmailMask) || config.EmailAddres == config.EmailMask) {
                        vlStrAuxStep = "Remitente";
                        vlMailMesage.From = new MailAddress(config.EmailAddres);
                    }
                    else
                    {
                        vlStrAuxStep = "Remitente y Mascara";
                        vlMailMesage.From = new MailAddress(config.EmailAddres, config.EmailMask);
                    }
                    if (!string.IsNullOrEmpty(config.ReplyTo)) {
                        vlStrAuxStep = "Responder A";
                        vlMailMesage.ReplyToList.Add(new MailAddress(config.ReplyTo));
                    }
                    vlStrAuxStep = "Confirmaciones";
                    if (mail.ConfirmReading) {
                        vlMailMesage.Headers.Add("Disposition-Notification-To", string.IsNullOrEmpty(config.ReplyTo) ? config.EmailAddres : config.ReplyTo);
                    }
                    if (mail.ConfirmDelivery) {
                        vlMailMesage.DeliveryNotificationOptions = DeliveryNotificationOptions.OnSuccess;
                    }
                    if (!string.IsNullOrEmpty(mail.To)) {
                        vlStrAuxStep = "Destinatarios";
                        vlMailMesage.To.Add(mail.To);
                    }
                    if (!string.IsNullOrEmpty(mail.CC)) {
                        vlStrAuxStep = "Copias";
                        vlMailMesage.CC.Add(mail.CC);
                    }
                    if (!string.IsNullOrEmpty(mail.CCO))
                    {
                        vlStrAuxStep = "Copias Ocultas";
                        vlMailMesage.Bcc.Add(mail.CCO);
                    }
                    if (!string.IsNullOrEmpty(config.EmailCCO))
                    {
                        vlStrAuxStep = "Copias Ocultas Fijas";
                        vlMailMesage.Bcc.Add(config.EmailCCO);
                    }
                    vlStrAuxStep = "Asunto";
                    vlMailMesage.Subject = mail.Subjet;
                    vlStrAuxStep = "Cuerpo";
                    vlMailMesage.Body = mail.Body;
                    vlMailMesage.IsBodyHtml = mail.IsHTML;
                    vlStrAuxStep = "Adjuntos";
                    if (attachments != null) {
                        foreach (var i in attachments) {
                            vlMailMesage.Attachments.Add(new Attachment(i.FilePath));
                        }
                    }
                }
                catch (Exception ex)
                {
                    vOutStrError = string.Format("Error al preparar el Correo, paso '{0}', error: " + Environment.NewLine + "{1}", vlStrAuxStep, ex.Message);
                }
                using (var vlSmtpClient = new SmtpClient())
                {
                    if (string.IsNullOrEmpty(vOutStrError))
                    {
                        try
                        {
                            vlStrAuxStep = "TimeOut";
                            if (config.TimeOut.HasValue && config.TimeOut.Value > 0) {
                                vlSmtpClient.Timeout = config.TimeOut.Value * 1000;
                            }
                            vlStrAuxStep = "Host";
                            vlSmtpClient.Host = config.Server;
                            vlStrAuxStep = "Puerto";
                            if (config.SmtpPort.HasValue && config.SmtpPort.Value > 0) {
                                vlSmtpClient.Port = config.SmtpPort.Value;
                            }
                            vlStrAuxStep = "Autenticacion";
                            vlSmtpClient.Credentials = new System.Net.NetworkCredential(config.User, config.Pass);
                            vlStrAuxStep = "SSL";
                            vlSmtpClient.EnableSsl = config.UseSsl;
                        }
                        catch (Exception ex)
                        {

                            vOutStrError = string.Format("Error al momento de establecer el parametro '{0}' del Correo. " + Environment.NewLine + "{1}", vlStrAuxStep, ex.Message);
                        }
                    }
                    if (string.IsNullOrEmpty(vOutStrError)) {
                        try
                        {
                            vlSmtpClient.Send(vlMailMesage);
                        }
                        catch (Exception ex)
                        {
                            vOutStrError = string.Format("Error al momento de enviar el correo del Correo. " + Environment.NewLine + "{0}", ex.Message);
                        }
                    }
                }
            }
            return string.IsNullOrEmpty(vOutStrError);
        }

        public void Dispose()
        {
            //Nada que hacer
        }

        public bool SendOneEmail(string mailErrorContent, out string error)
        {
            error = string.Empty;
            EmailSender config = null;
            emailBody mail = null;
            try
            {
                config = new EmailSender()
                {
                    Server = _Server(),
                    User = _User(),
                    Pass = _Pass(),
                    EmailAddres = _EmailSender(),
                    EmailMask = _EmailMask(),
                    UseSsl = _UseSsl()
                };
                mail = new emailBody()
                {
                    Subjet = string.IsNullOrEmpty(mailErrorContent) ? _SubjectSuccess() : _SubjectFailure(),
                    Body = string.IsNullOrEmpty(mailErrorContent) ? _BodySuccess() : string.Format(_BodyFailure(), Environment.NewLine + mailErrorContent),
                    IsHTML = false,
                    To = _To(),
                    CC = _Cc()
                };
                if (int.TryParse(_Port(), out int port))
                    config.SmtpPort = port;
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            if (string.IsNullOrEmpty(error))
                return SendOneEmail(config, mail, null, ref error);
            else
                return false;
        }
    }
}
