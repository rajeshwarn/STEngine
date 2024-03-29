﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Security.AccessControl;
using System.Xml;
using System.Text;
using System.Text.RegularExpressions;

namespace STBuildTool
{
    public abstract class RemoteToolChain : STToolChain
    {
        protected void RegisterRemoteToolChain(STTargetPlatform InPlatform, CPPTargetPlatform CPPPlatform)
        {
            RemoteToolChainPlatform = InPlatform;

            // Register this tool chain for IOS
            Log.TraceVerbose("        Registered for {0}", CPPPlatform.ToString());
            STToolChain.RegisterPlatformToolChain(CPPPlatform, this);
        }

        /** These two variables will be loaded from XML config file in XmlConfigLoader.Init() */
        [XmlConfig]
        public static string RemoteServerName = "";
        [XmlConfig]
        public static string[] PotentialServerNames = new string[] { };

        /** Keep a list of remote files that are potentially copied from local to remote */
        private static Dictionary<FileItem, FileItem> CachedRemoteFileItems = new Dictionary<FileItem, FileItem>();

        /** The path (on the Mac) to the your particular development directory, where files will be copied to from the PC */
        public static string UserDevRootMac = "/UE4/Builds/";

        /** Whether or not to connect to UnrealRemoteTool using RPCUtility */
        [XmlConfig]
        public static bool bUseRPCUtil = true;

        /** Path to rsync executable and parameters for your rsync utility */
        [XmlConfig]
        public static string RSyncExe = "${PROGRAM_FILES}\\DeltaCopy\\rsync.exe";
        public static string ResolvedRSyncExe = null;

        /** Path to rsync executable and parameters for your rsync utility */
        [XmlConfig]
        public static string SSHExe = "${PROGRAM_FILES}\\DeltaCopy\\ssh.exe";
        public static string ResolvedSSHExe = null;

        /** Instead of looking for RemoteToolChainPrivate.key in the usual places (Documents/Unreal Engine/UnrealBuildTool/SSHKeys, Engine/Build/SSHKeys), this private key will be used if set */
        [XmlConfig]
        public static string SSHPrivateKeyOverridePath = "";
        public static string ResolvedSSHPrivateKey = null;

        /** The authentication used for Rsync (for the -e rsync flag) */
        [XmlConfig]
        public static string RsyncAuthentication = "ssh -i '${CYGWIN_SSH_PRIVATE_KEY}'";
        public static string ResolvedRsyncAuthentication = null;

        /** The authentication used for SSH (probably similar to RsyncAuthentication) */
        [XmlConfig]
        public static string SSHAuthentication = "-i '${CYGWIN_SSH_PRIVATE_KEY}'";
        public static string ResolvedSSHAuthentication = null;

        /** Username on the remote machine to connect to with RSync */
        [XmlConfig]
        public static string RSyncUsername = "${CURRENT_USER}";
        public static string ResolvedRSyncUsername = null;

        // has the toolchain initialized remote execution yet? no need to do it multiple times
        private static bool bHasBeenInitialized = false;

        /** The directory that this local branch is in, without drive information (strip off X:\ from X:\UE4\iOS) */
        public static string BranchDirectory = Path.GetFullPath(".\\");


        /** Substrings that indicate a line contains an error */
        protected static List<string> ErrorMessageTokens;

        /** The platform this toolchain is compiling for */
        protected STTargetPlatform RemoteToolChainPlatform;

        /** The average amound of memory a compile takes, used so that we don't compile too many things at once */
        public static int MemoryPerCompileMB = 1000;

        static RemoteToolChain()
        {
            ErrorMessageTokens = new List<string>();
            ErrorMessageTokens.Add("ERROR ");
            ErrorMessageTokens.Add("** BUILD FAILED **");
            ErrorMessageTokens.Add("[BEROR]");
            ErrorMessageTokens.Add("IPP ERROR");
            ErrorMessageTokens.Add("System.Net.Sockets.SocketException");

            BranchDirectory = BranchDirectory.Replace("Engine\\Binaries\\DotNET", "");
            BranchDirectory = BranchDirectory.Replace("Engine\\Source\\", "");
        }

        private static string ResolveString(string Input, bool bIsPath)
        {
            string Result = Input;

            // these assume entire string is a path, and will do file operations on the whole string
            if (bIsPath)
            {
                if (Result.Contains("${PROGRAM_FILES}"))
                {
                    // first look in real ProgramFiles
                    string Temp = Result.Replace("${PROGRAM_FILES}", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolderOption.DoNotVerify));
                    if (File.Exists(Temp) || Directory.Exists(Temp))
                    {
                        Result = Temp;
                    }
                    else
                    {
                        // fallback to ProgramFilesX86
                        Temp = Result.Replace("${PROGRAM_FILES}", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolderOption.DoNotVerify));
                        if (File.Exists(Temp) || Directory.Exists(Temp))
                        {
                            Result = Temp;
                        }
                    }
                }

