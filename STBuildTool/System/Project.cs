﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Linq;
using System.Linq;
using System.Text;

namespace STBuildTool
{
    public interface IntelliSenseGatherer
    {
        /// <summary>
        /// Adds all of the specified preprocessor definitions to this VCProject's list of preprocessor definitions for all modules in the project
        /// </summary>
        /// <param name="NewPreprocessorDefinitions">List of preprocessor definitons to add</param>
        void AddIntelliSensePreprocessorDefinitions(List<string> NewPreprocessorDefinitions);

        /// <summary>
        /// Adds all of the specified include paths to this VCProject's list of include paths for all modules in the project
        /// </summary>
        /// <param name="NewIncludePaths">List of include paths to add</param>
        /// <param name="bAddingSystemIncludes">Are the include paths to add system include paths</param>
        void AddInteliiSenseIncludePaths(HashSet<string> NewIncludePaths, bool bAddingSystemIncludes);
    }

    /// <summary>
    /// A single target within a project.  A project may have any number of targets within it, which are basically compilable projects
    /// in themselves that the project wraps up.
    /// </summary>
    public class ProjectTarget
    {
        /// The target rules file path on disk, if we have one
        public string TargetFilePath;

        /// Optional target rules for this target.  If the target came from a *.Target.cs file on disk, then it will have one of these.
        /// For targets that are synthetic (like STBuildTool or other manually added project files) we won't have a rules object for those.
        public TargetRules TargetRules;

        /// Extra supported build platforms.  Normally the target rules determines these, but for synthetic targets we'll add them here.
        public List<STTargetPlatform> ExtraSupportedPlatforms = new List<STTargetPlatform>();

        /// Extra supported build configurations.  Normally the target rules determines these, but for synthetic targets we'll add them here.
        public List<STTargetConfiguration> ExtraSupportedConfigurations = new List<STTargetConfiguration>();

        /// If true, forces Development configuration regardless of which configuration is set as the Solution Configuration
        public bool ForceDevelopmentConfiguration = false;

        /// Whether the project requires 'Deploy' option set (VC projects)
        public bool ProjectDeploys = false;

        public override string ToString()
        {
            return Path.GetFileNameWithoutExtension(TargetFilePath);
        }
    }

    /// <summary>
    /// Class that stores info about aliased file.
    /// </summary>
    public class AliasedFile
    {
        public AliasedFile(string FileSystemPath, string ProjectPath)
        {
            this.FileSystemPath = FileSystemPath;
            this.ProjectPath = ProjectPath;
        }

        // File system path.
        public string FileSystemPath { get; private set; }

        // Project path.
        public string ProjectPath { get; private set; }
    }

    public abstract class ProjectFile : IntelliSenseGatherer
    {
        /// <summary>
        /// Represents a single source file (or other type of file) in a project
        /// </summary>
        public class SourceFile
        {
            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="InitFilePath">Path to the source file on disk</param>
            /// <param name="InitRelativeBaseFolder">The directory on this the path within the project will be relative to</param>
            public SourceFile(string InitFilePath, string InitRelativeBaseFolder)
            {
                FilePath = InitFilePath;
                RelativeBaseFolder = InitRelativeBaseFolder;
            }

            public SourceFile()
            {
            }

            /// <summary>
            /// File path to file on disk
            /// </summary>
            public string FilePath
            {
                get;
                private set;
            }

            /// <summary>
            /// Optional directory that overrides where files in this project are relative to when displayed in the IDE.  If null, will default to the project's RelativeBaseFolder.
            /// </summary>
            public string RelativeBaseFolder
            {
                get;
                private set;
            }
        }


        /// <summary>
        /// Constructs a new project file object
        /// </summary>
        /// <param name="InitFilePath">The path to the project file, relative to the master project file</param>
        protected ProjectFile(string InitRelativeFilePath)
        {
            RelativeProjectFilePath = InitRelativeFilePath;
        }


        /// Full path to the project file on disk
        public string ProjectFilePath
        {
            get
            {
                return Path.Combine(ProjectFileGenerator.MasterProjectRelativePath, RelativeProjectFilePath);
            }
        }


