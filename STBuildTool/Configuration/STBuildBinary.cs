﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;

namespace STBuildTool
{
    public enum STBuildBinaryType
    {
        Executable,
        DynamicLinkLibrary,
        StaticLibrary,
        Object,
        PrecompiledHeader
    }
    public class STBuildBinaryConfiguration
    {
        public STBuildBinaryType Type;
        public string[] OutputFilePaths;
        public string OutputFilePath
        {
            get
            {
                if (OutputFilePaths.Length != 1)
                {
                    throw new BuildException("Attempted to use UEBuildBinaryConfiguration.OutputFilePath property, but there are multiple (or no) OutputFilePaths. You need to handle multiple in the code that called this (size = {0})", OutputFilePaths.Length);
                }
                return OutputFilePaths[0];
            }
        }
        public string[] OriginalOutputFilePaths;
        public string OriginalOutputFilePath
        {
            get
            {
                if (OutputFilePaths.Length != 1)
                {
                    throw new BuildException("Attempted to use UEBuildBinaryConfiguration.OutputFilePath property, but there are multiple (or no) OutputFilePaths. You need to handle multiple in the code that called this (size = {0})", OutputFilePaths.Length);
                }
                return OutputFilePaths[0];
            }
        }

        /// <summary>
        /// The intermediate directory for this binary. Modules should create separate intermediate directories below this. Must be set before a binary can be built using it.
        /// </summary>
        public string IntermediateDirectory;

        /// <summary>
        /// If true, build exports lib
        /// </summary>
        public bool bAllowExports = false;

        /// <summary>
        /// If true, create a separate import library
        /// </summary>
        public bool bCreateImportLibrarySeparately = false;

        /// <summary>
        /// If true, include dependent libraries in the static library being built
        /// </summary>
        public bool bIncludeDependentLibrariesInLibrary = false;

        /// <summary>
        /// If false, this binary will not be compiled and it is only used to set up link environments
        /// </summary>
        public bool bAllowCompilation = true;

        /// <summary>
        /// True if this binary has any Build.cs files, if not this is probably a binary-only plugins
        /// </summary>
        public bool bHasModuleRules = true;

        /// <summary>
        /// For most binaries, this is false. If this is a cross-platform binary build for a specific platform (for example XB1 DLL for a windows editor) this will be true.
        /// </summary>
        public bool bIsCrossTarget = false;

        /// <summary>
        /// If true, the binary is being compiled as a monolithic build
        /// </summary>
        public bool bCompileMonolithic = false;

        /// <summary>
        /// The build target configuration being compiled
        /// </summary>
        public STTargetConfiguration TargetConfiguration = STTargetConfiguration.Development;

        /// <summary>
        /// The name of the target being compiled
        /// </summary>
        public string TargetName = "";

        /// <summary>
        /// The projectfile path
        /// </summary>
        public string ProjectFilePath = "";

        /// <summary>
        /// List of modules to link together into this executable
        /// </summary>
        public List<string> ModuleNames = new List<string>();

