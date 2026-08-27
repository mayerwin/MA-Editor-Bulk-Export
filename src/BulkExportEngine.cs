using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MAEditorBulkExport
{
    public class BulkExportEngine
    {
        public string AppDirectory { get; private set; } = string.Empty;
        public string DatabaseDirectory { get; private set; } = string.Empty;
        public string SystemDbPath { get; private set; } = string.Empty;

        private Assembly? _editorAsm;
        private Assembly? _clientServicesAsm;
        private Assembly? _commonAsm;
        private Assembly? _dataProviderAsm;
        private Assembly? _importEntitiesAsm;
        private Assembly? _toolkitAsm;
        private Assembly? _coreAsm;
        private Assembly? _sqliteAsm;

        private object? _exportService;
        private MethodInfo? _exportAllMethod;
        private Type? _subType;
        private object? _pilotsManager;
        private MethodInfo? _addPilotsMethod;

        private object? _exportTypeAll;
        private object? _exportExclusionFlagsBi;
        private object? _textFormatTsv;
        private object? _currencyVal;

        public bool IsInitialized { get; private set; }

        public BulkExportEngine(string? customAppDir = null)
        {
            LocateDirectories(customAppDir);
            SetupAssemblyResolution();
            LoadCoreAssemblies();
        }

        private void LocateDirectories(string? customAppDir)
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string baseEditorDir = Path.Combine(localAppData, "MicrosoftAdvertisingEditor");

            if (!string.IsNullOrEmpty(customAppDir) && Directory.Exists(customAppDir))
            {
                AppDirectory = customAppDir;
            }
            else
            {
                if (!Directory.Exists(baseEditorDir))
                {
                    throw new DirectoryNotFoundException($"Microsoft Advertising Editor folder not found at: {baseEditorDir}");
                }

                var appDirs = Directory.GetDirectories(baseEditorDir, "app-*");
                if (appDirs.Length == 0)
                {
                    throw new DirectoryNotFoundException($"No 'app-*' installation folders found inside: {baseEditorDir}");
                }

                Array.Sort(appDirs, StringComparer.OrdinalIgnoreCase);
                AppDirectory = appDirs[^1]; // Highest version
            }

            DatabaseDirectory = Path.Combine(baseEditorDir, "Database");
            SystemDbPath = Path.Combine(DatabaseDirectory, "SystemDB.db");

            if (!File.Exists(SystemDbPath))
            {
                throw new FileNotFoundException($"SystemDB.db not found at: {SystemDbPath}. Please ensure accounts are downloaded in Microsoft Advertising Editor.");
            }
        }

        private void SetupAssemblyResolution()
        {
            EnsureAppConfig();

            AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) =>
            {
                var assemblyName = new AssemblyName(eventArgs.Name).Name + ".dll";
                var assemblyPath = Path.Combine(AppDirectory, assemblyName);
                if (File.Exists(assemblyPath))
                {
                    return Assembly.LoadFrom(assemblyPath);
                }
                return null;
            };
        }

        private void LoadCoreAssemblies()
        {
            _editorAsm = Assembly.LoadFrom(Path.Combine(AppDirectory, "Microsoft.Advertising.Editor.dll"));
            _dataProviderAsm = Assembly.LoadFrom(Path.Combine(AppDirectory, "DataProvider.dll"));
            _clientServicesAsm = Assembly.LoadFrom(Path.Combine(AppDirectory, "Microsoft.AdvertisingDesktop.ClientServices.dll"));
            _commonAsm = Assembly.LoadFrom(Path.Combine(AppDirectory, "Microsoft.AdvertisingDesktop.Common.dll"));
            _importEntitiesAsm = Assembly.LoadFrom(Path.Combine(AppDirectory, "ImportEntities.dll"));
            _toolkitAsm = Assembly.LoadFrom(Path.Combine(AppDirectory, "Toolkit.dll"));
            _coreAsm = Assembly.LoadFrom(Path.Combine(AppDirectory, "Core.dll"));
            _sqliteAsm = Assembly.LoadFrom(Path.Combine(AppDirectory, "System.Data.SQLite.dll"));

            RegisterSqliteFunctions();
        }

        private void RegisterSqliteFunctions()
        {
            try
            {
                var sqliteFuncType = _sqliteAsm?.GetType("System.Data.SQLite.SQLiteFunction");
                if (sqliteFuncType == null) return;

                var regMethod = sqliteFuncType.GetMethod("RegisterFunction", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Type) }, null);
                if (regMethod == null) return;

                if (_dataProviderAsm != null)
                {
                    foreach (var type in _dataProviderAsm.GetTypes())
                    {
                        if (sqliteFuncType.IsAssignableFrom(type) && !type.IsAbstract)
                        {
                            regMethod.Invoke(null, new object[] { type });
                        }
                    }
                }
            }
            catch {}
        }

        public void Initialize()
        {
            if (IsInitialized) return;

            // Initialize BootStrapper and DI
            var bsType = _editorAsm!.GetType("Microsoft.AdvertisingDesktop.BingAdsEditor.BootStrapper")
                ?? throw new InvalidOperationException("Could not resolve BootStrapper type.");
            var bs = Activator.CreateInstance(bsType)
                ?? throw new InvalidOperationException("Could not create BootStrapper instance.");

            var regMethod = bsType.GetMethod("RegisterDependencies", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("Could not resolve RegisterDependencies method.");
            regMethod.Invoke(bs, new object[] { Guid.NewGuid() });

            // Resolve ExportService
            var exportServiceType = _clientServicesAsm!.GetType("Microsoft.AdvertisingDesktop.ClientServices.Export.ExportService")
                ?? throw new InvalidOperationException("Could not resolve ExportService type.");
            _exportService = Activator.CreateInstance(exportServiceType)
                ?? throw new InvalidOperationException("Could not create ExportService instance.");
            _exportAllMethod = exportServiceType.GetMethods().First(m => m.Name == "ExportAll");

            // Resolve Subscription model
            _subType = _dataProviderAsm!.GetType("Microsoft.AdvertisingDesktop.DataAccessLayer.DataProvider.ModelsSystemDB.Subscription")
                ?? throw new InvalidOperationException("Could not resolve Subscription model type.");

            // Resolve PilotsManager
            var injectorType = _toolkitAsm!.GetType("Toolkit.Injector")
                ?? throw new InvalidOperationException("Could not resolve Injector type.");
            var injectorInstance = injectorType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                ?? throw new InvalidOperationException("Could not get Injector.Instance.");
            var resolveMethod = injectorType.GetMethods().First(m => m.Name == "Resolve" && m.IsGenericMethod && m.GetParameters().Length == 0);
            var iPilotsManagerType = _commonAsm!.GetType("Microsoft.AdvertisingDesktop.Common.Pilots.IPilotsManager")
                ?? throw new InvalidOperationException("Could not resolve IPilotsManager interface.");
            _pilotsManager = resolveMethod.MakeGenericMethod(iPilotsManagerType).Invoke(injectorInstance, null);
            _addPilotsMethod = iPilotsManagerType.GetMethod("AddOrUpdatePilotsInCacheAsync");

            // Resolve Enums
            var exportTypeEnum = _commonAsm.GetType("Microsoft.AdvertisingDesktop.Common.ExportType")!;
            var exportExclusionFlagsEnum = _commonAsm.GetType("Microsoft.AdvertisingDesktop.Common.Enums.ExportExclusionFlags")!;
            var textFormatEnum = _importEntitiesAsm!.GetType("Microsoft.BingAds.Import.Entities.Enums.TextFormat")!;
            var currencyEnum = _coreAsm!.GetType("Toolkit.Core.Currency");

            _exportTypeAll = Enum.Parse(exportTypeEnum, "All");
            _exportExclusionFlagsBi = Enum.Parse(exportExclusionFlagsEnum, "BI"); // Exclude BI performance metrics to match standard export
            _textFormatTsv = Enum.Parse(textFormatEnum, "TabSeparatedValue");
            _currencyVal = currencyEnum != null ? Activator.CreateInstance(currencyEnum) : null;

            IsInitialized = true;
        }

        private void EnsureAppConfig()
        {
            string srcConfig = Path.Combine(AppDirectory, "Microsoft.Advertising.Editor.dll.config");
            string exePath = Assembly.GetExecutingAssembly().Location;
            string targetConfig = exePath + ".config";

            if (File.Exists(srcConfig) && (!File.Exists(targetConfig) || File.GetLastWriteTimeUtc(srcConfig) > File.GetLastWriteTimeUtc(targetConfig)))
            {
                try
                {
                    File.Copy(srcConfig, targetConfig, true);
                }
                catch {}
            }
        }

        public List<AccountInfo> GetAccounts()
        {
            var accounts = new List<AccountInfo>();

            var connType = _sqliteAsm!.GetType("System.Data.SQLite.SQLiteConnection")!;
            
            using var conn = (IDbConnection)Activator.CreateInstance(connType, $"Data Source={SystemDbPath};Mode=ReadOnly")!;
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT StoreId, StoreName, AccountNumber, AccountName, CustomerId, CustomerName, UserName, Status FROM Subscription ORDER BY AccountName";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var acc = new AccountInfo
                {
                    StoreId = reader.GetInt64(0),
                    StoreName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    AccountNumber = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    AccountName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    CustomerId = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                    CustomerName = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    UserName = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    Status = reader.IsDBNull(7) ? 31 : Convert.ToInt64(reader.GetValue(7))
                };

                if (string.IsNullOrEmpty(acc.StoreName))
                {
                    acc.StoreName = $"{acc.StoreId}_{acc.AccountNumber}_0";
                }

                string dbFile = Path.Combine(DatabaseDirectory, $"{acc.StoreName}.db");
                acc.DatabasePath = dbFile;
                acc.DatabaseExists = File.Exists(dbFile);

                accounts.Add(acc);
            }

            return accounts;
        }

        public async Task<string> ExportAccountAsync(AccountInfo account, string outputDirectory, CancellationToken cancellationToken = default)
        {
            if (!IsInitialized) Initialize();

            Directory.CreateDirectory(outputDirectory);

            string dateSuffix = DateTime.Now.ToString("yyyyMMdd");
            string safeAccountName = string.Join("_", account.AccountName.Split(Path.GetInvalidFileNameChars())).Trim();
            string fileName = $"{account.AccountNumber}_{safeAccountName}_{dateSuffix}.csv";
            string outputPath = Path.Combine(outputDirectory, fileName);

            var sub = Activator.CreateInstance(_subType!)!;
            _subType!.GetProperty("StoreId")!.SetValue(sub, account.StoreId);
            _subType.GetProperty("StoreName")!.SetValue(sub, account.StoreName);
            _subType.GetProperty("AccountNumber")!.SetValue(sub, account.AccountNumber);
            _subType.GetProperty("AccountName")!.SetValue(sub, account.AccountName);
            _subType.GetProperty("CustomerId")!.SetValue(sub, account.CustomerId);
            _subType.GetProperty("CustomerName")!.SetValue(sub, account.CustomerName);
            _subType.GetProperty("UserName")!.SetValue(sub, account.UserName);

            var statusProp = _subType.GetProperty("Status");
            if (statusProp != null)
            {
                var t = Nullable.GetUnderlyingType(statusProp.PropertyType) ?? statusProp.PropertyType;
                if (t.IsEnum)
                {
                    statusProp.SetValue(sub, Enum.ToObject(t, account.Status));
                }
                else
                {
                    statusProp.SetValue(sub, Convert.ChangeType(account.Status, t));
                }
            }

            // Load pilots for account into cache
            if (_addPilotsMethod != null && _pilotsManager != null)
            {
                var pilotTask = (Task)_addPilotsMethod.Invoke(_pilotsManager, new object[] { sub, cancellationToken })!;
                await pilotTask;
            }

            // Execute export
            var exportTask = (Task)_exportAllMethod!.Invoke(_exportService, new object[] {
                sub,
                true, // writeHeaderRow
                true, // writeFormatVersionRow
                outputPath,
                _exportTypeAll!,
                _currencyVal!,
                _textFormatTsv!,
                _exportExclusionFlagsBi!,
                null!,
                cancellationToken
            })!;

            await exportTask;

            if (!File.Exists(outputPath))
            {
                throw new IOException($"Export completed but output file was not found at: {outputPath}");
            }

            return outputPath;
        }
    }
}