        /// Project file path, relative to the master project
        public string RelativeProjectFilePath
        {
            get;
            private set;
        }


        /// Returns true if this is a generated project (as opposed to an imported project)
        public bool IsGeneratedProject
        {
            get;
            set;
        }


        /// Returns true if this is a "stub" project.  Stub projects function as dumb containers for source files
        /// and are never actually "built" by the master project.  Stub projects are always "generated" projects.
        public bool IsStubProject
        {
            get;
            set;
        }

        /// Returns true if this is a foreign project, and requires UBT to be passed the path to the .uproject file 
        /// on the command line.
        public bool IsForeignProject
        {
            get;
            set;
        }

        /// Whether this project should be built for all solution targets
        public bool ShouldBuildForAllSolutionTargets
        {
            get;
            set;
        }




        /// All of the targets in this project.  All non-stub projects must have at least one target.
        public readonly List<ProjectTarget> ProjectTargets = new List<ProjectTarget>();



        /// <summary>
        /// Adds a list of files to this project, ignoring dupes
        /// </summary>
        /// <param name="FilesToAdd">Files to add</param>
        /// <param name="RelativeBaseFolder">The directory the path within the project will be relative to</param>
        public void AddFilesToProject(List<string> FilesToAdd, string RelativeBaseFolder)
        {
            foreach (var CurFile in FilesToAdd)
            {
                AddFileToProject(CurFile, RelativeBaseFolder);
            }
        }

        /// Aliased (i.e. files is custom filter tree) in this project
        public readonly List<AliasedFile> AliasedFiles = new List<AliasedFile>();

        /// <summary>
        /// Adds aliased file to the project.
        /// </summary>
        /// <param name="File">Aliased file.</param>
        public void AddAliasedFileToProject(AliasedFile File)
        {
            AliasedFiles.Add(File);
        }

        /// <summary>
        /// Adds a file to this project, ignoring dupes
        /// </summary>
        /// <param name="FilePath">Path to the file on disk</param>
        /// <param name="RelativeBaseFolder">The directory the path within the project will be relative to</param>
        public void AddFileToProject(string FilePath, string RelativeBaseFolder)
        {
            // Don't add duplicates
            SourceFile ExistingFile = null;
            if (SourceFileMap.TryGetValue(FilePath, out ExistingFile))
            {
                if (ExistingFile.RelativeBaseFolder != RelativeBaseFolder)
                {
                    if ((ExistingFile.RelativeBaseFolder != null) != (RelativeBaseFolder != null) ||
                        !ExistingFile.RelativeBaseFolder.Equals(RelativeBaseFolder, StringComparison.InvariantCultureIgnoreCase))
                    {
                        throw new BuildException("Trying to add file '" + FilePath + "' to project '" + ProjectFilePath + "' when the file already exists, but with a different relative base folder '" + RelativeBaseFolder + "' is different than the current file's '" + ExistingFile.RelativeBaseFolder + "'!");
                    }
                    else
                    {
                        throw new BuildException("Trying to add file '" + FilePath + "' to project '" + ProjectFilePath + "' when the file already exists, but the specified project relative base folder is different than the current file's!");
                    }
                }
            }
            else
            {
                SourceFile File = AllocSourceFile(FilePath, RelativeBaseFolder);
                if (File != null)
                {
                    SourceFileMap[FilePath] = File;
                    SourceFiles.Add(File);
                }
            }
        }

        /// <summary>
        /// Splits the definition text into macro name and value (if any).
        /// </summary>
        /// <param name="Definition">Definition text</param>
        /// <param name="Key">Out: The definition name</param>
        /// <param name="Value">Out: The definition value or null if it has none</param>
        /// <returns>Pair representing macro name and value.</returns>
        private void SplitDefinitionAndValue(string Definition, out String Key, out String Value)
        {
            int EqualsIndex = Definition.IndexOf('=');
            if (EqualsIndex >= 0)
            {
                Key = Definition.Substring(0, EqualsIndex);
                Value = Definition.Substring(EqualsIndex + 1);
            }
            else
            {
                Key = Definition;
                Value = "";
            }
        }