        /// <summary>
        /// The configuration class for a binary build.
        /// </summary>
        /// <param name="InType"></param>
        /// <param name="InOutputFilePath"></param>
        /// <param name="bInAllowExports"></param>
        /// <param name="bInCreateImportLibrarySeparately"></param>
        /// <param name="bInIsCrossTarget">For most binaries, this is false. If this is a cross-platform binary build for a specific platform (for example XB1 DLL for a windows editor) this will be true.</param>
        /// <param name="InProjectFilePath"></param>
        /// <param name="InModuleNames"></param>
        public STBuildBinaryConfiguration(
                STBuildBinaryType InType,
                string[] InOutputFilePaths = null,
                string InIntermediateDirectory = null,
                bool bInAllowExports = false,
                bool bInCreateImportLibrarySeparately = false,
                bool bInIncludeDependentLibrariesInLibrary = false,
                bool bInAllowCompilation = true,
                bool bInHasModuleRules = true,
                bool bInIsCrossTarget = false,
                bool bInCompileMonolithic = false,
                STTargetConfiguration InTargetConfiguration = STTargetConfiguration.Development,
                string InTargetName = "",
                string InProjectFilePath = "",
                List<string> InModuleNames = null
            )
        {
            Type = InType;
            OutputFilePaths = InOutputFilePaths != null ? (string[])InOutputFilePaths.Clone() : null;
            IntermediateDirectory = InIntermediateDirectory;
            bAllowExports = bInAllowExports;
            bCreateImportLibrarySeparately = bInCreateImportLibrarySeparately;
            bIncludeDependentLibrariesInLibrary = bInIncludeDependentLibrariesInLibrary;
            bAllowCompilation = bInAllowCompilation;
            bHasModuleRules = bInHasModuleRules;
            bIsCrossTarget = bInIsCrossTarget;
            bCompileMonolithic = bInCompileMonolithic;
            TargetConfiguration = InTargetConfiguration;
            TargetName = InTargetName;
            ProjectFilePath = InProjectFilePath;
            ModuleNames = InModuleNames;
        }
    }

    public abstract class STBuildBinary
    {
        public STBuildTarget Target;
        public STBuildBinaryConfiguration Config = null;

        /// <summary>
        /// Create an instance of the class with the given configuration data
        /// </summary>
        /// <param name="InConfig">The build binary configuration to initialize the class with</param>
        public STBuildBinary(STBuildTarget InTarget, STBuildBinaryConfiguration InConfig)
        {
            Debug.Assert(InConfig.OutputFilePath != null && InConfig.IntermediateDirectory != null);
            Target = InTarget;
            Config = InConfig;
        }
        public virtual void BindModules() { }

        /// <summary>
        /// Builds the binary.
        /// </summary>
        /// <param name="ToolChain">The toolchain which to use for building</param>
        /// <param name="CompileEnvironment">The environment to compile the binary in</param>
        /// <param name="LinkEnvironment">The environment to link the binary in</param>
        /// <returns></returns>
        public abstract IEnumerable<FileItem> Build(ISTToolChain ToolChain, CPPEnvironment CompileEnvironment, LinkEnvironment LinkEnvironment);

        /// <summary>
        /// Called to allow the binary to modify the link environment of a different binary containing 
        /// a module that depends on a module in this binary. */
        /// </summary>
        /// <param name="DependentLinkEnvironment">The link environment of the dependency</param>
        public virtual void SetupDependentLinkEnvironment(ref LinkEnvironment DependentLinkEnvironment) { }

        /// <summary>
        /// Called to allow the binary to to determine if it matches the Only module "short module name".
        /// </summary>
        /// <param name="OnlyModules"></param>
        /// <returns>The OnlyModule if found, null if not</returns>
        public virtual OnlyModule FindOnlyModule(List<OnlyModule> OnlyModules)
        {
            return null;
        }

        /// <summary>
        /// Called to allow the binary to find game modules.
        /// </summary>
        /// <param name="OnlyModules"></param>
        /// <returns>The OnlyModule if found, null if not</returns>
        public virtual List<STBuildModule> FindGameModules()
        {
            return null;
        }

        /// <summary>
        /// Generates a list of all modules referenced by this binary
        /// </summary>
        /// <param name="bIncludeDynamicallyLoaded">True if dynamically loaded modules (and all of their dependent modules) should be included.</param>
        /// <param name="bForceCircular">True if circular dependencies should be process</param>
        /// <returns>List of all referenced modules</returns>
        public virtual List<STBuildModule> GetAllDependencyModules(bool bIncludeDynamicallyLoaded, bool bForceCircular)
        {
            return new List<STBuildModule>();
        }

        /// <summary>
        /// Process all modules that aren't yet bound, creating binaries for modules that don't yet have one (if needed),
        /// and updating modules for circular dependencies.
        /// </summary>
        /// <param name="ExecutableBinary">The executable binary, which links against all unbound modules when building monolithically</param>
        /// <returns>List of newly-created binaries (may be empty)</returns>
        public virtual List<STBuildBinary> ProcessUnboundModules(STBuildBinary ExecutableBinary)
        {
            return null;
        }

