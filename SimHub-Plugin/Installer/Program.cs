using System;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using System.Linq;
using Microsoft.Win32;

namespace InternalWindMachineInstaller
{
    class Program
    {
        private static string simHubPath = "";
        private static string dllSource = "";

        [STAThread]
        static void Main(string[] args)
        {
            Console.Title = "Internal Wind Machine - Plugin Installer";
            
            ShowHeader();

            // Find DLL in same directory as installer
            dllSource = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "InternalWindMachinePlugin.dll");
            
            if (!File.Exists(dllSource))
            {
                ShowError("Plugin file not found!\n\nPlease ensure 'InternalWindMachinePlugin.dll' is in the same folder as this installer.");
                return;
            }

            // Step 1: Find SimHub
            Console.WriteLine("[1/4] Searching for SimHub installation...\n");
            if (!FindSimHub())
            {
                ShowError("SimHub installation not found or cancelled.");
                return;
            }
            Console.WriteLine($"[OK] SimHub found at: {simHubPath}\n");

            // Step 2: Check if SimHub is running
            Console.WriteLine("[2/4] Checking if SimHub is running...");
            if (IsSimHubRunning())
            {
                var result = MessageBox.Show(
                    "SimHub is currently running!\n\nPlease close SimHub before continuing.\n\nClick OK after closing SimHub, or Cancel to abort.",
                    "SimHub Running",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Cancel || IsSimHubRunning())
                {
                    ShowError("SimHub is still running. Installation cancelled.");
                    return;
                }
            }
            Console.WriteLine("[OK] SimHub is not running.\n");

            // Step 3: Install Plugin
            Console.WriteLine("[3/4] Installing plugin...\n");
            if (!InstallPlugin())
            {
                ShowError("Failed to install plugin.\n\nPlease check permissions and try again.");
                return;
            }
            Console.WriteLine("[OK] Plugin installed successfully!\n");

            // Step 4: Create output directory
            Console.WriteLine("[4/4] Setting up output directory...\n");
            CreateOutputDirectory();
            Console.WriteLine("[OK] Setup complete!\n");

            // Success message
            ShowSuccess();
        }

        static void ShowHeader()
        {
            Console.WriteLine();
            Console.WriteLine("============================================================");
            Console.WriteLine(" INTERNAL WIND MACHINE - PLUGIN INSTALLER");
            Console.WriteLine(" by The Real Karle | https://linktr.ee/therealkarle");
            Console.WriteLine("============================================================");
            Console.WriteLine();
        }

        static bool FindSimHub()
        {
            // Check common paths
            string[] commonPaths = {
                @"C:\Program Files (x86)\SimHub",
                @"C:\Program Files\SimHub"
            };

            foreach (var path in commonPaths)
            {
                if (File.Exists(Path.Combine(path, "SimHub.exe")))
                {
                    simHubPath = path;
                    return true;
                }
            }

            // Try to find via registry
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    if (key != null)
                    {
                        foreach (var subKeyName in key.GetSubKeyNames())
                        {
                            using (var subKey = key.OpenSubKey(subKeyName))
                            {
                                var displayName = subKey?.GetValue("DisplayName") as string;
                                if (displayName != null && displayName.Contains("SimHub"))
                                {
                                    var installLocation = subKey.GetValue("InstallLocation") as string;
                                    if (!string.IsNullOrEmpty(installLocation) && File.Exists(Path.Combine(installLocation, "SimHub.exe")))
                                    {
                                        simHubPath = installLocation;
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // Ask user
            Console.WriteLine("SimHub not found in default locations.\n");
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Please select your SimHub installation folder";
                dialog.ShowNewFolderButton = false;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    if (File.Exists(Path.Combine(dialog.SelectedPath, "SimHub.exe")))
                    {
                        simHubPath = dialog.SelectedPath;
                        return true;
                    }
                    else
                    {
                        MessageBox.Show("SimHub.exe not found in the selected folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                }
            }

            return false;
        }

        static bool IsSimHubRunning()
        {
            return Process.GetProcessesByName("SimHub").Length > 0;
        }

        static bool InstallPlugin()
        {
            try
            {
                string destPath = Path.Combine(simHubPath, "InternalWindMachinePlugin.dll");

                // Backup existing version
                if (File.Exists(destPath))
                {
                    string backupPath = destPath + ".backup";
                    File.Copy(destPath, backupPath, true);
                    Console.WriteLine("Created backup: InternalWindMachinePlugin.dll.backup");
                }

                // Copy new version
                File.Copy(dllSource, destPath, true);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return false;
            }
        }

        static void CreateOutputDirectory()
        {
            try
            {
                string outputDir = Path.Combine(simHubPath, "InternalWindMachineOutput");
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                    Console.WriteLine($"Created output directory: {outputDir}");
                }
                else
                {
                    Console.WriteLine("Output directory already exists.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not create output directory: {ex.Message}");
            }
        }

        static void ShowSuccess()
        {
            Console.WriteLine("============================================================");
            Console.WriteLine(" INSTALLATION COMPLETE!");
            Console.WriteLine("============================================================");
            Console.WriteLine();
            Console.WriteLine("The Internal Wind Machine Plugin has been installed.");
            Console.WriteLine();
            Console.WriteLine("NEXT STEPS:");
            Console.WriteLine(" 1. Start SimHub");
            Console.WriteLine(" 2. Enable the plugin in SimHub settings (if not auto-enabled)");
            Console.WriteLine(" 3. Configure the plugin in 'Internal Wind Machine' section");
            Console.WriteLine(" 4. Set up Fan Control to read from:");
            Console.WriteLine($"    {Path.Combine(simHubPath, "InternalWindMachineOutput")}");
            Console.WriteLine();
            Console.WriteLine("For detailed setup instructions, visit:");
            Console.WriteLine("https://github.com/therealkarle/InternalWindMachine");
            Console.WriteLine();
            Console.WriteLine("============================================================");
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static void ShowError(string message)
        {
            Console.WriteLine();
            Console.WriteLine("[ERROR] " + message);
            Console.WriteLine();
            MessageBox.Show(message, "Installation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
