using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Cryptography;

using Console = Colorful.Console;

namespace Spotlight_Copier
{
    internal static class Program
    {
        private static void Main()
        {
            Console.Title = $"SPC v{Assembly.GetExecutingAssembly().GetName().Version}";

            #region Default Config

            string AutoExit;

            var AutoRemoveDuplicated = false;
            var ManualSaveLoop = 3;
            var SpotlightPath = "";

            var SavedColor = Color.FromArgb(0, 148, 50);
            var ErrorColor = Color.FromArgb(234, 32, 39);
            var WarningColor = Color.FromArgb(255, 121, 63);
            var CheckingColor = Color.FromArgb(255, 195, 18);

            var FileName = DateTime.Now.ToString("dd-MM-yyyy");
            var SavePath = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System)) + @"\SPC\";

            try
            {
                AutoExit = Environment.GetEnvironmentVariable("LMSQSPCAutoExit", EnvironmentVariableTarget.User);
            }
            catch (Exception) { AutoExit = "0"; }

            try
            {
                AutoRemoveDuplicated = (Environment.GetEnvironmentVariable("LMSQSPCAutoRemoveDuplicated", EnvironmentVariableTarget.User) == "1") ? true : false;
                AutoRemoveDuplicated = true;
            }
            catch (Exception) { AutoRemoveDuplicated = true; }