        /// <summary>
        /// Sets whether to create a separate import library to resolve circular dependencies for this binary
        /// </summary>
        /// <param name="bInCreateImportLibrarySeparately">True to create a separate import library</param>
        public virtual void SetCreateImportLibrarySeparately(bool bInCreateImportLibrarySeparately)
        {
        }
        public virtual void SetIncludeDependentLibrariesInLibrary(bool bInIncludeDependentLibrariesInLibrary)
        {
        }
        public virtual void AddModule(string ModuleName)
        {
        }
        public static string GetAdditionalConsoleAppPath(string BinaryPath)
        {
            return Path.Combine(Path.GetDirectoryName(BinaryPath), Path.GetFileNameWithoutExtension(BinaryPath) + "-Cmd" + Path.GetExtension(BinaryPath));
        }
    };
    public class STBuildBinaryCPP : STBuildBinary
    {
        public HashSet<string> ModuleNames
        {
            get;
            private set;
        }
        private bool bCreateImportLibrarySeparately;
        private bool bIncludeDependentLibrariesInLibrary;
        public STBuildBinaryCPP(STBuildTarget InTarget, STBuildBinaryConfiguration InConfig)
            : base(InTarget, InConfig)
        {
            ModuleNames = new HashSet<string>(InConfig.ModuleNames);
            bCreateImportLibrarySeparately = InConfig.bCreateImportLibrarySeparately;
            bIncludeDependentLibrariesInLibrary = InConfig.bIncludeDependentLibrariesInLibrary;
        }

        /// <summary>
        /// Adds a module to the binary.
        /// </summary>
        /// <param name="ModuleName">The module to add</param>
        public override void AddModule(string ModuleName)
        {
            if (!ModuleNames.Contains(ModuleName))
            {
                ModuleNames.Add(ModuleName);
            }
        }

        // UEBuildBinary interface.

        /// <summary>
        /// Called to resolve module names and uniquely bind modules to a binary.
        /// </summary>
        /// <param name="BuildTarget">The build target the modules are being bound for</param>
        /// <param name="Target">The target info</param>
        public override void BindModules()
        {
            foreach (var ModuleName in ModuleNames)
            {
                STBuildModule Module = null;
                if (Config.bHasModuleRules)
                {
                    Module = Target.FindOrCreateModuleByName(ModuleName);
                    if (Module.Binary != null)
                    {
                        throw new BuildException("Module \"{0}\" linked into both {1} and {2}, which creates ambiguous linkage for dependents.", ModuleName, Module.Binary.Config.OutputFilePath, Config.OutputFilePath);
                    }
                    Module.Binary = this;
                    Module.bIncludedInTarget = true;
                }

                // We set whether the binary is being compiled monolithic here to know later - specifically
                // when we are determining whether to use SharedPCHs or not for static lib builds of plugins.
                Config.bCompileMonolithic = Target.ShouldCompileMonolithic();

                // We also need to know what the actual build target configuration is later in the process
                // where we do not have access to the Target itself... This is for generating the paths
                // to the plugins.
                Config.TargetConfiguration = Target.Configuration;
                Config.TargetName = Target.GetAppName();

                if (Module != null && (Target.Rules == null || Target.Rules.bOutputToEngineBinaries == false))
                {
                    // Fix up the binary path if this is module specifies an alternate output directory
                    for (int Index = 0; Index < Config.OutputFilePaths.Length; Index++)
                    {
                        Config.OutputFilePaths[Index] = Module.FixupOutputPath(Config.OutputFilePaths[Index]);
                    }
                }
            }
        }

