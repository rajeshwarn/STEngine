﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace STBuildTool
{
    /// <summary>
    /// Host platform abstraction
    /// </summary>
    public abstract class BuildHostPlatform
    {
        private static BuildHostPlatform CurrentPlatform;
        private static bool bIsMac = File.Exists("/System/Library/CoreServices/SystemVersion.plist");

        /// <summary>
        /// Returns the name of platform UBT is running on. Internal use only. If you need access this this enum, use BuildHostPlatform.Current.Platform */
        /// </summary>
        private static STTargetPlatform GetRuntimePlatform()
        {
            PlatformID Platform = Environment.OSVersion.Platform;
            switch (Platform)
            {
                case PlatformID.Win32NT:
                    return STTargetPlatform.Win64;
                case PlatformID.Unix:
                    return bIsMac ? STTargetPlatform.Mac : STTargetPlatform.Linux;
                case PlatformID.MacOSX:
                    return STTargetPlatform.Mac;
                default:
                    throw new BuildException("Unhandled runtime platform " + Platform);
            }
        }

        /// <summary>
        /// Host platform singleton.
        /// </summary>
        static public BuildHostPlatform Current
        {
            get
            {
                if (CurrentPlatform == null)
                {
                    switch (GetRuntimePlatform())
                    {
                        case STTargetPlatform.Win64:
                            CurrentPlatform = new WindowsBuildHostPlatform();
                            break;
                        case STTargetPlatform.Mac:
                            CurrentPlatform = new MacBuildHostPlatform();
                            break;
                        case STTargetPlatform.Linux:
                            CurrentPlatform = new LinuxBuildHostPlatform();
                            break;
                    }
                }
                return CurrentPlatform;
            }
        }

        /// <summary>
        /// Gets the current host platform type.
        /// </summary>
        abstract public STTargetPlatform Platform { get; }

        /// <summary>
        /// Checks the API version of a dynamic library
        /// </summary>
        /// <param name="Filename">Filename of the library</param>
        /// <returns>API version of -1 if not found.</returns>
        abstract public int GetDllApiVersion(string Filename);

        /// <summary>
        /// Class that holds information about a running process
        /// </summary>
        public class ProcessInfo
        {
            /// <summary>
            /// Process ID
            /// </summary>
            public int PID;
            /// <summary>
            /// Name of the process
            /// </summary>
            public string Name;
            /// <summary>
            /// Filename of the process binary
            /// </summary>
            public string Filename;

            public ProcessInfo(int InPID, string InName, string InFilename, string[] InModules)
            {
                PID = InPID;
                Name = InName;
                Filename = InFilename;
            }
            public ProcessInfo(Process Proc)
            {
                PID = Proc.Id;
                Name = Proc.ProcessName;
                Filename = Path.GetFullPath(Proc.MainModule.FileName);
            }
            public override string ToString()
            {
                return String.Format("{0}, {1}", Name, Filename);
            }
        }

        /// <summary>
        /// Gets all currently running processes.
        /// </summary>
        /// <returns></returns>
        public virtual ProcessInfo[] GetProcesses()
        {
            var AllProcesses = Process.GetProcesses();
            var Result = new List<ProcessInfo>(AllProcesses.Length);
            foreach (var Proc in AllProcesses)
            {
                try
                {
                    if (!Proc.HasExited)
                    {
                        Result.Add(new ProcessInfo(Proc));
                    }
                }
                catch { }
            }
            return Result.ToArray();
        }

        /// <summary>
        /// Gets a process by name.
        /// </summary>
        /// <param name="Name">Name of the process to get information for.</param>
        /// <returns></returns>
        public virtual ProcessInfo GetProcessByName(string Name)
        {
            var AllProcess = GetProcesses();
            foreach (var Info in AllProcess)
            {
                if (Info.Name == Name)
                {
                    return Info;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets processes by name.
        /// </summary>
        /// <param name="Name">Name of the process to get information for.</param>
        /// <returns></returns>
        public virtual ProcessInfo[] GetProcessesByName(string Name)
        {
            var AllProcess = GetProcesses();
            var Result = new List<ProcessInfo>();
            foreach (var Info in AllProcess)
            {
                if (Info.Name == Name)
                {
                    Result.Add(Info);
                }
            }
            return Result.ToArray();
        }

        /// <summary>
        /// Gets the filenames of all modules associated with a process
        /// </summary>
        /// <param name="PID">Process ID</param>
        /// <param name="Filename">Filename of the binary associated with the process.</param>
        /// <returns>An array of all module filenames associated with the process. Can be empty of the process is no longer running.</returns>
        public virtual string[] GetProcessModules(int PID, string Filename)
        {
            List<string> Modules = new List<string>();
            try
            {
                var Proc = Process.GetProcessById(PID);
                if (Proc != null)
                {
                    foreach (var Module in Proc.Modules.Cast<System.Diagnostics.ProcessModule>())
                    {
                        Modules.Add(Path.GetFullPath(Module.FileName));
                    }
                }
            }
            catch { }
            return Modules.ToArray();
        }
    }

    class WindowsBuildHostPlatform : BuildHostPlatform
    {
        public override STTargetPlatform Platform
        {
            get { return STTargetPlatform.Win64; }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr FindResource(IntPtr hModule, string lpName, string lpType);
        [DllImport("kernel32.dll")]
        static extern IntPtr FindResource(IntPtr hModule, IntPtr lpID, IntPtr lpType);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint SizeofResource(IntPtr hModule, IntPtr hResInfo);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr LockResource(IntPtr hResData);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern void UnlockResource(IntPtr hResInfo);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern void FreeLibrary(IntPtr hModule);

        [Flags]
        enum LoadLibraryFlags : uint
        {
            DONT_RESOLVE_DLL_REFERENCES = 0x00000001,
            LOAD_LIBRARY_AS_DATAFILE = 0x00000002,
            LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008,
            LOAD_IGNORE_CODE_AUTHZ_LEVEL = 0x00000010,
            LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x00000020,
            LOAD_LIBRARY_AS_DATAFILE_EXCLUSIVE = 0x00000040,
            LOAD_LIBRARY_REQUIRE_SIGNED_TARGET = 0x00000080
        }

        enum ResourceType
        {
            RT_CURSOR = 1,
            RT_BITMAP = 2,
            RT_ICON = 3,
            RT_MENU = 4,
            RT_DIALOG = 5,
            RT_STRING = 6,
            RT_FONTDIR = 7,
            RT_FONT = 8,
            RT_ACCELERATOR = 9,
            RT_RCDATA = 10,
            RT_MESSAGETABLE = 11
        }

        public override int GetDllApiVersion(string Filename)
        {
            int Result = -1;

            try
            {
                const int ID_MODULE_API_VERSION_RESOURCE = 191;

                // Retrieves the embedded API version from a DLL
                IntPtr hModule = LoadLibraryEx(Filename, IntPtr.Zero, (uint)LoadLibraryFlags.LOAD_LIBRARY_AS_DATAFILE);
                if (hModule != IntPtr.Zero)
                {
                    IntPtr hResInfo = FindResource(hModule, new IntPtr(ID_MODULE_API_VERSION_RESOURCE), new IntPtr((int)ResourceType.RT_RCDATA));
                    if (hResInfo != IntPtr.Zero)
                    {
                        IntPtr hResGlobal = LoadResource(hModule, hResInfo);
                        if (hResGlobal != IntPtr.Zero)
                        {
                            IntPtr pResData = LockResource(hResGlobal);
                            if (pResData != IntPtr.Zero)
                            {
                                uint Length = SizeofResource(hModule, hResInfo);
                                if (Length > 0)
                                {
                                    var Str = Marshal.PtrToStringAnsi(pResData);
                                    Result = Int32.Parse(Str);
                                }
                            }
                        }
                    }
                    FreeLibrary(hModule);
                }
            }
            catch (Exception Ex)
            {
                Log.TraceWarning("Failed to get DLL API version for {0}. Exception: {1}", Filename, Ex.Message);
            }

            return Result;
        }

    }

    class MacBuildHostPlatform : BuildHostPlatform
    {
        public override STTargetPlatform Platform
        {
            get { return STTargetPlatform.Mac; }
        }

        public override int GetDllApiVersion(string Filename)
        {
            // @TODO: Implement GetDllApiVersion for Mac
            return -1;
        }

        /// <summary>
        /// Currently Mono returns incomplete process names in Process.GetProcesses() so we need to parse 'ps' output.
        /// </summary>
        /// <returns></returns>
        public override ProcessInfo[] GetProcesses()
        {
            var Result = new List<ProcessInfo>();

            var StartInfo = new ProcessStartInfo();
            StartInfo.FileName = "ps";
            StartInfo.Arguments = "-eaw -o pid,comm";
            StartInfo.CreateNoWindow = true;
            StartInfo.UseShellExecute = false;
            StartInfo.RedirectStandardOutput = true;

            var Proc = new Process();
            Proc.StartInfo = StartInfo;
            try
            {
                Proc.Start();
                Proc.WaitForExit();
                for (string Line = Proc.StandardOutput.ReadLine(); Line != null; Line = Proc.StandardOutput.ReadLine())
                {
                    Line = Line.Trim();
                    int PIDEnd = Line.IndexOf(' ');
                    var PIDString = Line.Substring(0, PIDEnd);
                    if (PIDString != "PID")
                    {
                        var Filename = Line.Substring(PIDEnd + 1);
                        var Pid = Int32.Parse(PIDString);
                        try
                        {
                            var ExistingProc = Process.GetProcessById(Pid);
                            if (ExistingProc != null && Pid != Process.GetCurrentProcess().Id && ExistingProc.HasExited == false)
                            {
                                var ProcInfo = new ProcessInfo(ExistingProc);
                                ProcInfo.Name = Path.GetFileName(Filename);
                                ProcInfo.Filename = Filename;
                                Result.Add(ProcInfo);
                            }
                        }
                        catch { }
                    }
                }

            }
            catch { }
            return Result.ToArray();
        }

        /// <summary>
        /// Currently Mono returns incomplete list of modules for Process.Modules so we need to parse vmmap output.
        /// </summary>
        /// <param name="PID"></param>
        /// <param name="Filename"></param>
        /// <returns></returns>
        public override string[] GetProcessModules(int PID, string Filename)
        {
            HashSet<string> Modules = new HashSet<string>();
            // Add the process file name to the module list. This is to make it compatible with the results of Process.Modules on Windows.
            Modules.Add(Filename);

            var StartInfo = new ProcessStartInfo();
            StartInfo.FileName = "vmmap";
            StartInfo.Arguments = String.Format("{0} -w", PID);
            StartInfo.CreateNoWindow = true;
            StartInfo.UseShellExecute = false;
            StartInfo.RedirectStandardOutput = true;

            var Proc = new Process();
            Proc.StartInfo = StartInfo;
            try
            {
                Proc.Start();
                // Start processing output before vmmap exits otherwise it's going to hang
                while (!Proc.WaitForExit(1))
                {
                    ProcessVMMapOutput(Proc, Modules);
                }
                ProcessVMMapOutput(Proc, Modules);
            }
            catch { }
            return Modules.ToArray();
        }
        private void ProcessVMMapOutput(Process Proc, HashSet<string> Modules)
        {
            for (string Line = Proc.StandardOutput.ReadLine(); Line != null; Line = Proc.StandardOutput.ReadLine())
            {
                Line = Line.Trim();
                if (Line.EndsWith(".dylib"))
                {
                    const int SharingModeLength = 6;
                    int SMStart = Line.IndexOf("SM=");
                    int PathStart = SMStart + SharingModeLength;
                    string Module = Line.Substring(PathStart).Trim();
                    if (!Modules.Contains(Module))
                    {
                        Modules.Add(Module);
                    }
                }
            }
        }
    }

    class LinuxBuildHostPlatform : BuildHostPlatform
    {
        public override STTargetPlatform Platform
        {
            get { return STTargetPlatform.Linux; }
        }

        public override int GetDllApiVersion(string Filename)
        {
            // @TODO: Implement GetDllApiVersion for Linux
            return -1;
        }

        /// <summary>
        /// Currently Mono returns incomplete process names in Process.GetProcesses() so we need to use /proc
        /// (also, Mono locks up during process traversal sometimes, trying to open /dev/snd/pcm*)
        /// </summary>
        /// <returns></returns>
        public override ProcessInfo[] GetProcesses()
        {
            // @TODO: Implement for Linux
            return new List<ProcessInfo>().ToArray();
        }

        /// <summary>
        /// Currently Mono returns incomplete list of modules for Process.Modules so we need to parse /proc/PID/maps.
        /// (also, Mono locks up during process traversal sometimes, trying to open /dev/snd/pcm*)
        /// </summary>
        /// <param name="PID"></param>
        /// <param name="Filename"></param>
        /// <returns></returns>
        public override string[] GetProcessModules(int PID, string Filename)
        {
            // @TODO: Implement for Linux
            return new List<string>().ToArray();
        }
    }
}
