using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using IWshRuntimeLibrary;
using File = System.IO.File;


namespace UEFN_Patcher
{
    class Program
    {
        static async Task Main()
        {
            try
            {
                Console.WriteLine("-----------------------------------------");
                Console.WriteLine("Enter UEFN VERSION:");
                Console.WriteLine("-----------------------------------------");
                string version = Console.ReadLine()?.Trim();
                Console.Clear();

                if (string.IsNullOrWhiteSpace(version))
                {
                    Console.WriteLine("--------------------------------------------");
                    Console.WriteLine("Version Cannot Be Empty.");
                    Console.WriteLine("------------------------------------------- ");
                    return;
                }

                Console.WriteLine("-----------------------------------------");
                Console.WriteLine("Enter the PATH (WIN64):");
                Console.WriteLine("-----------------------------------------");
                string path = Console.ReadLine()?.Trim();
                Console.Clear();

                if (string.IsNullOrWhiteSpace(path))
                {
                    Console.WriteLine("--------------------------------------------");
                    Console.WriteLine("Path Cannot Be Empty.");
                    Console.WriteLine("--------------------------------------------");
                    return;
                }

                if (!Directory.Exists(path))
                {
                    Console.WriteLine("--------------------------------------------");
                    Console.WriteLine("Path Does Not Exist.");
                    Console.WriteLine("--------------------------------------------");
                    return;
                }

                string originalExePath = Path.Combine(path, "UnrealEditorFortnite-Win64-Shipping.exe");
                string patchedExePath = Path.Combine(path, "UnrealEditorFortnite-Win64-Shipping-PlayInEditor.exe");

                if (!File.Exists(originalExePath))
                {
                    Console.WriteLine("--------------------------------------------");
                    Console.WriteLine("UnrealEditorFortnite-Win64-Shipping.exe Not Found.");
                    Console.WriteLine("--------------------------------------------");
                    return;
                }

                File.Copy(originalExePath, patchedExePath, overwrite: true);

                await ApplyPatches(patchedExePath, version);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }

        static async Task ApplyPatches(string exePath, string version)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    string url = $"https://raw.githubusercontent.com/SpaceClientInc/uefn-patches/main/{version}.xml";
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; UEFN_Patcher/1.0)");
                    string xmlContent = await client.GetStringAsync(url);

                    XDocument xmlDoc = XDocument.Parse(xmlContent);

                    foreach (XElement patch in xmlDoc.Descendants("patch"))
                    {
                        string offsetStr = patch.Element("offset")?.Value;
                        string originalHex = patch.Element("original")?.Value;
                        string replaceHex = patch.Element("replace")?.Value;

                        if (offsetStr != null && originalHex != null && replaceHex != null)
                        {
                            long offset = Convert.ToInt64(offsetStr, 16);
                            byte[] originalBytes = StringToByteArray(originalHex);
                            byte[] replaceBytes = StringToByteArray(replaceHex);

                            ApplyPatch(exePath, offset, originalBytes, replaceBytes);
                        }
                    }

                    Console.WriteLine("Patching completed successfully.");
                    CreateDesktopShortcut(exePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to apply patches: {ex.Message}");
                }
            }
        }

        static void ApplyPatch(string filePath, long offset, byte[] originalBytes, byte[] replaceBytes)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite))
            {
                fs.Seek(offset, SeekOrigin.Begin);

                byte[] buffer = new byte[originalBytes.Length];
                fs.Read(buffer, 0, buffer.Length);

                if (buffer.SequenceEqual(originalBytes))
                {
                    fs.Seek(offset, SeekOrigin.Begin);
                    fs.Write(replaceBytes, 0, replaceBytes.Length);
                }
                else
                {
                    Console.WriteLine($"Warning: Original bytes at offset {offset:X} do not match expected values. Skipping patch.");
                }
            }
        }

        static byte[] StringToByteArray(string hex)
        {
            int numberChars = hex.Length;
            byte[] bytes = new byte[numberChars / 2];
            for (int i = 0; i < numberChars; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }


        static void CreateDesktopShortcut(string targetPath)
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string shortcutPath = Path.Combine(desktopPath, "UnrealEditorFortnite-PlayInEditor.lnk");

            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
            }

            WshShell shell = new WshShell();
            IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);

            shortcut.TargetPath = targetPath;
            shortcut.Arguments = "-disableplugins=\"AtomVK,ValkyrieFortnite\"";
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
            shortcut.Save();
        }
    }
}
