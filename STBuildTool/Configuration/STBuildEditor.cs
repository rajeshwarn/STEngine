using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace STBuildTool
{
    [Serializable]
    public class STBuildEditor : STBuildTarget
    {
        public STBuildEditor(
            string InGameName,
            STTargetPlatform InPlatform,
            STTargetConfiguration InConfiguration,
            TargetRules InRulesObject,
            List<string> InAdditionalDefinitions,
            string InRemoteRoot,
            List<OnlyModule> InOnlyModules,
            bool bInEditorRecompile)
            // NOTE: If we're building a monolithic binary, then the game and engine code are linked together into one
            //       program executable, so we want the application name to be the game name.  In the case of a modular
            //       binary, we use 'UnrealEngine' for our application name
            : base(
                InAppName: STBuildTarget.GetBinaryBaseName(
                    InGameName,
                    InRulesObject,
                    InPlatform,
                    InConfiguration,
                    (InRulesObject.Type == TargetRules.TargetType.Editor) ? "Editor" : ""
                    ),
                InGameName: InGameName,
                InPlatform: InPlatform,
                InConfiguration: InConfiguration,
                InRulesObject: InRulesObject,
                InAdditionalDefinitions: InAdditionalDefinitions,
                InRemoteRoot: InRemoteRoot,
                InOnlyModules: InOnlyModules,
                bInEditorRecompile: bInEditorRecompile
            )
        {
        }

        /// <summary>
        /// Setup the binaries for this target
        /// </summary>
        protected override void SetupBinaries()
        {
            base.SetupBinaries();

            {
                // Make the editor executable.
                STBuildBinaryConfiguration Config = new STBuildBinaryConfiguration(InType: STBuildBinaryType.Executable,
                                                                                    InOutputFilePaths: OutputPaths,
                                                                                    InIntermediateDirectory: EngineIntermediateDirectory,
                                                                                    bInCreateImportLibrarySeparately: (ShouldCompileMonolithic() ? false : true),
                                                                                    bInAllowExports: !ShouldCompileMonolithic(),
                                                                                    InModuleNames: new List<string>() { "Launch" });

                AppBinaries.Add(new STBuildBinaryCPP(this, Config));
            }

            // Add the other modules that we want to compile along with the executable.  These aren't necessarily
            // dependencies to any of the other modules we're building, so we need to opt in to compile them.
            {
                // Modules should properly identify the 'extra modules' they need now.
                // There should be nothing here!
            }

            // Allow the platform to setup binaries
            STBuildPlatform.GetBuildPlatform(Platform).SetupBinaries(this);
        }

        public override void SetupDefaultGlobalEnvironment(
            TargetInfo Target,
            ref LinkEnvironmentConfiguration OutLinkEnvironmentConfiguration,
            ref CPPEnvironmentConfiguration OutCPPEnvironmentConfiguration
            )
        {
            STBuildConfiguration.bCompileLeanAndMeanUE = false;

            // Do not include the editor
            STBuildConfiguration.bBuildEditor = true;
            STBuildConfiguration.bBuildWithEditorOnlyData = true;

            // Require cooked data
            STBuildConfiguration.bBuildRequiresCookedData = false;

            // Compile the engine
            STBuildConfiguration.bCompileAgainstEngine = true;

            // Tag it as a 'Editor' build
            OutCPPEnvironmentConfiguration.Definitions.Add("UE_EDITOR=1");
        }
    }
}
