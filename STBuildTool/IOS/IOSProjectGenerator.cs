using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace STBuildTool
{
    /**
     *	Base class for platform-specific project generators 
     */
    class IOSProjectGenerator : STPlatformProjectGenerator
    {
        /**
         *	Register the platform with the UEPlatformProjectGenerator class
         */
        public override void RegisterPlatformProjectGenerator()
        {
            // Register this project generator for Mac
            Log.TraceVerbose("        Registering for {0}", STTargetPlatform.IOS.ToString());
            STPlatformProjectGenerator.RegisterPlatformProjectGenerator(STTargetPlatform.IOS, this);
        }

        ///
        ///	VisualStudio project generation functions
        ///	
        /**
         *	Whether this build platform has native support for VisualStudio
         *	
         *	@param	InPlatform			The UnrealTargetPlatform being built
         *	@param	InConfiguration		The UnrealTargetConfiguration being built
         *	
         *	@return	bool				true if native VisualStudio support (or custom VSI) is available
         */
        public override bool HasVisualStudioSupport(STTargetPlatform InPlatform, STTargetConfiguration InConfiguration)
        {
            // iOS is not supported in VisualStudio
            return false;
        }
    }
}