                if (Result.Contains("${ENGINE_ROOT}"))
                {
                    string Temp = Result.Replace("${ENGINE_ROOT}", Path.GetFullPath(BuildConfiguration.RelativeEnginePath + "\\.."));

                    // get the best version
                    Result = LookForSpecialFile(Temp);
                }

                if (Result.Contains("${PROJECT_ROOT}"))
                {
                    if (!STBuildTool.HasUProjectFile())
                    {
                        throw new BuildException("Configuration setting was using ${PROJECT_ROOT}, but there was no project specified");
                    }

                    string Temp = Result.Replace("${PROJECT_ROOT}", Path.GetFullPath(STBuildTool.GetUProjectPath()));

                    // get the best version
                    Result = LookForSpecialFile(Temp);
                }
            }

            // non path variables
            Result = Result.Replace("${CURRENT_USER}", Environment.UserName);

            // needs a resolved key (which isn't required if user is using alternate authentication)
            if (Result.Contains("${SSH_PRIVATE_KEY}") || Result.Contains("${CYGWIN_SSH_PRIVATE_KEY}"))
            {
                // if it needs the key, then make sure we have it!
                if (ResolvedSSHPrivateKey != null)
                {
                    Result = Result.Replace("${SSH_PRIVATE_KEY}", ResolvedSSHPrivateKey);
                    Result = Result.Replace("${CYGWIN_SSH_PRIVATE_KEY}", ConvertPathToCygwin(ResolvedSSHPrivateKey));
                }
                else
                {
                    Result = null;
                }
            }

