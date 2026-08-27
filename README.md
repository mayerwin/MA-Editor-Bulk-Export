# Bulk Export Tool For Microsoft Advertising Editor

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)]()
[![.NET Version](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)

A fast, automated bulk export utility for **Microsoft Advertising Editor** desktop application on Windows. It allows advertisers and agencies managing tens or hundreds of accounts to export full account data into individual CSV bulksheet files in a single click with **100% byte-for-byte parity** to official exports.

---

## 🎯 The Problem

Unlike Google Ads Editor (which natively supports multi-account bulk exports), Microsoft Advertising Editor only provides a single-account export option via its graphical user interface:

* Exporting 100+ accounts manually takes hours of clicking, switching accounts, and waiting.
* Using the cloud API requires developer approval, OAuth2 configuration, API token requests, and rate-limiting handling.

## 💡 The Solution

**Bulk Export Tool For Microsoft Advertising Editor** interfaces directly with the offline local store created by Microsoft Advertising Editor:

1. **Zero Cloud API Setup:** Works entirely offline against your locally downloaded accounts in `%LOCALAPPDATA%\MicrosoftAdvertisingEditor`.
2. **Exact Official Parity:** Invokes the native Microsoft Advertising Editor .NET export assemblies in-process, ensuring exact header formatting, entity columns, ID brackets, and encoding matching official exports.
3. **High Performance:** Exports 400+ complete accounts (including Campaigns, Ad Groups, Keywords, Negative Keywords, RSAs, Extensions, and Targets) in just minutes.
4. **Clean Naming:** Automatically names files following `{AccountNumber}_{AccountName}_{YYYYMMDD}.csv`.

---

## 📋 Prerequisites

* **Windows 10 / 11 / Server**
* **[.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)** (or later)
* **[Microsoft Advertising Editor](https://about.ads.microsoft.com/en-us/solutions/tools/microsoft-advertising-editor)** installed, with accounts downloaded.

---

## 🚀 Quick Start

### 1. Clone the Repository

```bash
git clone https://github.com/your-username/MA-Editor-Bulk-Export.git
cd MA-Editor-Bulk-Export
```

### 2. Export All Accounts

Double-click `export.bat`, or run via terminal:

```bash
dotnet run --project src/MAEditorBulkExport.csproj -- --all
```

By default, all CSV files will be saved directly to:
```text
%USERPROFILE%\Desktop\Microsoft_Ads_Exports\
```

---

## 💻 CLI Usage & Options

```text
Usage: MAEditorBulkExport [options]

Options:
  -a, --all                      Export all accounts available locally (default)
  -n, --account <Name_or_ID>     Export a single account by Account Name, Account Number, or Store ID
  -o, --output <DirectoryPath>   Custom destination directory (default: Desktop/Microsoft_Ads_Exports)
  -l, --list                     List all local accounts and download status without exporting
      --editor-path <Path>       Override Microsoft Advertising Editor installation directory
  -h, --help                     Show help message
```

### Examples

**List all local accounts:**
```bash
dotnet run --project src/MAEditorBulkExport.csproj -- --list
```

**Export a specific account by name or number:**
```bash
dotnet run --project src/MAEditorBulkExport.csproj -- --account "MyAccountName"
dotnet run --project src/MAEditorBulkExport.csproj -- --account X1234567
```

**Export all accounts to a custom directory:**
```bash
dotnet run --project src/MAEditorBulkExport.csproj -- --all --output "C:\MyExports"
```

**Using PowerShell launcher:**
```powershell
.\export.ps1 -All -Output "C:\MyExports"
.\export.ps1 -Account "MyAccount"
.\export.ps1 -List
```

---

## 🔬 How It Works (Technical Architecture)

```
┌─────────────────────────────────────────────────────────────┐
│               Microsoft Advertising Editor                  │
│  (Downloads offline campaign data from Microsoft Ads Cloud) │
└──────────────────────────────┬──────────────────────────────┘
                               │ Local Storage
                               ▼
┌─────────────────────────────────────────────────────────────┐
│  %LOCALAPPDATA%\MicrosoftAdvertisingEditor\Database         │
│  ├── SystemDB.db (Master index of all subscriptions/accounts)│
│  └── <StoreID>_<AccountNum>_0.db (SQLite database per acct) │
└──────────────────────────────┬──────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────┐
│                 MAEditorBulkExport Engine                   │
│  1. Discovers SystemDB.db & lists downloaded accounts       │
│  2. Loads Microsoft.AdvertisingDesktop.*.dll in-process     │
│  3. Injects account pilots into cache (IPilotsManager)      │
│  4. Invokes ExportService.ExportAll with standard filters   │
└──────────────────────────────┬──────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────┐
│             Output: Standard UTF-16 TSV/CSV Files           │
│        {AccountNumber}_{AccountName}_{YYYYMMDD}.csv         │
└─────────────────────────────────────────────────────────────┘
```

---

## 🛡️ License

Distributed under the [MIT License](LICENSE).

---

## ⚠️ Disclaimer

This tool is an independent open-source utility for local data export and automation. Microsoft Advertising and Microsoft Advertising Editor are trademarks of Microsoft Corporation. This project is not affiliated with, endorsed by, or sponsored by Microsoft.