        /// <summary>
        /// Generates a list of all modules referenced by this binary
        /// </summary>
        /// <param name="bIncludeDynamicallyLoaded">True if dynamically loaded modules (and all of their dependent modules) should be included.</param>
        /// <param name="bForceCircular">True if circular dependencies should be process</param>
        /// <returns>List of all referenced modules</returns>
        public override List<STBuildModule> GetAllDependencyModules(bool bIncludeDynamicallyLoaded, bool bForceCircular)
        {
            var OrderedModules = new List<STBuildModule>();
            var ReferencedModules = new Dictionary<string, STBuildModule>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var ModuleName in ModuleNames)
            {
                if (!ReferencedModules.ContainsKey(ModuleName))
                {
                    var Module = Target.GetModuleByName(ModuleName);
                    ReferencedModules[ModuleName] = Module;

                    bool bOnlyDirectDependencies = false;
                    Module.GetAllDependencyModules(ref ReferencedModules, ref OrderedModules, bIncludeDynamicallyLoaded, bForceCircular, bOnlyDirectDependencies);

                    OrderedModules.Add(Module);
                }
            }

            return OrderedModules;
        }

        /// <summary>
        /// Process all modules that aren't yet bound, creating binaries for modules that don't yet have one (if needed),
        /// and updating modules for circular dependencies.
        /// </summary>
        /// <param name="ExecutableBinary">The executable binary, which links against all unbound modules when building monolithically</param>
        /// <returns>List of newly-created binaries (may be empty)</returns>
        public override List<STBuildBinary> ProcessUnboundModules(STBuildBinary ExecutableBinary)
        {
            var Binaries = new Dictionary<string, STBuildBinary>(StringComparer.InvariantCultureIgnoreCase);
            if (Config.bHasModuleRules)
            {
                foreach (var ModuleName in ModuleNames)
                {
                    var Module = Target.FindOrCreateModuleByName(ModuleName);
                    Module.RecursivelyProcessUnboundModules(Target, ref Binaries, ExecutableBinary);
                }
            }
            else
            {
                // There's only one module in this case, so just bind it to this binary
                foreach (var ModuleName in ModuleNames)
                {
                    Binaries.Add(ModuleName, this);
                }
            }

            // Now build a final list of newly-created binaries that were bound to.  The hash may contain duplicates, so
            // we filter those out here.
            var BinaryList = new List<STBuildBinary>();
            foreach (var CurBinary in Binaries.Values)
            {
                // Never include ourselves in the new binary list (monolithic case)
                if (CurBinary != this)
                {
                    if (!BinaryList.Contains(CurBinary))
                    {
                        BinaryList.Add(CurBinary);
                    }
                }
            }
            return BinaryList;
        }

        /// <summary>
        /// Sets whether to create a separate import library to resolve circular dependencies for this binary
        /// </summary>
        /// <param name="bInCreateImportLibrarySeparately">True to create a separate import library</param>
        public override void SetCreateImportLibrarySeparately(bool bInCreateImportLibrarySeparately)
        {
            bCreateImportLibrarySeparately = bInCreateImportLibrarySeparately;
        }

        /// <summary>
        /// Sets whether to include dependent libraries when building a static library
        /// </summary>
        /// <param name="bInIncludeDependentLibrariesInLibrary"></param>
        public override void SetIncludeDependentLibrariesInLibrary(bool bInIncludeDependentLibrariesInLibrary)
        {
            bIncludeDependentLibrariesInLibrary = bInIncludeDependentLibrariesInLibrary;
        }

