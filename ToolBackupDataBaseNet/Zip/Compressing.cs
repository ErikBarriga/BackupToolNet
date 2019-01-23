using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ToolBackupDataBaseNet.Zip
{
    class Compressing : IDisposable
    {
        private string _currentFile = string.Empty;
        private static readonly Func<string> PathNameResult = () => Config.GetstringProperty("Zip_Destiny_Folder_Path").Trim();

        private static readonly Func<string> ByFolder_FullFolderToCompress = () => Config.GetstringProperty("Zip_Source_FullFolderPath");
        private static readonly Func<string> ByFolder_FileNameResult = () => Config.GetstringProperty("Zip_Destiny_FileName");
        private static readonly Func<bool> _AddDateTime = () => Config.GetBoolProperty("Zip_Destiny_AddDateTimeInFileName");

        private static readonly Func<string> ByFile_FolderToCompressEveryFile = () => Config.GetstringProperty("Zip_Source_EachFileInFolder_Path");
        private static readonly Func<string> ByFile_FilterExpression = () => Config.GetstringProperty("Zip_Source_FilterExpression");
        private static readonly Func<bool> ByFile_DeleteAfterCompressing = () => Config.GetBoolProperty("Zip_Source_DeleteAfterCompressing");

        private static readonly Func<CompressionLevel> Compression = () => (int.TryParse(Config.GetstringProperty("Zip_CompressionLevel"), out int vlIntTmp) && vlIntTmp > 0 && vlIntTmp < 2) ? (CompressionLevel)vlIntTmp : CompressionLevel.NoCompression;

        public readonly Func<bool> NeedToCompressFullFolder = () => Config.GetBoolProperty("Zip_Files_Enable");
        public readonly Func<bool> NeedToCompressEachFile = () => Config.GetBoolProperty("Zip_Files_Enable");

        #region "events"
        private EventHandler<ZipperProgressArgs> _onTrigger;
        public enum ZipperProgressType
        {
            ProcessStarted,
            FileCompressionStarted,
            Progress,
            FileCompressionFinished,
            ProcessFinished
        }
        public class ZipperProgressArgs : EventArgs
        {
            public ZipperProgressArgs(ZipperProgressType step, double percent, string error, string fileName)
            {
                Step = step;
                Percent = percent;
                FileName = fileName;
                Error = error;
            }

            public ZipperProgressType Step { get; private set; }
            public double Percent { get; private set; }
            public string Error { get; private set; }
            public string FileName { get; private set; }
            
        }
        public event EventHandler<ZipperProgressArgs> Trigger
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
        protected void OnTrigger(ZipperProgressArgs e)
        {
            _onTrigger?.Invoke(this, e);
        }
        #endregion
        #region "compress"
        private static T InlineAssignHelper<T>(ref T target, T value)
        {
            target = value;
            return value;
        }
        private bool CompressFiles(List<FileInfo> sourceFileInfos, string destinyFileName, string destinyDirectoryPath, out string error, CompressionLevel compressionLevel, bool overwrite)
        {
            const int BlockSizeToRead = 1048576; //1MB Buffer
            var Buffer = new byte[BlockSizeToRead - 1];
            long LiveProg = 0;
            if (!destinyDirectoryPath.Substring(destinyDirectoryPath.Length - 1, 1).Equals(@"\"))
                destinyDirectoryPath = destinyDirectoryPath + @"\";
            destinyFileName = destinyDirectoryPath + destinyFileName;
            error = string.Empty;
            try
            {
                if (!Directory.Exists(destinyDirectoryPath))
                    throw new Exception(string.Format("Ruta de Destino inaccesible {0}", destinyDirectoryPath));
                else if (!sourceFileInfos.Any())
                    throw new Exception("Rutas de Origen no especificadas");
                else if (sourceFileInfos.Where(_ => !_.Exists).Any())
                    throw new Exception(string.Format("Ruta de Origen inaccesible {0}", sourceFileInfos.Where(_ => !_.Exists).First()));
                else if (File.Exists(destinyFileName))
                {
                    if (!overwrite)
                        throw new Exception(string.Format("Target File Already Exists ({0})", destinyFileName));
                    else
                        File.Delete(destinyFileName);
                }
                using (var FS = new FileStream(destinyFileName, FileMode.CreateNew, FileAccess.Write))
                {
                    using (var Archive = new ZipArchive(FS, ZipArchiveMode.Create))
                    {
                        ZipArchiveEntry Entry = null;
                        long TotalBytesRequired = sourceFileInfos.Sum(_ => _.Length);
                        long PrevProg = 0;
                        int BytesRead = 0;
                        long TotalBytesRead = 0;
                        foreach (var FI in sourceFileInfos)
                        {
                            _currentFile = FI.Name;
                            try
                            {
                                OnTrigger(new ZipperProgressArgs(ZipperProgressType.FileCompressionStarted, LiveProg, string.Empty, _currentFile));
                                using (FileStream Reader = File.Open(FI.FullName, FileMode.Open, FileAccess.Read))
                                {
                                    Entry = Archive.CreateEntry(_currentFile, compressionLevel);
                                    using (Stream Writer = Entry.Open())
                                    {
                                        while (InlineAssignHelper(ref BytesRead, Reader.Read(Buffer, 0, Buffer.Length - 1)) > 0)
                                        {
                                            Writer.Write(Buffer, 0, BytesRead);
                                            TotalBytesRead += BytesRead;
                                            LiveProg = (long)(100 * TotalBytesRead / TotalBytesRequired);
                                            if (LiveProg != PrevProg)
                                            {
                                                PrevProg = LiveProg;
                                                OnTrigger(new ZipperProgressArgs(ZipperProgressType.Progress, LiveProg, string.Empty, _currentFile));
                                            }
                                        }
                                    }
                                }
                                OnTrigger(new ZipperProgressArgs(ZipperProgressType.FileCompressionFinished, LiveProg, string.Empty, _currentFile));
                            }
                            catch (Exception ex)
                            {
                                TotalBytesRead += FI.Length;
                                throw new Exception(string.Format("Unable to add file to archive: '{0}' Error: {1}", FI.FullName, ex.Message));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            return string.IsNullOrEmpty(error);
        }
        private bool CompressFiles(List<string> sourceFiles, string destinyFileName, string destinyDirectoryPath, out string error, CompressionLevel compressionLevel, bool overwrite)
        {
            List<FileInfo> sourceFileInfos = sourceFiles.Select(_ => new FileInfo(_)).ToList();
            return CompressFiles(sourceFileInfos, destinyFileName, destinyDirectoryPath, out error, compressionLevel, overwrite);
        }
        private bool CompressFile(FileInfo sourceFile, string destinyFileName, string destinyDirectoryPath, out string error, CompressionLevel compressionLevel, bool overwrite)
        {
            var sourceFileInfos = new List<FileInfo>
            {
                sourceFile
            };
            return CompressFiles(sourceFileInfos, destinyFileName, destinyDirectoryPath, out error, compressionLevel, overwrite);
        }
        private bool CompressFolder(string sourceFolder, string destinyFileName, string destinyDirectoryPath, out string error, CompressionLevel compressionLevel, bool overwrite)
        {
            bool zipping = true;
            long TotalBytesRead = 0;
            string fullFileName;
            if (!destinyDirectoryPath.Substring(destinyDirectoryPath.Length - 1, 1).Equals(@"\")) destinyDirectoryPath = destinyDirectoryPath + @"\";
            if (!destinyFileName.Contains(".")) destinyFileName = destinyFileName + ".zip";
            if (_AddDateTime()) {
                var s = destinyFileName.Split('.');
                s[s.Length - 2] = s[s.Length - 2] + DateTime.Now.ToString("_yyyyMMdd_HHmmss");
                destinyFileName = string.Join(".", s);
            }
            fullFileName = destinyDirectoryPath + destinyFileName;
            error = string.Empty;
            OnTrigger(new ZipperProgressArgs(ZipperProgressType.ProcessStarted, TotalBytesRead, error, string.Empty));
            try
            {
                string internalError = string.Empty;
                if (File.Exists(fullFileName) && !overwrite)
                    throw new Exception(string.Format("Target File Already Exists ({0})", fullFileName));
                else if (!Directory.Exists(destinyDirectoryPath))
                    throw new Exception(string.Format("Ruta de Destino inaccesible {0}", destinyDirectoryPath));
                else if (!Directory.Exists(sourceFolder))
                    throw new Exception(string.Format("Ruta de Origen inaccesible {0}", sourceFolder));
                else if (File.Exists(fullFileName))
                {
                    if (!overwrite)
                        throw new Exception(string.Format("Target File Already Exists ({0})", fullFileName));
                    else
                        File.Delete(fullFileName);
                }
                OnTrigger(new ZipperProgressArgs(ZipperProgressType.FileCompressionStarted, 100, error, sourceFolder));
                TotalBytesRead = Directory.GetFiles(sourceFolder).Select(_ => new FileInfo(_).Length).Aggregate((a, b) => a + b);
                Task.Run(() =>
                {
                    try
                    {
                        ZipFile.CreateFromDirectory(sourceFolder, fullFileName, compressionLevel, false);
                    }
                    catch (Exception er)
                    {
                        internalError = er.Message;
                    }
                    zipping = false;
                });
                while (zipping)
                {
                    var FI = new FileInfo(fullFileName);
                    if (FI.Exists)
                        OnTrigger(new ZipperProgressArgs(ZipperProgressType.Progress, Math.Round((FI.Length / (double)TotalBytesRead)*100,0), string.Empty, sourceFolder));
                    System.Threading.Thread.Sleep(100);
                }
                error = internalError;
                OnTrigger(new ZipperProgressArgs(ZipperProgressType.FileCompressionFinished, 100, error, sourceFolder));
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            OnTrigger(new ZipperProgressArgs(ZipperProgressType.ProcessFinished, 100, error, string.Empty));
            return string.IsNullOrEmpty(error);
        }

        public bool CompressFolder(out string error, bool overwrite)
        {
            return CompressFolder(ByFolder_FullFolderToCompress(), ByFolder_FileNameResult(), PathNameResult(), out error, Compression(), overwrite);
        }
        public bool CompressFiles(out string error, bool overwrite)
        {
            OnTrigger(new ZipperProgressArgs(ZipperProgressType.ProcessStarted, 0, string.Empty, string.Empty));
            try
            {
                var Dir = new DirectoryInfo(ByFile_FolderToCompressEveryFile());
                if (!Dir.Exists)
                    error = "El directorio especificado para comprimir cada uno de sus archivos no existe o no es accesible";
                else
                {
                    string fecha = _AddDateTime() ? DateTime.Now.ToString("_yyyyMMdd_HHmmss") : string.Empty;
                    error = string.Empty;
                    List<FileInfo> files = Dir.EnumerateFiles(ByFile_FilterExpression(), SearchOption.AllDirectories).ToList();
                    foreach (var f in files)
                    {
                        if (!CompressFile(f, f.Name.Replace(f.Extension, fecha + ".zip"), PathNameResult(), out string localError, Compression(), overwrite))
                            error = error + Environment.NewLine + localError;
                        else if (ByFile_DeleteAfterCompressing())
                            try { f.Delete(); } catch (Exception) { }
                    }
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            OnTrigger(new ZipperProgressArgs(ZipperProgressType.ProcessFinished, 0, error, string.Empty));
            return string.IsNullOrEmpty(error);
        }
        #endregion
        public void Dispose()
        {
            //Nothing to do
        }
    }
}