            if (!Directory.Exists(SavePath))
            {
                Console.WriteLine("Directory Does Not Exist. Creating...", WarningColor);

                try
                {
                    Directory.CreateDirectory(SavePath);
                }
                catch(Exception Error)
                {
                    Console.WriteLine("Create Directory Fail.", ErrorColor);
                    Console.WriteLine($"Error: {Error.Message}", ErrorColor);
                    
                    Console.WriteLine(Environment.NewLine);
                    
                    Console.Write("Press Eny Key To Enter It Manually.!", ErrorColor);

                    Console.ReadKey();

                    while(ManualSaveLoop > 0)
                    {
                        Console.Clear();

                        if (Directory.Exists(SavePath)) break;

                        Console.Write("Enter Save Path: ");

                        SavePath = Console.ReadLine();

                        if (SavePath != null && !SavePath.EndsWith(@"\")) SavePath = SavePath + @"\";

                        if (!Directory.Exists(SavePath))
                        {
                            Console.WriteLine("Directory Does Not Exist. Creating...", ErrorColor);

                            try
                            {
                                Directory.CreateDirectory(SavePath);
                            }
                            catch(Exception)
                            {
                                Console.WriteLine("Create Fail.!", ErrorColor);
                                Console.WriteLine($"Error: {Error.Message}", ErrorColor);
                                Console.WriteLine("Press Any Key To Re-Enter It.!", ErrorColor);
                                
                                Console.ReadKey();
                            }
                        }

                        ManualSaveLoop--;
                    }

                    if (ManualSaveLoop <= 0)
                    {
                        Console.WriteLine("Created Fail (Max Try Exceeded). Press Any Key To Exit.!", ErrorColor);
                        Console.ReadKey();

                        Environment.Exit(0x1);
                    }
                }
                finally
                {
                    Console.WriteLine("Created.");
                }
            }

            var PackagesPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\AppData\Local\Packages\";

            var DirList = CustomSearcher.GetDirectories(PackagesPath);

            #endregion

            foreach (var DirPath in DirList.Where(DirPath => DirPath.Contains("Microsoft.Windows.ContentDeliveryManager")))
            {
                SpotlightPath = DirPath + @"\LocalState\Assets";
                break;
            }

            var WallPath = Directory.GetFiles(SpotlightPath, "*", SearchOption.TopDirectoryOnly);
            var SavedFilePath = Directory.GetFiles(SavePath, "*", SearchOption.TopDirectoryOnly);

            var SavedNewWall = 0;
            var CurrentFileIndex = 1;
            var MaxFileIndex = WallPath.Length;
            var HashArray = new Dictionary<string, string>();

            foreach (var Wall in WallPath)
            {
                var SaveFileFullPath = "";
                var ChecksumFailed = false;
                var ImageInfo = Image.FromFile(Wall);

                Console.WriteLine($"Processing {CurrentFileIndex} In Total {MaxFileIndex} Files...");
                CurrentFileIndex++;

                Console.Write("    Checking File Is Valid...", CheckingColor);

                if (ImageInfo.Width < 1080) {Console.WriteLine($" Mask Invalid.! Hash Integrity Rejected.!", ErrorColor); continue;}
                Console.WriteLine(" Validated.!", WarningColor);
                Console.WriteLine();

                var DataStream = File.OpenRead(Wall);
                var HashInfo = Checksum(DataStream);
                DataStream.Dispose();

                Console.WriteLine("    Checking File Exist By Hash Integrity...", CheckingColor);
            
                if (HashArray.Count == 0)
                {
                    foreach (var SavedFile in SavedFilePath)
                    {
                        DataStream = File.OpenRead(SavedFile);
                        var ChecksumHashInfo = Checksum(DataStream);

                        DataStream.Dispose();

                        var CurrentFileInfo = new FileInfo(SavedFile);

                        try
                        {
                            HashArray.Add(ChecksumHashInfo, CurrentFileInfo.Name);
                        }
                        catch (Exception Error)
                        {
                            if (Error.HResult != -2147024809) continue;

                            HashArray.TryGetValue(ChecksumHashInfo, out var Duplicated);
                            Console.WriteLine($"      - Found Duplicate File {Duplicated} With {CurrentFileInfo.Name}", CheckingColor);

                            if (AutoRemoveDuplicated == true)
                            {
                                Console.WriteLine($"      - Auto Remove Duplicate Enabled.", CheckingColor);
                                
                                try
                                {
                                    File.Delete(SavedFile);
                                    Console.WriteLine($"      - Removed.", SavedColor);
                                }
                                catch (Exception AutoRemoveError)
                                {
                                    Console.WriteLine($"      - Error: {AutoRemoveError.Message}." , SavedColor);
                                }
                                finally
                                {
                                    Console.Write(Environment.NewLine);
                                }
                            }
                        }

                        if (ChecksumHashInfo != HashInfo) continue;

                        Console.WriteLine($"      {ChecksumHashInfo} - Mark Matched. Copy Rejected.! {Environment.NewLine}", ErrorColor);
                        ChecksumFailed = true;
                    }
                }
                else
                {
                    if (HashArray.ContainsKey(HashInfo))
                    {
                        Console.WriteLine($"      {HashInfo} - Mark Matched. Copy Rejected.! {Environment.NewLine}", ErrorColor);
                        ChecksumFailed = true;
                    }
                }

                if (ChecksumFailed)
                    continue;
                
                Console.WriteLine($"      Hash Integrity Passed.!", WarningColor);

                if (ImageInfo.Width >= 1920)
                {
                    try
                    {
                        Console.Write("      Saving File...", WarningColor);
                        
                        SaveFileFullPath = $"{SavePath}{FileName}_Horizontal_{RandomString(8)}.jpg";
                        
                        File.Copy(Wall, SaveFileFullPath, false);
                    }
                    catch
                    {
                        HorizontalFileDuplicated:
                        Console.Write(" Duplicated.! Saving File With Other Name...", ErrorColor);
                        
                        SaveFileFullPath = $"{SavePath}{FileName}_Horizontal_{RandomString(16)}.jpg";

                        try
                        {
                            File.Copy(Wall, SaveFileFullPath, false);
                        }
                        catch (Exception)
                        {
                            goto HorizontalFileDuplicated;
                        }
                    }
                    finally
                    {
                        if (SaveFileFullPath != "" && File.Exists(SaveFileFullPath))
                        {
                            Console.WriteLine($" Saved.! {Environment.NewLine}", SavedColor);
                            SavedNewWall++;
                        }
                        else
                            Console.WriteLine($" Fail.! {Environment.NewLine}", ErrorColor);
                    }
                }

                if (ImageInfo.Width >= 1080 && ImageInfo.Width < 1920)
                {
                    try
                    {
                        Console.Write("      Saving File...", WarningColor);

                        SaveFileFullPath = $"{SavePath}{FileName}_Vertical_{RandomString(8)}.jpg";

                        File.Copy(Wall, SaveFileFullPath, false);
                    }
                    catch
                    {
                        VerticalFileDuplicated:
                        Console.Write(" Duplicated.! Saving File With Other Name...", ErrorColor);

                        SaveFileFullPath = $"{SavePath}{FileName}_Vertical_{RandomString(16)}.jpg";

                        try
                        {
                            File.Copy(Wall, SaveFileFullPath, false);
                        }
                        catch (Exception)
                        {
                            goto VerticalFileDuplicated;
                        }
                    }
                    finally
                    {
                        if (SaveFileFullPath != "" && File.Exists(SaveFileFullPath))
                        {
                            Console.WriteLine($" Saved.! {Environment.NewLine}", SavedColor);
                            SavedNewWall++;
                        }
                        else
                            Console.WriteLine($" Fail.! {Environment.NewLine}", ErrorColor);
                    }
                }
            }

            if (SavedNewWall > 0)
            {
                Console.Write($"Done.! Saving ", SavedColor);
                
                Console.Write(SavedNewWall, WarningColor);
                
                Console.WriteLine(" Spotlight Copies.", SavedColor);
            }
            else
                Console.WriteLine("Done.! No New File Was Copied.!", ErrorColor);
            

            if (AutoExit == "1")
            {
                System.Threading.Thread.Sleep(1500);
            }
            else 
            { 
                Console.WriteLine("Press Any Key To Exist.!", WarningColor); 
                Console.ReadKey(); 
            }

            Environment.Exit(1);
        }

        private static string RandomString(int Length)
        {
            Random Random = new Random();
            const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(Chars, Length)
              .Select(String => String[Random.Next(String.Length)]).ToArray());
        }
        
        private static string Checksum(Stream ChecksumStream)
        {
            var CurrentPosition = ChecksumStream.Position;

            var Hasher = MD5.Create();
            ChecksumStream.Position = 0;

            var Hash = Hasher.ComputeHash(ChecksumStream);
            ChecksumStream.Position = CurrentPosition;

            return BitConverter.ToString(Hash).Replace("-", "").ToUpper();
        }
        
    }
    
    public static class CustomSearcher
    {
        public static IEnumerable<string> GetDirectories(string Path, string SearchPattern = "*", SearchOption SearchOptionList = SearchOption.TopDirectoryOnly)
        {
            if (SearchOptionList == SearchOption.TopDirectoryOnly)
                return Directory.GetDirectories(Path, SearchPattern).ToList();

            var Directories = new List<string>(GetDirectories(Path, SearchPattern));

            for (var Loop = 0; Loop < Directories.Count; Loop++)
                Directories.AddRange(GetDirectories(Directories[index: Loop], SearchPattern));

            return Directories;
        }

        private static IEnumerable<string> GetDirectories(string Path, string SearchPattern)
        {
            try
            {
                return Directory.GetDirectories(Path, SearchPattern).ToList();
            }
            catch (UnauthorizedAccessException)
            {
                return new List<string>();
            }
        }
    }
}