        /// <summary>
        /// Builds the binary.
        /// </summary>
        /// <param name="CompileEnvironment">The environment to compile the binary in</param>
        /// <param name="LinkEnvironment">The environment to link the binary in</param>
        /// <returns></returns>
        public override IEnumerable<FileItem> Build(ISTToolChain TargetToolChain, CPPEnvironment CompileEnvironment, LinkEnvironment LinkEnvironment)
        {

            // Determine the type of binary we're linking.
            switch (Config.Type)
            {
                case STBuildBinaryType.DynamicLinkLibrary:
                    CompileEnvironment.Config.bIsBuildingDLL = true;
                    CompileEnvironment.Config.bIsBuildingLibrary = false;
                    break;
                case STBuildBinaryType.StaticLibrary:
                    CompileEnvironment.Config.bIsBuildingDLL = false;
                    CompileEnvironment.Config.bIsBuildingLibrary = true;
                    break;
                default:
                    CompileEnvironment.Config.bIsBuildingDLL = false;
                    CompileEnvironment.Config.bIsBuildingLibrary = false;
                    break;
            };

            var OutputFiles = new List<FileItem>();

            var BinaryCompileEnvironment = CompileEnvironment.DeepCopy();
            var BinaryLinkEnvironment = LinkEnvironment.DeepCopy();

            // Process each module that is linked into the binary.
            var BinaryDependencies = new List<STBuildBinary>();
            var LinkEnvironmentVisitedModules = new Dictionary<STBuildModule, bool>();

            // @Hack: This to prevent UHT from listing CoreUObject.generated.cpp as its dependency.
            // We flag the compile environment when we build UHT so that we don't need to check
            // this for each file when generating their dependencies.
            BinaryCompileEnvironment.bHackHeaderGenerator = (Target.GetAppName() == "UnrealHeaderTool");

            // @todo: This should be in some Windows code somewhere...
            // Set the original file name macro; used in PCLaunch.rc to set the binary metadata fields.
            var OriginalFilename = (Config.OriginalOutputFilePaths != null) ?
                Path.GetFileName(Config.OriginalOutputFilePaths[0]) :
                Path.GetFileName(Config.OutputFilePaths[0]);
            BinaryCompileEnvironment.Config.Definitions.Add("ORIGINAL_FILE_NAME=\"" + OriginalFilename + "\"");

            foreach (var ModuleName in ModuleNames)
            {
                var Module = Target.GetModuleByName(ModuleName);

                // Compile each module.
                Log.TraceVerbose("Compile module: " + ModuleName);

                var LinkInputFiles = Module.Compile(CompileEnvironment, BinaryCompileEnvironment, Config.bCompileMonolithic);

                // NOTE: Because of 'Shared PCHs', in monolithic builds the same PCH file may appear as a link input
                // multiple times for a single binary.  We'll check for that here, and only add it once.  This avoids
                // a linker warning about redundant .obj files. 
                foreach (var LinkInputFile in LinkInputFiles)
                {
                    if (!BinaryLinkEnvironment.InputFiles.Contains(LinkInputFile))
                    {
                        BinaryLinkEnvironment.InputFiles.Add(LinkInputFile);
                    }
                }

                // Allow the module to modify the link environment for the binary.
                Module.SetupPrivateLinkEnvironment(ref BinaryLinkEnvironment, ref BinaryDependencies, ref LinkEnvironmentVisitedModules);
            }

            // Remove the default resource file on Windows (PCLaunch.rc) if the user has specified their own
            if (BinaryLinkEnvironment.InputFiles.Select(Item => Path.GetFileName(Item.AbsolutePath).ToLower()).Any(Name => Name.EndsWith(".res") && !Name.EndsWith(".inl.res") && Name != "pclaunch.rc.res"))
            {
                BinaryLinkEnvironment.InputFiles.RemoveAll(x => Path.GetFileName(x.AbsolutePath).ToLower() == "pclaunch.rc.res");
            }

            // Allow the binary dependencies to modify the link environment.
            foreach (var BinaryDependency in BinaryDependencies)
            {
                BinaryDependency.SetupDependentLinkEnvironment(ref BinaryLinkEnvironment);
            }

            // Set the link output file.
            BinaryLinkEnvironment.Config.OutputFilePaths = Config.OutputFilePaths != null ? (string[])Config.OutputFilePaths.Clone() : null;

            // Set whether the link is allowed to have exports.
            BinaryLinkEnvironment.Config.bHasExports = Config.bAllowExports;

            // Set the output folder for intermediate files
            BinaryLinkEnvironment.Config.IntermediateDirectory = Config.IntermediateDirectory;

            // Put the non-executable output files (PDB, import library, etc) in the same directory as the production
            BinaryLinkEnvironment.Config.OutputDirectory = Path.GetDirectoryName(Config.OutputFilePaths[0]);

            // Determine the type of binary we're linking.
            switch (Config.Type)
            {
                case STBuildBinaryType.DynamicLinkLibrary:
                    BinaryLinkEnvironment.Config.bIsBuildingDLL = true;
                    BinaryLinkEnvironment.Config.bIsBuildingLibrary = false;
                    break;
                case STBuildBinaryType.StaticLibrary:
                    BinaryLinkEnvironment.Config.bIsBuildingDLL = false;
                    BinaryLinkEnvironment.Config.bIsBuildingLibrary = true;
                    break;
                default:
                    BinaryLinkEnvironment.Config.bIsBuildingDLL = false;
                    BinaryLinkEnvironment.Config.bIsBuildingLibrary = false;
                    break;
            };

            if (ProjectFileGenerator.bGenerateProjectFiles)
            {
                // We're generating projects.  Since we only need include paths and definitions, there is no need
                // to go ahead and run through the linking logic.
                OutputFiles = BinaryLinkEnvironment.InputFiles;
            }
            else if (BuildConfiguration.bEnableCodeAnalysis)
            {
                // We're only analyzing code, so we won't actually link any executables.  Instead, our output
                // files will simply be the .obj files that were compiled during static analysis.
                OutputFiles = BinaryLinkEnvironment.InputFiles;
            }
            else
            {
                if (bCreateImportLibrarySeparately)
                {
                    // Mark the link environment as cross-referenced.
                    BinaryLinkEnvironment.Config.bIsCrossReferenced = true;

                    if (BinaryLinkEnvironment.Config.Target.Platform != CPPTargetPlatform.Mac && BinaryLinkEnvironment.Config.Target.Platform != CPPTargetPlatform.Linux)
                    {
                        // Create the import library.
                        OutputFiles.AddRange(BinaryLinkEnvironment.LinkExecutable(true));
                    }
                }

                BinaryLinkEnvironment.Config.bIncludeDependentLibrariesInLibrary = bIncludeDependentLibrariesInLibrary;

                // Link the binary.
                FileItem[] Executables = BinaryLinkEnvironment.LinkExecutable(false);
                OutputFiles.AddRange(Executables);

                // Produce additional console app if requested
                if (BinaryLinkEnvironment.Config.CanProduceAdditionalConsoleApp && STBuildConfiguration.bBuildEditor)
                {
                    // Produce additional binary but link it as a console app
                    var ConsoleAppLinkEvironment = BinaryLinkEnvironment.DeepCopy();
                    ConsoleAppLinkEvironment.Config.bIsBuildingConsoleApplication = true;
                    ConsoleAppLinkEvironment.Config.WindowsEntryPointOverride = "WinMainCRTStartup";		// For WinMain() instead of "main()" for Launch module
                    for (int Index = 0; Index < Config.OutputFilePaths.Length; Index++)
                    {
                        ConsoleAppLinkEvironment.Config.OutputFilePaths[Index] = GetAdditionalConsoleAppPath(ConsoleAppLinkEvironment.Config.OutputFilePaths[Index]);
                    }

                    // Link the console app executable
                    OutputFiles.AddRange(ConsoleAppLinkEvironment.LinkExecutable(false));
                }

                foreach (var Executable in Executables)
                {
                    OutputFiles.AddRange(TargetToolChain.PostBuild(Executable, BinaryLinkEnvironment));
                }
            }

            return OutputFiles;
        }

