﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace STBuildTool
{
    /// <summary>
    /// Information about a target, passed along when creating a module descriptor
    /// </summary>
    public class TargetInfo
    {
        /// Target platform
        public readonly STTargetPlatform Platform;

        /// Target architecture (or empty string if not needed)
        public readonly string Architecture;

        /// Target build configuration
        public readonly STTargetConfiguration Configuration;

        /// Target type (if known)
        public readonly TargetRules.TargetType? Type;

        /// <summary>
        /// Constructs a TargetInfo
        /// </summary>
        /// <param name="InitPlatform">Target platform</param>
        /// <param name="InitConfiguration">Target build configuration</param>
        public TargetInfo(STTargetPlatform InitPlatform, STTargetConfiguration InitConfiguration, TargetRules.TargetType? InitType = null)
        {
            Platform = InitPlatform;
            Configuration = InitConfiguration;
            Type = InitType;

            // get the platform's architecture
            //var BuildPlatform = STBuildPlatform.GetBuildPlatform(Platform);
            //Architecture = BuildPlatform.GetActiveArchitecture();
        }

        /// <summary>
        /// True if the target type is a cooked game.
        /// </summary>
        public bool IsCooked
        {
            get
            {
                if (!Type.HasValue)
                {
                    throw new BuildException("Trying to access TargetInfo.IsCooked when TargetInfo.Type is not set. Make sure IsCooked is used only in ModuleRules.");
                }
                return Type == TargetRules.TargetType.Client ||
                     Type == TargetRules.TargetType.Game ||
                     Type == TargetRules.TargetType.Server;
            }
        }

        /// <summary>
        /// True if the target type is a monolithic binary
        /// </summary>
        public bool IsMonolithic
        {
            get
            {
                if (!Type.HasValue)
                {
                    throw new BuildException("Trying to access TargetInfo.IsMonolithic when TargetInfo.Type is not set. Make sure IsMonolithic is used only in ModuleRules.");
                }
                return Type == TargetRules.TargetType.Client ||
                     Type == TargetRules.TargetType.Game ||
                     Type == TargetRules.TargetType.Server;
            }
        }
    }


    /// <summary>
    /// ModuleRules is a data structure that contains the rules for defining a module
    /// </summary>
    public abstract class ModuleRules
    {
        /// Type of module
        public enum ModuleType
        {
            /// C++
            CPlusPlus,

            /// CLR module (mixed C++ and C++/CLI)
            CPlusPlusCLR,

            /// External (third-party)
            External,
        }

        /// Code optimization settings
        public enum CodeOptimization
        {
            /// Code should never be optimized if possible.
            Never,

            /// Code should only be optimized in non-debug builds (not in Debug).
            InNonDebugBuilds,

            /// Code should always be optimized if possible.
            Always,

            /// Default: 'InNonDebugBuilds' for game modules, 'Always' otherwise.
            Default,
        }

        /// Type of module
        public ModuleType Type = ModuleType.CPlusPlus;

        /// Subfolder of Binaries/PLATFORM folder to put this module in when building DLLs
        /// This should only be used by modules that are found via searching like the
        /// TargetPlatform or ShaderFormat modules. 
        /// If FindModules is not used to track them down, the modules will not be found.
        public string BinariesSubFolder = "";

        /// When this module's code should be optimized.
        public CodeOptimization OptimizeCode = CodeOptimization.Default;

        /// Header file name for a shared PCH provided by this module.  Must be a valid relative path to a public C++ header file.
        /// This should only be set for header files that are included by a significant number of other C++ modules.
        public string SharedPCHHeaderFile = String.Empty;

        public enum PCHUsageMode
        {
            /// Default: Engine modules use shared PCHs, game modules do not
            Default,

            /// Never use shared PCHs.  Always generate a unique PCH for this module if appropriate
            NoSharedPCHs,

            /// Shared PCHs are OK!
            UseSharedPCHs
        }

        /// Precompiled header usage for this module
        public PCHUsageMode PCHUsage = PCHUsageMode.Default;

        /** Use run time type information */
        public bool bUseRTTI = false;

        /** Enable buffer security checks.  This should usually be enabled as it prevents severe security risks. */
        public bool bEnableBufferSecurityChecks = true;

        /** Enable exception handling */
        public bool bEnableExceptions = false;

        /** If true and unity builds are enabled, this module will build without unity. */
        public bool bFasterWithoutUnity = false;

        /** If true then the engine will call it's StartupModule at engine initialization automatically. */
        public bool bIsAutoStartupModule = false;

        /** Overrides BuildConfiguration.MinFilesUsingPrecompiledHeader if non-zero. */
        public int MinFilesUsingPrecompiledHeaderOverride = 0;

        /// List of modules with header files that our module's public headers needs access to, but we don't need to "import" or link against.
        public List<string> PublicIncludePathModuleNames = new List<string>();

        /// List of public dependency module names.  These are modules that are required by our public source files.
        public List<string> PublicDependencyModuleNames = new List<string>();

        /// List of modules with header files that our module's private code files needs access to, but we don't need to "import" or link against.
        public List<string> PrivateIncludePathModuleNames = new List<string>();

        /// List of private dependency module names.  These are modules that our private code depends on but nothing in our public
        /// include files depend on.
        public List<string> PrivateDependencyModuleNames = new List<string>();

        /// List of module dependencies that should be treated as circular references.  This modules must have already been added to
        /// either the public or private dependent module list.
        public List<string> CircularlyReferencedDependentModules = new List<string>();

        /// System include paths.  These are public stable header file directories that are not checked when resolving header dependencies.
        public List<string> PublicSystemIncludePaths = new List<string>();

        /// List of all paths to include files that are exposed to other modules
        public List<string> PublicIncludePaths = new List<string>();

        /// List of all paths to this module's internal include files, not exposed to other modules
        public List<string> PrivateIncludePaths = new List<string>();

        /// List of library paths - typically used for External (third party) modules
        public List<string> PublicLibraryPaths = new List<string>();

        /// List of addition libraries - typically used for External (third party) modules
        public List<string> PublicAdditionalLibraries = new List<string>();

        // List of frameworks
        public List<string> PublicFrameworks = new List<string>();

        // List of weak frameworks (for OS version transitions)
        public List<string> PublicWeakFrameworks = new List<string>();

        /// List of addition frameworks - typically used for External (third party) modules on Mac and iOS
        //public List<UEBuildFramework> PublicAdditionalFrameworks = new List<UEBuildFramework>();

        /// List of addition resources that should be copied to the app bundle for Mac or iOS
        //public List<UEBuildBundleResource> AdditionalBundleResources = new List<UEBuildBundleResource>();

        /// For builds that execute on a remote machine (e.g. iOS), this list contains additional files that
        /// need to be copied over in order for the app to link successfully.  Source/header files and PCHs are
        /// automatically copied.  Usually this is simply a list of precompiled third party library dependencies.
        public List<string> PublicAdditionalShadowFiles = new List<string>();

        /// List of delay load DLLs - typically used for External (third party) modules
        public List<string> PublicDelayLoadDLLs = new List<string>();

        /// Additional compiler definitions for this module
        public List<string> Definitions = new List<string>();

        /** CLR modules only: The assemblies referenced by the module's private implementation. */
        public List<string> PrivateAssemblyReferences = new List<string>();

        /// Addition modules this module may require at run-time 
        public List<string> DynamicallyLoadedModuleNames = new List<string>();

        /// Extra modules this module may require at run time, that are on behalf of another platform (i.e. shader formats and the like)
        public List<string> PlatformSpecificDynamicallyLoadedModuleNames = new List<string>();

        /// <summary>
        /// Add the given ThirdParty modules as static private dependencies
        ///	Statically linked to this module, meaning they utilize exports from the other module
        ///	Private, meaning the include paths for the included modules will not be exposed when giving this modules include paths
        ///	NOTE: There is no AddThirdPartyPublicStaticDependencies function.
        /// </summary>
        /// <param name="ModuleNames">The names of the modules to add</param>
        public void AddThirdPartyPrivateStaticDependencies(TargetInfo Target, params string[] InModuleNames)
        {
            /*
            if (UnrealBuildTool.RunningRocket() == false || Target.Type == TargetRules.TargetType.Game)
            {
                PrivateDependencyModuleNames.AddRange(InModuleNames);
            }
             * */
        }

        /// <summary>
        /// Add the given ThirdParty modules as dynamic private dependencies
        ///	Dynamically linked to this module, meaning they do not utilize exports from the other module
        ///	Private, meaning the include paths for the included modules will not be exposed when giving this modules include paths
        ///	NOTE: There is no AddThirdPartyPublicDynamicDependencies function.
        /// </summary>
        /// <param name="ModuleNames">The names of the modules to add</param>
        public void AddThirdPartyPrivateDynamicDependencies(TargetInfo Target, params string[] InModuleNames)
        {
            /*
            if (UnrealBuildTool.RunningRocket() == false || Target.Type == TargetRules.TargetType.Game)
            {
                PrivateIncludePathModuleNames.AddRange(InModuleNames);
                DynamicallyLoadedModuleNames.AddRange(InModuleNames);
            }
             * */
        }

        /// <summary>
        /// Setup this module for PhysX/APEX support (based on the settings in UEBuildConfiguration)
        /// </summary>
        public void SetupModulePhysXAPEXSupport(TargetInfo Target)
        {
            if (STBuildConfiguration.bCompilePhysX == true)
            {
                AddThirdPartyPrivateStaticDependencies(Target, "PhysX");
                Definitions.Add("WITH_PHYSX=1");
                if (UEBuildConfiguration.bCompileAPEX == true)
                {
                    AddThirdPartyPrivateStaticDependencies(Target, "APEX");
                    Definitions.Add("WITH_APEX=1");
                }
                else
                {
                    Definitions.Add("WITH_APEX=0");
                }
            }
            else
            {
                Definitions.Add("WITH_PHYSX=0");
                Definitions.Add("WITH_APEX=0");
            }

            if (UEBuildConfiguration.bRuntimePhysicsCooking == true)
            {
                Definitions.Add("WITH_RUNTIME_PHYSICS_COOKING");
            }
        }

        /// <summary>
        /// Setup this module for Box2D support (based on the settings in UEBuildConfiguration)
        /// </summary>
        public void SetupModuleBox2DSupport(TargetInfo Target)
        {
            //@TODO: This need to be kept in sync with RulesCompiler.cs for now
            bool bSupported = false;
            if ((Target.Platform == STTargetPlatform.Win64) || (Target.Platform == STTargetPlatform.Win32))
            {
                bSupported = true;
            }

            bSupported = bSupported && UEBuildConfiguration.bCompileBox2D;

            if (bSupported)
            {
                AddThirdPartyPrivateStaticDependencies(Target, "Box2D");
            }

            // Box2D included define (required because pointer types may be in public exported structures)
            Definitions.Add(string.Format("WITH_BOX2D={0}", bSupported ? 1 : 0));
        }

        /** Redistribution override flag for this module. */
        public bool? IsRedistributableOverride { get; set; }
    }

    /// <summary>
    /// TargetRules is a data structure that contains the rules for defining a target (application/executable)
    /// </summary>
    public abstract class TargetRules
    {
        /// Type of target
        [Serializable]
        public enum TargetType
        {
            /// Cooked monolithic game executable (GameName.exe).  Also used for a game-agnostic engine executable (UE4Game.exe or RocketGame.exe)
            Game,

            /// Uncooked modular editor executable and DLLs (UE4Editor.exe, UE4Editor*.dll, GameName*.dll)
            Editor,

            /// Cooked monolithic game client executable (GameNameClient.exe, but no server code)
            Client,

            /// Cooked monolithic game server executable (GameNameServer.exe, but no client code)
            Server,

            /// Program (standalone program, e.g. ShaderCompileWorker.exe, can be modular or monolithic depending on the program)
            Program,
        }

        /// <summary>
        /// The name of the game, this is set up by the rules compiler after it compiles and constructs this
        /// </summary>
        public string TargetName = null;

        /// <summary>
        /// Whether the target uses Steam (todo: substitute with more generic functionality)
        /// </summary>
        public bool bUsesSteam;

        /// <summary>
        /// Whether the project uses visual Slate UI (as opposed to the low level windowing/messaging which is always used)
        /// </summary>
        public bool bUsesSlate = true;

        /// <summary>
        /// Hack for legacy game styling isses.  No new project should ever set this to true
        /// Whether the project uses the Slate editor style in game.  
        /// </summary>
        public bool bUsesSlateEditorStyle = false;

        /// <summary>
        /// Forces linking against the static CRT. This is not supported across the engine due to the need for allocator implementations to be shared (for example), and TPS 
        /// libraries to be consistent with each other, but can be used for utility programs.
        /// </summary>
        public bool bUseStaticCRT = false;

        /// <summary>
        /// By default we use the Release C++ Runtime (CRT), even when compiling Debug builds.  This is because the Debug C++
        /// Runtime isn't very useful when debugging Unreal Engine projects, and linking against the Debug CRT libraries forces
        /// our third party library dependencies to also be compiled using the Debug CRT (and often perform more slowly.)  Often
        /// it can be inconvenient to require a separate copy of the debug versions of third party static libraries simply
        /// so that you can debug your program's code.
        /// </summary>
        public bool bDebugBuildsActuallyUseDebugCRT = false;

        /// <summary>
        /// A list of additional plugins which need to be built for this target. Game and editor targets can use the EnabledPlugins 
        /// setting in their config files to control this.
        /// </summary>
        public List<string> AdditionalPlugins = new List<string>();

        /// <summary>
        /// Is the given type a 'game' type (Game/Editor/Server) wrt building?
        /// </summary>
        /// <param name="InType">The target type of interest</param>
        /// <returns>true if it *is* a 'game' type</returns>
        static public bool IsGameType(TargetType InType)
        {
            return (
                (InType == TargetType.Game) ||
                (InType == TargetType.Editor) ||
                (InType == TargetType.Client) ||
                (InType == TargetType.Server)
                );
        }

        /// <summary>
        /// Is the given type a game?
        /// </summary>
        /// <param name="InType">The target type of interest</param>
        /// <returns>true if it *is* a game</returns>
        static public bool IsAGame(TargetType InType)
        {
            return (
                (InType == TargetType.Game) ||
                (InType == TargetType.Client)
                );
        }

        /// <summary>
        /// Is the given type an 'editor' type with regard to building?
        /// </summary>
        /// <param name="InType">The target type of interest</param>
        /// <returns>true if it *is* an 'editor' type</returns>
        static public bool IsEditorType(TargetType InType)
        {
            return (InType == TargetType.Editor);
        }


        /// <summary>
        /// Type of target
        /// </summary>
        public TargetType Type = TargetType.Game;

        /// <summary>
        /// The name of this target's 'configuration' within the development IDE.  No project may have more than one target with the same configuration name.
        /// If no configuration name is set, then it defaults to the TargetType name
        /// </summary>
        public string ConfigurationName
        {
            get
            {
                if (String.IsNullOrEmpty(ConfigurationNameVar))
                {
                    return Type.ToString();
                }
                else
                {
                    return ConfigurationNameVar;
                }
            }
            set
            {
                ConfigurationNameVar = value;
            }
        }
        private string ConfigurationNameVar = String.Empty;


        /// <summary>
        /// If true, the built target goes into the Engine/Binaries/<PLATFORM> folder
        /// </summary>
        public bool bOutputToEngineBinaries = false;

        /// <summary>
        /// Sub folder where the built target goes: Engine/Binaries/<PLATFORM>/<SUBDIR>
        /// </summary>
        public string ExeBinariesSubFolder = String.Empty;

        /// <summary>
        /// Whether this target should be compiled in monolithic mode
        /// </summary>
        /// <param name="InPlatform">The platform being built</param>
        /// <param name="InConfiguration">The configuration being built</param>
        /// <returns>true if it should, false if not</returns>
        public virtual bool ShouldCompileMonolithic(STTargetPlatform InPlatform, STTargetConfiguration InConfiguration)
        {
            // By default, only Editor types compile non-monolithic
            if (IsEditorType(Type) == false)
            {
                // You can build a modular game/program/server via passing '-modular' to UBT
                if (UnrealBuildTool.CommandLineContains("-modular") == false)
                {
                    return true;
                }
            }

            if (UnrealBuildTool.CommandLineContains("-monolithic"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get the supported platforms for this target
        /// </summary>
        /// <param name="OutPlatforms">The list of platforms supported</param>
        /// <returns>true if successful, false if not</returns>
        public virtual bool GetSupportedPlatforms(ref List<STTargetPlatform> OutPlatforms)
        {
            if (Type == TargetType.Program)
            {
                // By default, all programs are desktop only.
                return UnrealBuildTool.GetAllDesktopPlatforms(ref OutPlatforms, false);
            }
            else if (IsEditorType(Type))
            {
                return UnrealBuildTool.GetAllEditorPlatforms(ref OutPlatforms, false);
            }
            else if (TargetRules.IsGameType(Type))
            {
                // By default all games support all platforms
                return UnrealBuildTool.GetAllPlatforms(ref OutPlatforms, false);
            }

            return false;
        }

        public bool SupportsPlatform(STTargetPlatform InPlatform)
        {
            List<STTargetPlatform> SupportedPlatforms = new List<STTargetPlatform>();
            if (GetSupportedPlatforms(ref SupportedPlatforms) == true)
            {
                return SupportedPlatforms.Contains(InPlatform);
            }
            return false;
        }

        /// <summary>
        /// Get the supported configurations for this target
        /// </summary>
        /// <param name="OutConfigurations">The list of configurations supported</param>
        /// <returns>true if successful, false if not</returns>
        public virtual bool GetSupportedConfigurations(ref List<STTargetConfiguration> OutConfigurations, bool bIncludeTestAndShippingConfigs)
        {
            if (Type == TargetType.Program)
            {
                // By default, programs are Debug and Development only.
                OutConfigurations.Add(STTargetConfiguration.Debug);
                OutConfigurations.Add(STTargetConfiguration.Development);
            }
            else
            {
                // By default all games support all configurations
                foreach (STTargetConfiguration Config in Enum.GetValues(typeof(STTargetConfiguration)))
                {
                    if (Config != STTargetConfiguration.Unknown)
                    {
                        // Some configurations just don't make sense for the editor
                        if (IsEditorType(Type) &&
                            (Config == STTargetConfiguration.Shipping || Config == STTargetConfiguration.Test))
                        {
                            // We don't currently support a "shipping" editor config
                        }
                        else if (!bIncludeTestAndShippingConfigs &&
                            (Config == STTargetConfiguration.Shipping || Config == STTargetConfiguration.Test))
                        {
                            // User doesn't want 'Test' or 'Shipping' configs in their project files
                        }
                        else
                        {
                            OutConfigurations.Add(Config);
                        }
                    }
                }
            }

            return (OutConfigurations.Count > 0) ? true : false;
        }

        /// <summary>
        /// Setup the binaries associated with this target.
        /// </summary>
        /// <param name="Target">The target information - such as platform and configuration</param>
        /// <param name="OutBuildBinaryConfigurations">Output list of binaries to generated</param>
        /// <param name="OutExtraModuleNames">Output list of extra modules that this target could utilize</param>
        public virtual void SetupBinaries(
            TargetInfo Target,
            ref List<STBuildBinaryConfiguration> OutBuildBinaryConfigurations,
            ref List<string> OutExtraModuleNames
            )
        {
        }


        /// <summary>
        /// Returns true if this target's output path needs to be the same as for the development configuration.
        /// Currently only used by the CrashReportClient.
        /// </summary>
        /// <returns>true if this target's output path needs to be the same as for the development configuration.</returns>
        public virtual bool ForceNameAsForDevelopment()
        {
            return false;
        }

        /// <summary>
        /// Setup the global environment for building this target
        /// IMPORTANT: Game targets will *not* have this function called unless they are built as monolithic targets.
        /// This is due to non-monolithic games generating a shared executable.
        /// </summary>
        /// <param name="Target">The target information - such as platform and configuration</param>
        /// <param name="OutLinkEnvironmentConfiguration">Output link environment settings</param>
        /// <param name="OutCPPEnvironmentConfiguration">Output compile environment settings</param>
        public virtual void SetupGlobalEnvironment(
            TargetInfo Target,
            ref LinkEnvironmentConfiguration OutLinkEnvironmentConfiguration,
            ref CPPEnvironmentConfiguration OutCPPEnvironmentConfiguration
            )
        {
        }
        /// <summary>
        /// Return true if this target should always be built with the base editor. Usually programs like shadercompilerworker.
        /// </summary>
        /// <returns>true if this target should always be built with the base editor.</returns>
        public virtual bool GUBP_AlwaysBuildWithBaseEditor()
        {
            return false;
        }
        /// <summary>
        /// Return true if this target should always be built with the tools. Usually programs like unrealpak.
        /// <param name="SeparateNode">If this is set to true, the program will get its own node</param>
        /// </summary>
        /// <returns>true if this target should always be built with the base editor.</returns>
        [Obsolete]
        public virtual bool GUBP_AlwaysBuildWithTools(STTargetPlatform InHostPlatform, out bool bInternalToolOnly, out bool SeparateNode)
        {
            bInternalToolOnly = false;
            SeparateNode = false;
            return false;
        }
        /// <summary>
        /// Return true if this target should always be built with the tools. Usually programs like unrealpak.
        /// <param name="SeparateNode">If this is set to true, the program will get its own node</param>
        /// </summary>
        /// <returns>true if this target should always be built with the base editor.</returns>        
        public virtual bool GUBP_AlwaysBuildWithTools(STTargetPlatform InHostPlatform, bool bBuildingRocket, out bool bInternalToolOnly, out bool SeparateNode)
        {
            bInternalToolOnly = false;
            SeparateNode = false;
            return false;
        }
        /// <summary>
        /// Return a list of platforms to build a tool for
        /// </summary>
        /// <returns>a list of platforms to build a tool for</returns>
        public virtual List<STTargetPlatform> GUBP_ToolPlatforms(STTargetPlatform InHostPlatform)
        {
            return new List<STTargetPlatform> { InHostPlatform };
        }
        /// <summary>
        /// Return a list of configs to build a tool for
        /// </summary>
        /// <returns>a list of configs to build a tool for</returns>
        public virtual List<STTargetConfiguration> GUBP_ToolConfigs(STTargetPlatform InHostPlatform)
        {
            return new List<STTargetConfiguration> { STTargetConfiguration.Development };
        }
        /// <summary>
        /// Return true if target should include a NonUnity test
        /// </summary>
        /// <returns>true if this target should include a NonUnity test
        public virtual bool GUBP_IncludeNonUnityToolTest()
        {
            return false;
        }
        /// <summary>
        /// Return true if this target should use a platform specific pass
        /// </summary>
        /// <returns>true if this target should use a platform specific pass
        public virtual bool GUBP_NeedsPlatformSpecificDLLs()
        {
            return false;
        }

        /// <summary>
        /// Return a list of target platforms for the monolithic
        /// </summary>
        /// <returns>a list of target platforms for the monolithic</returns>        
        public virtual List<STTargetPlatform> GUBP_GetPlatforms_MonolithicOnly(STTargetPlatform HostPlatform)
        {
            var Result = new List<STTargetPlatform> { HostPlatform };
            // hack to set up the templates without adding anything to their .targets.cs files
            if (!String.IsNullOrEmpty(TargetName) && TargetName.StartsWith("TP_"))
            {
                if (HostPlatform == STTargetPlatform.Win64)
                {
                    Result.Add(STTargetPlatform.IOS);
                    Result.Add(STTargetPlatform.Android);
                }
                else if (HostPlatform == STTargetPlatform.Mac)
                {
                    Result.Add(STTargetPlatform.IOS);
                }
            }
            return Result;
        }
        /// <summary>
        /// Return a list of target platforms for the monolithic without cook
        /// </summary>
        /// <returns>a list of target platforms for the monolithic without cook</returns>        
        public virtual List<STTargetPlatform> GUBP_GetBuildOnlyPlatforms_MonolithicOnly(STTargetPlatform HostPlatform)
        {
            var Result = new List<STTargetPlatform> { };
            return Result;
        }
        /// <summary>
        /// Return a list of configs for target platforms for the monolithic
        /// </summary>
        /// <returns>a list of configs for a target platforms for the monolithic</returns>        
        public virtual List<STTargetConfiguration> GUBP_GetConfigs_MonolithicOnly(STTargetPlatform HostPlatform, STTargetPlatform Platform)
        {
            return new List<STTargetConfiguration> { STTargetConfiguration.Development };
        }
        /// <summary>
        /// Return a list of configs for target platforms for formal builds
        /// </summary>
        /// <returns>a list of configs for a target platforms for the monolithic</returns>        
        [Obsolete]
        public virtual List<STTargetConfiguration> GUBP_GetConfigsForFormalBuilds_MonolithicOnly(STTargetPlatform HostPlatform, STTargetPlatform Platform)
        {
            return new List<STTargetConfiguration>();
        }

        public class GUBPFormalBuild
        {
            public STTargetPlatform TargetPlatform = STTargetPlatform.Unknown;
            public STTargetConfiguration TargetConfig = STTargetConfiguration.Unknown;
            public bool bTest = false;
            public GUBPFormalBuild(STTargetPlatform InTargetPlatform, STTargetConfiguration InTargetConfig, bool bInTest = false)
            {
                TargetPlatform = InTargetPlatform;
                TargetConfig = InTargetConfig;
                bTest = bInTest;
            }
        }
        /// <summary>
        /// Return a list of formal builds
        /// </summary>
        /// <returns>a list of formal builds</returns>        
        public virtual List<GUBPFormalBuild> GUBP_GetConfigsForFormalBuilds_MonolithicOnly(STTargetPlatform HostPlatform)
        {
            return new List<GUBPFormalBuild>();
        }


        /// <summary>
        /// Return true if this target should be included in a promotion and indicate shared or not
        /// </summary>
        /// <returns>if this target should be included in a promotion.</returns>
        public class GUBPProjectOptions
        {
            public bool bIsPromotable = false;
            public bool bSeparateGamePromotion = false;
            public bool bTestWithShared = false;
            public bool bIsMassive = false;
            public bool bCustomWorkflowForPromotion = false;
        }
        public virtual GUBPProjectOptions GUBP_IncludeProjectInPromotedBuild_EditorTypeOnly(STTargetPlatform HostPlatform)
        {
            var Result = new GUBPProjectOptions();
            // hack to set up the templates without adding anything to their .targets.cs files
            if (!String.IsNullOrEmpty(TargetName) && TargetName.StartsWith("TP_"))
            {
                Result.bTestWithShared = true;
            }
            return Result;
        }
        /// <summary>
        /// Return a list of the non-code projects to test
        /// </summary>
        /// <returns>a list of the non-code projects to build cook and test</returns>
        public virtual Dictionary<string, List<STTargetPlatform>> GUBP_NonCodeProjects_BaseEditorTypeOnly(STTargetPlatform HostPlatform)
        {
            return new Dictionary<string, List<STTargetPlatform>>();
        }
        /// <summary>
        /// Return a list of the non-code projects to make formal builds for
        /// </summary>
        /// <returns>a list of the non-code projects to build cook and test</returns>
        [Obsolete]
        public virtual Dictionary<string, List<KeyValuePair<STTargetPlatform, STTargetConfiguration>>> GUBP_NonCodeFormalBuilds_BaseEditorTypeOnly()
        {
            return new Dictionary<string, List<KeyValuePair<STTargetPlatform, STTargetConfiguration>>>();
        }
        /// <summary>
        /// Return a list of the non-code projects to make formal builds for
        /// </summary>
        /// <returns>a list of the non-code projects to build cook and test</returns>
        public virtual Dictionary<string, List<GUBPFormalBuild>> GUBP_GetNonCodeFormalBuilds_BaseEditorTypeOnly()
        {
            return new Dictionary<string, List<GUBPFormalBuild>>();
        }

        /// <summary>
        /// Return a list of "test name", "UAT command" pairs for testing the editor
        /// </summary>
        public virtual Dictionary<string, string> GUBP_GetEditorTests_EditorTypeOnly(STTargetPlatform HostPlatform)
        {
            var MacOption = HostPlatform == STTargetPlatform.Mac ? " -Mac" : "";
            var Result = new Dictionary<string, string>();
            Result.Add("EditorTest", "BuildCookRun -run -editortest -unattended -nullrhi -NoP4" + MacOption);
            Result.Add("GameTest", "BuildCookRun -run -unattended -nullrhi -NoP4" + MacOption);
            Result.Add("EditorAutomationTest", "BuildCookRun -run -editortest -RunAutomationTests -unattended -nullrhi -NoP4" + MacOption);
            Result.Add("GameAutomationTest", "BuildCookRun -run -RunAutomationTests -unattended -nullrhi -NoP4" + MacOption);
            return Result;
        }
        /// <summary>
        /// Allow the platform to setup emails for the GUBP for folks that care about node failures relating to this platform
        /// Obsolete. Included to avoid breaking existing projects.
        /// </summary>
        /// <param name="Branch">p4 root of the branch we are running</param>
        [Obsolete]
        public virtual string GUBP_GetGameFailureEMails_EditorTypeOnly(string Branch)
        {
            return "";
        }
        /// <summary>
        /// Allow the Game to set up emails for Promotable and Promotion
        /// Obsolete. Included to avoid breaking existing projects.
        /// </summary>
        [Obsolete]
        public virtual string GUBP_GetPromotionEMails_EditorTypeOnly(string Branch)
        {
            return "";
        }

        /// <summary>
        /// Return a list of "test name", "UAT command" pairs for testing a monolithic
        /// </summary>
        public virtual Dictionary<string, string> GUBP_GetGameTests_MonolithicOnly(STTargetPlatform HostPlatform, STTargetPlatform AltHostPlatform, STTargetPlatform Platform)
        {
            var Result = new Dictionary<string, string>();
            if ((Platform == HostPlatform || Platform == AltHostPlatform) && Type == TargetType.Game)  // for now, we will only run these for the dev config of the host platform
            {
                Result.Add("CookedGameTest", "BuildCookRun -run -skipcook -stage -pak -deploy -unattended -nullrhi -NoP4 -platform=" + Platform.ToString());
                Result.Add("CookedGameAutomationTest", "BuildCookRun -run -skipcook -stage -pak -deploy -RunAutomationTests -unattended -nullrhi -NoP4 -platform=" + Platform.ToString());
            }
            return Result;
        }
        /// <summary>
        /// Return a list of "test name", "UAT command" pairs for testing a monolithic
        /// </summary>
        public virtual Dictionary<string, string> GUBP_GetClientServerTests_MonolithicOnly(STTargetPlatform HostPlatform, STTargetPlatform AltHostPlatform, STTargetPlatform ServerPlatform, STTargetPlatform ClientPlatform)
        {
            var Result = new Dictionary<string, string>();
#if false // needs work
            if ((ServerPlatform == HostPlatform || ServerPlatform == AltHostPlatform) &&
                (ClientPlatform == HostPlatform || ClientPlatform == AltHostPlatform) && 
                Type == TargetType.Game)  // for now, we will only run these for the dev config of the host platform and only the game executable, not sure how to deal with a client only executable
            {
                Result.Add("CookedNetTest", "BuildCookRun -run -skipcook -stage -pak -deploy -unattended -server -nullrhi -NoP4  -addcmdline=\"-nosteam\" -platform=" + ClientPlatform.ToString() + " -serverplatform=" + ServerPlatform.ToString());
            }
#endif
            return Result;
        }
        /// <summary>
        /// Return additional parameters to cook commandlet
        /// </summary>
        public virtual string GUBP_AdditionalCookParameters(STTargetPlatform HostPlatform, string Platform)
        {
            return "";
        }

        /// <summary>
        /// Return additional parameters to package commandlet
        /// </summary>
        public virtual string GUBP_AdditionalPackageParameters(STTargetPlatform HostPlatform, STTargetPlatform Platform)
        {
            return "";
        }

        /// <summary>
        /// Allow Cook Platform Override from a target file
        /// </summary>
        public virtual string GUBP_AlternateCookPlatform(STTargetPlatform HostPlatform, string Platform)
        {
            return "";
        }
    }

    class RulesCompiler
    {
        public class RulesTypePropertiesAttribute : Attribute
        {
            public string Suffix;
            public string Extension;
            public RulesTypePropertiesAttribute(string Suffix, string Extension)
            {
                this.Suffix = Suffix;
                this.Extension = Extension;
            }
        }
        public enum RulesFileType
        {
            /// *.Build.cs files
            [RulesTypeProperties(Suffix: "Build", Extension: ".cs")]
            Module,

            /// *.Target.cs files
            [RulesTypeProperties(Suffix: "Target", Extension: ".cs")]
            Target,

            /// *.Automation.cs files
            [RulesTypeProperties(Suffix: "Automation", Extension: ".cs")]
            Automation,

            /// *.Automation.csproj files
            [RulesTypeProperties(Suffix: "Automation", Extension: ".csproj")]
            AutomationModule
        }
        class RulesFileCache
        {
            /// List of rules file paths for each of the known types in RulesFileType
            public List<string>[] RulesFilePaths = new List<string>[typeof(RulesFileType).GetEnumValues().Length];
        }

        private static void FindAllRulesFilesRecursively(DirectoryInfo DirInfo, RulesFileCache RulesFileCache)
        {
            if (DirInfo.Exists)
            {
                var RulesFileTypeEnum = typeof(RulesFileType);
                bool bFoundModuleRulesFile = false;
                var RulesFileTypes = typeof(RulesFileType).GetEnumValues();
                foreach (RulesFileType CurRulesType in RulesFileTypes)
                {
                    // Get the suffix and extension associated with this RulesFileType enum value.
                    var MemberInfo = RulesFileTypeEnum.GetMember(CurRulesType.ToString());
                    var Attributes = MemberInfo[0].GetCustomAttributes(typeof(RulesTypePropertiesAttribute), false);
                    var EnumProperties = (RulesTypePropertiesAttribute)Attributes[0];

                    var SearchRuleSuffix = "." + EnumProperties.Suffix + EnumProperties.Extension; // match files with the right suffix and extension.
                    var FilesInDirectory = DirInfo.GetFiles("*" + EnumProperties.Extension);
                    foreach (var RuleFile in FilesInDirectory)
                    {
                        // test if filename has the appropriate suffix.
                        // this handles filenames such as Foo.build.cs, Foo.Build.cs, foo.bUiLd.cs to fix bug 266743 on platforms where case-sensitivity matters
                        if (RuleFile.Name.EndsWith(SearchRuleSuffix, StringComparison.InvariantCultureIgnoreCase))
                        {
                            // Skip Uncooked targets, as those are no longer valid.  This is just for easier backwards compatibility with existing projects.
                            // @todo: Eventually we can eliminate this conditional and just allow it to be an error when these are compiled
                            if (CurRulesType != RulesFileType.Target || !RuleFile.Name.EndsWith("Uncooked" + SearchRuleSuffix, StringComparison.InvariantCultureIgnoreCase))
                            {
                                if (RulesFileCache.RulesFilePaths[(int)CurRulesType] == null)
                                {
                                    RulesFileCache.RulesFilePaths[(int)CurRulesType] = new List<string>();
                                }

                                // Convert file info to the full file path for this file and update our cache
                                RulesFileCache.RulesFilePaths[(int)CurRulesType].Add(RuleFile.FullName);

                                // NOTE: Multiple rules files in the same folder are supported.  We'll continue iterating along.
                                if (CurRulesType == RulesFileType.Module)
                                {
                                    bFoundModuleRulesFile = true;
                                }
                            }
                            else
                            {
                                Log.TraceVerbose("Skipped deprecated Target rules file with Uncooked extension: " + RuleFile.Name);
                            }
                        }
                    }
                }

                // Only recurse if we didn't find a module rules file.  In the interest of performance and organizational sensibility
                // we don't want to support folders with Build.cs files containing other folders with Build.cs files.  Performance-
                // wise, this is really important to avoid scanning every folder in the Source/ThirdParty directory, for example.
                if (!bFoundModuleRulesFile)
                {
                    // Add all the files recursively
                    foreach (DirectoryInfo SubDirInfo in DirInfo.GetDirectories())
                    {
                        if (SubDirInfo.Name.Equals("Intermediate", StringComparison.InvariantCultureIgnoreCase))
                        {
                            Console.WriteLine("WARNING: UnrealBuildTool found an Intermediate folder while looking for rules '{0}'.  It should only ever be searching under 'Source' folders -- an Intermediate folder is unexpected and will greatly decrease iteration times!", SubDirInfo.FullName);
                        }
                        FindAllRulesFilesRecursively(SubDirInfo, RulesFileCache);
                    }
                }
            }
        }

        /// Map of root folders to a cached list of all UBT-related source files in that folder or any of its sub-folders.
        /// We cache these file names so we can avoid searching for them later on.
        static Dictionary<string, RulesFileCache> RootFolderToRulesFileCache = new Dictionary<string, RulesFileCache>();

        /// Name of the assembly file to cache rules data within
        static string AssemblyName = String.Empty;

        /// List of all game folders that we will be able to search for rules files within.  This must be primed at startup.
        public static List<string> AllGameFolders
        {
            get;
            private set;
        }

        /// <summary>
        /// Sets which game folders to look at when harvesting for rules source files.  This must be called before
        /// other functions in the RulesCompiler.  The idea here is that we can actually cache rules files for multiple
        /// games in a single assembly, if necessary.  In practice, multiple game folder's rules should only be cached together
        /// when generating project files.
        /// </summary>
        /// <param name="GameFolders">List of all game folders that rules files will ever be requested for</param>
        public static void SetAssemblyNameAndGameFolders(string AssemblyName, List<string> GameFolders)
        {
            RulesCompiler.AssemblyName = AssemblyName + "ModuleRules";

            AllGameFolders = new List<string>();
            AllGameFolders.AddRange(GameFolders);
        }
        public static List<string> FindAllRulesSourceFiles(RulesFileType RulesFileType, List<string> AdditionalSearchPaths)
        {
            List<string> Folders = new List<string>();

            // Add all engine source (including third party source)
            Folders.Add(Path.Combine(ProjectFileGenerator.EngineRelativePath, "Source"));

            // @todo plugin: Disallow modules from including plugin modules as dependency modules? (except when the module is part of that plugin)

            // And plugin directories
            {
                foreach (var Plugin in Plugins.AllPlugins)
                {
                    // Plugin source directory
                    var PluginSourceDirectory = Path.Combine(Plugin.Directory, "Source");
                    if (Directory.Exists(PluginSourceDirectory))
                    {
                        Folders.Add(PluginSourceDirectory);
                    }
                }
            }

            // Add in the game folders to search
            if (AllGameFolders != null)
            {
                foreach (var GameFolder in AllGameFolders)
                {
                    var GameSourceFolder = Path.GetFullPath(Path.Combine(GameFolder, "Source"));
                    Folders.Add(GameSourceFolder);
                }
            }

            // Process the additional search path, if sent in
            if (AdditionalSearchPaths != null)
            {
                foreach (var AdditionalSearchPath in AdditionalSearchPaths)
                {
                    if (!string.IsNullOrEmpty(AdditionalSearchPath))
                    {
                        if (Directory.Exists(AdditionalSearchPath))
                        {
                            Folders.Add(AdditionalSearchPath);
                        }
                        else
                        {
                            throw new BuildException("Couldn't find AdditionalSearchPath for rules source files '{0}'", AdditionalSearchPath);
                        }
                    }
                }
            }

            var SourceFiles = new List<string>();

            // Iterate over all the folders to check
            foreach (string Folder in Folders)
            {
                // Check to see if we've already cached source files for this folder
                RulesFileCache FolderRulesFileCache;
                if (!RootFolderToRulesFileCache.TryGetValue(Folder, out FolderRulesFileCache))
                {
                    FolderRulesFileCache = new RulesFileCache();
                    FindAllRulesFilesRecursively(new DirectoryInfo(Folder), FolderRulesFileCache);
                    RootFolderToRulesFileCache[Folder] = FolderRulesFileCache;

                    if (BuildConfiguration.bPrintDebugInfo)
                    {
                        foreach (var CurType in Enum.GetValues(typeof(RulesFileType)))
                        {
                            var RulesFiles = FolderRulesFileCache.RulesFilePaths[(int)CurType];
                            if (RulesFiles != null)
                            {
                                Log.TraceVerbose("Found {0} rules files for folder {1} of type {2}", RulesFiles.Count, Folder, CurType.ToString());
                            }
                        }
                    }
                }

                var RulesFilePathsForType = FolderRulesFileCache.RulesFilePaths[(int)RulesFileType];
                if (RulesFilePathsForType != null)
                {
                    foreach (string RulesFilePath in RulesFilePathsForType)
                    {
                        if (!SourceFiles.Contains(RulesFilePath))
                        {
                            SourceFiles.Add(RulesFilePath);
                        }
                    }
                }
            }

            return SourceFiles;
        }

        /// <summary>
        /// Creates a target object for the specified target name.
        /// </summary>
        /// <param name="GameFolder">Root folder for the target's game, if this is a game target</param>
        /// <param name="TargetName">Name of the target</param>
        /// <param name="Target">Information about the target associated with this target</param>
        /// <returns>The build target object for the specified build rules source file</returns>
        public static STBuildTarget CreateTarget(string TargetName, TargetInfo Target,
            List<string> InAdditionalDefinitions, string InRemoteRoot, List<OnlyModule> InOnlyModules, bool bInEditorRecompile)
        {
            var CreateTargetStartTime = DateTime.UtcNow;

            string TargetFileName;
            TargetRules RulesObject = CreateTargetRules(TargetName, Target, bInEditorRecompile, out TargetFileName);
            if (bInEditorRecompile)
            {
                // Now that we found the actual Editor target, make sure we're no longer using the old TargetName (which is the Game target)
                var TargetSuffixIndex = RulesObject.TargetName.LastIndexOf("Target");
                TargetName = (TargetSuffixIndex > 0) ? RulesObject.TargetName.Substring(0, TargetSuffixIndex) : RulesObject.TargetName;
            }
            if ((ProjectFileGenerator.bGenerateProjectFiles == false) && (RulesObject.SupportsPlatform(Target.Platform) == false))
            {
                if (UEBuildConfiguration.bCleanProject)
                {
                    return null;
                }
                throw new BuildException("{0} does not support the {1} platform.", TargetName, Target.Platform.ToString());
            }

            // Generate a build target from this rules module
            UEBuildTarget BuildTarget = null;
            switch (RulesObject.Type)
            {
                case TargetRules.TargetType.Game:
                    {
                        BuildTarget = new UEBuildGame(
                            InGameName: TargetName,
                            InPlatform: Target.Platform,
                            InConfiguration: Target.Configuration,
                            InRulesObject: RulesObject,
                            InAdditionalDefinitions: InAdditionalDefinitions,
                            InRemoteRoot: InRemoteRoot,
                            InOnlyModules: InOnlyModules,
                            bInEditorRecompile: bInEditorRecompile);
                    }
                    break;
                case TargetRules.TargetType.Editor:
                    {
                        BuildTarget = new UEBuildEditor(
                            InGameName: TargetName,
                            InPlatform: Target.Platform,
                            InConfiguration: Target.Configuration,
                            InRulesObject: RulesObject,
                            InAdditionalDefinitions: InAdditionalDefinitions,
                            InRemoteRoot: InRemoteRoot,
                            InOnlyModules: InOnlyModules,
                            bInEditorRecompile: bInEditorRecompile);
                    }
                    break;
                case TargetRules.TargetType.Client:
                    {
                        BuildTarget = new UEBuildClient(
                            InGameName: TargetName,
                            InPlatform: Target.Platform,
                            InConfiguration: Target.Configuration,
                            InRulesObject: RulesObject,
                            InAdditionalDefinitions: InAdditionalDefinitions,
                            InRemoteRoot: InRemoteRoot,
                            InOnlyModules: InOnlyModules,
                            bInEditorRecompile: bInEditorRecompile);
                    }
                    break;
                case TargetRules.TargetType.Server:
                    {
                        BuildTarget = new UEBuildServer(
                            InGameName: TargetName,
                            InPlatform: Target.Platform,
                            InConfiguration: Target.Configuration,
                            InRulesObject: RulesObject,
                            InAdditionalDefinitions: InAdditionalDefinitions,
                            InRemoteRoot: InRemoteRoot,
                            InOnlyModules: InOnlyModules,
                            bInEditorRecompile: bInEditorRecompile);
                    }
                    break;
                case TargetRules.TargetType.Program:
                    {
                        BuildTarget = new UEBuildTarget(
                            InAppName: TargetName,
                            InGameName: "",
                            InPlatform: Target.Platform,
                            InConfiguration: Target.Configuration,
                            InRulesObject: RulesObject,
                            InAdditionalDefinitions: InAdditionalDefinitions,
                            InRemoteRoot: InRemoteRoot,
                            InOnlyModules: InOnlyModules,
                            bInEditorRecompile: bInEditorRecompile);
                    }
                    break;
            }

            if (BuildConfiguration.bPrintPerformanceInfo)
            {
                var CreateTargetTime = (DateTime.UtcNow - CreateTargetStartTime).TotalSeconds;
                Log.TraceInformation("CreateTarget for " + TargetName + " took " + CreateTargetTime + "s");
            }

            if (BuildTarget == null)
            {
                throw new BuildException("Failed to create build target for '{0}'.", TargetName);
            }

            return BuildTarget;
        }
    }
}
