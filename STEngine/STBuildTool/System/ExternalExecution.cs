using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace STBuildTool
{
    public enum ECompilationResult
    {
        /** All targets were up to date, used only with -canskiplink */
        UpToDate = -2,
        /** Build was canceled, this is used on the engine side only */
        Canceled = -1,
        /** Compilation succeeded */
        Succeeded = 0,
        /** Compilation failed because generated code changed which was not supported */
        FailedDueToHeaderChange = 1,
        /** Compilation failed due to compilation errors */
        OtherCompilationError = 2,
        /** The process has most likely crashed. This is what UE returns in case of an assert */
        CrashOrAssert = 3,
        /** Compilation is not supported in the current build */
        Unsupported,
        /** Unknown error */
        Unknown
    }
    class ExternalExecution
    {
    }
}