        /// <summary>
        /// Called to allow the binary to modify the link environment of a different binary containing 
        /// a module that depends on a module in this binary.
        /// </summary>
        /// <param name="DependentLinkEnvironment">The link environment of the dependency</param>
        public override void SetupDependentLinkEnvironment(ref LinkEnvironment DependentLinkEnvironment)
        {
            foreach (string OutputFilePath in Config.OutputFilePaths)
            {
                string LibraryFileName;
                if (Config.Type == STBuildBinaryType.StaticLibrary
                    || DependentLinkEnvironment.Config.Target.Platform == CPPTargetPlatform.Mac
                    || DependentLinkEnvironment.Config.Target.Platform == CPPTargetPlatform.Linux)
                {
                    LibraryFileName = OutputFilePath;
                }
                else
                {
                    LibraryFileName = Path.Combine(Config.IntermediateDirectory, Path.GetFileNameWithoutExtension(OutputFilePath) + ".lib");
                }
                DependentLinkEnvironment.Config.AdditionalLibraries.Add(LibraryFileName);
            }
        }

        /// <summary>
        /// Called to allow the binary to to determine if it matches the Only module "short module name".
        /// </summary>
        /// <param name="OnlyModules"></param>
        /// <returns>The OnlyModule if found, null if not</returns>
        public override OnlyModule FindOnlyModule(List<OnlyModule> OnlyModules)
        {
            foreach (var ModuleName in ModuleNames)
            {
                foreach (var OnlyModule in OnlyModules)
                {
                    if (OnlyModule.OnlyModuleName.ToLower() == ModuleName.ToLower())
                    {
                        return OnlyModule;
                    }
                }
            }
            return null;
        }

