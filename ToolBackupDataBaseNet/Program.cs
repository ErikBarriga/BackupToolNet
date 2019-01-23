using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ToolBackupDataBaseNet
{
    class Program
    {
        static void Main(string[] args)
        {
            var sb = new StringBuilder();
            using (var sql = new Sql.Backing())
            {
                if (sql.NeedToBackupDatabases())
                {
                    sql.Trigger += Sql_Trigger;
                    if (!sql.ExecuteBackupDatabases(out string error))
                        sb.AppendLine(error);
                    sql.Trigger -= Sql_Trigger;
                }
            }
            using (var zip = new Zip.Compressing())
            {
                zip.Trigger += Zip_Trigger;
                if (zip.NeedToCompressEachFile())
                {
                    if (!zip.CompressFiles(out string error, true))
                        sb.AppendLine(error);
                }
                if (zip.NeedToCompressFullFolder())
                {
                    if (!zip.CompressFolder(out string error, true))
                        sb.AppendLine(error);
                }
                zip.Trigger -= Zip_Trigger;
            }
            using (var ftp = new Ftp.Uploading())
            {
                if (ftp.NeedToUploadFiles())
                {
                    ftp.Trigger += Ftp_Trigger;
                    if (!ftp.UploadFiles(out string error))
                        sb.AppendLine(error);
                    ftp.Trigger -= Ftp_Trigger;
                }
            }

            using (var mail = new Mail.Email()) {
                Display.Write(Display.TypeText.Partial, "Email Send Start", false);
                if (!mail.SendOneEmail(sb.ToString(), out string error))
                    Display.Write(Display.TypeText.Error, error, false);
                Display.Write(Display.TypeText.Partial, "Email Send End", false);
            }
        }

        private static void Ftp_Trigger(object sender, Ftp.Uploading.FtpProgressArgs e)
        {
            switch (e.Step)
            {
                case Ftp.Uploading.FtpProgressType.ProcessStarted:
                    Display.Write(Display.TypeText.Partial, "Ftp Upload Start", false);
                    break;
                case Ftp.Uploading.FtpProgressType.FileUploadingStarted:
                    Display.Write(Display.TypeText.Default, e.FileName + " 0%", false);
                    break;
                case Ftp.Uploading.FtpProgressType.Progress:
                    Display.Write(Display.TypeText.Default, e.FileName + " " + e.Percent.ToString("N0") + "% " + e.Error, true);
                    break;
                case Ftp.Uploading.FtpProgressType.FileUploadingFinished:
                    if (string.IsNullOrEmpty(e.Error))
                        Display.Write(Display.TypeText.OK, e.FileName + " 100%", true);
                    else
                        Display.Write(Display.TypeText.Error, e.FileName + " " + e.Error, true);
                    break;
                case Ftp.Uploading.FtpProgressType.ProcessFinished:
                    Display.Write(Display.TypeText.Partial, "Ftp Upload End", false);
                    break;
            }
        }

        private static void Zip_Trigger(object sender, Zip.Compressing.ZipperProgressArgs e)
        {
            switch (e.Step)
            {
                case Zip.Compressing.ZipperProgressType.ProcessStarted:
                    Display.Write(Display.TypeText.Partial, "Zip Compression Start", false);
                    break;
                case Zip.Compressing.ZipperProgressType.FileCompressionStarted:
                    Display.Write(Display.TypeText.Default, e.FileName + " 0%", false);
                    break;
                case Zip.Compressing.ZipperProgressType.Progress:
                    Display.Write(Display.TypeText.Default, e.FileName + " " + e.Percent.ToString() + "% " + e.Error, true);
                    break;
                case Zip.Compressing.ZipperProgressType.FileCompressionFinished:
                    if (string.IsNullOrEmpty(e.Error))
                        Display.Write(Display.TypeText.OK, e.FileName + " 100%", true);
                    else
                        Display.Write(Display.TypeText.Error, e.FileName + " " + e.Error, true);
                    break;
                case Zip.Compressing.ZipperProgressType.ProcessFinished:
                    Display.Write(Display.TypeText.Partial, "Zip Compression End", false);
                    break;
            }
        }

        private static void Sql_Trigger(object sender, Sql.Backing.SqlProgressArgs e)
        {
            switch (e.Step)
            {
                case Sql.Backing.SqlProgressType.ProcessStarted:
                    Display.Write(Display.TypeText.Partial, "Sql Backup Start", false);
                    break;
                case Sql.Backing.SqlProgressType.BackupStarted:
                    Display.Write(Display.TypeText.Default, e.DataBaseName + " 0%", false);
                    break;
                case Sql.Backing.SqlProgressType.Progress:
                    Display.Write(Display.TypeText.Default, e.DataBaseName + " " + e.Percent.ToString() + e.Error, true);
                    break;
                case Sql.Backing.SqlProgressType.BackupFinished:
                    if (string.IsNullOrEmpty(e.Error))
                        Display.Write(Display.TypeText.OK, e.DataBaseName + " 100%", true);
                    else
                        Display.Write(Display.TypeText.Error, e.DataBaseName + " " + e.Error, true);
                    break;
                case Sql.Backing.SqlProgressType.ProcessFinished:
                    Display.Write(Display.TypeText.Partial, "Sql Backup End", false);
                    break;
            }
        }
    }
}
