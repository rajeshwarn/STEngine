﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace STBuildTool
{
    public enum SDKStatus
    {
        Valid,			// Desired SDK is installed and set up.
        Invalid,		// Could not find the desired SDK, SDK setup failed, etc.		
    };

    public interface ISTBuildPlatform
    {
        /**
         * Whether the required external SDKs are installed for this platform
         */
        SDKStatus HasRequiredSDKsInstalled();

        /**
         * If this platform can be compiled with XGE
         */
        bool CanUseXGE();

        /**
         * If this platform can be compiled with DMUCS/Distcc
         */
        bool CanUseDistcc();

        /**
         * If this platform can be compiled with SN-DBS
         */
        bool CanUseSNDBS();

        /**
         * Register the platform with the UEBuildPlatform class
         */
        void RegisterBuildPlatform();

        /**
         * Attempt to set up AutoSDK for this platform
         */
        void ManageAndValidateSDK();

        /**
		 * Retrieve the CPPTargetPlatform for the given STTargetPlatform
		 *
		 * @param InSTTargetPlatform The STTargetPlatform being build
		 * 
		 * @return CPPTargetPlatform The CPPTargetPlatform to compile for
		 */
        CPPTargetPlatform GetCPPTargetPlatform(STTargetPlatform InSTTargetPlatform);

        /**
         * Get the extension to use for the given binary type
         * 
         * @param InBinaryType The binary type being built
         * 
         * @return string The binary extension (i.e. 'exe' or 'dll')
         */
        string GetBinaryExtension(STBuildBinaryType InBinaryType);

        /**
         * Get the extension to use for debug info for the given binary type
         * 
         * @param InBinaryType The binary type being built
         * 
         * @return string The debug info extension (i.e. 'pdb')
         */
        string GetDebugInfoExtension(STBuildBinaryType InBinaryType);

        /**
         * Whether incremental linking should be used
         * 
         * @param InPlatform The CPPTargetPlatform being built
         * @param InConfiguration The CPPTargetConfiguration being built
         * 
         * @return bool true if incremental linking should be used, false if not
         */
        bool ShouldUseIncrementalLinking(CPPTargetPlatform Platform, CPPTargetConfiguration Configuration);

        /**
         * Whether PDB files should be used
         * 
         * @param InPlatform The CPPTargetPlatform being built
         * @param InConfiguration The CPPTargetConfiguration being built
         * @param bInCreateDebugInfo true if debug info is getting create, false if not
         * 
         * @return bool true if PDB files should be used, false if not
         */
        bool ShouldUsePDBFiles(CPPTargetPlatform Platform, CPPTargetConfiguration Configuration, bool bCreateDebugInfo);

        /**
         * Whether PCH files should be used
         * 
         * @param InPlatform The CPPTargetPlatform being built
         * @param InConfiguration The CPPTargetConfiguration being built
         * 
         * @return bool true if PCH files should be used, false if not
         */
        bool ShouldUsePCHFiles(CPPTargetPlatform Platform, CPPTargetConfiguration Configuration);

        /**
         * Whether the editor should be built for this platform or not
         * 
         * @param InPlatform The STTargetPlatform being built
         * @param InConfiguration The STTargetConfiguration being built
         * @return bool true if the editor should be built, false if not
         */
        bool ShouldNotBuildEditor(STTargetPlatform InPlatform, STTargetConfiguration InConfiguration);

        /**
         * Whether this build should support ONLY cooked data or not
         * 
         * @param InPlatform The STTargetPlatform being built
         * @param InConfiguration The STTargetConfiguration being built
         * @return bool true if the editor should be built, false if not
         */
        bool BuildRequiresCookedData(STTargetPlatform InPlatform, STTargetConfiguration InConfiguration);

        /**
         * Whether the platform requires the extra UnityCPPWriter
         * This is used to add an extra file for UBT to get the #include dependencies from
         * 
         * @return bool true if it is required, false if not
         */
        bool RequiresExtraUnityCPPWriter();

        /**
         * Whether this platform should build a monolithic binary
         */
        bool ShouldCompileMonolithicBinary(STTargetPlatform InPlatform);

        /**
         * Get a list of extra modules the platform requires.
         * This is to allow undisclosed platforms to add modules they need without exposing information about the platform.
         * 
         * @param Target The target being build
         * @param BuildTarget The STBuildTarget getting build
         * @param PlatformExtraModules OUTPUT the list of extra modules the platform needs to add to the target
         */
        void GetExtraModules(TargetInfo Target, STBuildTarget BuildTarget, ref List<string> PlatformExtraModules);

        /**
         * Modify the newly created module passed in for this platform.
         * This is not required - but allows for hiding details of a
         * particular platform.
         * 
         * @param InModule The newly loaded module
         * @param Target The target being build
         */
        void ModifyNewlyLoadedModule(STBuildModule InModule, TargetInfo Target);

        /**
         * Setup the target environment for building
         * 
         * @param InBuildTarget The target being built
         */
        void SetUpEnvironment(STBuildTarget InBuildTarget);

        /**
         * Allow the platform to set an optional architecture
         */
        string GetActiveArchitecture();

        /**
         * Allow the platform to apply architecture-specific name according to its rules
         * 
         * @param BinaryName name of the binary, not specific to any architecture
         *
         * @return Possibly architecture-specific name
         */
        string ApplyArchitectureName(string BinaryName);

        /**
         * For platforms that need to output multiple files per binary (ie Android "fat" binaries)
         * this will emit multiple paths. By default, it simply makes an array from the input
         */
        string[] FinalizeBinaryPaths(string BinaryName);

        /**
         * Setup the configuration environment for building
         * 
         * @param InBuildTarget The target being built
         */
        void SetUpConfigurationEnvironment(STBuildTarget InBuildTarget);

        /**
         * Whether this platform should create debug information or not
         * 
         * @param InPlatform The STTargetPlatform being built
         * @param InConfiguration The STTargetConfiguration being built
         * 
         * @return bool true if debug info should be generated, false if not
         */
        bool ShouldCreateDebugInfo(STTargetPlatform Platform, STTargetConfiguration Configuration);

        /**
         * Gives the platform a chance to 'override' the configuration settings 
         * that are overridden on calls to RunUBT.
         * 
         * @param InPlatform The STTargetPlatform being built
         * @param InConfiguration The STTargetConfiguration being built
         * 
         * @return bool true if debug info should be generated, false if not
         */
        void ResetBuildConfiguration(STTargetPlatform InPlatform, STTargetConfiguration InConfiguration);

        /**
         * Validate the UEBuildConfiguration for this platform
         * This is called BEFORE calling UEBuildConfiguration to allow setting 
         * various fields used in that function such as CompileLeanAndMean...
         */
        void ValidateSTBuildConfiguration();

        /**
         * Validate configuration for this platform
         * NOTE: This function can/will modify BuildConfiguration!
         * 
         * @param InPlatform The CPPTargetPlatform being built
         * @param InConfiguration The CPPTargetConfiguration being built
         * @param bInCreateDebugInfo true if debug info is getting create, false if not
         */
        void ValidateBuildConfiguration(CPPTargetConfiguration Configuration, CPPTargetPlatform Platform, bool bCreateDebugInfo);

        /**
         * Return whether this platform has uniquely named binaries across multiple games
         */
        bool HasUniqueBinaries();

        /**
         * Return whether we wish to have this platform's binaries in our builds
         */
        bool IsBuildRequired();

        /**
         * Return whether we wish to have this platform's binaries in our CIS tests
         */
        bool IsCISRequired();

        /// <summary>
        /// Whether the build platform requires deployment prep
        /// </summary>
        /// <returns></returns>
        bool RequiresDeployPrepAfterCompile();

        /**
         * Return all valid configurations for this platform
         * 
         *  Typically, this is always Debug, Development, and Shipping - but Test is a likely future addition for some platforms
         */
        List<STTargetConfiguration> GetConfigurations(STTargetPlatform InSTTargetPlatform, bool bIncludeDebug);

        /**
         * Setup the binaries for this specific platform.
         * 
         * @param InBuildTarget The target being built
         */
        void SetupBinaries(STBuildTarget InBuildTarget);
    }

    public abstract partial class STBuildPlatform : ISTBuildPlatform
    {
        public static Dictionary<STTargetPlatform, ISTBuildPlatform> BuildPlatformDictionary = new Dictionary<STTargetPlatform, ISTBuildPlatform>();

        // a mapping of a group to the platforms in the group (ie, Microsoft contains Win32 and Win64)
        static Dictionary<STPlatformGroup, List<STTargetPlatform>> PlatformGroupDictionary = new Dictionary<STPlatformGroup, List<STTargetPlatform>>();

        /**
         * Attempt to convert a string to an STTargetPlatform enum entry
         * 
         * @return STTargetPlatform.Unknown on failure (the platform didn't match the enum)
         */
        public static STTargetPlatform ConvertStringToPlatform(string InPlatformName)
        {
            // special case x64, to not break anything
            // @todo: Is it possible to remove this hack?
            if (InPlatformName.Equals("X64", StringComparison.InvariantCultureIgnoreCase))
            {
                return STTargetPlatform.Win64;
            }

            // we can't parse the string into an enum because Enum.Parse is case sensitive, so we loop over the enum
            // looking for matches
            foreach (string PlatformName in Enum.GetNames(typeof(STTargetPlatform)))
            {
                if (InPlatformName.Equals(PlatformName, StringComparison.InvariantCultureIgnoreCase))
                {
                    // convert the known good enum string back to the enum value
                    return (STTargetPlatform)Enum.Parse(typeof(STTargetPlatform), PlatformName);
                }
            }
            return STTargetPlatform.Unknown;
        }

        /**
         *	Register the given platforms UEBuildPlatform instance
         *	
         *	@param	InPlatform			The STTargetPlatform to register with
         *	@param	InBuildPlatform		The UEBuildPlatform instance to use for the InPlatform
         */
        public static void RegisterBuildPlatform(STTargetPlatform InPlatform, STBuildPlatform InBuildPlatform)
        {
            if (BuildPlatformDictionary.ContainsKey(InPlatform) == true)
            {
                Log.TraceWarning("RegisterBuildPlatform Warning: Registering build platform {0} for {1} when it is already set to {2}",
                    InBuildPlatform.ToString(), InPlatform.ToString(), BuildPlatformDictionary[InPlatform].ToString());
                BuildPlatformDictionary[InPlatform] = InBuildPlatform;
            }
            else
            {
                BuildPlatformDictionary.Add(InPlatform, InBuildPlatform);
            }
        }

        /**
         *	Unregister the given platform
         */
        public static void UnregisterBuildPlatform(STTargetPlatform InPlatform)
        {
            if (BuildPlatformDictionary.ContainsKey(InPlatform) == true)
            {
                BuildPlatformDictionary.Remove(InPlatform);
            }
        }

        /**
         * Assign a platform as a member of the given group
         */
        public static void RegisterPlatformWithGroup(STTargetPlatform InPlatform, STPlatformGroup InGroup)
        {
            // find or add the list of groups for this platform
            PlatformGroupDictionary.GetOrAddNew(InGroup).Add(InPlatform);
        }

        /**
         * Retrieve the list of platforms in this group (if any)
         */
        public static List<STTargetPlatform> GetPlatformsInGroup(STPlatformGroup InGroup)
        {
            List<STTargetPlatform> PlatformList;
            PlatformGroupDictionary.TryGetValue(InGroup, out PlatformList);
            return PlatformList;
        }

        /**
         *	Retrieve the ISTBuildPlatform instance for the given TargetPlatform
         *	
         *	@param	InPlatform			The STTargetPlatform being built
         *	@param	bInAllowFailure		If true, do not throw an exception and return null
         *	
         *	@return	UEBuildPlatform		The instance of the build platform
         */
        public static ISTBuildPlatform GetBuildPlatform(STTargetPlatform InPlatform, bool bInAllowFailure = false)
        {
            if (BuildPlatformDictionary.ContainsKey(InPlatform) == true)
            {
                return BuildPlatformDictionary[InPlatform];
            }
            if (bInAllowFailure == true)
            {
                return null;
            }
            throw new BuildException("GetBuildPlatform: No BuildPlatform found for {0}", InPlatform.ToString());
        }

        /**
         *	Retrieve the ISTBuildPlatform instance for the given CPPTargetPlatform
         *	
         *	@param	InPlatform			The CPPTargetPlatform being built
         *	@param	bInAllowFailure		If true, do not throw an exception and return null
         *	
         *	@return	UEBuildPlatform		The instance of the build platform
         */
        public static ISTBuildPlatform GetBuildPlatformForCPPTargetPlatform(CPPTargetPlatform InPlatform, bool bInAllowFailure = false)
        {
            STTargetPlatform UTPlatform = STBuildTarget.CPPTargetPlatformToSTTargetPlatform(InPlatform);
            if (BuildPlatformDictionary.ContainsKey(UTPlatform) == true)
            {
                return BuildPlatformDictionary[UTPlatform];
            }
            if (bInAllowFailure == true)
            {
                return null;
            }
            throw new BuildException("UEBuildPlatform::GetBuildPlatformForCPPTargetPlatform: No BuildPlatform found for {0}", InPlatform.ToString());
        }

        /**
         *	Allow all registered build platforms to modify the newly created module 
         *	passed in for the given platform.
         *	This is not required - but allows for hiding details of a particular platform.
         *	
         *	@param	InModule		The newly loaded module
         *	@param	Target			The target being build
         *	@param	Only			If this is not unknown, then only run that platform
         */
        public static void PlatformModifyNewlyLoadedModule(STBuildModule InModule, TargetInfo Target, STTargetPlatform Only = STTargetPlatform.Unknown)
        {
            foreach (var PlatformEntry in BuildPlatformDictionary)
            {
                if (Only == STTargetPlatform.Unknown || PlatformEntry.Key == Only || PlatformEntry.Key == Target.Platform)
                {
                    PlatformEntry.Value.ModifyNewlyLoadedModule(InModule, Target);
                }
            }
        }

        /**
         * Returns the delimiter used to separate paths in the PATH environment variable for the platform we are executing on.
         */
        public String GetPathVarDelimiter()
        {
            switch (BuildHostPlatform.Current.Platform)
            {
                case STTargetPlatform.Linux:
                case STTargetPlatform.Mac:
                    return ":";
                case STTargetPlatform.Win32:
                case STTargetPlatform.Win64:
                    return ";";
                default:
                    Log.TraceWarning("PATH var delimiter unknown for platform " + BuildHostPlatform.Current.Platform.ToString() + " using ';'");
                    return ";";
            }
        }



        /**
         *	If this platform can be compiled with XGE
         */
        public virtual bool CanUseXGE()
        {
            return true;
        }

        /**
         *	If this platform can be compiled with DMUCS/Distcc
         */
        public virtual bool CanUseDistcc()
        {
            return false;
        }

        /**
         *	If this platform can be compiled with SN-DBS
         */
        public virtual bool CanUseSNDBS()
        {
            return false;
        }

        /**
         *	Register the platform with the UEBuildPlatform class
         */
        public void RegisterBuildPlatform()
        {
            ManageAndValidateSDK();
            RegisterBuildPlatformInternal();
        }

        protected abstract void RegisterBuildPlatformInternal();

        /**
         *	Retrieve the CPPTargetPlatform for the given STTargetPlatform
         *
         *	@param	InSTTargetPlatform		The STTargetPlatform being build
         *	
         *	@return	CPPTargetPlatform			The CPPTargetPlatform to compile for
         */
        public abstract CPPTargetPlatform GetCPPTargetPlatform(STTargetPlatform InSTTargetPlatform);

        /// <summary>
        /// Return whether the given platform requires a monolithic build
        /// </summary>
        /// <param name="InPlatform">The platform of interest</param>
        /// <param name="InConfiguration">The configuration of interest</param>
        /// <returns></returns>
        public static bool PlatformRequiresMonolithicBuilds(STTargetPlatform InPlatform, STTargetConfiguration InConfiguration)
        {
            // Some platforms require monolithic builds...
            var BuildPlatform = GetBuildPlatform(InPlatform, true);
            if (BuildPlatform != null)
            {
                return BuildPlatform.ShouldCompileMonolithicBinary(InPlatform);
            }

            // We assume it does not
            return false;
        }

        /**
         *	Get the extension to use for the given binary type
         *	
         *	@param	InBinaryType		The binary type being built
         *	
         *	@return	string				The binary extension (i.e. 'exe' or 'dll')
         */
        public virtual string GetBinaryExtension(STBuildBinaryType InBinaryType)
        {
            throw new BuildException("GetBinaryExtensiton for {0} not handled in {1}", InBinaryType.ToString(), this.ToString());
        }

        /**
         *	Get the extension to use for debug info for the given binary type
         *	
         *	@param	InBinaryType		The binary type being built
         *	
         *	@return	string				The debug info extension (i.e. 'pdb')
         */
        public virtual string GetDebugInfoExtension(STBuildBinaryType InBinaryType)
        {
            throw new BuildException("GetDebugInfoExtension for {0} not handled in {1}", InBinaryType.ToString(), this.ToString());
        }

        /**
         *	Whether incremental linking should be used
         *	
         *	@param	InPlatform			The CPPTargetPlatform being built
         *	@param	InConfiguration		The CPPTargetConfiguration being built
         *	
         *	@return	bool	true if incremental linking should be used, false if not
         */
        public virtual bool ShouldUseIncrementalLinking(CPPTargetPlatform Platform, CPPTargetConfiguration Configuration)
        {
            return false;
        }

        /**
         *	Whether PDB files should be used
         *	
         *	@param	InPlatform			The CPPTargetPlatform being built
         *	@param	InConfiguration		The CPPTargetConfiguration being built
         *	@param	bInCreateDebugInfo	true if debug info is getting create, false if not
         *	
         *	@return	bool	true if PDB files should be used, false if not
         */
        public virtual bool ShouldUsePDBFiles(CPPTargetPlatform Platform, CPPTargetConfiguration Configuration, bool bCreateDebugInfo)
        {
            return false;
        }

        /**
         *	Whether PCH files should be used
         *	
         *	@param	InPlatform			The CPPTargetPlatform being built
         *	@param	InConfiguration		The CPPTargetConfiguration being built
         *	
         *	@return	bool				true if PCH files should be used, false if not
         */
        public virtual bool ShouldUsePCHFiles(CPPTargetPlatform Platform, CPPTargetConfiguration Configuration)
        {
            return BuildConfiguration.bUsePCHFiles;
        }

        /**
         *	Whether the editor should be built for this platform or not
         *	
         *	@param	InPlatform		The STTargetPlatform being built
         *	@param	InConfiguration	The STTargetConfiguration being built
         *	@return	bool			true if the editor should be built, false if not
         */
        public virtual bool ShouldNotBuildEditor(STTargetPlatform InPlatform, STTargetConfiguration InConfiguration)
        {
            return false;
        }

        /**
         *	Whether this build should support ONLY cooked data or not
         *	
         *	@param	InPlatform		The STTargetPlatform being built
         *	@param	InConfiguration	The STTargetConfiguration being built
         *	@return	bool			true if the editor should be built, false if not
         */
        public virtual bool BuildRequiresCookedData(STTargetPlatform InPlatform, STTargetConfiguration InConfiguration)
        {
            return false;
        }

        /**
         *	Whether the platform requires the extra UnityCPPWriter
         *	This is used to add an extra file for UBT to get the #include dependencies from
         *	
         *	@return	bool	true if it is required, false if not
         */
        public virtual bool RequiresExtraUnityCPPWriter()
        {
            return false;
        }

        /**
         * Whether this platform should build a monolithic binary
         */
        public virtual bool ShouldCompileMonolithicBinary(STTargetPlatform InPlatform)
        {
            return false;
        }

        /**
         *	Get a list of extra modules the platform requires.
         *	This is to allow undisclosed platforms to add modules they need without exposing information about the platform.
         *	
         *	@param	Target						The target being build
         *	@param	BuildTarget					The STBuildTarget getting build
         *	@param	PlatformExtraModules		OUTPUT the list of extra modules the platform needs to add to the target
         */
        public virtual void GetExtraModules(TargetInfo Target, STBuildTarget BuildTarget, ref List<string> PlatformExtraModules)
        {
        }

        /**
         *	Modify the newly created module passed in for this platform.
         *	This is not required - but allows for hiding details of a
         *	particular platform.
         *	
         *	@param	InModule		The newly loaded module
         *	@param	Target			The target being build
         */
        public virtual void ModifyNewlyLoadedModule(STBuildModule InModule, TargetInfo Target)
        {
        }

        /**
         *	Setup the target environment for building
         *	
         *	@param	InBuildTarget		The target being built
         */
        public abstract void SetUpEnvironment(STBuildTarget InBuildTarget);

        /**
         * Allow the platform to set an optional architecture
         */
        public virtual string GetActiveArchitecture()
        {
            // by default, use an empty architecture (which is really just a modifer to the platform for some paths/names)
            return "";
        }

        /**
         * Allow the platform to override the NMake output name
         */
        public virtual string ModifyNMakeOutput(string ExeName)
        {
            // by default, use original
            return ExeName;
        }

        /**
         * Allow the platform to apply architecture-specific name according to its rules
         * 
         * @param BinaryName name of the binary, not specific to any architecture
         *
         * @return Possibly architecture-specific name
         */
        public virtual string ApplyArchitectureName(string BinaryName)
        {
            // by default, use logic that assumes architectures to start with "-"
            return BinaryName + GetActiveArchitecture();
        }

        /**
         * For platforms that need to output multiple files per binary (ie Android "fat" binaries)
         * this will emit multiple paths. By default, it simply makes an array from the input
         */
        public virtual string[] FinalizeBinaryPaths(string BinaryName)
        {
            List<string> TempList = new List<string>() { BinaryName };
            return TempList.ToArray();
        }

        /**
         *	Setup the configuration environment for building
         *	
         *	@param	InBuildTarget		The target being built
         */
        public virtual void SetUpConfigurationEnvironment(STBuildTarget InBuildTarget)
        {
            // Determine the C++ compile/link configuration based on the Unreal configuration.
            CPPTargetConfiguration CompileConfiguration;
            STTargetConfiguration CheckConfig = InBuildTarget.Configuration;
            //@todo SAS: Add a true Debug mode!
            if (STBuildTool.RunningRocket())
            {
                if (Utils.IsFileUnderDirectory(InBuildTarget.OutputPaths[0], STBuildTool.GetUProjectPath()))
                {
                    if (CheckConfig == STTargetConfiguration.Debug)
                    {
                        CheckConfig = STTargetConfiguration.DebugGame;
                    }
                }
                else
                {
                    // Only Development and Shipping are supported for engine modules
                    if (CheckConfig != STTargetConfiguration.Development && CheckConfig != STTargetConfiguration.Shipping)
                    {
                        CheckConfig = STTargetConfiguration.Development;
                    }
                }
            }
            switch (CheckConfig)
            {
                default:
                case STTargetConfiguration.Debug:
                    CompileConfiguration = CPPTargetConfiguration.Debug;
                    if (BuildConfiguration.bDebugBuildsActuallyUseDebugCRT)
                    {
                        InBuildTarget.GlobalCompileEnvironment.Config.Definitions.Add("_DEBUG=1"); // the engine doesn't use this, but lots of 3rd party stuff does
                    }
                    else
                    {
                        InBuildTarget.GlobalCompileEnvironment.Config.Definitions.Add("NDEBUG=1"); // the engine doesn't use this, but lots of 3rd party stuff does
                    }
                    InBuildTarget.GlobalCompileEnvironment.Config.Definitions.Add("UE_BUILD_DEBUG=1");
                    break;
                case STTargetConfiguration.DebugGame:
                // Individual game modules can be switched to be compiled in debug as necessary. By default, everything is compiled in development.
                case STTargetConfiguration.Development:
                    CompileConfiguration = CPPTargetConfiguration.Development;
                    InBuildTarget.GlobalCompileEnvironment.Config.Definitions.Add("NDEBUG=1"); // the engine doesn't use this, but lots of 3rd party stuff does
                    InBuildTarget.GlobalCompileEnvironment.Config.Definitions.Add("UE_BUILD_DEVELOPMENT=1");
                    break;
                case STTargetConfiguration.Shipping:
                    CompileConfiguration = CPPTargetConfiguration.Shipping;
                    InBuildTarget.GlobalCompileEnvironment.Config.Definitions.Add("NDEBUG=1"); // the engine doesn't use this, but lots of 3rd party stuff does
                    InBuildTarget.GlobalCompileEnvironment.Config.Definitions.Add("UE_BUILD_SHIPPING=1");
                    break;
                case STTargetConfiguration.Test:
                    CompileConfiguration = CPPTargetConfiguration.Shipping;
                    InBuildTarget.GlobalCompileEnvironment.Config.Definitions.Add("NDEBUG=1"); // the engine doesn't use this, but lots of 3rd party stuff does
                    InBuildTarget.GlobalCompileEnvironment.Config.Definitions.Add("UE_BUILD_TEST=1");
                    break;
            }

            // Set up the global C++ compilation and link environment.
            InBuildTarget.GlobalCompileEnvironment.Config.Target.Configuration = CompileConfiguration;
            InBuildTarget.GlobalLinkEnvironment.Config.Target.Configuration = CompileConfiguration;

            // Create debug info based on the heuristics specified by the user.
            InBuildTarget.GlobalCompileEnvironment.Config.bCreateDebugInfo =
                !BuildConfiguration.bDisableDebugInfo && ShouldCreateDebugInfo(InBuildTarget.Platform, CheckConfig);
            InBuildTarget.GlobalLinkEnvironment.Config.bCreateDebugInfo = InBuildTarget.GlobalCompileEnvironment.Config.bCreateDebugInfo;
        }

        /**
         *	Whether this platform should create debug information or not
         *	
         *	@param	InPlatform			The STTargetPlatform being built
         *	@param	InConfiguration		The STTargetConfiguration being built
         *	
         *	@return	bool				true if debug info should be generated, false if not
         */
        public abstract bool ShouldCreateDebugInfo(STTargetPlatform Platform, STTargetConfiguration Configuration);

        /**
         *	Gives the platform a chance to 'override' the configuration settings 
         *	that are overridden on calls to RunUBT.
         *	
         *	@param	InPlatform			The STTargetPlatform being built
         *	@param	InConfiguration		The STTargetConfiguration being built
         *	
         *	@return	bool				true if debug info should be generated, false if not
         */
        public virtual void ResetBuildConfiguration(STTargetPlatform InPlatform, STTargetConfiguration InConfiguration)
        {
        }

        /**
         *	Validate the UEBuildConfiguration for this platform
         *	This is called BEFORE calling UEBuildConfiguration to allow setting 
         *	various fields used in that function such as CompileLeanAndMean...
         */
        public virtual void ValidateSTBuildConfiguration()
        {
        }

        /**
         *	Validate configuration for this platform
         *	NOTE: This function can/will modify BuildConfiguration!
         *	
         *	@param	InPlatform			The CPPTargetPlatform being built
         *	@param	InConfiguration		The CPPTargetConfiguration being built
         *	@param	bInCreateDebugInfo	true if debug info is getting create, false if not
         */
        public virtual void ValidateBuildConfiguration(CPPTargetConfiguration Configuration, CPPTargetPlatform Platform, bool bCreateDebugInfo)
        {
        }

        /**
         *	Return whether this platform has uniquely named binaries across multiple games
         */
        public virtual bool HasUniqueBinaries()
        {
            return true;
        }

        /**
         *	Return whether we wish to have this platform's binaries in our builds
         */
        public virtual bool IsBuildRequired()
        {
            return true;
        }

        /**
         *	Return whether we wish to have this platform's binaries in our CIS tests
         */
        public virtual bool IsCISRequired()
        {
            return true;
        }

        /// <summary>
        /// Whether the build platform requires deployment prep
        /// </summary>
        /// <returns></returns>
        public virtual bool RequiresDeployPrepAfterCompile()
        {
            return false;
        }

        /**
         *	Return all valid configurations for this platform
         *	
         *  Typically, this is always Debug, Development, and Shipping - but Test is a likely future addition for some platforms
         */
        public virtual List<STTargetConfiguration> GetConfigurations(STTargetPlatform InSTTargetPlatform, bool bIncludeDebug)
        {
            List<STTargetConfiguration> Configurations = new List<STTargetConfiguration>()
			{
				STTargetConfiguration.Development, 
			};

            if (bIncludeDebug)
            {
                Configurations.Insert(0, STTargetConfiguration.Debug);
            }

            return Configurations;
        }

        /**
         *	Setup the binaries for this specific platform.
         *	
         *	@param	InBuildTarget		The target being built
         */
        public virtual void SetupBinaries(STBuildTarget InBuildTarget)
        {
        }
    }

    // AutoSDKs handling portion
    public abstract partial class STBuildPlatform : ISTBuildPlatform
    {

        #region protected AutoSDKs Utility

        /** Name of the file that holds currently install SDK version string */
        protected static string CurrentlyInstalledSDKStringManifest = "CurrentlyInstalled.txt";

        /** name of the file that holds the last succesfully run SDK setup script version */
        protected static string LastRunScriptVersionManifest = "CurrentlyInstalled.Version.txt";

        /** Name of the file that holds environment variables of current SDK */
        protected static string SDKEnvironmentVarsFile = "OutputEnvVars.txt";

        protected static readonly string SDKRootEnvVar = "UE_SDKS_ROOT";

        protected static string AutoSetupEnvVar = "AutoSDKSetup";

        public static bool bShouldLogInfo = false;

        /** 
         * Whether platform supports switching SDKs during runtime
         * 
         * @return true if supports
         */
        protected virtual bool PlatformSupportsAutoSDKs()
        {
            return false;
        }

        static private bool bCheckedAutoSDKRootEnvVar = false;
        static private bool bAutoSDKSystemEnabled = false;
        static private bool HasAutoSDKSystemEnabled()
        {
            if (!bCheckedAutoSDKRootEnvVar)
            {
                string SDKRoot = Environment.GetEnvironmentVariable(SDKRootEnvVar);
                if (SDKRoot != null)
                {
                    bAutoSDKSystemEnabled = true;
                }
                bCheckedAutoSDKRootEnvVar = true;
            }
            return bAutoSDKSystemEnabled;
        }

        // Whether AutoSDK setup is safe. AutoSDKs will damage manual installs on some platforms.
        protected bool IsAutoSDKSafe()
        {
            return !IsAutoSDKDestructive() || !HasAnyManualInstall();
        }

        /** 
         * Returns SDK string as required by the platform 
         * 
         * @return Valid SDK string
         */
        protected virtual string GetRequiredSDKString()
        {
            return "";
        }

        /**
        * Gets the version number of the SDK setup script itself.  The version in the base should ALWAYS be the master revision from the last refactor.  
        * If you need to force a rebuild for a given platform, override this for the given platform.
        * @return Setup script version
        */
        protected virtual String GetRequiredScriptVersionString()
        {
            return "3.0";
        }

        /** 
         * Returns path to platform SDKs
         * 
         * @return Valid SDK string
         */
        protected string GetPathToPlatformAutoSDKs()
        {
            string SDKPath = "";
            string SDKRoot = Environment.GetEnvironmentVariable(SDKRootEnvVar);
            if (SDKRoot != null)
            {
                if (SDKRoot != "")
                {
                    SDKPath = Path.Combine(SDKRoot, "Host" + BuildHostPlatform.Current.Platform, GetSDKTargetPlatformName());
                }
            }
            return SDKPath;
        }

        /**
         * Because most ManualSDK determination depends on reading env vars, if this process is spawned by a process that ALREADY set up
         * AutoSDKs then all the SDK env vars will exist, and we will spuriously detect a Manual SDK. (children inherit the environment of the parent process).
         * Therefore we write out an env var to set in the command file (OutputEnvVars.txt) such that child processes can determine if their manual SDK detection
         * is bogus.  Make it platform specific so that platforms can be in different states.
         */
        protected string GetPlatformAutoSDKSetupEnvVar()
        {
            return GetSDKTargetPlatformName() + AutoSetupEnvVar;
        }

        /**
         * Gets currently installed version
         * 
         * @param PlatformSDKRoot absolute path to platform SDK root
         * @param OutInstalledSDKVersionString version string as currently installed
         * 
         * @return true if was able to read it
         */
        protected bool GetCurrentlyInstalledSDKString(string PlatformSDKRoot, out string OutInstalledSDKVersionString)
        {
            if (Directory.Exists(PlatformSDKRoot))
            {
                string VersionFilename = Path.Combine(PlatformSDKRoot, CurrentlyInstalledSDKStringManifest);
                if (File.Exists(VersionFilename))
                {
                    using (StreamReader Reader = new StreamReader(VersionFilename))
                    {
                        string Version = Reader.ReadLine();
                        string Type = Reader.ReadLine();

                        // don't allow ManualSDK installs to count as an AutoSDK install version.
                        if (Type != null && Type == "AutoSDK")
                        {
                            if (Version != null)
                            {
                                OutInstalledSDKVersionString = Version;
                                return true;
                            }
                        }
                    }
                }
            }

            OutInstalledSDKVersionString = "";
            return false;
        }

        /**
         * Gets the version of the last successfully run setup script.
         * 
         * @param PlatformSDKRoot absolute path to platform SDK root
         * @param OutLastRunScriptVersion version string
         * 
         * @return true if was able to read it
         */
        protected bool GetLastRunScriptVersionString(string PlatformSDKRoot, out string OutLastRunScriptVersion)
        {
            if (Directory.Exists(PlatformSDKRoot))
            {
                string VersionFilename = Path.Combine(PlatformSDKRoot, LastRunScriptVersionManifest);
                if (File.Exists(VersionFilename))
                {
                    using (StreamReader Reader = new StreamReader(VersionFilename))
                    {
                        string Version = Reader.ReadLine();
                        if (Version != null)
                        {
                            OutLastRunScriptVersion = Version;
                            return true;
                        }
                    }
                }
            }

            OutLastRunScriptVersion = "";
            return false;
        }

        /**
         * Sets currently installed version
         * 
         * @param PlatformSDKRoot absolute path to platform SDK root
         * @param InstalledSDKVersionString SDK version string to set
         * 
         * @return true if was able to set it
         */
        protected bool SetCurrentlyInstalledAutoSDKString(String InstalledSDKVersionString)
        {
            String PlatformSDKRoot = GetPathToPlatformAutoSDKs();
            if (Directory.Exists(PlatformSDKRoot))
            {
                string VersionFilename = Path.Combine(PlatformSDKRoot, CurrentlyInstalledSDKStringManifest);
                if (File.Exists(VersionFilename))
                {
                    File.Delete(VersionFilename);
                }

                using (StreamWriter Writer = File.CreateText(VersionFilename))
                {
                    Writer.WriteLine(InstalledSDKVersionString);
                    Writer.WriteLine("AutoSDK");
                    return true;
                }
            }

            return false;
        }

        protected void SetupManualSDK()
        {
            if (PlatformSupportsAutoSDKs() && HasAutoSDKSystemEnabled())
            {
                String InstalledSDKVersionString = GetRequiredSDKString();
                String PlatformSDKRoot = GetPathToPlatformAutoSDKs();
                if (Directory.Exists(PlatformSDKRoot))
                {
                    string VersionFilename = Path.Combine(PlatformSDKRoot, CurrentlyInstalledSDKStringManifest);
                    if (File.Exists(VersionFilename))
                    {
                        File.Delete(VersionFilename);
                    }

                    string EnvVarFile = Path.Combine(PlatformSDKRoot, SDKEnvironmentVarsFile);
                    if (File.Exists(EnvVarFile))
                    {
                        File.Delete(EnvVarFile);
                    }

                    using (StreamWriter Writer = File.CreateText(VersionFilename))
                    {
                        Writer.WriteLine(InstalledSDKVersionString);
                        Writer.WriteLine("ManualSDK");
                    }
                }
            }
        }

        protected bool SetLastRunAutoSDKScriptVersion(string LastRunScriptVersion)
        {
            String PlatformSDKRoot = GetPathToPlatformAutoSDKs();
            if (Directory.Exists(PlatformSDKRoot))
            {
                string VersionFilename = Path.Combine(PlatformSDKRoot, LastRunScriptVersionManifest);
                if (File.Exists(VersionFilename))
                {
                    File.Delete(VersionFilename);
                }

                using (StreamWriter Writer = File.CreateText(VersionFilename))
                {
                    Writer.WriteLine(LastRunScriptVersion);
                    return true;
                }
            }
            return false;
        }

        /**
        * Returns Hook names as needed by the platform
        * (e.g. can be overriden with custom executables or scripts)
        *
        * @param Hook Hook type
        */
        protected virtual string GetHookExecutableName(SDKHookType Hook)
        {
            if (Hook == SDKHookType.Uninstall)
            {
                return "unsetup.bat";
            }

            return "setup.bat";
        }

        /**
         * Runs install/uninstall hooks for SDK
         * 
         * @param PlatformSDKRoot absolute path to platform SDK root
         * @param SDKVersionString version string to run for (can be empty!)
         * @param Hook which one of hooks to run
         * @param bHookCanBeNonExistent whether a non-existing hook means failure
         * 
         * @return true if succeeded
         */
        protected virtual bool RunAutoSDKHooks(string PlatformSDKRoot, string SDKVersionString, SDKHookType Hook, bool bHookCanBeNonExistent = true)
        {
            if (!IsAutoSDKSafe())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                LogAutoSDK(GetSDKTargetPlatformName() + " attempted to run SDK hook which could have damaged manual SDK install!");
                Console.ResetColor();

                return false;
            }
            if (SDKVersionString != "")
            {
                string SDKDirectory = Path.Combine(PlatformSDKRoot, SDKVersionString);
                string HookExe = Path.Combine(SDKDirectory, GetHookExecutableName(Hook));

                if (File.Exists(HookExe))
                {
                    LogAutoSDK("Running {0} hook {1}", Hook, HookExe);

                    // run it
                    Process HookProcess = new Process();
                    HookProcess.StartInfo.WorkingDirectory = SDKDirectory;
                    HookProcess.StartInfo.FileName = HookExe;
                    HookProcess.StartInfo.Arguments = "";
                    HookProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

                    // seems to break the build machines?
                    //HookProcess.StartInfo.UseShellExecute = false;
                    //HookProcess.StartInfo.RedirectStandardOutput = true;
                    //HookProcess.StartInfo.RedirectStandardError = true;					

                    //installers may require administrator access to succeed. so run as an admmin.
                    HookProcess.StartInfo.Verb = "runas";
                    HookProcess.Start();
                    HookProcess.WaitForExit();

                    //LogAutoSDK(HookProcess.StandardOutput.ReadToEnd());
                    //LogAutoSDK(HookProcess.StandardError.ReadToEnd());
                    if (HookProcess.ExitCode != 0)
                    {
                        LogAutoSDK("Hook exited uncleanly (returned {0}), considering it failed.", HookProcess.ExitCode);
                        return false;
                    }

                    return true;
                }
                else
                {
                    LogAutoSDK("File {0} does not exist", HookExe);
                }
            }
            else
            {
                LogAutoSDK("Version string is blank for {0}. Can't determine {1} hook.", PlatformSDKRoot, Hook.ToString());
            }

            return bHookCanBeNonExistent;
        }

        /**
         * Loads environment variables from SDK
         * If any commands are added or removed the handling needs to be duplicated in
         * TargetPlatformManagerModule.cpp
         * 
         * @param PlatformSDKRoot absolute path to platform SDK         
         * 
         * @return true if succeeded
         */
        protected bool SetupEnvironmentFromAutoSDK(string PlatformSDKRoot)
        {
            string EnvVarFile = Path.Combine(PlatformSDKRoot, SDKEnvironmentVarsFile);
            if (File.Exists(EnvVarFile))
            {
                using (StreamReader Reader = new StreamReader(EnvVarFile))
                {
                    List<string> PathAdds = new List<string>();
                    List<string> PathRemoves = new List<string>();

                    List<string> EnvVarNames = new List<string>();
                    List<string> EnvVarValues = new List<string>();

                    bool bNeedsToWriteAutoSetupEnvVar = true;
                    String PlatformSetupEnvVar = GetPlatformAutoSDKSetupEnvVar();
                    for (; ; )
                    {
                        string VariableString = Reader.ReadLine();
                        if (VariableString == null)
                        {
                            break;
                        }

                        string[] Parts = VariableString.Split('=');
                        if (Parts.Length != 2)
                        {
                            LogAutoSDK("Incorrect environment variable declaration:");
                            LogAutoSDK(VariableString);
                            return false;
                        }

                        if (String.Compare(Parts[0], "strippath", true) == 0)
                        {
                            PathRemoves.Add(Parts[1]);
                        }
                        else if (String.Compare(Parts[0], "addpath", true) == 0)
                        {
                            PathAdds.Add(Parts[1]);
                        }
                        else
                        {
                            if (String.Compare(Parts[0], PlatformSetupEnvVar) == 0)
                            {
                                bNeedsToWriteAutoSetupEnvVar = false;
                            }
                            // convenience for setup.bat writers.  Trim any accidental whitespace from var names/values.
                            EnvVarNames.Add(Parts[0].Trim());
                            EnvVarValues.Add(Parts[1].Trim());
                        }
                    }

                    // don't actually set anything until we successfully validate and read all values in.
                    // we don't want to set a few vars, return a failure, and then have a platform try to
                    // build against a manually installed SDK with half-set env vars.
                    for (int i = 0; i < EnvVarNames.Count; ++i)
                    {
                        string EnvVarName = EnvVarNames[i];
                        string EnvVarValue = EnvVarValues[i];
                        if (BuildConfiguration.bPrintDebugInfo)
                        {
                            LogAutoSDK("Setting variable '{0}' to '{1}'", EnvVarName, EnvVarValue);
                        }
                        Environment.SetEnvironmentVariable(EnvVarName, EnvVarValue);
                    }


                    // actually perform the PATH stripping / adding.
                    String OrigPathVar = Environment.GetEnvironmentVariable("PATH");
                    String PathDelimiter = GetPathVarDelimiter();
                    String[] PathVars = OrigPathVar.Split(PathDelimiter.ToCharArray());

                    List<String> ModifiedPathVars = new List<string>();
                    ModifiedPathVars.AddRange(PathVars);

                    // perform removes first, in case they overlap with any adds.
                    foreach (String PathRemove in PathRemoves)
                    {
                        foreach (String PathVar in PathVars)
                        {
                            if (PathVar.IndexOf(PathRemove, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                LogAutoSDK("Removing Path: '{0}'", PathVar);
                                ModifiedPathVars.Remove(PathVar);
                            }
                        }
                    }

                    // remove all the of ADDs so that if this function is executed multiple times, the paths will be guarateed to be in the same order after each run.
                    // If we did not do this, a 'remove' that matched some, but not all, of our 'adds' would cause the order to change.
                    foreach (String PathAdd in PathAdds)
                    {
                        foreach (String PathVar in PathVars)
                        {
                            if (String.Compare(PathAdd, PathVar, true) == 0)
                            {
                                LogAutoSDK("Removing Path: '{0}'", PathVar);
                                ModifiedPathVars.Remove(PathVar);
                            }
                        }
                    }

                    // perform adds, but don't add duplicates
                    foreach (String PathAdd in PathAdds)
                    {
                        if (!ModifiedPathVars.Contains(PathAdd))
                        {
                            LogAutoSDK("Adding Path: '{0}'", PathAdd);
                            ModifiedPathVars.Add(PathAdd);
                        }
                    }

                    String ModifiedPath = String.Join(PathDelimiter, ModifiedPathVars);
                    Environment.SetEnvironmentVariable("PATH", ModifiedPath);

                    Reader.Close();

                    // write out env var command so any process using this commandfile will mark itself as having had autosdks set up.
                    // avoids child processes spuriously detecting manualsdks.
                    if (bNeedsToWriteAutoSetupEnvVar)
                    {
                        using (StreamWriter Writer = File.AppendText(EnvVarFile))
                        {
                            Writer.WriteLine("{0}=1", PlatformSetupEnvVar);
                        }
                        // set the var in the local environment in case this process spawns any others.
                        Environment.SetEnvironmentVariable(PlatformSetupEnvVar, "1");
                    }

                    // make sure we know that we've modified the local environment, invalidating manual installs for this run.
                    bLocalProcessSetupAutoSDK = true;

                    return true;
                }
            }
            else
            {
                LogAutoSDK("Cannot set up environment for {1} because command file {1} does not exist.", PlatformSDKRoot, EnvVarFile);
            }

            return false;
        }

        protected void InvalidateCurrentlyInstalledAutoSDK()
        {
            String PlatformSDKRoot = GetPathToPlatformAutoSDKs();
            if (Directory.Exists(PlatformSDKRoot))
            {
                string SDKFilename = Path.Combine(PlatformSDKRoot, CurrentlyInstalledSDKStringManifest);
                if (File.Exists(SDKFilename))
                {
                    File.Delete(SDKFilename);
                }

                string VersionFilename = Path.Combine(PlatformSDKRoot, LastRunScriptVersionManifest);
                if (File.Exists(VersionFilename))
                {
                    File.Delete(VersionFilename);
                }

                string EnvVarFile = Path.Combine(PlatformSDKRoot, SDKEnvironmentVarsFile);
                if (File.Exists(EnvVarFile))
                {
                    File.Delete(EnvVarFile);
                }
            }
        }

        /** 
         * Currently installed AutoSDK is written out to a text file in a known location.  
         * This function just compares the file's contents with the current requirements.
         */
        protected SDKStatus HasRequiredAutoSDKInstalled()
        {
            if (PlatformSupportsAutoSDKs() && HasAutoSDKSystemEnabled())
            {
                string AutoSDKRoot = GetPathToPlatformAutoSDKs();
                if (AutoSDKRoot != "")
                {
                    // check script version so script fixes can be propagated without touching every build machine's CurrentlyInstalled file manually.
                    bool bScriptVersionMatches = false;
                    string CurrentScriptVersionString;
                    if (GetLastRunScriptVersionString(AutoSDKRoot, out CurrentScriptVersionString) && CurrentScriptVersionString == GetRequiredScriptVersionString())
                    {
                        bScriptVersionMatches = true;
                    }

                    // check to make sure OutputEnvVars doesn't need regenerating
                    string EnvVarFile = Path.Combine(AutoSDKRoot, SDKEnvironmentVarsFile);
                    bool bEnvVarFileExists = File.Exists(EnvVarFile);

                    string CurrentSDKString;
                    if (bEnvVarFileExists && GetCurrentlyInstalledSDKString(AutoSDKRoot, out CurrentSDKString) && CurrentSDKString == GetRequiredSDKString() && bScriptVersionMatches)
                    {
                        return SDKStatus.Valid;
                    }
                    return SDKStatus.Invalid;
                }
            }
            return SDKStatus.Invalid;
        }

        // This tracks if we have already checked the sdk installation.
        private Int32 SDKCheckStatus = -1;

        // true if we've ever overridden the process's environment with AutoSDK data.  After that, manual installs cannot be considered valid ever again.
        private bool bLocalProcessSetupAutoSDK = false;

        protected bool HasSetupAutoSDK()
        {
            return bLocalProcessSetupAutoSDK || HasParentProcessSetupAutoSDK();
        }

        protected bool HasParentProcessSetupAutoSDK()
        {
            bool bParentProcessSetupAutoSDK = false;
            String AutoSDKSetupVarName = GetPlatformAutoSDKSetupEnvVar();
            String AutoSDKSetupVar = Environment.GetEnvironmentVariable(AutoSDKSetupVarName);
            if (!String.IsNullOrEmpty(AutoSDKSetupVar))
            {
                bParentProcessSetupAutoSDK = true;
            }
            return bParentProcessSetupAutoSDK;
        }

        protected SDKStatus HasRequiredManualSDK()
        {
            if (HasSetupAutoSDK())
            {
                return SDKStatus.Invalid;
            }

            // manual installs are always invalid if we have modified the process's environment for AutoSDKs
            return HasRequiredManualSDKInternal();
        }

        // for platforms with destructive AutoSDK.  Report if any manual sdk is installed that may be damaged by an autosdk.
        protected virtual bool HasAnyManualInstall()
        {
            return false;
        }

        // tells us if the user has a valid manual install.
        protected abstract SDKStatus HasRequiredManualSDKInternal();

        // some platforms will fail if there is a manual install that is the WRONG manual install.
        protected virtual bool AllowInvalidManualInstall()
        {
            return true;
        }

        // platforms can choose if they prefer a correct the the AutoSDK install over the manual install.
        protected virtual bool PreferAutoSDK()
        {
            return true;
        }

        // some platforms don't support parallel SDK installs.  AutoSDK on these platforms will
        // actively damage an existing manual install by overwriting files in it.  AutoSDK must NOT
        // run any setup if a manual install exists in this case.
        protected virtual bool IsAutoSDKDestructive()
        {
            return false;
        }

        /**
        * Runs batch files if necessary to set up required AutoSDK.
        * AutoSDKs are SDKs that have not been setup through a formal installer, but rather come from
        * a source control directory, or other local copy.
        */
        private void SetupAutoSDK()
        {
            if (IsAutoSDKSafe() && PlatformSupportsAutoSDKs() && HasAutoSDKSystemEnabled())
            {
                // run installation for autosdk if necessary.
                if (HasRequiredAutoSDKInstalled() == SDKStatus.Invalid)
                {
                    //reset check status so any checking sdk status after the attempted setup will do a real check again.
                    SDKCheckStatus = -1;

                    string AutoSDKRoot = GetPathToPlatformAutoSDKs();
                    string CurrentSDKString;
                    GetCurrentlyInstalledSDKString(AutoSDKRoot, out CurrentSDKString);

                    // switch over (note that version string can be empty)
                    if (!RunAutoSDKHooks(AutoSDKRoot, CurrentSDKString, SDKHookType.Uninstall))
                    {
                        LogAutoSDK("Failed to uninstall currently installed SDK {0}", CurrentSDKString);
                        InvalidateCurrentlyInstalledAutoSDK();
                        return;
                    }
                    // delete Manifest file to avoid multiple uninstalls
                    InvalidateCurrentlyInstalledAutoSDK();

                    if (!RunAutoSDKHooks(AutoSDKRoot, GetRequiredSDKString(), SDKHookType.Install, false))
                    {
                        LogAutoSDK("Failed to install required SDK {0}.  Attemping to uninstall", GetRequiredSDKString());
                        RunAutoSDKHooks(AutoSDKRoot, GetRequiredSDKString(), SDKHookType.Uninstall, false);
                        return;
                    }

                    string EnvVarFile = Path.Combine(AutoSDKRoot, SDKEnvironmentVarsFile);
                    if (!File.Exists(EnvVarFile))
                    {
                        LogAutoSDK("Installation of required SDK {0}.  Did not generate Environment file {1}", GetRequiredSDKString(), EnvVarFile);
                        RunAutoSDKHooks(AutoSDKRoot, GetRequiredSDKString(), SDKHookType.Uninstall, false);
                        return;
                    }

                    SetCurrentlyInstalledAutoSDKString(GetRequiredSDKString());
                    SetLastRunAutoSDKScriptVersion(GetRequiredScriptVersionString());
                }

                // fixup process environment to match autosdk
                SetupEnvironmentFromAutoSDK();
            }
        }

        #endregion

        #region public AutoSDKs Utility

        /** Enum describing types of hooks a platform SDK can have */
        public enum SDKHookType
        {
            Install,
            Uninstall
        };

        /** 
         * Returns platform-specific name used in SDK repository
         * 
         * @return path to SDK Repository
         */
        public virtual string GetSDKTargetPlatformName()
        {
            return "";
        }

        /* Whether or not we should try to automatically switch SDKs when asked to validate the platform's SDK state. */
        public static bool bAllowAutoSDKSwitching = true;

        public SDKStatus SetupEnvironmentFromAutoSDK()
        {
            string PlatformSDKRoot = GetPathToPlatformAutoSDKs();

            // load environment variables from current SDK
            if (!SetupEnvironmentFromAutoSDK(PlatformSDKRoot))
            {
                LogAutoSDK("Failed to load environment from required SDK {0}", GetRequiredSDKString());
                InvalidateCurrentlyInstalledAutoSDK();
                return SDKStatus.Invalid;
            }
            return SDKStatus.Valid;
        }

        /**
         *	Whether the required external SDKs are installed for this platform.  
         *	Could be either a manual install or an AutoSDK.
         */
        public SDKStatus HasRequiredSDKsInstalled()
        {
            // avoid redundant potentially expensive SDK checks.
            if (SDKCheckStatus == -1)
            {
                bool bHasManualSDK = HasRequiredManualSDK() == SDKStatus.Valid;
                bool bHasAutoSDK = HasRequiredAutoSDKInstalled() == SDKStatus.Valid;

                // Per-Platform implementations can choose how to handle non-Auto SDK detection / handling.
                SDKCheckStatus = (bHasManualSDK || bHasAutoSDK) ? 1 : 0;
            }
            return SDKCheckStatus == 1 ? SDKStatus.Valid : SDKStatus.Invalid;
        }

        // Arbitrates between manual SDKs and setting up AutoSDK based on program options and platform preferences.
        public void ManageAndValidateSDK()
        {
            bShouldLogInfo = BuildConfiguration.bPrintDebugInfo || Environment.GetEnvironmentVariable("IsBuildMachine") == "1";

            // do not modify installed manifests if parent process has already set everything up.
            // this avoids problems with determining IsAutoSDKSafe and doing an incorrect invalidate.
            if (bAllowAutoSDKSwitching && !HasParentProcessSetupAutoSDK())
            {
                bool bSetSomeSDK = false;
                bool bHasRequiredManualSDK = HasRequiredManualSDK() == SDKStatus.Valid;
                if (IsAutoSDKSafe() && (PreferAutoSDK() || !bHasRequiredManualSDK))
                {
                    SetupAutoSDK();
                    bSetSomeSDK = true;
                }

                //Setup manual SDK if autoSDK setup was skipped or failed for whatever reason.
                if (bHasRequiredManualSDK && (HasRequiredAutoSDKInstalled() != SDKStatus.Valid))
                {
                    SetupManualSDK();
                    bSetSomeSDK = true;
                }

                if (!bSetSomeSDK)
                {
                    InvalidateCurrentlyInstalledAutoSDK();
                }
            }


            if (bShouldLogInfo)
            {
                PrintSDKInfo();
            }
        }

        public void PrintSDKInfo()
        {
            if (HasRequiredSDKsInstalled() == SDKStatus.Valid)
            {
                bool bHasRequiredManualSDK = HasRequiredManualSDK() == SDKStatus.Valid;
                if (HasSetupAutoSDK())
                {
                    string PlatformSDKRoot = GetPathToPlatformAutoSDKs();
                    LogAutoSDK(GetSDKTargetPlatformName() + " using SDK from: " + Path.Combine(PlatformSDKRoot, GetRequiredSDKString()));
                }
                else if (bHasRequiredManualSDK)
                {
                    LogAutoSDK(this.ToString() + " using manually installed SDK " + GetRequiredSDKString());
                }
                else
                {
                    LogAutoSDK(this.ToString() + " setup error.  Inform platform team.");
                }
            }
            else
            {
                LogAutoSDK(this.ToString() + " has no valid SDK");
            }
        }

        protected static void LogAutoSDK(string Format, params object[] Args)
        {
            if (bShouldLogInfo)
            {
                Log.WriteLine(TraceEventType.Information, Format, Args);
            }
        }

        protected static void LogAutoSDK(String Message)
        {
            if (bShouldLogInfo)
            {
                Log.WriteLine(TraceEventType.Information, Message);
            }
        }

        #endregion
    }
}