        public override List<STBuildModule> FindGameModules()
        {
            var GameModules = new List<STBuildModule>();
            foreach (var ModuleName in ModuleNames)
            {
                STBuildModule Module = Target.GetModuleByName(ModuleName);
                if (!Utils.IsFileUnderDirectory(Module.ModuleDirectory, BuildConfiguration.RelativeEnginePath))
                {
                    GameModules.Add(Module);
                }
            }
            return GameModules;
        }

        // Object interface.

        /// <summary>
        /// ToString implementation
        /// </summary>
        /// <returns>Returns the OutputFilePath for this binary</returns>
        public override string ToString()
        {
            return Config.OutputFilePath;
        }
    };

    /// <summary>
    /// A DLL built by MSBuild from a C# project.
    /// </summary>
    public class STBuildBinaryCSDLL : STBuildBinary
    {
        /// <summary>
        /// Create an instance initialized to the given configuration
        /// </summary>
        /// <param name="InConfig">The build binary configuration to initialize the instance to</param>
        public STBuildBinaryCSDLL(STBuildTarget InTarget, STBuildBinaryConfiguration InConfig)
            : base(InTarget, InConfig)
        {
        }

        /// <summary>
        /// Builds the binary.
        /// </summary>
        /// <param name="ToolChain">The toolchain to use for building</param>
        /// <param name="CompileEnvironment">The environment to compile the binary in</param>
        /// <param name="LinkEnvironment">The environment to link the binary in</param>
        /// <returns></returns>
        public override IEnumerable<FileItem> Build(ISTToolChain ToolChain, CPPEnvironment CompileEnvironment, LinkEnvironment LinkEnvironment)
        {
            var ProjectCSharpEnviroment = new CSharpEnvironment();
            if (LinkEnvironment.Config.Target.Configuration == CPPTargetConfiguration.Debug)
            {
                ProjectCSharpEnviroment.TargetConfiguration = CSharpTargetConfiguration.Debug;
            }
            else
            {
                ProjectCSharpEnviroment.TargetConfiguration = CSharpTargetConfiguration.Development;
            }
            ProjectCSharpEnviroment.EnvironmentTargetPlatform = LinkEnvironment.Config.Target.Platform;

            // Currently only supported by windows...
            STToolChain.GetPlatformToolChain(CPPTargetPlatform.Win64).CompileCSharpProject(
                ProjectCSharpEnviroment, Config.ProjectFilePath, Config.OutputFilePath);

            return new FileItem[] { FileItem.GetItemByPath(Config.OutputFilePath) };
        }
    };
}
