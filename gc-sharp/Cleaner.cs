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
            Console.Write("Reading app config...");

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
            Console.Write("Initializing db connection...");

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
            Console.Write("Locating files...");

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
            Console.WriteLine("Cleaning up files task started...");

            await CleanUnattachedFiles();

            Connection.Close();

            Console.WriteLine("Cleaning up files task finished...");

            return;
        }

        internal async Task CleanUnattachedFiles()
        {
            Console.WriteLine("Task start: Clean up files unattached to db");
            var timeStart = DateTime.Now;

            int count = 0;
            long size = 0;

            var timeWalk = DateTime.Now;

            var cmd = Connection.CreateCommand();

            cmd.CommandText = $"SELECT COUNT(*) FROM `{Config.Files.Table}`";

            if (Config.Debug)
            {
                Console.WriteLine($"[DEBUG] sql count: {cmd.CommandText}");
            }

            int dbFilesCount = 0;

            object result = await cmd.ExecuteScalarAsync();
            if (result != null)
            {
                dbFilesCount = Convert.ToInt32(result);

                Console.WriteLine($"File entries in DB to check: {dbFilesCount}");
            }

            cmd.CommandText = $"SELECT `id`, `file` FROM `{Config.Files.Table}`";

            if (Config.Debug)
            {
                Console.WriteLine($"[DEBUG] sql scan: {cmd.CommandText}");
            }

            Console.WriteLine("Scanning files in db...");

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

                        Console.WriteLine($"[{checkedRows} of {dbFilesCount}] File {id} in path `{path}` not exists - add to ignore list");
                    }
                    else
                    {
                        Console.WriteLine($"[{checkedRows} of {dbFilesCount}] File {id} in path `{path}` exists");

                        existList.Add(Path.GetFileNameWithoutExtension(path));
                    }

                    checkedRows++;
                }
            }

            checkedRows--;

            Console.WriteLine("Scanning finished");

            Console.WriteLine($"Checked rows: {checkedRows}");
            Console.WriteLine($"Ignored rows (files in DB, but not exist IRL): {ignoreList.Count}");
            Console.WriteLine($"Time taken: {DateTime.Now - timeWalk}");

            timeWalk = DateTime.Now;
            var timeStep = DateTime.Now;

            int countAll = 0;
            long sizeAll = 0;

            var toRemove = new Dictionary<string, List<string>>();

            var fileList = Directory.GetFiles(FilesPath, "*", SearchOption.TopDirectoryOnly);

            Console.WriteLine("Scanning files in folder...");

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
                    Console.WriteLine($"Scanned {countAll} files");

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
                    Console.WriteLine($"[DEBUG] path: {path}");
                    Console.WriteLine($"[DEBUG] fileName: {fileName}");
                    Console.WriteLine($"[DEBUG] fileExt: {fileExt}");
                    Console.WriteLine($"[DEBUG] baseName: {baseName}");
                    Console.WriteLine($"[DEBUG] exists: {exists}");
                    Console.WriteLine($"[DEBUG] markRemoved: {markRemoved}");
                }
            }

            Console.WriteLine("Scanning finished");

            Console.WriteLine($"File entries scanned: {countAll}");
            Console.WriteLine($"Scanned file entries total size: {sizeAll}");
            Console.WriteLine($"Time taken: {DateTime.Now - timeWalk}");

            timeWalk = DateTime.Now;

            Console.WriteLine("Removing files...");

            int progress = 1;
            int all = toRemove.Count;

            foreach (var item in toRemove)
            {
                string baseName = item.Key;
                List<string> list = item.Value;

                Console.WriteLine($"[{progress} of {all}] Process entries by base name '{baseName}' - count of files that will be removed: {list.Count}");

                foreach (var path in list)
                {
                    var info = new FileInfo(path);

                    long fileSize = info.Length;

                    if (!Simulate)
                    {
                        File.Delete(path);
                    }

                    Console.WriteLine($"[{progress} of {all}] >> Remove file '{path}'");

                    count++;

                    size += fileSize;
                }

                progress++;
            }

            Console.WriteLine("Removing files finished");
            Console.WriteLine($"Files removed: {count}");
            Console.WriteLine($"Removed files total size: {size}");
            Console.WriteLine($"Time taken: {DateTime.Now - timeWalk}");

            Console.WriteLine($"Task completed in {DateTime.Now - timeStart}");

            return;
        }
    }
}