        /// <summary>
        /// Adds all of the specified preprocessor definitions to this VCProject's list of preprocessor definitions for all modules in the project
        /// </summary>
        /// <param name="NewPreprocessorDefinitions">List of preprocessor definitons to add</param>
        public void AddIntelliSensePreprocessorDefinitions(List<string> NewPreprocessorDefinitions)
        {
            if (ProjectFileGenerator.OnlyGenerateIntelliSenseDataForProject == null ||
                ProjectFileGenerator.OnlyGenerateIntelliSenseDataForProject == this)
            {
                foreach (var CurDef in NewPreprocessorDefinitions)
                {
                    // Don't add definitions and value combinations that have already been added for this project
                    if (KnownIntelliSensePreprocessorDefinitions.Add(CurDef))
                    {
                        // Go ahead and check to see if the definition already exists, but the value is different
                        var AlreadyExists = false;

                        string Def, Value;
                        SplitDefinitionAndValue(CurDef, out Def, out Value);
                        for (int DefineIndex = 0; DefineIndex < IntelliSensePreprocessorDefinitions.Count; ++DefineIndex)
                        {
                            string ExistingDef, ExistingValue;
                            SplitDefinitionAndValue(IntelliSensePreprocessorDefinitions[DefineIndex], out ExistingDef, out ExistingValue);
                            if (ExistingDef == Def)
                            {
                                // Already exists, but the value is changing.  We don't bother clobbering values for existing defines for this project.
                                AlreadyExists = true;
                                break;
                            }
                        }

                        if (!AlreadyExists)
                        {
                            IntelliSensePreprocessorDefinitions.Add(CurDef);
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Adds all of the specified include paths to this VCProject's list of include paths for all modules in the project
        /// </summary>
        /// <param name="NewIncludePaths">List of include paths to add</param>
        public void AddInteliiSenseIncludePaths(HashSet<string> NewIncludePaths, bool bAddingSystemIncludes)
        {
            if (ProjectFileGenerator.OnlyGenerateIntelliSenseDataForProject == null ||
                ProjectFileGenerator.OnlyGenerateIntelliSenseDataForProject == this)
            {
                foreach (var CurPath in NewIncludePaths)
                {
                    if (bAddingSystemIncludes ? KnownIntelliSenseSystemIncludeSearchPaths.Add(CurPath) : KnownIntelliSenseIncludeSearchPaths.Add(CurPath))
                    {
                        string PathRelativeToProjectFile;

                        // If the include string is an environment variable (e.g. $(DXSDK_DIR)), then we never want to
                        // give it a relative path
                        if (CurPath.StartsWith("$("))
                        {
                            PathRelativeToProjectFile = CurPath;
                        }
                        else
                        {
                            // Incoming include paths are relative to the solution directory, but we need these paths to be
                            // relative to the project file's directory
                            PathRelativeToProjectFile = NormalizeProjectPath(CurPath);
                        }

                        // Trim any trailing slash
                        PathRelativeToProjectFile = PathRelativeToProjectFile.TrimEnd('/', '\\');

                        // Make sure that it doesn't exist already
                        var AlreadyExists = false;
                        List<string> SearchPaths = bAddingSystemIncludes ? IntelliSenseSystemIncludeSearchPaths : IntelliSenseIncludeSearchPaths;
                        foreach (var ExistingPath in SearchPaths)
                        {
                            if (PathRelativeToProjectFile == ExistingPath)
                            {
                                AlreadyExists = true;
                                break;
                            }
                        }

                        if (!AlreadyExists)
                        {
                            SearchPaths.Add(PathRelativeToProjectFile);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Add the given project to the DepondsOn project list.
        /// </summary>
        /// <param name="InProjectFile">The project this project is dependent on</param>
        public void AddDependsOnProject(ProjectFile InProjectFile)
        {
            // Make sure that it doesn't exist already
            var AlreadyExists = false;
            foreach (var ExistingDependentOn in DependsOnProjects)
            {
                if (ExistingDependentOn == InProjectFile)
                {
                    AlreadyExists = true;
                    break;
                }
            }

            if (AlreadyExists == false)
            {
                DependsOnProjects.Add(InProjectFile);
            }
        }

        /// <summary>
        /// Writes a project file to disk
        /// </summary>
        /// <param name="InPlatforms">The platforms to write the project files for</param>
        /// <param name="InConfigurations">The configurations to add to the project files</param>
        /// <returns>True on success</returns>
        public virtual bool WriteProjectFile(List<STTargetPlatform> InPlatforms, List<STTargetConfiguration> InConfigurations)
        {
            throw new BuildException("STBuildTool cannot automatically generate this project type because WriteProjectFile() was not overridden.");
        }

        public virtual void LoadGUIDFromExistingProject()
        {
        }

        /// <summary>
        /// Allocates a generator-specific source file object
        /// </summary>
        /// <param name="InitFilePath">Path to the source file on disk</param>
        /// <param name="InitProjectSubFolder">Optional sub-folder to put the file in.  If empty, this will be determined automatically from the file's path relative to the project file</param>
        /// <returns>The newly allocated source file object</returns>
        public virtual SourceFile AllocSourceFile(string InitFilePath, string InitProjectSubFolder = null)
        {
            return new SourceFile(InitFilePath, InitProjectSubFolder);
        }

        /** Takes the given path and tries to rebase it relative to the project or solution directory variables. */
        public string NormalizeProjectPath(string InputPath)
        {
            // If the path is rooted in an environment variable, leave it be.
            if (InputPath.StartsWith("$("))
            {
                return InputPath;
            }

            // Otherwise make sure it's absolute
            string FullInputPath = Utils.CleanDirectorySeparators(Path.GetFullPath(InputPath));

            // Try to make it relative to the solution directory.
            string FullSolutionPath = Utils.CleanDirectorySeparators(Path.GetFullPath(ProjectFileGenerator.MasterProjectRelativePath));
            if (FullSolutionPath.Last() != Path.DirectorySeparatorChar)
            {
                FullSolutionPath += Path.DirectorySeparatorChar;
            }
            if (FullInputPath.StartsWith(FullSolutionPath))
            {
                FullInputPath = Utils.MakePathRelativeTo(Utils.CleanDirectorySeparators(FullInputPath), Path.GetDirectoryName(Path.GetFullPath(ProjectFilePath)));
            }
            else
            {
                FullInputPath = Utils.CleanDirectorySeparators(FullInputPath);
            }

            // Otherwise return the input
            return FullInputPath;
        }

        /** Takes the given path, normalizes it, and quotes it if necessary. */
        public string EscapePath(string InputPath)
        {
            string Result = InputPath;
            if (Result.Contains(' '))
            {
                Result = "\"" + Result + "\"";
            }
            return Result;
        }

        /** Visualizer for the debugger */
        public override string ToString()
        {
            return RelativeProjectFilePath;
        }

        /// Map of file paths to files in the project.
        private readonly Dictionary<string, SourceFile> SourceFileMap = new Dictionary<string, SourceFile>(StringComparer.InvariantCultureIgnoreCase);

        /// Files in this project
        public readonly List<SourceFile> SourceFiles = new List<SourceFile>();

        /// Include paths for every single module in the project file, merged together
        public readonly List<string> IntelliSenseIncludeSearchPaths = new List<string>();
        public readonly List<string> IntelliSenseSystemIncludeSearchPaths = new List<string>();
        public readonly HashSet<string> KnownIntelliSenseIncludeSearchPaths = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        public readonly HashSet<string> KnownIntelliSenseSystemIncludeSearchPaths = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

        /// Preprocessor definitions for every single module in the project file, merged together
        public readonly List<string> IntelliSensePreprocessorDefinitions = new List<string>();
        public readonly HashSet<string> KnownIntelliSensePreprocessorDefinitions = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

        /// Projects that this project is dependent on
        public readonly List<ProjectFile> DependsOnProjects = new List<ProjectFile>();
    }
}
