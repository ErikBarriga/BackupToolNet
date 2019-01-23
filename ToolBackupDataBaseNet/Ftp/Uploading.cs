using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ToolBackupDataBaseNet.Ftp
{
    class Uploading:IDisposable
    {
        private string _currentFileName = string.Empty;
        private double _currentProgress = 0;
        private string _firstErrorTrigger = string.Empty;
        private static readonly Func<string> _FolderSource = () => Config.GetstringProperty("Ftp_Folder_WithFilesToUpload");
        private static readonly Func<string> _FilterExpression = () => Config.GetstringProperty("Ftp_Folder_FilterExpression");
        private static readonly Func<bool> _AddDateTime = () => Config.GetBoolProperty("Ftp_Destiny_AddDateTimeInFileName");
        private static readonly Func<bool> _DeleteAfterCompressing = () => Config.GetBoolProperty("Ftp_Source_DeleteAfterUploading");

        private static readonly Func<string> _Url = () => Config.GetstringProperty("Ftp_Url");
        private static readonly Func<string> _Path = () => Config.GetstringProperty("Ftp_ServerPath");
        private static readonly Func<string> _User = () => Config.GetstringProperty("Ftp_User");
        private static readonly Func<string> _Pass = () => Config.GetstringProperty("Ftp_Pass");

        #region "events"
        private EventHandler<FtpProgressArgs> _onTrigger;
        public enum FtpProgressType
        {
            ProcessStarted,
            FileUploadingStarted,
            Progress,
            FileUploadingFinished,
            ProcessFinished
        }
        public class FtpProgressArgs : EventArgs
        {
            public FtpProgressArgs(FtpProgressType step, double percent, string error, string fileName)
            {
                Step = step;
                Percent = percent;
                FileName = fileName;
                Error = error;
            }

            public FtpProgressType Step { get; private set; }
            public double Percent { get; private set; }
            public string Error { get; private set; }
            public string FileName { get; private set; }

        }
        public event EventHandler<FtpProgressArgs> Trigger
        {
            add
            {
                _onTrigger += value;
            }
            remove
            {
                _onTrigger -= value;
            }
        }
        protected void OnTrigger(FtpProgressArgs e)
        {
            _onTrigger?.Invoke(this, e);
        }
        #endregion

        public readonly Func<bool> NeedToUploadFiles = () => Config.GetBoolProperty("Ftp_Upload_Enable");

        private bool UploadFile(string localFilePath, string serverFileName, out string error)
        {
            var vlStrUrl = _Url();
            var vlStrPath = _Path();
            if (!vlStrUrl.Contains(":")) vlStrUrl = "ftp://" + vlStrUrl;
            if (!vlStrPath.Substring(1,1).Equals("/") && !vlStrPath.Substring(vlStrPath.Length - 1, 1).Equals("/")) vlStrPath = "/" + vlStrPath;
            if (!vlStrPath.Substring(vlStrPath.Length - 1, 1).Equals("/")) vlStrPath = vlStrPath + "/";
            error = string.Empty;
            try
            {
                Uri fullUrl = new Uri(vlStrUrl + vlStrPath + serverFileName);
                double _previousProgress = 0;
                _currentProgress = 0;
                _firstErrorTrigger = string.Empty;
                using (var client = new WebClient())
                {
                    client.Credentials = new NetworkCredential(_User(), _Pass());
                    client.UploadProgressChanged += Client_UploadProgressChanged;
                    client.UploadFileCompleted += Client_UploadFileCompleted;
                    //client.UploadFile(vlStrUrl + vlStrPath + serverFileName, WebRequestMethods.Ftp.UploadFile, localFilePath);
                    client.UploadFileAsync(fullUrl, WebRequestMethods.Ftp.UploadFile, localFilePath);

                    while (_currentProgress < 100 && string.IsNullOrEmpty(_firstErrorTrigger)) {
                        System.Threading.Thread.Sleep(100);
                        if (!_previousProgress.Equals(_currentProgress))
                        {
                            _previousProgress = _currentProgress;
                            OnTrigger(new FtpProgressArgs(FtpProgressType.Progress, _currentProgress, string.Empty, _currentFileName));
                        }
                    }

                    client.UploadProgressChanged -= Client_UploadProgressChanged;
                    client.UploadFileCompleted -= Client_UploadFileCompleted;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            OnTrigger(new FtpProgressArgs(FtpProgressType.Progress, _currentProgress, _firstErrorTrigger, _currentFileName));
            return string.IsNullOrEmpty(error);
        }

        private void Client_UploadFileCompleted(object sender, UploadFileCompletedEventArgs e)
        {
            if (string.IsNullOrEmpty(_firstErrorTrigger) && !string.IsNullOrEmpty(e.Error?.Message))
                _firstErrorTrigger = e.Error.Message;
            else
                _currentProgress = 100;
        }

        private void Client_UploadProgressChanged(object sender, UploadProgressChangedEventArgs e)
        {
            _currentProgress = 100 * e.BytesSent / e.TotalBytesToSend;
        }

        public bool UploadFiles(out string error)
        {
            OnTrigger(new FtpProgressArgs(FtpProgressType.ProcessStarted, 0, string.Empty, string.Empty));
            error = string.Empty;
            try
            {
                DirectoryInfo Dir = new DirectoryInfo(_FolderSource());
                if (!Dir.Exists)
                    error = "El directorio especificado para comprimir cada uno de sus archivos no existe o no es accesible";
                else {
                    List<FileInfo> files = Dir.EnumerateFiles(_FilterExpression(), SearchOption.AllDirectories).ToList();
                    for (var f = 0; f < files.Count; f++)
                    {
                        _currentFileName = files[f].Name;
                        var serverFileName = _currentFileName;
                        if (_AddDateTime())
                        {
                            if (serverFileName.Contains("."))
                            {
                                var s = serverFileName.Split('.');
                                s[s.Length - 2] = s[s.Length - 2] + DateTime.Now.ToString("_yyyyMMdd_HHmmss");
                                serverFileName = string.Join(".", s);
                            }
                            else
                                serverFileName = serverFileName + DateTime.Now.ToString("_yyyyMMdd_HHmmss");
                        }
                        OnTrigger(new FtpProgressArgs(FtpProgressType.FileUploadingStarted, f / (double)files.Count, string.Empty, _currentFileName));
                        if (!UploadFile(files[f].FullName, serverFileName, out string localError))
                            error = error + Environment.NewLine + localError;
                        else if (_DeleteAfterCompressing())
                            try { files[f].Delete(); } catch (Exception) { }
                        OnTrigger(new FtpProgressArgs(FtpProgressType.FileUploadingFinished, f / (double)files.Count, string.Empty, _currentFileName));
                    }
                }
            }
            catch (Exception)
            {

                throw;
            }
            OnTrigger(new FtpProgressArgs(FtpProgressType.ProcessFinished, 0, error, string.Empty));
            return string.IsNullOrEmpty(error);
        }

        public void Dispose()
        {
            //Nothing to do
        }
    }
}
