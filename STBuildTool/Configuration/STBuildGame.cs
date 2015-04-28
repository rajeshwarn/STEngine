using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace STBuildTool
{
    [Serializable]
    public class STBuildGame : STBuildTarget
    {
        public STBuildGame(
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
                InAppName: STBuildTarget.GetBinaryBaseName(InGameName, InRulesObject, InPlatform, InConfiguration, ""),
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
            if (ShouldCompileMonolithic())
            {
                if ((STBuildTool.IsDesktopPlatform(Platform) == false) ||
                    (Platform == STTargetPlatform.WinRT) ||
                    (Platform == STTargetPlatform.WinRT_ARM))
                {
                    // We are compiling for a console...
                    // We want the output to go into the <GAME>\Binaries folder
                    if (InRulesObject.bOutputToEngineBinaries == false)
                    {
                        for (int Index = 0; Index < OutputPaths.Length; Index++)
                        {
                            OutputPaths[Index] = OutputPaths[Index].Replace("Engine\\Binaries", InGameName + "\\Binaries");
                        }
                    }
                }
            }
        }


        //
        // UEBuildTarget interface.
        //


        protected override void SetupModules()
        {
            base.SetupModules();
        }


        /// <summary>
        /// Setup the binaries for this target
        /// </summary>
        protected override void SetupBinaries()
        {
            base.SetupBinaries();

            {
                // Make the game executable.
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
        }

        public override void SetupDefaultGlobalEnvironment(
            TargetInfo Target,
            ref LinkEnvironmentConfiguration OutLinkEnvironmentConfiguration,
            ref CPPEnvironmentConfiguration OutCPPEnvironmentConfiguration
            )
        {
            STBuildConfiguration.bCompileLeanAndMeanUE = true;

            // Do not include the editor
            STBuildConfiguration.bBuildEditor = false;
            STBuildConfiguration.bBuildWithEditorOnlyData = false;

            // Require cooked data
            STBuildConfiguration.bBuildRequiresCookedData = true;

            // Compile the engine
            STBuildConfiguration.bCompileAgainstEngine = true;

            // Tag it as a 'Game' build
            OutCPPEnvironmentConfiguration.Definitions.Add("UE_GAME=1");

            // no exports, so no need to verify that a .lib and .exp file was emitted by the linker.
            OutLinkEnvironmentConfiguration.bHasExports = false;
        }
    }
}
