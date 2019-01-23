using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ToolBackupDataBaseNet.Sql
{
    class Backing:IDisposable
    {
        private string _currentDataBase = string.Empty;
        private string _firstErrorTrigger = string.Empty;
        private static readonly Func<string, string> _QuoteIdentifier = _ => "[" + _.Replace("]", "]]") + "]";
        private static readonly Func<string, string> _Quotestring = _ => "'" + _.Replace("'", "''") + "'";

        //private static readonly Func<bool> _WindowsUser = () => Config.GetBoolProperty("Sql_Conn_WindowsUser");
        private static readonly Func<string> _Server = () => Config.GetstringProperty("Sql_Conn_Server");
        private static readonly Func<int?> _Port = () => Config.GetIntNullableProperty("Sql_Conn_Port");
        private static readonly Func<string> _User = () => Config.GetstringProperty("Sql_Conn_User");
        private static readonly Func<string> _Psw = () => Config.GetstringProperty("Sql_Conn_Psw");

        private static readonly Func<string> _BackupFolderPath = () => Config.GetstringProperty("Sql_Backup_FolderPath").Trim();
        private static readonly Func<bool> _UseCompression = () => Config.GetBoolProperty("Sql_Backup_UseCompression");
        private static readonly Func<bool> _AddDateTime = () => Config.GetBoolProperty("Sql_Backup_AddDateTimeInFileName");
        private static readonly Func<bool> _DeletePreviousFiles = () => Config.GetBoolProperty("Sql_Backup_DeleteExistingPreviousFiles"); 

        private static string _Connectionstring()
        {
            //Recuperamos la cadena de conexion requerida para los querys
            //;Initial Catalog=master
            int? vlPort = _Port();
            //if (_WindowsUser())
            //    return string.Format("Persist Security Info=True;Data Source={0}{1};", _Server(), (vlPort.HasValue) ? ":" + vlPort.ToString() : "");
            //else
            return string.Format("Persist Security Info=False;Data Source={0}{1};User ID={2};Password={3};", _Server(), (vlPort.HasValue) ? ":" + vlPort.ToString() : "", _User(), _Psw());
        }
        private static bool _ReturnData(ref DataTable outTable, string query, List<SqlParameter> parameters, ref string error)
        {
            //Recuperamos los datos del query requerido
            //vInCommandType As System.Data.CommandType = CommandType.Text, Optional ByVal vInIntCommandTimeOut As Integer = 180
            error = string.Empty;
            using (var vlConn = new SqlConnection(_Connectionstring()))
            {
                using (var vlCommand = new SqlCommand(query, vlConn))
                {
                    vlCommand.CommandTimeout = 180;
                    vlCommand.CommandType = CommandType.Text;
                    if (parameters != null)
                    {
                        foreach (SqlParameter i in parameters)
                        {
                            vlCommand.Parameters.Add(i);
                        }
                    }
                    try
                    {
                        vlConn.Open();
                        using (var vlAdapter = new SqlDataAdapter(vlCommand))
                        {
                            using (var vlDataSet = new DataSet())
                            {
                                vlAdapter.Fill(vlDataSet);
                                outTable = vlDataSet.Tables[0];
                            }
                        }
                        if (parameters != null)
                        {
                            vlCommand.Parameters.Clear();
                        }
                    }
                    catch (Exception ex)
                    {
                        outTable = null;
                        error = ex.Message;
                    }
                    try
                    {
                        vlConn.Close();
                    }
                    catch (Exception) { }
                }
            }
            return string.IsNullOrEmpty(error);
        }
        private static bool _GetDatabases(ref List<KeyValuePair<string,long>> dataBases, ref string error)
        {
            //retorna la lista de empresas
            DataTable vlTbl = null;
            //state = 0 ->ONLINE
            //and Only look at databases to which we have access
            string vlQuery = "SELECT db_name(database_id)[DB], sum(size)/1024[MB] FROM sys.master_files WHERE state = 0 AND db_name(database_id) NOT IN ('master', 'model', 'msdb', 'tempdb') GROUP BY database_id ORDER BY 1";
            error = string.Empty;
            dataBases = new List<KeyValuePair<string, long>>();
            List<SqlParameter> parameters = null;
            if (_ReturnData(ref vlTbl, vlQuery, parameters, ref error)) {
                foreach (DataRow r in vlTbl.Rows)
                {
                    dataBases.Add(new KeyValuePair<string, long> (r["DB"].ToString(), int.Parse(r["MB"].ToString())));
                }
                vlTbl.Clear();
                vlTbl.Dispose();
            }
            vlTbl = null;
            return string.IsNullOrEmpty(error);
        }

        public readonly Func<bool> NeedToBackupDatabases = () => Config.GetBoolProperty("Sql_Backup_Enable");

        #region "events"
        private EventHandler<SqlProgressArgs> _onTrigger;
        public enum SqlProgressType
        {
            ProcessStarted,
            BackupStarted,
            Progress,
            BackupFinished,
            ProcessFinished
        }
        public class SqlProgressArgs : EventArgs
        {
            public SqlProgressArgs(SqlProgressType step, double percent, string error, string dataBaseName)
            {
                Step = step;
                Percent = percent;
                Error = error;
                DataBaseName = dataBaseName;
            }

            public SqlProgressType Step { get; private set; }
            public double Percent { get; private set; }
            public string Error { get; private set; }
            public string DataBaseName { get; private set; }
        }
        public event EventHandler<SqlProgressArgs> Trigger
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
        protected void OnTrigger(SqlProgressArgs e)
        {
            _onTrigger?.Invoke(this, e);
        }
        #endregion

        //public event EventHandler ProgressStarted;
        //public event SqlInfoMessageEventHandler ProgressEvent;
        //public event EventHandler ProgressFinished;

        public bool ExecuteBackupDatabases(out string error)
        {
            error = string.Empty;
            List<KeyValuePair<string, long>> basesDeDatos = null;
            if (_GetDatabases(ref basesDeDatos, ref error))
            {
                OnTrigger(new SqlProgressArgs(SqlProgressType.ProcessStarted, 0, string.Empty, string.Empty));
                try
                {
                    string fecha = _AddDateTime() ? DateTime.Now.ToString("_yyyyMMdd_HHmmss") : string.Empty;
                    string folderPath = _BackupFolderPath() + (_BackupFolderPath().Substring(_BackupFolderPath().Length - 1, 1).Equals(@"\") ? "" : @"\");
                    //if (ProgressStarted != null) ProgressStarted.Invoke(this, new EventArgs());
                    if (_DeletePreviousFiles()) {
                        var Dir = new DirectoryInfo(folderPath);
                        var files = Dir.EnumerateFiles("*.bak", SearchOption.TopDirectoryOnly);
                        foreach(var f in files)
                        {
                            try { f.Delete(); } catch (Exception) { }
                        }
                    }
                    for (var i = 0; i < basesDeDatos.Count; i++)//foreach (var i in basesDeDatos)
                    {
                        _currentDataBase = basesDeDatos[i].Key;
                        OnTrigger(new SqlProgressArgs(SqlProgressType.BackupStarted, 0, string.Empty, _currentDataBase));
                        var err = string.Empty;
                        if (!BackupDatabase(_currentDataBase, folderPath, string.Format("{0}{1}", _currentDataBase, fecha), _UseCompression(), basesDeDatos[i].Value, out err))
                            error += string.Format("Error al respaldar la BD '{0}': {1}" + Environment.NewLine, _currentDataBase, err);
                        OnTrigger(new SqlProgressArgs(SqlProgressType.BackupFinished, 0, err, _currentDataBase));
                    }
                    _currentDataBase = string.Empty;
                    //if (ProgressFinished != null) ProgressFinished.Invoke(this, new EventArgs());
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
                OnTrigger(new SqlProgressArgs(SqlProgressType.ProcessFinished, 100, string.Empty, string.Empty));
            }
            return string.IsNullOrEmpty(error);
        }

        private bool BackupDatabase (string database, string backupPath, string backupName, bool useCompression, long size, out string error)
        {
            using (var conn = new SqlConnection(_Connectionstring()))
            {
                //if (ProgressEvent != null) { 
                conn.FireInfoMessageEventOnUserErrors = true;
                conn.InfoMessage += EventHandler;// ProgressEvent;
                //}
                try
                {
                    int stats = 10;
                    if (size > 300)
                        stats = 1;
                    else if (size > 200)
                        stats = 2;
                    else if (size > 100)
                        stats = 4;
                    else if (size > 50)
                        stats = 5;
                    conn.Open();
                    _firstErrorTrigger = string.Empty;
                    var command = string.Format("BACKUP DATABASE {0} TO  DISK = {1} WITH NOINIT, NOUNLOAD, NAME={2}, NOSKIP, STATS = {3}, NOFORMAT {4}",
                                                    _QuoteIdentifier(database),
                                                    _Quotestring(backupPath + backupName + ".bak"),
                                                    _Quotestring(backupName),
                                                    stats,
                                                    useCompression ? ", COMPRESSION" : "");
                    using (var cmd = new SqlCommand(command, conn))
                    {
                        cmd.CommandTimeout = 60 * 30;//30 minutos
                        cmd.ExecuteNonQuery();
                    }
                    error = _firstErrorTrigger;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
                finally {
                    if (conn.State == ConnectionState.Connecting || conn.State == ConnectionState.Executing || conn.State == ConnectionState.Open)
                        conn.Close();
                }
                //if (ProgressEvent != null)
                //{
                conn.InfoMessage -= EventHandler;// ProgressEvent;
                conn.FireInfoMessageEventOnUserErrors = false;
                //}
            }
            return string.IsNullOrEmpty(error);
        }

        public void Dispose()
        {
            //Nada que limpiar
        }

        private void EventHandler(object sender, SqlInfoMessageEventArgs e)
        {
            foreach (SqlError info in e.Errors)
            {
                if (info.Class > 10)
                {
                    if (string.IsNullOrEmpty(_firstErrorTrigger))
                        _firstErrorTrigger = (e.Errors.Count > 0) ? e.Errors[0].Message : string.Empty;
                    // TODO: treat this as a genuine error
                    OnTrigger(new SqlProgressArgs(SqlProgressType.Progress, 0, _firstErrorTrigger, _currentDataBase));
                }
                else
                {
                    string menssage = string.Empty;
                    double vlPorcentaje = 0;
                    switch (info.Number)
                    {
                        case 3211:
                            if (e.Message.Contains("por ciento procesado."))
                            {
                                if (!double.TryParse(e.Message.Replace("por ciento procesado.", ""), out vlPorcentaje))
                                    vlPorcentaje = 0;
                            }
                            break;
                        case 4035:
                            vlPorcentaje = 100;
                            break;
                        case 3014:
                            vlPorcentaje = 100;
                            break;
                        default:
                            break;
                    }                    
                    // TODO: treat this as a progress message
                    OnTrigger(new SqlProgressArgs(SqlProgressType.Progress, vlPorcentaje, menssage, _currentDataBase));
                }
            }
        }
    }
}
