using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace STBuildTool
{
    public abstract class STPlatformProjectGenerator
    {
        static Dictionary<STTargetPlatform, STPlatformProjectGenerator> ProjectGeneratorDictionary = new Dictionary<STTargetPlatform, STPlatformProjectGenerator>();

        /**
         *	Register the given platforms UEPlatformProjectGenerator instance
         *	
         *	@param	InPlatform			The STTargetPlatform to register with
         *	@param	InProjectGenerator	The UEPlatformProjectGenerator instance to use for the InPlatform
         */
        public static void RegisterPlatformProjectGenerator(STTargetPlatform InPlatform, STPlatformProjectGenerator InProjectGenerator)
        {
            // Make sure the build platform is legal
            var BuildPlatform = STBuildPlatform.GetBuildPlatform(InPlatform, true);
            if (BuildPlatform != null)
            {
                if (ProjectGeneratorDictionary.ContainsKey(InPlatform) == true)
                {
                    Log.TraceInformation("RegisterPlatformProjectGenerator Warning: Registering project generator {0} for {1} when it is already set to {2}",
                        InProjectGenerator.ToString(), InPlatform.ToString(), ProjectGeneratorDictionary[InPlatform].ToString());
                    ProjectGeneratorDictionary[InPlatform] = InProjectGenerator;
                }
                else
                {
                    ProjectGeneratorDictionary.Add(InPlatform, InProjectGenerator);
                }
            }
            else
            {
                Log.TraceVerbose("Skipping project file generator registration for {0} due to no valid BuildPlatform.", InPlatform.ToString());
            }
        }

        /**
         *	Retrieve the UEPlatformProjectGenerator instance for the given TargetPlatform
         *	
         *	@param	InPlatform					The STTargetPlatform being built
         *	@param	bInAllowFailure				If true, do not throw an exception and return null
         *	
         *	@return	UEPlatformProjectGenerator	The instance of the project generator
         */
        public static STPlatformProjectGenerator GetPlatformProjectGenerator(STTargetPlatform InPlatform, bool bInAllowFailure = false)
        {
            if (ProjectGeneratorDictionary.ContainsKey(InPlatform) == true)
            {
                return ProjectGeneratorDictionary[InPlatform];
            }
            if (bInAllowFailure == true)
            {
                return null;
            }
            throw new BuildException("GetPlatformProjectGenerator: No PlatformProjectGenerator found for {0}", InPlatform.ToString());
        }

        /// <summary>
        /// Allow various platform project generators to generate stub projects if required
        /// </summary>
        /// <param name="InTargetName"></param>
        /// <param name="InTargetFilepath"></param>
        /// <returns></returns>
        public static bool GenerateGameProjectStubs(ProjectFileGenerator InGenerator, string InTargetName, string InTargetFilepath, TargetRules InTargetRules,
            List<STTargetPlatform> InPlatforms, List<STTargetConfiguration> InConfigurations)
        {
            foreach (KeyValuePair<STTargetPlatform, STPlatformProjectGenerator> Entry in ProjectGeneratorDictionary)
            {
                STPlatformProjectGenerator ProjGen = Entry.Value;
                ProjGen.GenerateGameProjectStub(InGenerator, InTargetName, InTargetFilepath, InTargetRules, InPlatforms, InConfigurations);
            }
            return true;
        }

        /// <summary>
        /// Allow various platform project generators to generate any special project properties if required
        /// </summary>
        /// <param name="InPlatform"></param>
        /// <returns></returns>
        public static bool GenerateGamePlatformSpecificProperties(STTargetPlatform InPlatform, STTargetConfiguration Configuration, TargetRules.TargetType TargetType, StringBuilder VCProjectFileContent, string RootDirectory, string TargetFilePath)
        {
            if (ProjectGeneratorDictionary.ContainsKey(InPlatform) == true)
            {
                ProjectGeneratorDictionary[InPlatform].GenerateGameProperties(Configuration, VCProjectFileContent, TargetType, RootDirectory, TargetFilePath); ;
            }
            return true;
        }

        public static bool PlatformRequiresVSUserFileGeneration(List<STTargetPlatform> InPlatforms, List<STTargetConfiguration> InConfigurations)
        {
            bool bRequiresVSUserFileGeneration = false;
            foreach (KeyValuePair<STTargetPlatform, STPlatformProjectGenerator> Entry in ProjectGeneratorDictionary)
            {
                if (InPlatforms.Contains(Entry.Key))
                {
                    STPlatformProjectGenerator ProjGen = Entry.Value;
                    bRequiresVSUserFileGeneration |= ProjGen.RequiresVSUserFileGeneration();
                }
            }
            return bRequiresVSUserFileGeneration;
        }

        /**
         *	Register the platform with the UEPlatformProjectGenerator class
         */
        public abstract void RegisterPlatformProjectGenerator();

        public virtual void GenerateGameProjectStub(ProjectFileGenerator InGenerator, string InTargetName, string InTargetFilepath, TargetRules InTargetRules,
            List<STTargetPlatform> InPlatforms, List<STTargetConfiguration> InConfigurations)
        {
            // Do nothing
        }

        public virtual void GenerateGameProperties(STTargetConfiguration Configuration, StringBuilder VCProjectFileContent, TargetRules.TargetType TargetType, string RootDirectory, string TargetFilePath)
        {
            // Do nothing
        }

        public virtual bool RequiresVSUserFileGeneration()
        {
            return false;
        }


        ///
        ///	VisualStudio project generation functions
        ///	
        /**
         *	Whether this build platform has native support for VisualStudio
         *	
         *	@param	InPlatform			The STTargetPlatform being built
         *	@param	InConfiguration		The STTargetConfiguration being built
         *	
         *	@return	bool				true if native VisualStudio support (or custom VSI) is available
         */
        public virtual bool HasVisualStudioSupport(STTargetPlatform InPlatform, STTargetConfiguration InConfiguration)
        {
            // By default, we assume this is true
            return true;
        }

        /**
         *	Return the VisualStudio platform name for this build platform
         *	
         *	@param	InPlatform			The STTargetPlatform being built
         *	@param	InConfiguration		The STTargetConfiguration being built
         *	
         *	@return	string				The name of the platform that VisualStudio recognizes
         */
        public virtual string GetVisualStudioPlatformName(STTargetPlatform InPlatform, STTargetConfiguration InConfiguration)
        {
            // By default, return the platform string
            return InPlatform.ToString();
        }

        /**
         *	Return the platform toolset string to write into the project configuration
         *	
         *	@param	InPlatform			The STTargetPlatform being built
         *	@param	InConfiguration		The STTargetConfiguration being built
         *	
         *	@return	string				The custom configuration section for the project file; Empty string if it doesn't require one
         */
        public virtual string GetVisualStudioPlatformToolsetString(STTargetPlatform InPlatform, STTargetConfiguration InConfiguration, VCProjectFile InProjectFile)
        {
            return "";
        }

        /**
         * Return any custom property group lines
         *	
         *	@param	InPlatform			The STTargetPlatform being built
         *	
         *	@return	string				The custom property import lines for the project file; Empty string if it doesn't require one
         */
        public virtual string GetAdditionalVisualStudioPropertyGroups(STTargetPlatform InPlatform)
        {
            return "";
        }

        /**
         * Return any custom property group lines
         *	
         *	@param	InPlatform			The STTargetPlatform being built
         *	
         *	@return	string				The platform configuration type.  Defaults to "Makefile" unless overridden
         */
        public virtual string GetVisualStudioPlatformConfigurationType(STTargetPlatform InPlatform)
        {
            return "Makefile";
        }

        /**
         * Return any custom paths for VisualStudio this platform requires
         * This include ReferencePath, LibraryPath, LibraryWPath, IncludePath and ExecutablePath.
         *	
         *	@param	InPlatform			The STTargetPlatform being built
         *	@param	TargetType			The type of target (game or program)
         *	
         *	@return	string				The custom path lines for the project file; Empty string if it doesn't require one
         */
        public virtual string GetVisualStudioPathsEntries(STTargetPlatform InPlatform, STTargetConfiguration InConfiguration, TargetRules.TargetType TargetType, string TargetRulesPath, string ProjectFilePath, string NMakeOutputPath)
        {
            // NOTE: We are intentionally overriding defaults for these paths with empty strings.  We never want Visual Studio's
            //       defaults for these fields to be propagated, since they are version-sensitive paths that may not reflect
            //       the environment that UBT is building in.  We'll set these environment variables ourselves!
            // NOTE: We don't touch 'ExecutablePath' because that would result in Visual Studio clobbering the system "Path"
            //       environment variable
            string PathsLines =
                "		<IncludePath />\n" +
                "		<ReferencePath />\n" +
                "		<LibraryPath />\n" +
                "		<LibraryWPath />\n" +
                "		<SourcePath />\n" +
                "		<ExcludePath />\n";

            return PathsLines;
        }

        /**
         * Return any custom property import lines
         *	
         *	@param	InPlatform			The STTargetPlatform being built
         *	
         *	@return	string				The custom property import lines for the project file; Empty string if it doesn't require one
         */
        public virtual string GetAdditionalVisualStudioImportSettings(STTargetPlatform InPlatform)
        {
            return "";
        }

        /**
         * Return any custom layout directory sections
         * 
         *	@param	InPlatform			The STTargetPlatform being built
         *	@param	TargetType			The type of target (game or program)
         *	
         *	@return	string				The custom property import lines for the project file; Empty string if it doesn't require one
         */
        public virtual string GetVisualStudioLayoutDirSection(STTargetPlatform InPlatform, STTargetConfiguration InConfiguration, string InConditionString, TargetRules.TargetType TargetType, string TargetRulesPath, string ProjectFilePath)
        {
            return "";
        }

        /**
         *	Get the output manifest section, if required
         *	
         *	@param	InPlatform			The STTargetPlatform being built
         *	
         *	@return	string				The output manifest section for the project file; Empty string if it doesn't require one
         */
        public virtual string GetVisualStudioOutputManifestSection(STTargetPlatform InPlatform, TargetRules.TargetType TargetType, string TargetRulesPath, string ProjectFilePath)
        {
            return "";
        }

        /**
         * Get whether this platform deploys 
         * 
         * @return	bool		true if the 'Deploy' option should be enabled
         */
        public virtual bool GetVisualStudioDeploymentEnabled(STTargetPlatform InPlatform, STTargetConfiguration InConfiguration)
        {
            return false;
        }

        /// <summary>
        /// Get the text to insert into the user file for the given platform/configuration/target
        /// </summary>
        /// <param name="InPlatform">The platform being added</param>
        /// <param name="InConfiguration">The configuration being added</param>
        /// <param name="InConditionString">The condition string </param>
        /// <param name="InTargetRules">The target rules </param>
        /// <param name="TargetRulesPath">The target rules path</param>
        /// <param name="ProjectFilePath">The project file path</param>
        /// <returns>The string to append to the user file</returns>
        public virtual string GetVisualStudioUserFileStrings(STTargetPlatform InPlatform, STTargetConfiguration InConfiguration,
            string InConditionString, TargetRules InTargetRules, string TargetRulesPath, string ProjectFilePath)
        {
            return "";
        }
    }
}
