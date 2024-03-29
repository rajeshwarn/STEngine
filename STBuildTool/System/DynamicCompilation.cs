﻿using System;
using System.Collections.Generic;
using System.IO;
using System.CodeDom.Compiler;
using Microsoft.CSharp;
using System.Reflection;
using System.Diagnostics;

namespace STBuildTool
{
    public class DynamicCompilation
    {
        /// File information for UnrealBuildTool.exe, cached at program start
        private static FileInfo UBTExecutableFileInfo = new FileInfo(Utils.GetExecutingAssemblyLocation());

        /*
         * Checks to see if the assembly needs compilation
         */
        private static bool RequiresCompilation(List<string> SourceFileNames, string AssemblySourceListFilePath, string OutputAssemblyPath)
        {
            if (STBuildTool.RunningRocket() && ProjectFileGenerator.bGenerateProjectFiles)
            {
                // @todo rocket Do we need a better way to determine if project generation rules modules need to be compiled?
                return true;
            }

            // Check to see if we already have a compiled assembly file on disk
            FileInfo OutputAssemblyInfo = new FileInfo(OutputAssemblyPath);
            if (OutputAssemblyInfo.Exists)
            {
                // Check the time stamp of the UnrealBuildTool.exe file.  If Unreal Build Tool was compiled more
                // recently than the dynamically-compiled assembly, then we'll always recompile it.  This is
                // because Unreal Build Tool's code may have changed in such a way that invalidate these
                // previously-compiled assembly files.
                if (UBTExecutableFileInfo.LastWriteTimeUtc > OutputAssemblyInfo.LastWriteTimeUtc)
                {
                    // UnrealBuildTool.exe has been recompiled more recently than our cached assemblies
                    Log.TraceVerbose("UnrealBuildTool.exe has been recompiled more recently than " + OutputAssemblyInfo.Name);

                    return true;
                }
                else
                {
                    // Make sure we have a manifest of source files used to compile the output assembly.  If it doesn't exist
                    // for some reason (not an expected case) then we'll need to recompile.
                    var AssemblySourceListFile = new FileInfo(AssemblySourceListFilePath);
                    if (!AssemblySourceListFile.Exists)
                    {
                        return true;
                    }
                    else
                    {
                        // Make sure the source files we're compiling are the same as the source files that were compiled
                        // for the assembly that we want to load
                        var ExistingAssemblySourceFileNames = new List<string>();
                        {
                            using (var Reader = AssemblySourceListFile.OpenRead())
                            {
                                using (var TextReader = new StreamReader(Reader))
                                {
                                    for (var ExistingSourceFileName = TextReader.ReadLine(); ExistingSourceFileName != null; ExistingSourceFileName = TextReader.ReadLine())
                                    {
                                        ExistingAssemblySourceFileNames.Add(ExistingSourceFileName);

                                        // Was the existing assembly compiled with a source file that we aren't interested in?  If so, then it needs to be recompiled.
                                        if (!SourceFileNames.Contains(ExistingSourceFileName))
                                        {
                                            return true;
                                        }
                                    }
                                }
                            }
                        }

                        // Test against source file time stamps
                        foreach (var SourceFileName in SourceFileNames)
                        {
                            // Was the existing assembly compiled without this source file?  If so, then we definitely need to recompile it!
                            if (!ExistingAssemblySourceFileNames.Contains(SourceFileName))
                            {
                                return true;
                            }

                            var SourceFileInfo = new FileInfo(Path.GetFullPath(SourceFileName));

                            // Check to see if the source file exists
                            if (!SourceFileInfo.Exists)
                            {
                                throw new BuildException("Could not locate source file for dynamic compilation: {0}", SourceFileName);
                            }

                            // Ignore temp files
                            if (!SourceFileInfo.Extension.Equals(".tmp", StringComparison.CurrentCultureIgnoreCase))
                            {
                                // Check to see if the source file is newer than the compiled assembly file.  We don't want to
                                // bother recompiling it if it hasn't changed.
                                if (SourceFileInfo.LastWriteTimeUtc > OutputAssemblyInfo.LastWriteTimeUtc)
                                {
                                    // Source file has changed since we last compiled the assembly, so we'll need to recompile it now!
                                    Log.TraceVerbose(SourceFileInfo.Name + " has been modified more recently than " + OutputAssemblyInfo.Name);

                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                // File doesn't exist, so we'll definitely have to compile it!
                Log.TraceVerbose(OutputAssemblyInfo.Name + " doesn't exist yet");
                return true;
            }

            return false;
        }

        /*
         * Compiles an assembly from source files
         */
        private static Assembly CompileAssembly(string OutputAssemblyPath, List<string> SourceFileNames, List<string> ReferencedAssembies, List<string> PreprocessorDefines = null, bool TreatWarningsAsErrors = false)
        {
            var TemporaryFiles = new TempFileCollection();

            // Setup compile parameters
            var CompileParams = new CompilerParameters();
            {
                // Always compile the assembly to a file on disk, so that we can load a cached version later if we have one
                CompileParams.GenerateInMemory = false;

                // This is the full path to the assembly file we're generating
                CompileParams.OutputAssembly = OutputAssemblyPath;

                // We always want to generate a class library, not an executable
                CompileParams.GenerateExecutable = false;

                // Always fail compiles for warnings
                CompileParams.TreatWarningsAsErrors = true;

                // Always generate debug information as it takes minimal time
                CompileParams.IncludeDebugInformation = true;
#if !DEBUG
                // Optimise the managed code in Development
                CompileParams.CompilerOptions += " /optimize";
#endif
                Log.TraceVerbose("Compiling " + OutputAssemblyPath);

                // Keep track of temporary files emitted by the compiler so we can clean them up later
                CompileParams.TempFiles = TemporaryFiles;

                // Warnings as errors if desired
                CompileParams.TreatWarningsAsErrors = TreatWarningsAsErrors;

                // Add assembly references
                {
                    if (ReferencedAssembies == null)
                    {
                        // Always depend on the CLR System assembly
                        CompileParams.ReferencedAssemblies.Add("System.dll");
                    }
                    else
                    {
                        // Add in the set of passed in referenced assemblies
                        CompileParams.ReferencedAssemblies.AddRange(ReferencedAssembies.ToArray());
                    }

                    // The assembly will depend on this application
                    var UnrealBuildToolAssembly = Assembly.GetExecutingAssembly();
                    CompileParams.ReferencedAssemblies.Add(UnrealBuildToolAssembly.Location);
                }

                // Add preprocessor definitions
                if (PreprocessorDefines != null && PreprocessorDefines.Count > 0)
                {
                    CompileParams.CompilerOptions += " /define:";
                    for (int DefinitionIndex = 0; DefinitionIndex < PreprocessorDefines.Count; ++DefinitionIndex)
                    {
                        if (DefinitionIndex > 0)
                        {
                            CompileParams.CompilerOptions += ";";
                        }
                        CompileParams.CompilerOptions += PreprocessorDefines[DefinitionIndex];
                    }
                }

                // @todo: Consider embedding resources in generated assembly file (version/copyright/signing)
            }

            // Create the output directory if it doesn't exist already
            DirectoryInfo DirInfo = new DirectoryInfo(Path.GetDirectoryName(OutputAssemblyPath));
            if (!DirInfo.Exists)
            {
                try
                {
                    DirInfo.Create();
                }
                catch (Exception Ex)
                {
                    throw new BuildException(Ex, "Unable to create directory '{0}' for intermediate assemblies (Exception: {1})", OutputAssemblyPath, Ex.Message);
                }
            }

            // Compile the code
            CompilerResults CompileResults;
            try
            {
                // Enable .NET 4.0 as we want modern language features like 'var'
                var ProviderOptions = new Dictionary<string, string>() { { "CompilerVersion", "v4.0" } };
                var Compiler = new CSharpCodeProvider(ProviderOptions);
                CompileResults = Compiler.CompileAssemblyFromFile(CompileParams, SourceFileNames.ToArray());
            }
            catch (Exception Ex)
            {
                throw new BuildException(Ex, "Failed to launch compiler to compile assembly from source files '{0}' (Exception: {1})", SourceFileNames.ToString(), Ex.Message);
            }

            // Display compilation errors
            if (CompileResults.Errors.Count > 0)
            {
                Log.TraceInformation("Errors detected while compiling {0}:", OutputAssemblyPath);
                foreach (var CurError in CompileResults.Errors)
                {
                    Log.TraceInformation(CurError.ToString());
                }
                throw new BuildException("UnrealBuildTool encountered an error while compiling source files");
            }

            // Grab the generated assembly
            Assembly CompiledAssembly = CompileResults.CompiledAssembly;
            if (CompiledAssembly == null)
            {
                throw new BuildException("UnrealBuildTool was unable to compile an assembly for '{0}'", SourceFileNames.ToString());
            }

            // Clean up temporary files that the compiler saved
            TemporaryFiles.Delete();

            return CompiledAssembly;
        }

        /// <summary>
        /// Dynamically compiles an assembly for the specified source file and loads that assembly into the application's
        /// current domain.  If an assembly has already been compiled and is not out of date, then it will be loaded and
        /// no compilation is necessary.
        /// </summary>
        /// <param name="SourceFileNames">List of source file name</param>
        /// <param name="OutputAssemblyPath">Full path to the assembly to be created</param>
        /// <returns>The assembly that was loaded</returns>
        public static Assembly CompileAndLoadAssembly(string OutputAssemblyPath, List<string> SourceFileNames, List<string> ReferencedAssembies = null, List<string> PreprocessorDefines = null, bool DoNotCompile = false, bool TreatWarningsAsErrors = false)
        {
            // Load assembly requires absolute paths
            OutputAssemblyPath = Path.GetFullPath(OutputAssemblyPath);

            // Check to see if the resulting assembly is compiled and up to date
            var AssemblySourcesListFilePath = Path.Combine(Path.GetDirectoryName(OutputAssemblyPath), Path.GetFileNameWithoutExtension(OutputAssemblyPath) + "SourceFiles.txt");
            bool bNeedsCompilation = false;
            if (!DoNotCompile)
            {
                bNeedsCompilation = RequiresCompilation(SourceFileNames, AssemblySourcesListFilePath, OutputAssemblyPath);
            }

            // Load the assembly to ensure it is correct
            Assembly CompiledAssembly = null;
            if (!bNeedsCompilation)
            {
                try
                {
                    // Load the previously-compiled assembly from disk
                    CompiledAssembly = Assembly.LoadFile(OutputAssemblyPath);
                }
                catch (FileLoadException Ex)
                {
                    Log.TraceInformation(String.Format("Unable to load the previously-compiled assembly file '{0}'.  Unreal Build Tool will try to recompile this assembly now.  (Exception: {1})", OutputAssemblyPath, Ex.Message));
                    bNeedsCompilation = true;
                }
                catch (BadImageFormatException Ex)
                {
                    Log.TraceInformation(String.Format("Compiled assembly file '{0}' appears to be for a newer CLR version or is otherwise invalid.  Unreal Build Tool will try to recompile this assembly now.  (Exception: {1})", OutputAssemblyPath, Ex.Message));
                    bNeedsCompilation = true;
                }
                catch (Exception Ex)
                {
                    throw new BuildException(Ex, "Error while loading previously-compiled assembly file '{0}'.  (Exception: {1})", OutputAssemblyPath, Ex.Message);
                }
            }

            // Compile the assembly if me
            if (bNeedsCompilation)
            {
                CompiledAssembly = CompileAssembly(OutputAssemblyPath, SourceFileNames, ReferencedAssembies, PreprocessorDefines, TreatWarningsAsErrors);

                // Save out a list of all the source files we compiled.  This is so that we can tell if whole files were added or removed
                // since the previous time we compiled the assembly.  In that case, we'll always want to recompile it!
                {
                    FileInfo AssemblySourcesListFile = new FileInfo(AssemblySourcesListFilePath);
                    using (var Writer = AssemblySourcesListFile.OpenWrite())
                    {
                        using (var TextWriter = new StreamWriter(Writer))
                        {
                            SourceFileNames.ForEach(x => TextWriter.WriteLine(x));
                        }
                    }
                }
            }

            // Load the assembly into our app domain
            try
            {
                AppDomain.CurrentDomain.Load(CompiledAssembly.GetName());
            }
            catch (Exception Ex)
            {
                throw new BuildException(Ex, "Unable to load the compiled build assembly '{0}' into our application's domain.  (Exception: {1})", OutputAssemblyPath, Ex.Message);
            }

            return CompiledAssembly;
        }
    }
}
