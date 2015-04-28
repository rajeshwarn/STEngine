﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace STBuildTool
{
    /// <summary>
    /// Represents a folder within the master project (e.g. Visual Studio solution)
    /// </summary>
    public class QMakefileFolder : MasterProjectFolder
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public QMakefileFolder(ProjectFileGenerator InitOwnerProjectFileGenerator, string InitFolderName)
            : base(InitOwnerProjectFileGenerator, InitFolderName)
        {
        }
    }

    public class QMakefileProjectFile : ProjectFile
    {
        public QMakefileProjectFile(string InitFilePath)
            : base(InitFilePath)
        {
        }
    }

    /// <summary>
    /// QMakefile project file generator implementation
    /// </summary>
    public class QMakefileGenerator : ProjectFileGenerator
    {
        /// File extension for project files we'll be generating (e.g. ".vcxproj")
        override public string ProjectFileExtension
        {
            get
            {
                return ".pro";
            }
        }

        protected override bool WriteMasterProjectFile(ProjectFile UBTProject)
        {
            bool bSuccess = true;
            return bSuccess;
        }

        private bool WriteQMakePro()
        {
            string GameProjectPath = "";
            string GameProjectFile = "";
            string GameProjectRootPath = "";

            string BuildCommand = "";

            string QMakeGameProjectFile = "";

            if (!String.IsNullOrEmpty(GameProjectName))
            {
                GameProjectPath = STBuildTool.GetUProjectPath();
                GameProjectFile = STBuildTool.GetUProjectFile();
                QMakeGameProjectFile = "gameProjectFile=" + GameProjectFile + "\n";
                BuildCommand = "build=mono $$unrealRootPath/Engine/Binaries/DotNET/STBuildTool.exe\n\n";
            }
            else
            {
                BuildCommand = "build=bash $$unrealRootPath/Engine/Build/BatchFiles/Linux/Build.sh\n";
            }

            var UnrealRootPath = Path.GetFullPath(ProjectFileGenerator.RootRelativePath);

            var FileName = MasterProjectName + ".pro";
            var QMakeFileContent = new StringBuilder();

            var QMakeSectionEnd = " \n\n";

            var QMakeSourceFilesList = "SOURCES += \\ \n";
            var QMakeHeaderFilesList = "HEADERS += \\ \n";
            var QMakeConfigFilesList = "OTHER_FILES += \\ \n";
            var QMakeTargetList = "QMAKE_EXTRA_TARGETS += \\ \n";

            if (!String.IsNullOrEmpty(GameProjectName))
            {
                GameProjectRootPath = GameProjectName + "RootPath=" + GameProjectPath + "\n\n";
            }

            QMakeFileContent.Append(
                "# UnrealEngine.pro generated by QMakefileGenerator.cs\n" +
                "# *DO NOT EDIT*\n\n" +
                "TEMPLATE = aux\n" +
                "CONFIG -= console\n" +
                "CONFIG -= app_bundle\n" +
                "CONFIG -= qt\n\n" +
                "TARGET = UE4 \n\n" +
                "unrealRootPath=" + UnrealRootPath + "\n" +
                GameProjectRootPath +
                QMakeGameProjectFile +
                BuildCommand +
                "args=$(ARGS)\n\n"
            );

            // Create SourceFiles, HeaderFiles, and ConfigFiles sections.
            var AllModuleFiles = DiscoverModules();
            foreach (string CurModuleFile in AllModuleFiles)
            {
                var FoundFiles = SourceFileSearch.FindModuleSourceFiles(CurModuleFile, ExcludeNoRedistFiles: bExcludeNoRedistFiles);
                foreach (string CurSourceFile in FoundFiles)
                {

                    string SourceFileRelativeToRoot = Utils.MakePathRelativeTo(CurSourceFile, Path.Combine(EngineRelativePath));
                    // Exclude Windows, Mac, and iOS only files/folders.
                    // This got ugly quick.
                    if (!SourceFileRelativeToRoot.Contains("Source/ThirdParty/") &&
                        !SourceFileRelativeToRoot.Contains("/Windows/") &&
                        !SourceFileRelativeToRoot.Contains("/Mac/") &&
                        !SourceFileRelativeToRoot.Contains("/IOS/") &&
                        !SourceFileRelativeToRoot.Contains("/iOS/") &&
                        !SourceFileRelativeToRoot.Contains("/VisualStudioSourceCodeAccess/") &&
                        !SourceFileRelativeToRoot.Contains("/XCodeSourceCodeAccess/") &&
                        !SourceFileRelativeToRoot.Contains("/WmfMedia/") &&
                        !SourceFileRelativeToRoot.Contains("/IOSDeviceProfileSelector/") &&
                        !SourceFileRelativeToRoot.Contains("/WindowsDeviceProfileSelector/") &&
                        !SourceFileRelativeToRoot.Contains("/WindowsMoviePlayer/") &&
                        !SourceFileRelativeToRoot.Contains("/AppleMoviePlayer/") &&
                        !SourceFileRelativeToRoot.Contains("/MacGraphicsSwitching/") &&
                        !SourceFileRelativeToRoot.Contains("/Apple/") &&
                        !SourceFileRelativeToRoot.Contains("/WinRT/")
                    )
                    {
                        if (SourceFileRelativeToRoot.EndsWith(".cpp"))
                        {
                            if (!SourceFileRelativeToRoot.StartsWith("..") && !Path.IsPathRooted(SourceFileRelativeToRoot))
                            {
                                // SourceFileRelativeToRoot = "Engine/" + SourceFileRelativeToRoot;
                                QMakeSourceFilesList += ("\t\"" + "$$unrealRootPath/Engine/" + SourceFileRelativeToRoot + "\" \\\n");
                            }
                            else
                            {
                                if (String.IsNullOrEmpty(GameProjectName))
                                {
                                    // SourceFileRelativeToRoot = SourceFileRelativeToRoot.Substring (3);
                                    QMakeSourceFilesList += ("\t\"" + SourceFileRelativeToRoot.Substring(3) + "\" \\\n");
                                }
                                else if (!String.IsNullOrEmpty(GameProjectName))
                                {
                                    QMakeSourceFilesList += ("\t\"$$" + GameProjectName + "RootPath/" + Utils.MakePathRelativeTo(CurSourceFile, GameProjectPath) + "\" \\\n");
                                }
                                else
                                {
                                    System.Console.WriteLine("Error!, you should not be here.");
                                }
                            }
                        }
                        if (SourceFileRelativeToRoot.EndsWith(".h"))
                        {
                            if (!SourceFileRelativeToRoot.StartsWith("..") && !Path.IsPathRooted(SourceFileRelativeToRoot))
                            {
                                // SourceFileRelativeToRoot = "Engine/" + SourceFileRelativeToRoot;
                                QMakeHeaderFilesList += ("\t\"" + "$$unrealRootPath/Engine/" + SourceFileRelativeToRoot + "\" \\\n");
                            }
                            else
                            {
                                if (String.IsNullOrEmpty(GameProjectName))
                                {
                                    // SourceFileRelativeToRoot = SourceFileRelativeToRoot.Substring (3);
                                    QMakeHeaderFilesList += ("\t\"" + SourceFileRelativeToRoot.Substring(3) + "\" \\\n");
                                }
                                else if (!String.IsNullOrEmpty(GameProjectName))
                                {
                                    QMakeHeaderFilesList += ("\t\"$$" + GameProjectName + "RootPath/" + Utils.MakePathRelativeTo(CurSourceFile, GameProjectPath) + "\" \\\n");
                                }
                                else
                                {
                                    System.Console.WriteLine("Error!, you should not be here.");
                                }
                            }
                        }
                        if (SourceFileRelativeToRoot.EndsWith(".cs"))
                        {
                            if (!SourceFileRelativeToRoot.StartsWith("..") && !Path.IsPathRooted(SourceFileRelativeToRoot))
                            {
                                // SourceFileRelativeToRoot = "Engine/" + SourceFileRelativeToRoot;
                                QMakeConfigFilesList += ("\t\"" + "$$unrealRootPath/Engine/" + SourceFileRelativeToRoot + "\" \\\n");

                            }
                            else
                            {
                                if (String.IsNullOrEmpty(GameProjectName))
                                {
                                    // SourceFileRelativeToRoot = SourceFileRelativeToRoot.Substring (3);
                                    QMakeConfigFilesList += ("\t\"" + SourceFileRelativeToRoot.Substring(3) + "\" \\\n");
                                }
                                else if (!String.IsNullOrEmpty(GameProjectName))
                                {
                                    QMakeConfigFilesList += ("\t\"$$" + GameProjectName + "RootPath/" + Utils.MakePathRelativeTo(CurSourceFile, GameProjectPath) + "\" \\\n");
                                }
                                else
                                {
                                    System.Console.WriteLine("Error!, you should not be here.");
                                }
                            }
                        }
                    }
                }

            }

            // Add section end to section strings;
            QMakeSourceFilesList += QMakeSectionEnd;
            QMakeHeaderFilesList += QMakeSectionEnd;
            QMakeConfigFilesList += QMakeSectionEnd;

            // Append sections to the QMakeLists.txt file
            QMakeFileContent.Append(QMakeSourceFilesList);
            QMakeFileContent.Append(QMakeHeaderFilesList);
            QMakeFileContent.Append(QMakeConfigFilesList);

            string QMakeProjectCmdArg = "";

            foreach (string TargetFilePath in DiscoverTargets())
            {
                var TargetName = Utils.GetFilenameWithoutAnyExtensions(TargetFilePath);		// Remove both ".cs" and ".

                foreach (STTargetConfiguration CurConfiguration in Enum.GetValues(typeof(STTargetConfiguration)))
                {
                    if (CurConfiguration != STTargetConfiguration.Unknown && CurConfiguration != STTargetConfiguration.Development)
                    {
                        if (STBuildTool.IsValidConfiguration(CurConfiguration))
                        {

                            if (TargetName == GameProjectName || TargetName == (GameProjectName + "Editor"))
                            {
                                QMakeProjectCmdArg = " -project=\"\\\"$$gameProjectFile\\\"\"";
                            }
                            var ConfName = Enum.GetName(typeof(STTargetConfiguration), CurConfiguration);
                            QMakeFileContent.Append(String.Format("{0}-Linux-{1}.commands = $$build {2} {0} Linux {1} $$args\n", TargetName, ConfName, QMakeProjectCmdArg));
                            QMakeTargetList += "\t" + TargetName + "-Linux-" + ConfName + " \\\n"; // , TargetName, ConfName);
                        }
                    }
                }

                if (TargetName == GameProjectName || TargetName == (GameProjectName + "Editor"))
                {
                    QMakeProjectCmdArg = " -project=\"\\\"$$gameProjectFile\\\"\"";
                }

                QMakeFileContent.Append(String.Format("{0}.commands = $$build {1} {0} Linux Development $$args\n\n", TargetName, QMakeProjectCmdArg));
                QMakeTargetList += "\t" + TargetName + " \\\n";
            }

            QMakeFileContent.Append(QMakeTargetList.TrimEnd('\\'));

            var FullFileName = Path.Combine(MasterProjectRelativePath, FileName);
            return WriteFileIfChanged(FullFileName, QMakeFileContent.ToString());
        }

        /// ProjectFileGenerator interface
        //protected override bool WriteMasterProjectFile( ProjectFile UBTProject )
        protected override bool WriteProjectFiles()
        {
            return WriteQMakePro();
        }

        /// ProjectFileGenerator interface
        public override MasterProjectFolder AllocateMasterProjectFolder(ProjectFileGenerator InitOwnerProjectFileGenerator, string InitFolderName)
        {
            return new QMakefileFolder(InitOwnerProjectFileGenerator, InitFolderName);
        }

        /// ProjectFileGenerator interface
        /// <summary>
        /// Allocates a generator-specific project file object
        /// </summary>
        /// <param name="InitFilePath">Path to the project file</param>
        /// <returns>The newly allocated project file object</returns>
        protected override ProjectFile AllocateProjectFile(string InitFilePath)
        {
            return new QMakefileProjectFile(InitFilePath);
        }

        /// ProjectFileGenerator interface
        public override void CleanProjectFiles(string InMasterProjectRelativePath, string InMasterProjectName, string InIntermediateProjectFilesPath)
        {
        }
    }
}