            return Result;
        }

        private static string LookForSpecialFile(string InPath)
        {
            // look in special NotForLicensees dir first
            string Special = Path.Combine(Path.GetDirectoryName(InPath), "NoRedist", Path.GetFileName(InPath));
            if (File.Exists(Special) || Directory.Exists(Special))
            {
                return Special;
            }

            Special = Path.Combine(Path.GetDirectoryName(InPath), "NotForLicensees", Path.GetFileName(InPath));
            if (File.Exists(Special) || Directory.Exists(Special))
            {
                return Special;
            }

            return InPath;
        }

        // Look for any build options in the engine config file.
        public override void ParseProjectSettings()
        {
            base.ParseProjectSettings();

            ConfigCacheIni Ini = new ConfigCacheIni(STTargetPlatform.IOS, "Engine", STBuildTool.GetUProjectPath());
            string ServerName = RemoteServerName;
            if (Ini.GetString("/Script/IOSRuntimeSettings.IOSRuntimeSettings", "RemoteServerName", out ServerName))
            {
                RemoteServerName = ServerName;
            }

            bool bUseRSync = false;
            if (Ini.GetBool("/Script/IOSRuntimeSettings.IOSRuntimeSettings", "bUseRSync", out bUseRSync))
            {
                bUseRPCUtil = !bUseRSync;
                Ini.GetString("/Script/IOSRuntimeSettings.IOSRuntimeSettings", "RSyncUsername", out RSyncUsername);

                string ConfigKeyPath;
                if (Ini.GetString("/Script/IOSRuntimeSettings.IOSRuntimeSettings", "SSHPrivateKeyOverridePath", out ConfigKeyPath))
                {
                    if (File.Exists(ConfigKeyPath))
                    {
                        SSHPrivateKeyOverridePath = ConfigKeyPath;
                    }
                }
            }
        }

        // Gather a users root path from the remote server. Should only be called once.
        public static void SetUserDevRootFromServer()
        {
            if (!bUseRPCUtil)
            {
                // Only set relative to the users root when using rsync, for now
                Hashtable Results = RPCUtilHelper.Command("/", "echo $HOME", null);

                if (Results == null)
                {
                    Log.TraceInformation("UserDevRoot Command failed to execute!");
                }
                else if (Results["CommandOutput"] != null)
                {
                    // pass back the string
                    string HomeLocation = Results["CommandOutput"] as string;
                    UserDevRootMac = HomeLocation + UserDevRootMac;
                }
            }
        }

        // Do any one-time, global initialization for the tool chain
        static int InitializationErrorCode = 0;
        private static int InitializeRemoteExecution()
        {
            if (bHasBeenInitialized)
            {
                return InitializationErrorCode;
            }

            if (BuildHostPlatform.Current.Platform != STTargetPlatform.Mac)
            {
                // If we don't care which machine we're going to build on, query and
                // pick the one with the most free command slots available
                if (RemoteServerName == "best_available")
                {
                    int AvailableSlots = 0;
                    int Attempts = 0;
                    if (!ProjectFileGenerator.bGenerateProjectFiles)
                    {
                        Log.TraceInformation("Picking a random Mac builder...");
                    }
                    while (AvailableSlots < 2 && Attempts < 20)
                    {
                        RemoteServerName = PotentialServerNames.OrderBy(x => Guid.NewGuid()).FirstOrDefault();

                        // make sure it's ready to take commands
                        AvailableSlots = GetAvailableCommandSlotCount(RemoteServerName);

                        Attempts++;
                    }

                    // make sure it succeeded
                    if (AvailableSlots <= 1)
                    {
                        throw new BuildException("Failed to find a Mac available to take commands!");
                    }
                    else if (!ProjectFileGenerator.bGenerateProjectFiles)
                    {
                        Log.TraceInformation("Chose {0} after {1} attempts to find a Mac, with {2} slots", RemoteServerName, Attempts, AvailableSlots);
                    }
                    /*
                     * this does not work right, because it pushes a lot of tasks to machines that have substantially more slots than others
                                        Log.TraceInformation("Picking the best available Mac builder...");
                                        Int32 MostAvailableCount = Int32.MinValue;
                                        foreach (string NextMacName in PotentialServerNames)
                                        {
                                            Int32 NextAvailableCount = GetAvailableCommandSlotCount(NextMacName);
                                            if (NextAvailableCount > MostAvailableCount)
                                            {
                                                MostAvailableName = NextMacName;
                                                MostAvailableCount = NextAvailableCount;
                                            }

                                            Log.TraceVerbose("... " + NextMacName + " has " + NextAvailableCount + " slots available");
                                        }
                                        Log.TraceVerbose("Picking the compile server with the most available command slots: " + MostAvailableName);

                                        // Finally, assign the name of the Mac we're going to use
                                        RemoteServerName = MostAvailableName;
                     */
                }
                else if (!ProjectFileGenerator.bGenerateProjectFiles)
                {
                    Log.TraceInformation("Picking the default remote server " + RemoteServerName);
                }

                // we need a server name!
                if (string.IsNullOrEmpty(RemoteServerName))
                {
                    Log.TraceError("Remote compiling requires a server name. Use the editor to set up your remote compilation settings.");
                    InitializationErrorCode = 99;
                }

                if (!bUseRPCUtil)
                {
                    // we need the RemoteServerName and the Username to find the private key
                    ResolvedRSyncUsername = ResolveString(RSyncUsername, false);

                    bool bFoundOverrideSSHPrivateKey = false;

                    // if the override path is set, just use it directly
                    if (!string.IsNullOrEmpty(SSHPrivateKeyOverridePath))
                    {
                        ResolvedSSHPrivateKey = ResolveString(SSHPrivateKeyOverridePath, true);

                        bFoundOverrideSSHPrivateKey = File.Exists(ResolvedSSHPrivateKey);

                        // make sure it exists
                        if (!bFoundOverrideSSHPrivateKey)
                        {
                            Log.TraceWarning("An SSHKey override was specified [" + SSHPrivateKeyOverridePath + "] but it doesn't exist. Looking elsewhere...");
                        }
                    }

                    if (!bFoundOverrideSSHPrivateKey)
                    {
                        // all the places to look for a key
                        string[] Locations = new string[] {
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Unreal Engine", "UnrealBuildTool"),
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Unreal Engine", "UnrealBuildTool"),
                            Path.Combine(STBuildTool.GetUProjectPath(), "Build", "NotForLicensees"),
                            Path.Combine(STBuildTool.GetUProjectPath(), "Build", "NoRedist"),
                            Path.Combine(STBuildTool.GetUProjectPath(), "Build"),
                            Path.Combine(BuildConfiguration.RelativeEnginePath, "Build", "NotForLicensees"),
                            Path.Combine(BuildConfiguration.RelativeEnginePath, "Build", "NoRedist"),
                            Path.Combine(BuildConfiguration.RelativeEnginePath, "Build"),
                        };

                        // look for a key file
                        foreach (string Location in Locations)
                        {
                            string KeyPath = Path.Combine(Location, "SSHKeys", RemoteServerName, ResolvedRSyncUsername, "RemoteToolChainPrivate.key");
                            if (File.Exists(KeyPath))
                            {
                                ResolvedSSHPrivateKey = KeyPath;
                                bFoundOverrideSSHPrivateKey = true;
                                break;
                            }
                        }
                    }

                    if (!bFoundOverrideSSHPrivateKey)
                    {
                        throw new BuildException("An SSHKey was required, but one cannot be found. Can't continue...");
                    }

                    // resolve the rest of the strings
                    ResolvedRSyncExe = ResolveString(RSyncExe, true);
                    ResolvedSSHExe = ResolveString(SSHExe, true);
                    ResolvedRsyncAuthentication = ResolveString(RsyncAuthentication, false);
                    ResolvedSSHAuthentication = ResolveString(SSHAuthentication, false);
                }

                // start up remote communication and record if it succeeds
                InitializationErrorCode = RPCUtilHelper.Initialize(RemoteServerName);

                // allow user to set up
                if (InitializationErrorCode == 100)
                {
                    Process KeyProcess = new Process();
                    KeyProcess.StartInfo.WorkingDirectory = Path.GetFullPath(Path.Combine(BuildConfiguration.RelativeEnginePath, "Build", "BatchFiles"));
                    KeyProcess.StartInfo.FileName = "MakeAndInstallSSHKey.bat";
                    KeyProcess.StartInfo.Arguments = string.Format(
                        "\"{0}\" \"{1}\" {2} {3} \"{4}\" \"{5}\" \"{6}\"",
                        ResolvedSSHExe,
                        ResolvedRSyncExe,
                        ResolvedRSyncUsername,
                        RemoteServerName,
                        Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                        ConvertPathToCygwin(Environment.GetFolderPath(Environment.SpecialFolder.Personal)),
                        Path.GetFullPath(BuildConfiguration.RelativeEnginePath));

                    KeyProcess.Start();
                    KeyProcess.WaitForExit();

                    // make sure it succeeded if we want to re-init
                    if (KeyProcess.ExitCode == 0)
                    {
                        InitializeRemoteExecution();
                    }
                }
            }
            else
            {
                RemoteServerName = Environment.MachineName;
                // can't error in this case
            }

            bHasBeenInitialized = true;
            return InitializationErrorCode;
        }

        public override void SetUpGlobalEnvironment()
        {
            base.SetUpGlobalEnvironment();

            // connect to server
            InitializeRemoteExecution();

            // Setup root directory to use.
            SetUserDevRootFromServer();
        }

        /** Converts the passed in path from UBT host to compiler native format. */
        public override String ConvertPath(String OriginalPath)
        {
            if (BuildHostPlatform.Current.Platform != STTargetPlatform.Mac)
            {
                if (OriginalPath[1] != ':')
                {
                    throw new BuildException("Can only convert full paths ({0})", OriginalPath);
                }

                string MacPath = string.Format("{0}{1}/{2}/{3}",
                    UserDevRootMac,
                    Environment.MachineName,
                    OriginalPath[0].ToString().ToUpper(),
                    OriginalPath.Substring(3));

                // clean the path
                MacPath = MacPath.Replace("\\", "/");

                return MacPath;
            }
            else
            {
                return OriginalPath.Replace("\\", "/");
            }
        }

        protected string GetMacDevSrcRoot()
        {
            if (BuildHostPlatform.Current.Platform != STTargetPlatform.Mac)
            {
                // figure out the remote version of Engine/Source
                return ConvertPath(Path.GetFullPath(Path.Combine(BranchDirectory, "Engine/Source/")));
            }
            else
            {
                return ".";
            }
        }

        private static List<string> RsyncDirs = new List<string>();
        private static List<string> RsyncExtensions = new List<string>();

        public void QueueFileForBatchUpload(FileItem LocalFileItem)
        {
            // Now, we actually just remember unique directories with any files, and upload all files in them to the remote machine
            // (either via rsync, or RPCUtil acting like rsync)
            string Entry = Path.GetDirectoryName(LocalFileItem.AbsolutePath);
            if (!RsyncDirs.Contains(Entry))
            {
                RsyncDirs.Add(Entry);
            }

            string Ext = Path.GetExtension(LocalFileItem.AbsolutePath);
            if (Ext == "")
            {
                Ext = Path.GetFileName(LocalFileItem.AbsolutePath);
            }
            if (!RsyncExtensions.Contains(Ext))
            {
                RsyncExtensions.Add(Ext);
            }
        }

        public FileItem LocalToRemoteFileItem(FileItem LocalFileItem, bool bShouldUpload)
        {
            FileItem RemoteFileItem = null;

            // Look to see if we've already made a remote FileItem for this local FileItem
            if (!CachedRemoteFileItems.TryGetValue(LocalFileItem, out RemoteFileItem))
            {
                // If not, create it now
                string RemoteFilePath = ConvertPath(LocalFileItem.AbsolutePath);
                RemoteFileItem = FileItem.GetRemoteItemByPath(RemoteFilePath, RemoteToolChainPlatform);

                // Is shadowing requested?
                if (bShouldUpload)
                {
                    QueueFileForBatchUpload(LocalFileItem);
                }

                CachedRemoteFileItems.Add(LocalFileItem, RemoteFileItem);
            }

            return RemoteFileItem;
        }

        /**
		 * Helper function to sync source files to and from the local system and a remote Mac
		 */
        //This chunk looks to be required to pipe output to VS giving information on the status of a remote build.
        public static bool OutputReceivedDataEventHandlerEncounteredError = false;
        public static string OutputReceivedDataEventHandlerEncounteredErrorMessage = "";
        public static void OutputReceivedDataEventHandler(Object Sender, DataReceivedEventArgs Line)
        {
            if ((Line != null) && (Line.Data != null))
            {
                Log.TraceInformation(Line.Data);

                foreach (string ErrorToken in ErrorMessageTokens)
                {
                    if (Line.Data.Contains(ErrorToken))
                    {
                        OutputReceivedDataEventHandlerEncounteredError = true;
                        OutputReceivedDataEventHandlerEncounteredErrorMessage += Line.Data;
                        break;
                    }
                }
            }
        }

        public override void PostCodeGeneration(SHTManifest Manifest)
        {
            if (BuildHostPlatform.Current.Platform != STTargetPlatform.Mac)
            {
                // @todo UHT: Temporary workaround for UBT no longer being able to follow includes from generated headers unless
                //  the headers already existed before the build started.  We're working on a proper fix.

                // Make sure all generated headers are synced.  If we had to generate code, we need to assume that not all of the
                // header files existed on disk at the time that UBT scanned include statements looking for prerequisite files.  Those
                // files are created during code generation and must exist on disk by the time this function is called.  We'll scan
                // for generated code files and make sure they are enqueued for copying to the remote machine.
                foreach (var UObjectModule in Manifest.Modules)
                {
                    // @todo uht: Ideally would only copy exactly the files emitted by UnrealHeaderTool, rather than scanning directory (could copy stale files; not a big deal though)
                    try
                    {
                        var GeneratedCodeDirectory = Path.GetDirectoryName(UObjectModule.GeneratedCPPFilenameBase);
                        var GeneratedCodeFiles = Directory.GetFiles(GeneratedCodeDirectory, "*", SearchOption.AllDirectories);
                        foreach (var GeneratedCodeFile in GeneratedCodeFiles)
                        {
                            // Skip copying "Timestamp" files (UBT temporary files)
                            if (!Path.GetFileName(GeneratedCodeFile).Equals(@"Timestamp", StringComparison.InvariantCultureIgnoreCase))
                            {
                                var GeneratedCodeFileItem = FileItem.GetExistingItemByPath(GeneratedCodeFile);
                                QueueFileForBatchUpload(GeneratedCodeFileItem);
                            }
                        }
                    }
                    catch (System.IO.DirectoryNotFoundException)
                    {
                        // Ignore directory not found
                    }

                    // For source files in legacy "Classes" directories, we need to make sure they all get copied over too, since
                    // they may not have been directly included in any C++ source files (only generated headers), and the initial
                    // header scan wouldn't have picked them up if they hadn't been generated yet!
                    try
                    {
                        var SourceFiles = Directory.GetFiles(UObjectModule.BaseDirectory, "*", SearchOption.AllDirectories);
                        foreach (var SourceFile in SourceFiles)
                        {
                            var SourceFileItem = FileItem.GetExistingItemByPath(SourceFile);
                            QueueFileForBatchUpload(SourceFileItem);
                        }
                    }
                    catch (System.IO.DirectoryNotFoundException)
                    {
                        // Ignore directory not found
                    }
                }
            }
        }

        static public void OutputReceivedForRsync(Object Sender, DataReceivedEventArgs Line)
        {
            if ((Line != null) && (Line.Data != null) && (Line.Data != ""))
            {
                Log.TraceInformation(Line.Data);
            }
        }

        private static Dictionary<Object, StringBuilder> SSHOutputMap = new Dictionary<object, StringBuilder>();
        static public void OutputReceivedForSSH(Object Sender, DataReceivedEventArgs Line)
        {
            if ((Line != null) && (Line.Data != null) && (Line.Data != ""))
            {
                StringBuilder SSHOutput = SSHOutputMap[Sender];
                if (SSHOutput.Length != 0)
                {
                    SSHOutput.Append(Environment.NewLine);
                }
                SSHOutput.Append(Line.Data);
            }
        }

        private static string ConvertPathToCygwin(string InPath)
        {
            if (InPath == null)
            {
                return null;
            }
            return "/cygdrive/" + Utils.CleanDirectorySeparators(InPath.Replace(":", ""), '/');
        }

        public override void PreBuildSync()
        {
            // no need to sync on the Mac!
            if (BuildHostPlatform.Current.Platform == STTargetPlatform.Mac)
            {
                return;
            }

            if (bUseRPCUtil)
            {
                string ExtString = "";

                // look only for useful extensions
                foreach (string Ext in RsyncExtensions)
                {
                    // for later ls
                    ExtString += Ext.StartsWith(".") ? ("*" + Ext) : Ext;
                    ExtString += " ";
                }

                List<string> BatchUploadCommands = new List<string>();
                // for each directory we visited, add all the files in that directory
                foreach (string Dir in RsyncDirs)
                {
                    List<string> LocalFilenames = new List<string>();

                    // look only for useful extensions
                    foreach (string Ext in RsyncExtensions)
                    {
                        string[] Files = Directory.GetFiles(Dir, "*" + Ext);
                        foreach (string SyncFile in Files)
                        {
                            // remember all local files
                            LocalFilenames.Add(Path.GetFileName(SyncFile));

                            string RemoteFilePath = ConvertPath(SyncFile);
                            // an upload command is local name and remote name
                            BatchUploadCommands.Add(SyncFile + ";" + RemoteFilePath);
                        }
                    }
                }

                // batch upload
                RPCUtilHelper.BatchUpload(BatchUploadCommands.ToArray());
            }
            else
            {
                List<string> RelativeRsyncDirs = new List<string>();
                foreach (string Dir in RsyncDirs)
                {
                    RelativeRsyncDirs.Add(Utils.CleanDirectorySeparators(Utils.MakePathRelativeTo(Dir, "../.."), '/') + "/");
                }

                // write out directories to copy
                string RSyncPathsFile = Path.GetTempFileName();
                string IncludeFromFile = Path.GetTempFileName();
                File.WriteAllLines(RSyncPathsFile, RelativeRsyncDirs.ToArray());
                File.WriteAllLines(IncludeFromFile, RsyncExtensions);

                // source and destination paths in the format rsync wants
                string CygRootPath = ConvertPathToCygwin(Path.GetFullPath("../../"));
                string RemotePath = ConvertPath(Path.GetFullPath("../../")).Replace(" ", "\\ ");

                // get the executable dir for SSH, so Rsync can call it easily
                string ExeDir = Path.GetDirectoryName(ResolvedSSHExe);

                Process RsyncProcess = new Process();
                if (ExeDir != "")
                {
                    RsyncProcess.StartInfo.WorkingDirectory = ExeDir;
                }

                // --exclude='*'  ??? why???
                RsyncProcess.StartInfo.FileName = ResolvedRSyncExe;
                RsyncProcess.StartInfo.Arguments = string.Format(
                    "-vzae \"{0}\" --rsync-path=\"mkdir -p {2} && rsync\" --chmod=ug=rwX,o=rxX --delete --files-from=\"{4}\" --include-from=\"{5}\" --include='*/' --exclude='*.o' --exclude='Timestamp' {1} {6}@{3}:'{2}'",
                    ResolvedRsyncAuthentication,
                    CygRootPath,
                    RemotePath,
                    RemoteServerName,
                    ConvertPathToCygwin(RSyncPathsFile),
                    ConvertPathToCygwin(IncludeFromFile),
                    RSyncUsername);

                RsyncProcess.OutputDataReceived += new DataReceivedEventHandler(OutputReceivedForRsync);
                RsyncProcess.ErrorDataReceived += new DataReceivedEventHandler(OutputReceivedForRsync);

                // run rsync
                Utils.RunLocalProcess(RsyncProcess);

                File.Delete(IncludeFromFile);
                File.Delete(RSyncPathsFile);
            }

            // we can now clear out the set of files
            RsyncDirs.Clear();
            RsyncExtensions.Clear();
        }

        static public bool UploadFile(string LocalPath, string RemotePath)
        {
            string RemoteDir = Path.GetDirectoryName(RemotePath).Replace("\\", "/");
            string RemoteFilename = Path.GetFileName(RemotePath);

            // get the executable dir for SSH, so Rsync can call it easily
            string ExeDir = Path.GetDirectoryName(ResolvedSSHExe);

            Process RsyncProcess = new Process();
            if (ExeDir != "")
            {
                RsyncProcess.StartInfo.WorkingDirectory = ExeDir;
            }

            // make simple rsync commandline to send a file
            RsyncProcess.StartInfo.FileName = ResolvedRSyncExe;
            RsyncProcess.StartInfo.Arguments = string.Format(
                "-zae \"{0}\" --rsync-path=\"mkdir -p {1} && rsync\" \"{2}\" {3}@{4}:\"{1}/{5}\"",
                ResolvedRsyncAuthentication,
                RemoteDir,
                ConvertPathToCygwin(LocalPath),
                RSyncUsername,
                RemoteServerName,
                RemoteFilename
                );

            RsyncProcess.OutputDataReceived += new DataReceivedEventHandler(OutputReceivedForRsync);
            RsyncProcess.ErrorDataReceived += new DataReceivedEventHandler(OutputReceivedForRsync);

            // run rsync (0 means success)
            return Utils.RunLocalProcess(RsyncProcess) == 0;
        }

        static public bool DownloadFile(string RemotePath, string LocalPath)
        {
            // get the executable dir for SSH, so Rsync can call it easily
            string ExeDir = Path.GetDirectoryName(ResolvedSSHExe);

            Process RsyncProcess = new Process();
            if (ExeDir != "")
            {
                RsyncProcess.StartInfo.WorkingDirectory = ExeDir;
            }

            // make sure directory exists to download to
            Directory.CreateDirectory(Path.GetDirectoryName(LocalPath));

            // make simple rsync commandline to send a file
            RsyncProcess.StartInfo.FileName = ResolvedRSyncExe;
            RsyncProcess.StartInfo.Arguments = string.Format(
                "-zae \"{0}\" {2}@{3}:\"{4}\" \"{1}\"",
                ResolvedRsyncAuthentication,
                ConvertPathToCygwin(LocalPath),
                RSyncUsername,
                RemoteServerName,
                RemotePath
                );

            RsyncProcess.OutputDataReceived += new DataReceivedEventHandler(OutputReceivedForRsync);
            RsyncProcess.ErrorDataReceived += new DataReceivedEventHandler(OutputReceivedForRsync);

            //Console.WriteLine("COPY: {0} {1}", RsyncProcess.StartInfo.FileName, RsyncProcess.StartInfo.Arguments);

            // run rsync (0 means success)
            return Utils.RunLocalProcess(RsyncProcess) == 0;
        }

        static public Hashtable SSHCommand(string WorkingDirectory, string Command, string RemoteOutputPath)
        {
            Console.WriteLine("Doing {0}", Command);

            // make the commandline for other end
            string RemoteCommandline = "cd " + WorkingDirectory;
            if (!string.IsNullOrWhiteSpace(RemoteOutputPath))
            {
                RemoteCommandline += " && mkdir -p " + Path.GetDirectoryName(RemoteOutputPath).Replace("\\", "/");
            }

            // get the executable dir for SSH
            string ExeDir = Path.GetDirectoryName(ResolvedSSHExe);

            Process SSHProcess = new Process();
            if (ExeDir != "")
            {
                SSHProcess.StartInfo.WorkingDirectory = ExeDir;
            }

            // long commands go as a file
            if (Command.Length > 1024)
            {
                // upload the commandline text file
                string CommandLineFile = Path.GetTempFileName();
                File.WriteAllText(CommandLineFile, Command);

                string RemoteCommandlineDir = "/var/tmp/" + Environment.MachineName;
                string RemoteCommandlinePath = RemoteCommandlineDir + "/" + Path.GetFileName(CommandLineFile);

                DateTime Now = DateTime.Now;
                UploadFile(CommandLineFile, RemoteCommandlinePath);
                Console.WriteLine("Upload took {0}", (DateTime.Now - Now).ToString());

                // execute the file, not a commandline
                RemoteCommandline += string.Format(" && bash < {0} && rm {0}", RemoteCommandlinePath);
            }
            else
            {
                RemoteCommandline += " && " + Command;
            }

            SSHProcess.StartInfo.FileName = ResolvedSSHExe;
            SSHProcess.StartInfo.Arguments = string.Format(
                "{0} {1}@{2} \"{3}\"",
                ResolvedSSHAuthentication,
                RSyncUsername,
                RemoteServerName,
                RemoteCommandline.Replace("\"", "\\\""));

            Hashtable Return = new Hashtable();

            // add this process to the map
            SSHOutputMap[SSHProcess] = new StringBuilder("");
            SSHProcess.OutputDataReceived += new DataReceivedEventHandler(OutputReceivedForSSH);
            SSHProcess.ErrorDataReceived += new DataReceivedEventHandler(OutputReceivedForSSH);

            DateTime Start = DateTime.Now;
            Int64 ExitCode = Utils.RunLocalProcess(SSHProcess);
            Console.WriteLine("Execute took {0}", (DateTime.Now - Start).ToString());

            // now we have enough to fill out the HashTable
            Return["CommandOutput"] = SSHOutputMap[SSHProcess].ToString();
            Return["ExitCode"] = (object)ExitCode;

            SSHOutputMap.Remove(SSHProcess);

            return Return;
        }

        public override void PostBuildSync(STBuildTarget Target)
        {

        }

        static public Double GetAdjustedProcessorCountMultiplier()
        {
            if (BuildHostPlatform.Current.Platform != STTargetPlatform.Mac)
            {
                Int32 RemoteCPUCount = RPCUtilHelper.GetCommandSlots();
                if (RemoteCPUCount == 0)
                {
                    RemoteCPUCount = Environment.ProcessorCount;
                }

                Double AdjustedMultiplier = (Double)RemoteCPUCount / (Double)Environment.ProcessorCount;
                Log.TraceVerbose("Adjusting the remote Mac compile process multiplier to " + AdjustedMultiplier.ToString());
                return AdjustedMultiplier;
            }
            else
            {
                return 1.0;
            }
        }

        static public Int32 GetAvailableCommandSlotCount(string TargetMacName)
        {
            // ask how many slots are available, and increase by 1 (not sure why)
            Int32 RemoteAvailableCommandSlotCount = 1 + QueryRemoteMachine(TargetMacName, "rpc:command_slots_available");

            Log.TraceVerbose("Available command slot count for " + TargetMacName + " is " + RemoteAvailableCommandSlotCount.ToString());
            return RemoteAvailableCommandSlotCount;
        }

        /**
         * Translates clang output warning/error messages into vs-clickable messages
         * 
         * @param	sender		Sending object
         * @param	e			Event arguments (In this case, the line of string output)
         */
        protected void RemoteOutputReceivedEventHandler(object sender, DataReceivedEventArgs e)
        {
            var Output = e.Data;
            if (Output == null)
            {
                return;
            }

            if (Utils.IsRunningOnMono)
            {
                Log.TraceInformation(Output);
            }
            else
            {
                // Need to match following for clickable links
                string RegexFilePath = @"^(\/[A-Za-z0-9_\-\.]*)+\.(cpp|c|mm|m|hpp|h)";
                string RegexLineNumber = @"\:\d+\:\d+\:";
                string RegexDescription = @"(\serror:\s|\swarning:\s).*";

                // Get Matches
                string MatchFilePath = Regex.Match(Output, RegexFilePath).Value.Replace("Engine/Source/../../", "");
                string MatchLineNumber = Regex.Match(Output, RegexLineNumber).Value;
                string MatchDescription = Regex.Match(Output, RegexDescription).Value;

                // If any of the above matches failed, do nothing
                if (MatchFilePath.Length == 0 ||
                    MatchLineNumber.Length == 0 ||
                    MatchDescription.Length == 0)
                {
                    Log.TraceInformation(Output);
                    return;
                }

                // Convert Path
                string RegexStrippedPath = @"\/Engine\/.*"; //@"(Engine\/|[A-Za-z0-9_\-\.]*\/).*";
                string ConvertedFilePath = Regex.Match(MatchFilePath, RegexStrippedPath).Value;
                ConvertedFilePath = Path.GetFullPath("..\\.." + ConvertedFilePath);

                // Extract Line + Column Number
                string ConvertedLineNumber = Regex.Match(MatchLineNumber, @"\d+").Value;
                string ConvertedColumnNumber = Regex.Match(MatchLineNumber, @"(?<=:\d+:)\d+").Value;

                // Write output
                string ConvertedExpression = "  " + ConvertedFilePath + "(" + ConvertedLineNumber + "," + ConvertedColumnNumber + "):" + MatchDescription;
                Log.TraceInformation(ConvertedExpression); // To create clickable vs link
                //			Log.TraceInformation(Output);				// To preserve readable output log
            }
        }

        /**
         * Queries the remote compile server for CPU information
         * and computes the proper ProcessorCountMultiplier.
         */
        static private Int32 QueryResult = 0;
        static public void OutputReceivedForQuery(Object Sender, DataReceivedEventArgs Line)
        {
            if ((Line != null) && (Line.Data != null) && (Line.Data != ""))
            {
                Int32 TestValue = 0;
                if (Int32.TryParse(Line.Data, out TestValue))
                {
                    QueryResult = TestValue;
                }
                else
                {
                    Log.TraceVerbose("Info: Unexpected output from remote Mac system info query, skipping");
                }
            }
        }

        static public Int32 QueryRemoteMachine(string MachineName, string Command)
        {
            // we must run the commandline RPCUtility, because we could run this before we have opened up the RemoteRPCUtlity
            Process QueryProcess = new Process();
            QueryProcess.StartInfo.WorkingDirectory = Path.GetFullPath("..\\Binaries\\DotNET");
            QueryProcess.StartInfo.FileName = QueryProcess.StartInfo.WorkingDirectory + "\\RPCUtility.exe";
            QueryProcess.StartInfo.Arguments = string.Format("{0} {1} sysctl -n hw.ncpu",
                MachineName,
                UserDevRootMac);
            QueryProcess.OutputDataReceived += new DataReceivedEventHandler(OutputReceivedForQuery);
            QueryProcess.ErrorDataReceived += new DataReceivedEventHandler(OutputReceivedForQuery);

            // Try to launch the query's process, and produce a friendly error message if it fails.
            Utils.RunLocalProcess(QueryProcess);

            return QueryResult;
        }
    };
}
