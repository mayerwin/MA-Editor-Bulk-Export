using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MAEditorBulkExport
{
    internal class Program
    {
        [STAThread]
        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("=================================================================");
            Console.WriteLine("  Bulk Export Tool For Microsoft Advertising Editor");
            Console.WriteLine("=================================================================\n");

            bool showHelp = args.Any(a => a == "-h" || a == "--help" || a == "/?");
            bool listOnly = args.Any(a => a == "-l" || a == "--list");
            bool exportAll = args.Any(a => a == "-a" || a == "--all");
            string? targetAccount = GetArgValue(args, "-n", "--account");
            string? customAppDir = GetArgValue(args, "--editor-path");
            string? outputDir = GetArgValue(args, "-o", "--output");

            if (showHelp)
            {
                PrintUsage();
                return 0;
            }

            try
            {
                Console.WriteLine("Detecting Microsoft Advertising Editor environment...");
                var engine = new BulkExportEngine(customAppDir);
                Console.WriteLine($"  App Directory:  {engine.AppDirectory}");
                Console.WriteLine($"  Data Directory: {engine.DatabaseDirectory}\n");

                var accounts = engine.GetAccounts();
                Console.WriteLine($"Discovered {accounts.Count} account(s) registered in SystemDB.");

                if (listOnly)
                {
                    Console.WriteLine("\n------------------------------------------------------------------------------------------------------");
                    Console.WriteLine(string.Format("{0,-12} | {1,-35} | {2,-15} | {3,-10}", "Account #", "Account Name", "Store ID", "Local DB"));
                    Console.WriteLine("------------------------------------------------------------------------------------------------------");
                    foreach (var a in accounts)
                    {
                        string dbStatus = a.DatabaseExists ? "Available" : "Missing";
                        Console.WriteLine(string.Format("{0,-12} | {1,-35} | {2,-15} | {3,-10}", a.AccountNumber, Truncate(a.AccountName, 35), a.StoreId, dbStatus));
                    }
                    Console.WriteLine("------------------------------------------------------------------------------------------------------");
                    return 0;
                }

                // Default destination folder if not provided: <UserProfile>/Desktop/Microsoft_Ads_Exports
                if (string.IsNullOrEmpty(outputDir))
                {
                    string userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    outputDir = Path.Combine(userDesktop, "Microsoft_Ads_Exports");
                }

                var targets = accounts;
                if (!string.IsNullOrEmpty(targetAccount))
                {
                    targets = accounts.Where(a => 
                        a.AccountNumber.Equals(targetAccount, StringComparison.OrdinalIgnoreCase) ||
                        a.AccountName.Equals(targetAccount, StringComparison.OrdinalIgnoreCase) ||
                        a.StoreId.ToString() == targetAccount).ToList();

                    if (targets.Count == 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"\n[Error] No account found matching: '{targetAccount}'");
                        Console.ResetColor();
                        return 1;
                    }
                }
                else if (!exportAll)
                {
                    // Default to ALL if no flags given
                    Console.WriteLine("\n[Note] No specific account specified. Proceeding with export of ALL accounts.");
                }

                Console.WriteLine($"\nInitializing Microsoft Advertising Editor export engine...");
                engine.Initialize();
                Console.WriteLine("Engine initialized successfully.\n");

                Console.WriteLine($"Starting export of {targets.Count} account(s) to: {outputDir}\n");
                var sw = Stopwatch.StartNew();

                int successCount = 0;
                int errorCount = 0;
                long totalBytes = 0;

                for (int i = 0; i < targets.Count; i++)
                {
                    var acc = targets[i];
                    Console.Write($"[{i + 1}/{targets.Count}] {acc.AccountNumber} - {Truncate(acc.AccountName, 30)}... ");

                    if (!acc.DatabaseExists)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Skipped (Local .db not downloaded)");
                        Console.ResetColor();
                        continue;
                    }

                    try
                    {
                        string outPath = await engine.ExportAccountAsync(acc, outputDir);
                        var fi = new FileInfo(outPath);
                        totalBytes += fi.Length;
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Done ({fi.Length:N0} bytes)");
                        Console.ResetColor();
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Error: {ex.Message}");
                        Console.ResetColor();
                        errorCount++;
                    }
                }

                sw.Stop();
                Console.WriteLine("\n=================================================================");
                Console.WriteLine($"  Bulk Export Completed in {sw.Elapsed.TotalSeconds:N1}s");
                Console.WriteLine($"  Successfully Exported: {successCount}/{targets.Count} accounts");
                if (errorCount > 0)
                {
                    Console.WriteLine($"  Errors / Failures:    {errorCount}");
                }
                Console.WriteLine($"  Total Output Data:    {totalBytes / (1024.0 * 1024.0):N2} MB");
                Console.WriteLine($"  Output Folder:        {outputDir}");
                Console.WriteLine("=================================================================\n");

                return errorCount > 0 ? 1 : 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[Fatal Error] {ex.Message}");
                Console.ResetColor();
                return 1;
            }
        }

        private static string? GetArgValue(string[] args, params string[] flags)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (flags.Contains(args[i], StringComparer.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                    {
                        return args[i + 1];
                    }
                }
            }
            return null;
        }

        private static string Truncate(string val, int maxLen)
        {
            if (string.IsNullOrEmpty(val)) return string.Empty;
            return val.Length <= maxLen ? val : val.Substring(0, maxLen - 3) + "...";
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: MAEditorBulkExport [options]\n");
            Console.WriteLine("Options:");
            Console.WriteLine("  -a, --all                      Export all accounts available locally (default)");
            Console.WriteLine("  -n, --account <Name_or_ID>     Export a single account by Account Name, Account Number, or Store ID");
            Console.WriteLine("  -o, --output <DirectoryPath>   Custom destination directory (default: Desktop/Microsoft_Ads_Exports)");
            Console.WriteLine("  -l, --list                     List all local accounts and download status without exporting");
            Console.WriteLine("      --editor-path <Path>       Override Microsoft Advertising Editor installation directory");
            Console.WriteLine("  -h, --help                     Show this help message\n");
            Console.WriteLine("Examples:");
            Console.WriteLine("  MAEditorBulkExport --all");
            Console.WriteLine("  MAEditorBulkExport --account \"SampleAccount\"");
            Console.WriteLine("  MAEditorBulkExport --account X1234567 --output C:\\Exports");
            Console.WriteLine("  MAEditorBulkExport --list\n");
        }
    }
}
