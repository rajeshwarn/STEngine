using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace STBuildTool
{
    public enum STTargetPlatform
    {
        Unknown,
        Win32,
        Win64,
        Mac,
        XboxOne,
        PS4,
        IOS,
        Android,
        WinRT,
        WinRT_ARM,
        HTML5,
        Linux,
    }

    public enum STPlatformGroup
    {
        Windows,	// this group is just to lump Win32 and Win64 into Windows directories, removing the special Windows logic in MakeListOfUnsupportedPlatforms
        Microsoft,
        Apple,
        Unix,
        Android,
        Sony,
        /*
        *  These two groups can be further used to conditionally compile files for a given platform. e.g
        *  Core/Private/HTML5/Simulator/<VC tool chain files>
        *  Core/Private/HTML5/Device/<emscripten toolchain files>.  
        *  Note: There's no default group - if the platform is not registered as device or simulator - both are rejected. 
        */
        Device,
        Simulator,
    }

    public enum STTargetConfiguration
    {
        Unknown,
        Debug,
        DebugGame,
        Development,
        Shipping,
        Test,
    }

    public enum STProjectType
    {
        CPlusPlus,	// C++ or C++/CLI
        CSharp,		// C#
    }

    public enum ErrorMode
    {
        Ignore,
        Suppress,
        Check,
    }

    public class OnlyModule
    {
        public OnlyModule(string InitOnlyModuleName)
        {
            OnlyModuleName = InitOnlyModuleName;
            OnlyModuleSuffix = String.Empty;
        }

        public OnlyModule(string InitOnlyModuleName, string InitOnlyModuleSuffix)
        {
            OnlyModuleName = InitOnlyModuleName;
            OnlyModuleSuffix = InitOnlyModuleSuffix;
        }

        /** If building only a single module, this is the module name to build */
        public readonly string OnlyModuleName;

        /** When building only a single module, the optional suffix for the module file name */
        public readonly string OnlyModuleSuffix;
    }

    public class TargetDescriptor
    {
        public string TargetName;
        public STTargetPlatform Platform;
        public STTargetConfiguration Configuration;
        public List<string> AdditionalDefinitions;
        public bool bIsEditorRecompile;
        public string RemoteRoot;
        public List<OnlyModule> OnlyModules;
    }

    [Serializable]
    class STBuildTarget
    {
        public static STBuildTarget CreateTarget(TargetDescriptor Desc)
        {
            string TargetName = Desc.TargetName;
            List<string> AdditionalDefinitions = Desc.AdditionalDefinitions;
            STTargetPlatform Platform = Desc.Platform;
            STTargetConfiguration Configuration = Desc.Configuration;
            string RemoteRoot = Desc.RemoteRoot;
            List<OnlyModule> OnlyModules = Desc.OnlyModules;
            bool bIsEditorRecompile = Desc.bIsEditorRecompile;
            STBuildTarget Target = RulesCompiler.CreateTarget(
            TargetName: TargetName,
            Target: new TargetInfo(Platform, Configuration),
            InAdditionalDefinitions: AdditionalDefinitions,
            InRemoteRoot: RemoteRoot,
            InOnlyModules: OnlyModules,
            bInEditorRecompile: bIsEditorRecompile);
            if (Target == null)
            {
                if (STBuildConfiguration.bCleanProject)
                {
                    return null;
                }
                throw new BuildException("Couldn't find target name {0}.", TargetName);
            }
            else
            {
                BuildTarget = Target;
            }
            return BuildTarget;
        }
    }
}
