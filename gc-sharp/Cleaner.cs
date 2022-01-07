using MySqlConnector;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace gc
{
    internal class Cleaner
    {
        internal bool Simulate = true;
        internal bool Confirm = false;
        internal bool Check = false;
        internal string FilesPath;

        private Config Config;
        private MySqlConnection Connection;

        public Cleaner(bool simulate, bool confirm, bool check)
        {
            Simulate = simulate;
            Confirm = confirm;
            Check = check;

            InitConfig();
            InitDB();
            InitPath();
        }

        public Cleaner(bool simulate, bool confirm) : this(simulate, confirm, false)
        {
        }
        public Cleaner(bool simulate) : this(simulate, false, false)
        {
        }
        public Cleaner() : this(true, false, false)
        {
        }

        internal void InitConfig()
        {
            Logger.Info("Reading app config...", false);

            if (!File.Exists("config.json"))
            {
                Console.WriteLine("FAIL");

                throw new Exception("Config file does not exists!");
            }

            string jsonString = File.ReadAllText("config.json");
            Config = JsonSerializer.Deserialize<Config>(jsonString);

            Console.WriteLine("OK");
        }

        internal void InitDB()
        {
            Logger.Info("Initializing db connection...", false);

            var builder = new MySqlConnectionStringBuilder
            {
                Server = Config.DB.Host,
                Port = uint.Parse(Config.DB.Port),
                Database = Config.DB.DBName,
                UserID = Config.DB.User,
                Password = Config.DB.Password,
            };

            Connection = new MySqlConnection(builder.ConnectionString);

            Connection.Open();

            if (!Connection.Ping())
            {
                Console.WriteLine("FAIL");

                throw new Exception("Can't connect to DB");
            }

            Console.WriteLine("OK");
        }

        internal void InitPath()
        {
            Logger.Info("Locating files...", false);

            FilesPath = Config.Files.Path;

            if (!Path.IsPathRooted(FilesPath))
            {
                FilesPath = Path.GetFullPath(Config.Files.Path);
            }

            if (!Directory.Exists(FilesPath))
            {
                Console.WriteLine("FAIL");

                throw new Exception($"Directory at '{FilesPath}' does not exists.");
            }

            Console.WriteLine("OK");
        }

        public async Task CleanUp()
        {
            Logger.Info("Cleaning up files task started...");

            await CleanUnattachedFiles();

            if (Check)
            {
                await CheckCleanUp();
            }

            Connection.Close();

            Logger.Info("Cleaning up files task finished");

            return;
        }

        internal MySqlCommand CreateCommand()
        {
            return Connection.CreateCommand();
        }

        internal MySqlCommand CreateCommand(string sql)
        {
            var cmd = Connection.CreateCommand();

            cmd.CommandText = sql;

            return cmd;
        }

        internal async Task<int> CountFilesInDB()
        {
            var cmd = CreateCommand($"SELECT COUNT(*) FROM `{Config.Files.Table}`");

            if (Config.Debug)
            {
                Logger.Info($"[DEBUG] sql: {cmd.CommandText}");
            }

            int dbFilesCount = 0;

            object result = await cmd.ExecuteScalarAsync();
            if (result != null)
            {
                dbFilesCount = Convert.ToInt32(result);
            }

            return dbFilesCount;
        }

        internal async Task CleanUnattachedFiles()
        {
            Logger.Info("Task start: Clean up files unattached to db");
            var timeStart = DateTime.Now;

            int count = 0;
            long size = 0;

            var timeWalk = DateTime.Now;

            int dbFilesCount = await CountFilesInDB();

            Logger.Info($"File entries in DB to check: {dbFilesCount}");

            var cmd = CreateCommand($"SELECT `id`, `file` FROM `{Config.Files.Table}`");

            Logger.Info("Scanning files in db...");

            var ignoreList = new List<int>();
            var existList = new List<string>();

            int checkedRows = 1;

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    int id = reader.GetInt32(0);
                    string dbPath = reader.GetString(1);

                    string path = Path.GetFullPath(Path.Join(FilesPath, dbPath.Replace("/pic/", "")));

                    if (!File.Exists(path))
                    {
                        ignoreList.Add(id);

                        if (Config.Debug)
                        {
                            Logger.Debug($"[{checkedRows} of {dbFilesCount}] File {id} in path `{path}` not exists - add to ignore list");
                        }
                    }
                    else
                    {
                        if (Config.Debug)
                        {
                            Logger.Debug($"[{checkedRows} of {dbFilesCount}] File {id} in path `{path}` exists");
                        }

                        existList.Add(Path.GetFileNameWithoutExtension(path));
                    }

                    Logger.Progress($"{checkedRows} of {dbFilesCount} >> {checkedRows / (double)dbFilesCount:0.00%}");

                    checkedRows++;
                }

                Console.WriteLine();
            }

            checkedRows--;

            Logger.Info("Scanning finished");

            Logger.Info($"Checked rows: {checkedRows}");
            Logger.Info($"Ignored rows (files in DB, but not exist IRL): {ignoreList.Count}");
            Logger.Info($"Time taken: {DateTime.Now - timeWalk}");

            timeWalk = DateTime.Now;
            var timeStep = DateTime.Now;

            int countAll = 0;
            long sizeAll = 0;

            var toRemove = new Dictionary<string, List<string>>();

            var fileList = Directory.GetFiles(FilesPath, "*", SearchOption.TopDirectoryOnly);

            Logger.Info("Scanning files in folder...");

            foreach (var path in fileList)
            {
                var attr = File.GetAttributes(path);
                var info = new FileInfo(path);

                if (attr.HasFlag(FileAttributes.Directory))
                {
                    continue;
                }

                countAll++;
                sizeAll += info.Length;

                if ((DateTime.Now - timeStep).Seconds >= 1)
                {
                    Logger.Info($"Scanned {countAll} files");

                    timeStep = DateTime.Now;
                }

                string fileExt = Path.GetExtension(path);
                string fileName = Path.GetFileNameWithoutExtension(path);

                if (fileName.Length < 32)
                {
                    continue;
                }

                bool exists = false;

                string baseName = fileName[..32];

                if (existList.Contains(baseName))
                {
                    exists = true;
                }

                bool markRemoved = toRemove.ContainsKey(baseName);

                if (!exists)
                {
                    if (!markRemoved)
                    {
                        toRemove[baseName] = new List<string>();
                    }

                    toRemove[baseName].Add(path);
                }

                if (Config.Debug)
                {
                    Logger.Debug($"path: {path}");
                    Logger.Debug($"fileName: {fileName}");
                    Logger.Debug($"fileExt: {fileExt}");
                    Logger.Debug($"baseName: {baseName}");
                    Logger.Debug($"exists: {exists}");
                    Logger.Debug($"markRemoved: {markRemoved}");
                }
            }

            Logger.Info("Scanning finished");

            Logger.Info($"File entries scanned: {countAll}");
            Logger.Info($"Scanned file entries total size: {sizeAll}");
            Logger.Info($"Time taken: {DateTime.Now - timeWalk}");

            timeWalk = DateTime.Now;

            Logger.Info("Removing files...");

            int progress = 1;
            int all = toRemove.Count;

            foreach (var item in toRemove)
            {
                string baseName = item.Key;
                List<string> list = item.Value;

                Logger.Info($"[{progress} of {all}] Process entries by base name '{baseName}' - count of files that will be removed: {list.Count}");

                foreach (var path in list)
                {
                    var info = new FileInfo(path);

                    long fileSize = info.Length;

                    if (!Simulate)
                    {
                        File.Delete(path);
                    }

                    Logger.Info($"[{progress} of {all}] >> Remove file '{path}'");

                    count++;

                    size += fileSize;
                }

                progress++;
            }

            Logger.Info("Removing files finished");
            Logger.Info($"Files removed: {count}");
            Logger.Info($"Removed files total size: {size}");
            Logger.Info($"Time taken: {DateTime.Now - timeWalk}");

            Logger.Info($"Task completed in {DateTime.Now - timeStart}");

            return;
        }

        internal async Task CheckCleanUp()
        {
            Logger.Info("Task start: Check files after clean up");
            var timeStart = DateTime.Now;

            var timeWalk = DateTime.Now;

            Logger.Info($"Task completed in {DateTime.Now - timeStart}");

            return;
        }
    }
}
