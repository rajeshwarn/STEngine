using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace STBuildTool
{
    public interface ISTToolChain
    {
        void RegisterToolChain();

        CPPOutput CompileCPPFiles(STBuildTarget Target, CPPEnvironment CompileEnvironment, List<FileItem> SourceFiles, string ModuleName);

        CPPOutput CompileRCFiles(STBuildTarget Target, CPPEnvironment Environment, List<FileItem> RCFiles);

        FileItem[] LinkAllFiles(LinkEnvironment LinkEnvironment, bool bBuildImportLibraryOnly);

        void CompileCSharpProject(CSharpEnvironment CompileEnvironment, string ProjectFileName, string DestinationFile);

        /** Converts the passed in path from UBT host to compiler native format. */
        String ConvertPath(String OriginalPath);

        /// <summary>
        /// Called immediately after UnrealHeaderTool is executed to generated code for all UObjects modules.  Only is called if UnrealHeaderTool was actually run in this session.
        /// </summary>
        /// <param name="Manifest">List of UObject modules we generated code for.</param>
        void PostCodeGeneration(SHTManifest Manifest);

        void PreBuildSync();

        void PostBuildSync(STBuildTarget Target);

        ICollection<FileItem> PostBuild(FileItem Executable, LinkEnvironment ExecutableLinkEnvironment);

        void SetUpGlobalEnvironment();

        void AddFilesToManifest(BuildManifest Manifest, STBuildBinary Binary);

        void SetupBundleDependencies(List<STBuildBinary> Binaries, string GameName);

        void FixBundleBinariesPaths(STBuildTarget Target, List<STBuildBinary> Binaries);

        string GetPlatformVersion();

        string GetPlatformDevices();

        STTargetPlatform GetPlatform();
    }

    public abstract class STToolChain : ISTToolChain
    {
        static Dictionary<CPPTargetPlatform, ISTToolChain> CPPToolChainDictionary = new Dictionary<CPPTargetPlatform, ISTToolChain>();

        public static void RegisterPlatformToolChain(CPPTargetPlatform InPlatform, ISTToolChain InToolChain)
        {
            if (CPPToolChainDictionary.ContainsKey(InPlatform) == true)
            {
                Log.TraceInformation("RegisterPlatformToolChain Warning: Registering tool chain {0} for {1} when it is already set to {2}",
                    InToolChain.ToString(), InPlatform.ToString(), CPPToolChainDictionary[InPlatform].ToString());
                CPPToolChainDictionary[InPlatform] = InToolChain;
            }
            else
            {
                CPPToolChainDictionary.Add(InPlatform, InToolChain);
            }
        }

        public static void UnregisterPlatformToolChain(CPPTargetPlatform InPlatform)
        {
            CPPToolChainDictionary.Remove(InPlatform);
        }

        public static ISTToolChain GetPlatformToolChain(CPPTargetPlatform InPlatform)
        {
            if (CPPToolChainDictionary.ContainsKey(InPlatform) == true)
            {
                return CPPToolChainDictionary[InPlatform];
            }
            throw new BuildException("GetPlatformToolChain: No tool chain found for {0}", InPlatform.ToString());
        }

        public abstract void RegisterToolChain();

        public abstract CPPOutput CompileCPPFiles(STBuildTarget Target, CPPEnvironment CompileEnvironment, List<FileItem> SourceFiles, string ModuleName);

        public virtual CPPOutput CompileRCFiles(STBuildTarget Target, CPPEnvironment Environment, List<FileItem> RCFiles)
        {
            CPPOutput Result = new CPPOutput();
            return Result;
        }

        public abstract FileItem LinkFiles(LinkEnvironment LinkEnvironment, bool bBuildImportLibraryOnly);
        public virtual FileItem[] LinkAllFiles(LinkEnvironment LinkEnvironment, bool bBuildImportLibraryOnly)
        {
            return new FileItem[] { LinkFiles(LinkEnvironment, bBuildImportLibraryOnly) };
        }


        public virtual void CompileCSharpProject(CSharpEnvironment CompileEnvironment, string ProjectFileName, string DestinationFile)
        {
        }

        /// <summary>
        /// Get the name of the response file for the current linker environment and output file
        /// </summary>
        /// <param name="LinkEnvironment"></param>
        /// <param name="OutputFile"></param>
        /// <returns></returns>
        public static string GetResponseFileName(LinkEnvironment LinkEnvironment, FileItem OutputFile)
        {
            // Construct a relative path for the intermediate response file
            string ResponseFileName = Path.Combine(LinkEnvironment.Config.IntermediateDirectory, Path.GetFileName(OutputFile.AbsolutePath) + ".response");
            if (STBuildTool.HasUProjectFile())
            {
                // If this is the uproject being built, redirect the intermediate
                if (Utils.IsFileUnderDirectory(OutputFile.AbsolutePath, STBuildTool.GetUProjectPath()))
                {
                    ResponseFileName = Path.Combine(
                        STBuildTool.GetUProjectPath(),
                        BuildConfiguration.PlatformIntermediateFolder,
                        Path.GetFileNameWithoutExtension(STBuildTool.GetUProjectFile()),
                        LinkEnvironment.Config.Target.Configuration.ToString(),
                        Path.GetFileName(OutputFile.AbsolutePath) + ".response");
                }
            }
            // Convert the relative path to an absolute path
            ResponseFileName = Path.GetFullPath(ResponseFileName);

            return ResponseFileName;
        }

        /** Converts the passed in path from UBT host to compiler native format. */
        public virtual String ConvertPath(String OriginalPath)
        {
            return OriginalPath;
        }


        /// <summary>
        /// Called immediately after UnrealHeaderTool is executed to generated code for all UObjects modules.  Only is called if UnrealHeaderTool was actually run in this session.
        /// </summary>
        /// <param name="Manifest">List of UObject modules we generated code for.</param>
        public virtual void PostCodeGeneration(SHTManifest Manifest)
        {
        }

        public virtual void PreBuildSync()
        {
        }

        public virtual void PostBuildSync(STBuildTarget Target)
        {
        }

        public virtual ICollection<FileItem> PostBuild(FileItem Executable, LinkEnvironment ExecutableLinkEnvironment)
        {
            return new List<FileItem>();
        }

        public virtual void SetUpGlobalEnvironment()
        {
            ParseProjectSettings();
        }

        public virtual void ParseProjectSettings()
        {
            ConfigCacheIni Ini = new ConfigCacheIni(GetPlatform(), "Engine", STBuildTool.GetUProjectPath());
            bool bValue = STBuildConfiguration.bCompileAPEX;
            if (Ini.GetBool("/Script/BuildSettings.BuildSettings", "bCompileApex", out bValue))
            {
                STBuildConfiguration.bCompileAPEX = bValue;
            }

            bValue = STBuildConfiguration.bCompileBox2D;
            if (Ini.GetBool("/Script/BuildSettings.BuildSettings", "bCompileBox2D", out bValue))
            {
                STBuildConfiguration.bCompileBox2D = bValue;
            }

            bValue = STBuildConfiguration.bCompileICU;
            if (Ini.GetBool("/Script/BuildSettings.BuildSettings", "bCompileICU", out bValue))
            {
                STBuildConfiguration.bCompileICU = bValue;
            }

            bValue = STBuildConfiguration.bCompileSimplygon;
            if (Ini.GetBool("/Script/BuildSettings.BuildSettings", "bCompileSimplygon", out bValue))
            {
                STBuildConfiguration.bCompileSimplygon = bValue;
            }

            bValue = STBuildConfiguration.bCompileLeanAndMeanUE;
            if (Ini.GetBool("/Script/BuildSettings.BuildSettings", "bCompileLeanAndMeanUE", out bValue))
            {
                STBuildConfiguration.bCompileLeanAndMeanUE = bValue;
            }

            bValue = STBuildConfiguration.bIncludeADO;
            if (Ini.GetBool("/Script/BuildSettings.BuildSettings", "bIncludeADO", out bValue))
            {
                STBuildConfiguration.bIncludeADO = bValue;
            }

            bValue = STBuildConfiguration.bCompileRecast;
            if (Ini.GetBool("/Script/BuildSettings.BuildSettings", "bCompileRecast", out bValue))
            {
                STBuildConfiguration.bCompileRecast = bValue;
            }

            bValue = STBuildConfiguration.bCompileSpeedTree;
            if (Ini.GetBool("/Script/BuildSettings.BuildSettings", "bCompileSpeedTree", out bValue))
            {
                STBuildConfiguration.bCompileSpeedTree = bValue;
            }

            bValue = STBuildConfiguration.bCompileWithPluginSupport;
            if (Ini.GetBool("/Script/BuildSettings.BuildSettings", "bCompileWithPluginSupport", out bValue))
            {
                STBuildConfiguration.bCompileWithPluginSupport = bValue;
            }

            bValue = STBuildConfiguration.bCompilePhysXVehicle;
            if (Ini.GetBool("/Script/BuildSettings.BuildSettings", "bCompilePhysXVehicle", out bValue))
            {
                STBuildConfiguration.bCompilePhysXVehicle = bValue;
            }

            bValue = STBuildConfiguration.bCompileFreeType;
            if (Ini.GetBool("/Script/BuildSettings.BuildSettings", "bCompileFreeType", out bValue))
            {
                STBuildConfiguration.bCompileFreeType = bValue;
            }

            bValue = STBuildConfiguration.bCompileForSize;
            if (Ini.GetBool("/Script/BuildSettings.BuildSettings", "bCompileForSize", out bValue))
            {
                STBuildConfiguration.bCompileForSize = bValue;
            }

            bValue = STBuildConfiguration.bCompileCEF3;
            if (Ini.GetBool("/Script/BuildSettings.BuildSettings", "bCompileCEF3", out bValue))
            {
                STBuildConfiguration.bCompileCEF3 = bValue;
            }
        }

        protected void RunUnrealHeaderToolIfNeeded()
        {

        }

        public virtual void AddFilesToManifest(BuildManifest Manifest, STBuildBinary Binary)
        {

        }


        protected void AddPrerequisiteSourceFile(STBuildTarget Target, ISTBuildPlatform BuildPlatform, CPPEnvironment CompileEnvironment, FileItem SourceFile, List<FileItem> PrerequisiteItems)
        {
            PrerequisiteItems.Add(SourceFile);

            var RemoteThis = this as RemoteToolChain;
            bool bAllowUploading = RemoteThis != null && BuildHostPlatform.Current.Platform != STTargetPlatform.Mac;	// Don't use remote features when compiling from a Mac
            if (bAllowUploading)
            {
                RemoteThis.QueueFileForBatchUpload(SourceFile);
            }

            if (!BuildConfiguration.bUseExperimentalFastBuildIteration)	// In fast build iteration mode, we'll gather includes later on
            {
                // @todo fastubt: What if one of the prerequisite files has become missing since it was updated in our cache? (usually, because a coder eliminated the source file)
                //		-> Two CASES:
                //				1) NOT WORKING: Non-unity file went away (SourceFile in this context).  That seems like an existing old use case.  Compile params or Response file should have changed?
                //				2) WORKING: Indirect file went away (unity'd original source file or include).  This would return a file that no longer exists and adds to the prerequiteitems list
                var IncludedFileList = CPPEnvironment.FindAndCacheAllIncludedFiles(Target, SourceFile, BuildPlatform, CompileEnvironment.Config.CPPIncludeInfo, bOnlyCachedDependencies: BuildConfiguration.bUseExperimentalFastDependencyScan);
                foreach (FileItem IncludedFile in IncludedFileList)
                {
                    PrerequisiteItems.Add(IncludedFile);

                    if (bAllowUploading &&
                        !BuildConfiguration.bUseExperimentalFastDependencyScan)	// With fast dependency scanning, we will not have an exhaustive list of dependencies here.  We rely on PostCodeGeneration() to upload these files.
                    {
                        RemoteThis.QueueFileForBatchUpload(IncludedFile);
                    }
                }
            }
        }

        public virtual void SetupBundleDependencies(List<STBuildBinary> Binaries, string GameName)
        {

        }

        public virtual void FixBundleBinariesPaths(STBuildTarget Target, List<STBuildBinary> Binaries)
        {

        }

        public virtual string GetPlatformVersion()
        {
            return "";
        }

        public virtual string GetPlatformDevices()
        {
            return "";
        }

        public virtual STTargetPlatform GetPlatform()
        {
            return STTargetPlatform.Unknown;
        }
    };
}

