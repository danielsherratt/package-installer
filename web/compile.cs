using System;
using System.Diagnostics;
using System.Net;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Linq;  // Add this for LINQ extension methods like Skip

class DanielInstaller
{
    static string scriptPath = @"C:\Program Files\Daniel-Package-Installer\installer.ps1";
    static string scriptUpdateUrl = "https://daniel-package-installer.pages.dev/installer.ps1"; // Replace with your real URL

    static void Main(string[] args)
    {
        EnsureRunningAsAdmin();

        if (args.Length == 0)
        {
            Console.WriteLine("Usage: DanielInstaller.exe <SoftwareName>");
            Console.WriteLine("       DanielInstaller.exe -updatescript");
            return;
        }

        if (args[0].Equals("-updatescript", StringComparison.OrdinalIgnoreCase))
        {
            UpdateScript();
            return;
        }

        RunInstaller(args[0]);
    }

    static void RunInstaller(string softwareName)
    {
        // Check if the script exists, if not, download it
        if (!File.Exists(scriptPath))
        {
            Console.WriteLine("Script not found at " + scriptPath + ". Attempting to download...");
            DownloadScript();
        }

        if (File.Exists(scriptPath))
        {
            string psArgs = "-ExecutionPolicy Bypass -File \"" + scriptPath + "\" -SoftwareName \"" + softwareName + "\"";
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = psArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (Process process = Process.Start(psi))
            {
                process.OutputDataReceived += (s, e) => { if (e.Data != null) Console.WriteLine(e.Data); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) Console.Error.WriteLine(e.Data); };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
            }
        }
        else
        {
            Console.Error.WriteLine("Failed to download the script. Cannot continue.");
        }
    }

    static void UpdateScript()
    {
        try
        {
            Console.WriteLine("Downloading new script from: " + scriptUpdateUrl);

            // Set TLS version to 1.2 to avoid SSL/TLS issues
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            using (var client = new WebClient())
            {
                client.DownloadFile(scriptUpdateUrl, scriptPath);
            }

            Console.WriteLine("Script updated successfully at: " + scriptPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Failed to update script: " + ex.Message);
        }
    }

    static void DownloadScript()
    {
        try
        {
            Console.WriteLine("Downloading script from: " + scriptUpdateUrl);

            // Set TLS version to 1.2 to avoid SSL/TLS issues
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            using (var client = new WebClient())
            {
                client.DownloadFile(scriptUpdateUrl, scriptPath);
            }

            Console.WriteLine("Script downloaded successfully to: " + scriptPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Failed to download script: " + ex.Message);
        }
    }

    static void EnsureRunningAsAdmin()
    {
        WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new WindowsPrincipal(identity);

        if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
        {
            // Restart as admin
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = Assembly.GetExecutingAssembly().Location,
                Arguments = string.Join(" ", Environment.GetCommandLineArgs().Skip(1)),
                Verb = "runas"  // Triggers UAC
            };

            try
            {
                Process.Start(psi);
            }
            catch
            {
                Console.WriteLine("User refused the elevation.");
            }

            Environment.Exit(0);
        }
    }
}
