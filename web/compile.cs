using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Principal;

class Package-Installer
{
    static string scriptPath = @"C:\Program Files\Daniel-Package-Installer\installer.ps1";
    static string scriptUpdateUrl = "https://daniel-package-installer.pages.dev/installer.ps1"; // <-- Replace with your real URL

    static void Main(string[] args)
    {
        EnsureRunningAsAdmin();

        if (args.Length == 0)
        {
            Console.WriteLine("Usage: Package-Installer.exe <SoftwareName>");
            Console.WriteLine("       Package-Installer.exe -updatescript");
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
        if (!File.Exists(scriptPath))
        {
            Console.Error.WriteLine($"ERROR: Script not found at {scriptPath}");
            return;
        }

        string psArgs = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" -SoftwareName \"{softwareName}\"";
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

    static void UpdateScript()
    {
        try
        {
            Console.WriteLine($"Downloading new script from: {scriptUpdateUrl}");
            using (var client = new WebClient())
            {
                client.DownloadFile(scriptUpdateUrl, scriptPath);
            }
            Console.WriteLine($"Script updated successfully at: {scriptPath}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Failed to update script: " + ex.Message);
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
                Arguments = string.Join(" ", Environment.GetCommandLineArgs()[1..]),
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
