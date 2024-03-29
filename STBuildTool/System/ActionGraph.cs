﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;

namespace STBuildTool
{
    public enum ActionType
    {
        BuildProject,

        Compile,

        CreateAppBundle,

        GenerateDebugInfo,

        Link,
    }

    /** A build action. */
    [Serializable]
    public class Action
    {
        ///
        /// Preparation and Assembly (serialized)
        /// 

        /** The type of this action (for debugging purposes). */
        public ActionType ActionType;

        /** Every file this action depends on.  These files need to exist and be up to date in order for this action to even be considered */
        public List<FileItem> PrerequisiteItems = new List<FileItem>();

        /** The files that this action produces after completing */
        public List<FileItem> ProducedItems = new List<FileItem>();

        /** Directory from which to execute the program to create produced items */
        public string WorkingDirectory = null;

        /** True if we should log extra information when we run a program to create produced items */
        public bool bPrintDebugInfo = false;

        /** The command to run to create produced items */
        public string CommandPath = null;

        /** Command-line parameters to pass to the program */
        public string CommandArguments = null;

        /** Optional friendly description of the type of command being performed, for example "Compile" or "Link".  Displayed by some executors. */
        public string CommandDescription = null;

        /** Human-readable description of this action that may be displayed as status while invoking the action.  This is often the name of the file being compiled, or an executable file name being linked.  Displayed by some executors. */
        public string StatusDescription = "...";

        /** True if this action is allowed to be run on a remote machine when a distributed build system is being used, such as XGE */
        public bool bCanExecuteRemotely = false;

        /** True if this action is using the Visual C++ compiler.  Some build systems may be able to optimize for this case. */
        public bool bIsVCCompiler = false;

        /** True if this action is using the GCC compiler.  Some build systems may be able to optimize for this case. */
        public bool bIsGCCCompiler = false;

        /** Whether the action is using a pre-compiled header to speed it up. */
        public bool bIsUsingPCH = false;

        /** Whether the files in ProducedItems should be deleted before executing this action, when the action is outdated */
        public bool bShouldDeleteProducedItems = false;

        /**
         * Whether we should log this action, whether executed locally or remotely.  This is useful for actions that take time
         * but invoke tools without any console output.
         */
        public bool bShouldOutputStatusDescription = true;

        /** True if any libraries produced by this action should be considered 'import libraries' */
        public bool bProducesImportLibrary = false;

        /** Optional custom event handler for standard output. */
        public DataReceivedEventHandler OutputEventHandler = null;	// @todo ubtmake urgent: Delegate variables are not saved, but we are comparing against this in ExecutActions() for XGE!

        /** Callback used to perform a special action instead of a generic command line */
        public delegate void BlockingActionHandler(Action Action, out int ExitCode, out string Output);
        public BlockingActionHandler ActionHandler = null;		// @todo ubtmake urgent: Delegate variables are not saved, but we are comparing against this in ExecutActions() for XGE!



        ///
        /// Preparation only (not serialized)
        ///

        /** Unique action identifier.  Used for displaying helpful info about detected cycles in the graph. */
        [NonSerialized]
        public int UniqueId;

        /** Always-incremented unique id */
        private static int NextUniqueId = 0;

        /** Total number of actions depending on this one. */
        [NonSerialized]
        public int NumTotalDependentActions = 0;

        /** Relative cost of producing items for this action. */
        [NonSerialized]
        public long RelativeCost = 0;


        ///
        /// Assembly only (not serialized)
        ///

        /** Start time of action, optionally set by executor. */
        [NonSerialized]
        public DateTimeOffset StartTime = DateTimeOffset.MinValue;

        /** End time of action, optionally set by executor. */
        [NonSerialized]
        public DateTimeOffset EndTime = DateTimeOffset.MinValue;



        public Action(ActionType InActionType)
        {
            ActionType = InActionType;

            ActionGraph.AllActions.Add(this);
            UniqueId = ++NextUniqueId;
        }


        /**
         * Compares two actions based on total number of dependent items, descending.
         * 
         * @param	A	Action to compare
         * @param	B	Action to compare
         */
        public static int Compare(Action A, Action B)
        {
            // Primary sort criteria is total number of dependent files, up to max depth.
            if (B.NumTotalDependentActions != A.NumTotalDependentActions)
            {
                return Math.Sign(B.NumTotalDependentActions - A.NumTotalDependentActions);
            }
            // Secondary sort criteria is relative cost.
            if (B.RelativeCost != A.RelativeCost)
            {
                return Math.Sign(B.RelativeCost - A.RelativeCost);
            }
            // Tertiary sort criteria is number of pre-requisites.
            else
            {
                return Math.Sign(B.PrerequisiteItems.Count - A.PrerequisiteItems.Count);
            }
        }

        public override string ToString()
        {
            string ReturnString = "";
            if (CommandPath != null)
            {
                ReturnString += CommandPath + " - ";
            }
            if (CommandArguments != null)
            {
                ReturnString += CommandArguments;
            }
            return ReturnString;
        }

        /// <summary>
        /// Returns the amount of time that this action is or has been executing in.
        /// </summary>
        public TimeSpan Duration
        {
            get
            {
                if (EndTime == DateTimeOffset.MinValue)
                {
                    return DateTimeOffset.Now - StartTime;
                }

                return EndTime - StartTime;
            }
        }
    };

    class ActionGraph
    {
        public static List<Action> AllActions = new List<Action>();

        public static void ResetAllActions()
        {
            AllActions = new List<Action>();
        }

        public static void FinalizeActionGraph()
        {
            // @todo fastubt: Can we use directory changed notifications or directory timestamps to accelerate C++ file outdatedness checking?

            // Link producing actions to the items they produce.
            LinkActionsAndItems();

            // Detect cycles in the action graph.
            DetectActionGraphCycles();

            // Sort action list by "cost" in descending order to improve parallelism.
            SortActionList();
        }

        /** Builds a list of actions that need to be executed to produce the specified output items. */
        public static List<Action> GetActionsToExecute(Action[] PrerequisiteActions, List<STBuildTarget> Targets, out Dictionary<STBuildTarget, List<FileItem>> TargetToOutdatedPrerequisitesMap)
        {
            var CheckOutdatednessStartTime = DateTime.UtcNow;

            // Build a set of all actions needed for this target.
            var IsActionOutdatedMap = new Dictionary<Action, bool>();
            foreach (var Action in PrerequisiteActions)
            {
                IsActionOutdatedMap.Add(Action, true);
            }

            // For all targets, build a set of all actions that are outdated.
            var OutdatedActionDictionary = new Dictionary<Action, bool>();
            var HistoryList = new List<ActionHistory>();
            var OpenHistoryFiles = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            TargetToOutdatedPrerequisitesMap = new Dictionary<STBuildTarget, List<FileItem>>();
            foreach (var BuildTarget in Targets)	// @todo ubtmake: Optimization: Ideally we don't even need to know about targets for ubtmake -- everything would come from the files
            {
                var HistoryFilename = ActionHistory.GeneratePathForTarget(BuildTarget);
                if (!OpenHistoryFiles.Contains(HistoryFilename))		// @todo ubtmake: Optimization: We should be able to move the command-line outdatedness and build product deletion over to the 'gather' phase, as the command-lines won't change between assembler runs
                {
                    var History = new ActionHistory(HistoryFilename);
                    HistoryList.Add(History);
                    OpenHistoryFiles.Add(HistoryFilename);
                    GatherAllOutdatedActions(BuildTarget, History, ref OutdatedActionDictionary, TargetToOutdatedPrerequisitesMap);
                }
            }

            // Delete produced items that are outdated.
            DeleteOutdatedProducedItems(OutdatedActionDictionary, BuildConfiguration.bShouldDeleteAllOutdatedProducedItems);

            // Save the action history.
            // This must happen after deleting outdated produced items to ensure that the action history on disk doesn't have
            // command-lines that don't match the produced items on disk.
            foreach (var TargetHistory in HistoryList)
            {
                TargetHistory.Save();
            }

            // Create directories for the outdated produced items.
            CreateDirectoriesForProducedItems(OutdatedActionDictionary);

            // Build a list of actions that are both needed for this target and outdated.
            List<Action> ActionsToExecute = new List<Action>();
            bool bHasOutdatedNonLinkActions = false;
            foreach (Action Action in AllActions)
            {
                if (Action.CommandPath != null && IsActionOutdatedMap.ContainsKey(Action) && OutdatedActionDictionary[Action])
                {
                    ActionsToExecute.Add(Action);
                    if (Action.ActionType != ActionType.Link)
                    {
                        bHasOutdatedNonLinkActions = true;
                    }
                }
            }

            // Remove link actions if asked to
            if (STBuildConfiguration.bSkipLinkingWhenNothingToCompile && !bHasOutdatedNonLinkActions)
            {
                ActionsToExecute.Clear();
            }

            if (BuildConfiguration.bPrintPerformanceInfo)
            {
                var CheckOutdatednessTime = (DateTime.UtcNow - CheckOutdatednessStartTime).TotalSeconds;
                Log.TraceInformation("Checking outdatedness took " + CheckOutdatednessTime + "s");
            }

            return ActionsToExecute;
        }

        /** Executes a list of actions. */
        public static bool ExecuteActions(List<Action> ActionsToExecute, out string ExecutorName)
        {
            bool Result = true;
            bool bUsedXGE = false;
            ExecutorName = "";
            if (ActionsToExecute.Count > 0)
            {
                if (BuildConfiguration.bAllowXGE || BuildConfiguration.bXGEExport)
                {
                    XGE.ExecutionResult XGEResult = XGE.ExecutionResult.TasksSucceeded;

                    // Batch up XGE execution by actions with the same output event handler.
                    List<Action> ActionBatch = new List<Action>();
                    ActionBatch.Add(ActionsToExecute[0]);
                    for (int ActionIndex = 1; ActionIndex < ActionsToExecute.Count && XGEResult == XGE.ExecutionResult.TasksSucceeded; ++ActionIndex)
                    {
                        Action CurrentAction = ActionsToExecute[ActionIndex];
                        if (CurrentAction.OutputEventHandler == ActionBatch[0].OutputEventHandler)
                        {
                            ActionBatch.Add(CurrentAction);
                        }
                        else
                        {
                            XGEResult = XGE.ExecuteActions(ActionBatch);
                            ActionBatch.Clear();
                            ActionBatch.Add(CurrentAction);
                        }
                    }
                    if (ActionBatch.Count > 0 && XGEResult == XGE.ExecutionResult.TasksSucceeded)
                    {
                        XGEResult = XGE.ExecuteActions(ActionBatch);
                        ActionBatch.Clear();
                    }

                    if (XGEResult != XGE.ExecutionResult.Unavailable)
                    {
                        ExecutorName = "XGE";
                        Result = (XGEResult == XGE.ExecutionResult.TasksSucceeded);
                        // don't do local compilation
                        bUsedXGE = true;
                    }
                }

                if (!bUsedXGE && BuildConfiguration.bAllowDistcc)
                {
                    ExecutorName = "Distcc";
                    Result = Distcc.ExecuteActions(ActionsToExecute);
                    // don't do local compilation
                    bUsedXGE = true;
                }

                if (!bUsedXGE && BuildConfiguration.bAllowSNDBS)
                {
                    SNDBS.ExecutionResult SNDBSResult = SNDBS.ExecuteActions(ActionsToExecute);
                    if (SNDBSResult != SNDBS.ExecutionResult.Unavailable)
                    {
                        ExecutorName = "SNDBS";
                        Result = (SNDBSResult == SNDBS.ExecutionResult.TasksSucceeded);
                        // don't do local compilation
                        bUsedXGE = true;
                    }
                }

                // If XGE is disallowed or unavailable, execute the commands locally.
                if (!bUsedXGE)
                {
                    ExecutorName = "Local";
                    Result = LocalExecutor.ExecuteActions(ActionsToExecute);
                }

                if (bUsedXGE && BuildConfiguration.bXGEExport)
                {
                    // we exported xge here, we do not test build products
                }
                else
                {
                    // Verify the link outputs were created (seems to happen with Win64 compiles)
                    foreach (Action BuildAction in ActionsToExecute)
                    {
                        if (BuildAction.ActionType == ActionType.Link)
                        {
                            foreach (FileItem Item in BuildAction.ProducedItems)
                            {
                                bool bExists;
                                if (Item.bIsRemoteFile)
                                {
                                    DateTime UnusedTime;
                                    long UnusedLength;
                                    bExists = RPCUtilHelper.GetRemoteFileInfo(Item.AbsolutePath, out UnusedTime, out UnusedLength);
                                }
                                else
                                {
                                    FileInfo ItemInfo = new FileInfo(Item.AbsolutePath);
                                    bExists = ItemInfo.Exists;
                                }
                                if (!bExists)
                                {
                                    throw new BuildException("UBT ERROR: Failed to produce item: " + Item.AbsolutePath);
                                }
                            }
                        }
                    }
                }
            }
            // Nothing to execute.
            else
            {
                ExecutorName = "NoActionsToExecute";
                Log.TraceInformation("Target is up to date.");
            }

            return Result;
        }

        /** Links actions with their prerequisite and produced items into an action graph. */
        static void LinkActionsAndItems()
        {
            foreach (Action Action in AllActions)
            {
                foreach (FileItem ProducedItem in Action.ProducedItems)
                {
                    ProducedItem.ProducingAction = Action;
                    Action.RelativeCost += ProducedItem.RelativeCost;
                }
            }
        }
        static string SplitFilename(string Filename, out string PlatformSuffix, out string ConfigSuffix, out string ProducedItemExtension)
        {
            string WorkingString = Filename;
            ProducedItemExtension = Path.GetExtension(WorkingString);
            if (!WorkingString.EndsWith(ProducedItemExtension))
            {
                throw new BuildException("Bogus extension");
            }
            WorkingString = WorkingString.Substring(0, WorkingString.Length - ProducedItemExtension.Length);

            ConfigSuffix = "";
            foreach (STTargetConfiguration CurConfig in Enum.GetValues(typeof(STTargetConfiguration)))
            {
                if (CurConfig != STTargetConfiguration.Unknown)
                {
                    string Test = "-" + CurConfig;
                    if (WorkingString.EndsWith(Test))
                    {
                        WorkingString = WorkingString.Substring(0, WorkingString.Length - Test.Length);
                        ConfigSuffix = Test;
                        break;
                    }
                }
            }
            PlatformSuffix = "";
            foreach (var CurPlatform in Enum.GetValues(typeof(STTargetPlatform)))
            {
                string Test = "-" + CurPlatform;
                if (WorkingString.EndsWith(Test))
                {
                    WorkingString = WorkingString.Substring(0, WorkingString.Length - Test.Length);
                    PlatformSuffix = Test;
                    break;
                }
            }
            return WorkingString;
        }


        /** Finds and deletes stale hot reload DLLs. */
        public static void DeleteStaleHotReloadDLLs()
        {
            var DeleteStartTime = DateTime.UtcNow;

            foreach (Action BuildAction in AllActions)
            {
                if (BuildAction.ActionType == ActionType.Link)
                {
                    foreach (FileItem Item in BuildAction.ProducedItems)
                    {
                        if (Item.bNeedsHotReloadNumbersDLLCleanUp)
                        {
                            string PlatformSuffix, ConfigSuffix, ProducedItemExtension;
                            string Base = SplitFilename(Item.AbsolutePath, out PlatformSuffix, out ConfigSuffix, out ProducedItemExtension);
                            String WildCard = Base + "-*" + PlatformSuffix + ConfigSuffix + ProducedItemExtension;
                            // Log.TraceInformation("Deleting old hot reload wildcard: \"{0}\".", WildCard);
                            // Wildcard search and delete
                            string DirectoryToLookIn = Path.GetDirectoryName(WildCard);
                            string FileName = Path.GetFileName(WildCard);
                            if (Directory.Exists(DirectoryToLookIn))
                            {
                                // Delete all files within the specified folder
                                string[] FilesToDelete = Directory.GetFiles(DirectoryToLookIn, FileName, SearchOption.TopDirectoryOnly);
                                foreach (string JunkFile in FilesToDelete)
                                {

                                    string JunkPlatformSuffix, JunkConfigSuffix, JunkProducedItemExtension;
                                    SplitFilename(JunkFile, out JunkPlatformSuffix, out JunkConfigSuffix, out JunkProducedItemExtension);
                                    // now make sure that this file has the same config and platform
                                    if (JunkPlatformSuffix == PlatformSuffix && JunkConfigSuffix == ConfigSuffix)
                                    {
                                        try
                                        {
                                            Log.TraceInformation("Deleting old hot reload file: \"{0}\".", JunkFile);
                                            File.Delete(JunkFile);
                                        }
                                        catch (Exception Ex)
                                        {
                                            // Ignore all exceptions
                                            Log.TraceInformation("Unable to delete old hot reload file: \"{0}\". Error: {0}", JunkFile, Ex.Message);
                                        }

                                        // Delete the PDB file.
                                        string JunkPDBFile = JunkFile.Replace(ProducedItemExtension, ".pdb");
                                        if (System.IO.File.Exists(JunkPDBFile))
                                        {
                                            try
                                            {
                                                Log.TraceInformation("Deleting old hot reload file: \"{0}\".", JunkPDBFile);
                                                File.Delete(JunkPDBFile);
                                            }
                                            catch (Exception Ex)
                                            {
                                                // Ignore all exceptions
                                                Log.TraceInformation("Unable to delete old hot reload file: \"{0}\". Error: {0}", JunkPDBFile, Ex.Message);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (BuildConfiguration.bPrintPerformanceInfo)
            {
                var DeleteTime = (DateTime.UtcNow - DeleteStartTime).TotalSeconds;
                Log.TraceInformation("Deleting stale hot reload DLLs took " + DeleteTime + "s");
            }
        }

        /**
         * Sorts the action list for improved parallelism with local execution.
         */
        public static void SortActionList()
        {
            // Mapping from action to a list of actions that directly or indirectly depend on it (up to a certain depth).
            Dictionary<Action, HashSet<Action>> ActionToDependentActionsMap = new Dictionary<Action, HashSet<Action>>();
            // Perform multiple passes over all actions to propagate dependencies.
            const int MaxDepth = 5;
            for (int Pass = 0; Pass < MaxDepth; Pass++)
            {
                foreach (Action DependendAction in AllActions)
                {
                    foreach (FileItem PrerequisiteItem in DependendAction.PrerequisiteItems)
                    {
                        Action PrerequisiteAction = PrerequisiteItem.ProducingAction;
                        if (PrerequisiteAction != null)
                        {
                            HashSet<Action> DependentActions = null;
                            if (ActionToDependentActionsMap.ContainsKey(PrerequisiteAction))
                            {
                                DependentActions = ActionToDependentActionsMap[PrerequisiteAction];
                            }
                            else
                            {
                                DependentActions = new HashSet<Action>();
                                ActionToDependentActionsMap[PrerequisiteAction] = DependentActions;
                            }
                            // Add dependent action...
                            DependentActions.Add(DependendAction);
                            // ... and all actions depending on it.
                            if (ActionToDependentActionsMap.ContainsKey(DependendAction))
                            {
                                DependentActions.UnionWith(ActionToDependentActionsMap[DependendAction]);
                            }
                        }
                    }
                }

            }
            // At this point we have a list of dependent actions for each action, up to MaxDepth layers deep.
            foreach (KeyValuePair<Action, HashSet<Action>> ActionMap in ActionToDependentActionsMap)
            {
                ActionMap.Key.NumTotalDependentActions = ActionMap.Value.Count;
            }
            // Sort actions by number of actions depending on them, descending. Secondary sort criteria is file size.
            AllActions.Sort(Action.Compare);
        }

        /** Checks for cycles in the action graph. */
        static void DetectActionGraphCycles()
        {
            // Starting with actions that only depend on non-produced items, iteratively expand a set of actions that are only dependent on
            // non-cyclical actions.
            Dictionary<Action, bool> ActionIsNonCyclical = new Dictionary<Action, bool>();
            Dictionary<Action, List<Action>> CyclicActions = new Dictionary<Action, List<Action>>();
            while (true)
            {
                bool bFoundNewNonCyclicalAction = false;

                foreach (Action Action in AllActions)
                {
                    if (!ActionIsNonCyclical.ContainsKey(Action))
                    {
                        // Determine if the action depends on only actions that are already known to be non-cyclical.
                        bool bActionOnlyDependsOnNonCyclicalActions = true;
                        foreach (FileItem PrerequisiteItem in Action.PrerequisiteItems)
                        {
                            if (PrerequisiteItem.ProducingAction != null)
                            {
                                if (!ActionIsNonCyclical.ContainsKey(PrerequisiteItem.ProducingAction))
                                {
                                    bActionOnlyDependsOnNonCyclicalActions = false;
                                    if (!CyclicActions.ContainsKey(Action))
                                    {
                                        CyclicActions.Add(Action, new List<Action>());
                                    }

                                    List<Action> CyclicPrereq = CyclicActions[Action];
                                    if (!CyclicPrereq.Contains(PrerequisiteItem.ProducingAction))
                                    {
                                        CyclicPrereq.Add(PrerequisiteItem.ProducingAction);
                                    }
                                }
                            }
                        }

                        // If the action only depends on known non-cyclical actions, then add it to the set of known non-cyclical actions.
                        if (bActionOnlyDependsOnNonCyclicalActions)
                        {
                            ActionIsNonCyclical.Add(Action, true);
                            bFoundNewNonCyclicalAction = true;
                        }
                    }
                }

                // If this iteration has visited all actions without finding a new non-cyclical action, then all non-cyclical actions have
                // been found.
                if (!bFoundNewNonCyclicalAction)
                {
                    break;
                }
            }

            // If there are any cyclical actions, throw an exception.
            if (ActionIsNonCyclical.Count < AllActions.Count)
            {
                // Describe the cyclical actions.
                string CycleDescription = "";
                foreach (Action Action in AllActions)
                {
                    if (!ActionIsNonCyclical.ContainsKey(Action))
                    {
                        CycleDescription += string.Format("Action #{0}: {1}\n", Action.UniqueId, Action.CommandPath);
                        CycleDescription += string.Format("\twith arguments: {0}\n", Action.CommandArguments);
                        foreach (FileItem PrerequisiteItem in Action.PrerequisiteItems)
                        {
                            CycleDescription += string.Format("\tdepends on: {0}\n", PrerequisiteItem.AbsolutePath);
                        }
                        foreach (FileItem ProducedItem in Action.ProducedItems)
                        {
                            CycleDescription += string.Format("\tproduces:   {0}\n", ProducedItem.AbsolutePath);
                        }
                        CycleDescription += string.Format("\tDepends on cyclic actions:\n");
                        if (CyclicActions.ContainsKey(Action))
                        {
                            foreach (Action CyclicPrerequisiteAction in CyclicActions[Action])
                            {
                                if (CyclicPrerequisiteAction.ProducedItems.Count == 1)
                                {
                                    CycleDescription += string.Format("\t\t{0} (produces: {1})\n", CyclicPrerequisiteAction.UniqueId, CyclicPrerequisiteAction.ProducedItems[0].AbsolutePath);
                                }
                                else
                                {
                                    CycleDescription += string.Format("\t\t{0}\n", CyclicPrerequisiteAction.UniqueId);
                                    foreach (FileItem CyclicProducedItem in CyclicPrerequisiteAction.ProducedItems)
                                    {
                                        CycleDescription += string.Format("\t\t\tproduces:   {0}\n", CyclicProducedItem.AbsolutePath);
                                    }
                                }
                            }
                            CycleDescription += "\n";
                        }
                        else
                        {
                            CycleDescription += string.Format("\t\tNone?? Coding error!\n");
                        }
                        CycleDescription += "\n\n";
                    }
                }

                throw new BuildException("Action graph contains cycle!\n\n{0}", CycleDescription);
            }
        }

        /**
         * Determines the full set of actions that must be built to produce an item.
         * @param OutputItem - The item to be built.
         * @param PrerequisiteActions - The actions that must be built and the root action are 
         */
        public static void GatherPrerequisiteActions(
            FileItem OutputItem,
            ref HashSet<Action> PrerequisiteActions
            )
        {
            if (OutputItem != null && OutputItem.ProducingAction != null)
            {
                if (!PrerequisiteActions.Contains(OutputItem.ProducingAction))
                {
                    PrerequisiteActions.Add(OutputItem.ProducingAction);
                    foreach (FileItem PrerequisiteItem in OutputItem.ProducingAction.PrerequisiteItems)
                    {
                        GatherPrerequisiteActions(PrerequisiteItem, ref PrerequisiteActions);
                    }
                }
            }
        }

        /**
         * Determines whether an action is outdated based on the modification times for its prerequisite
         * and produced items.
         * @param RootAction - The action being considered.
         * @param OutdatedActionDictionary - 
         * @return true if outdated
         */
        static public bool IsActionOutdated(STBuildTarget Target, Action RootAction, ref Dictionary<Action, bool> OutdatedActionDictionary, ActionHistory ActionHistory, Dictionary<STBuildTarget, List<FileItem>> TargetToOutdatedPrerequisitesMap)
        {
            // Only compute the outdated-ness for actions that don't aren't cached in the outdated action dictionary.
            bool bIsOutdated = false;
            if (!OutdatedActionDictionary.TryGetValue(RootAction, out bIsOutdated))
            {
                // Determine the last time the action was run based on the write times of its produced files.
                string LatestUpdatedProducedItemName = null;
                DateTimeOffset LastExecutionTime = DateTimeOffset.MaxValue;
                foreach (FileItem ProducedItem in RootAction.ProducedItems)
                {
                    // Optionally skip the action history check, as this only works for local builds
                    if (BuildConfiguration.bUseActionHistory)
                    {
                        // Check if the command-line of the action previously used to produce the item is outdated.
                        string OldProducingCommandLine = "";
                        string NewProducingCommandLine = RootAction.CommandPath + " " + RootAction.CommandArguments;
                        if (!ActionHistory.GetProducingCommandLine(ProducedItem, out OldProducingCommandLine)
                        || !String.Equals(OldProducingCommandLine, NewProducingCommandLine, StringComparison.InvariantCultureIgnoreCase))
                        {
                            Log.TraceVerbose(
                                "{0}: Produced item \"{1}\" was produced by outdated command-line.\nOld command-line: {2}\nNew command-line: {3}",
                                RootAction.StatusDescription,
                                Path.GetFileName(ProducedItem.AbsolutePath),
                                OldProducingCommandLine,
                                NewProducingCommandLine
                                );

                            bIsOutdated = true;

                            // Update the command-line used to produce this item in the action history.
                            ActionHistory.SetProducingCommandLine(ProducedItem, NewProducingCommandLine);
                        }
                    }

                    // If the produced file doesn't exist or has zero size, consider it outdated.  The zero size check is to detect cases
                    // where aborting an earlier compile produced invalid zero-sized obj files, but that may cause actions where that's
                    // legitimate output to always be considered outdated.
                    if (ProducedItem.bExists && (ProducedItem.bIsRemoteFile || ProducedItem.Length > 0 || ProducedItem.IsDirectory))
                    {
                        // When linking incrementally, don't use LIB, EXP pr PDB files when checking for the oldest produced item,
                        // as those files aren't always touched.
                        if (BuildConfiguration.bUseIncrementalLinking)
                        {
                            String ProducedItemExtension = Path.GetExtension(ProducedItem.AbsolutePath).ToUpperInvariant();
                            if (ProducedItemExtension == ".LIB" || ProducedItemExtension == ".EXP" || ProducedItemExtension == ".PDB")
                            {
                                continue;
                            }
                        }

                        // Use the oldest produced item's time as the last execution time.
                        if (ProducedItem.LastWriteTime < LastExecutionTime)
                        {
                            LastExecutionTime = ProducedItem.LastWriteTime;
                            LatestUpdatedProducedItemName = ProducedItem.AbsolutePath;
                        }
                    }
                    else
                    {
                        // If any of the produced items doesn't exist, the action is outdated.
                        Log.TraceVerbose(
                            "{0}: Produced item \"{1}\" doesn't exist.",
                            RootAction.StatusDescription,
                            Path.GetFileName(ProducedItem.AbsolutePath)
                            );
                        bIsOutdated = true;
                    }
                }

                Log.WriteLineIf(BuildConfiguration.bLogDetailedActionStats && !String.IsNullOrEmpty(LatestUpdatedProducedItemName),
                    TraceEventType.Verbose, "{0}: Oldest produced item is {1}", RootAction.StatusDescription, LatestUpdatedProducedItemName);

                bool bFindCPPIncludePrerequisites = false;
                if (RootAction.ActionType == ActionType.Compile)
                {
                    // Outdated targets don't need their headers scanned yet, because presumably they would already be out of dated based on already-cached
                    // includes before getting this far.  However, if we find them to be outdated after processing includes, we'll do a deep scan later
                    // on and cache all of the includes so that we have them for a quick outdatedness check the next run.
                    if (!bIsOutdated &&
                        BuildConfiguration.bUseExperimentalFastBuildIteration &&
                        STBuildTool.IsAssemblingBuild &&
                        RootAction.ActionType == ActionType.Compile)
                    {
                        bFindCPPIncludePrerequisites = true;
                    }

                    // Were we asked to force an update of our cached includes BEFORE we try to build?  This may be needed if our cache can no longer
                    // be trusted and we need to fill it with perfectly valid data (even if we're in assembler only mode)
                    if (BuildConfiguration.bUseExperimentalFastDependencyScan &&
                        STBuildTool.bNeedsFullCPPIncludeRescan)
                    {
                        bFindCPPIncludePrerequisites = true;
                    }
                }


                if (bFindCPPIncludePrerequisites)
                {
                    // Scan this file for included headers that may be out of date.  Note that it's OK if we break out early because we found
                    // the action to be outdated.  For outdated actions, we kick off a separate include scan in a background thread later on to
                    // catch all of the other includes and form an exhaustive set.
                    foreach (FileItem PrerequisiteItem in RootAction.PrerequisiteItems)
                    {
                        // @todo ubtmake: Make sure we are catching RC files here too.  Anything that the toolchain would have tried it on.  Logic should match the CACHING stuff below
                        if (PrerequisiteItem.CachedCPPIncludeInfo != null)
                        {
                            var BuildPlatform = STBuildPlatform.GetBuildPlatform(Target.GetTargetInfo().Platform);
                            var IncludedFileList = CPPEnvironment.FindAndCacheAllIncludedFiles(Target, PrerequisiteItem, BuildPlatform, PrerequisiteItem.CachedCPPIncludeInfo, bOnlyCachedDependencies: BuildConfiguration.bUseExperimentalFastDependencyScan);
                            foreach (var IncludedFile in IncludedFileList)	// @todo fastubt: @todo ubtmake: Optimization: This is "retesting" a lot of the same files over and over in a single run (common indirect includes)
                            {
                                if (IncludedFile.bExists)
                                {
                                    // allow a 1 second slop for network copies
                                    TimeSpan TimeDifference = IncludedFile.LastWriteTime - LastExecutionTime;
                                    bool bPrerequisiteItemIsNewerThanLastExecution = TimeDifference.TotalSeconds > 1;
                                    if (bPrerequisiteItemIsNewerThanLastExecution)
                                    {
                                        Log.TraceVerbose(
                                            "{0}: Included file {1} is newer than the last execution of the action: {2} vs {3}",
                                            RootAction.StatusDescription,
                                            Path.GetFileName(IncludedFile.AbsolutePath),
                                            IncludedFile.LastWriteTime.LocalDateTime,
                                            LastExecutionTime.LocalDateTime
                                            );
                                        bIsOutdated = true;

                                        // Don't bother checking every single include if we've found one that is out of date
                                        break;
                                    }
                                }
                            }
                        }

                        if (bIsOutdated)
                        {
                            break;
                        }
                    }
                }

                if (!bIsOutdated)
                {
                    // Check if any of the prerequisite items are produced by outdated actions, or have changed more recently than
                    // the oldest produced item.
                    foreach (FileItem PrerequisiteItem in RootAction.PrerequisiteItems)
                    {
                        // Only check for outdated import libraries if we were configured to do so.  Often, a changed import library
                        // won't affect a dependency unless a public header file was also changed, in which case we would be forced
                        // to recompile anyway.  This just allows for faster iteration when working on a subsystem in a DLL, as we
                        // won't have to wait for dependent targets to be relinked after each change.
                        bool bIsImportLibraryFile = false;
                        if (PrerequisiteItem.ProducingAction != null && PrerequisiteItem.ProducingAction.bProducesImportLibrary)
                        {
                            bIsImportLibraryFile = PrerequisiteItem.AbsolutePath.EndsWith(".LIB", StringComparison.InvariantCultureIgnoreCase);
                        }
                        if (!bIsImportLibraryFile || !BuildConfiguration.bIgnoreOutdatedImportLibraries)
                        {
                            // If the prerequisite is produced by an outdated action, then this action is outdated too.
                            if (PrerequisiteItem.ProducingAction != null)
                            {
                                if (IsActionOutdated(Target, PrerequisiteItem.ProducingAction, ref OutdatedActionDictionary, ActionHistory, TargetToOutdatedPrerequisitesMap))
                                {
                                    Log.TraceVerbose(
                                        "{0}: Prerequisite {1} is produced by outdated action.",
                                        RootAction.StatusDescription,
                                        Path.GetFileName(PrerequisiteItem.AbsolutePath)
                                        );
                                    bIsOutdated = true;
                                }
                            }

                            if (PrerequisiteItem.bExists)
                            {
                                // allow a 1 second slop for network copies
                                TimeSpan TimeDifference = PrerequisiteItem.LastWriteTime - LastExecutionTime;
                                bool bPrerequisiteItemIsNewerThanLastExecution = TimeDifference.TotalSeconds > 1;
                                if (bPrerequisiteItemIsNewerThanLastExecution)
                                {
                                    Log.TraceVerbose(
                                        "{0}: Prerequisite {1} is newer than the last execution of the action: {2} vs {3}",
                                        RootAction.StatusDescription,
                                        Path.GetFileName(PrerequisiteItem.AbsolutePath),
                                        PrerequisiteItem.LastWriteTime.LocalDateTime,
                                        LastExecutionTime.LocalDateTime
                                        );
                                    bIsOutdated = true;
                                }
                            }

                            // GatherAllOutdatedActions will ensure all actions are checked for outdated-ness, so we don't need to recurse with
                            // all this action's prerequisites once we've determined it's outdated.
                            if (bIsOutdated)
                            {
                                break;
                            }
                        }
                    }
                }

                // For compile actions, we have C++ files that are actually dependent on header files that could have been changed.  We only need to
                // know about the set of header files that are included for files that are already determined to be out of date (such as if the file
                // is missing or was modified.)  In the case that the file is out of date, we'll perform a deep scan to update our cached set of
                // includes for this file, so that we'll be able to determine whether it is out of date next time very quickly.
                if (BuildConfiguration.bUseExperimentalFastDependencyScan)
                {
                    var DeepIncludeScanStartTime = DateTime.UtcNow;

                    // @todo fastubt: we may be scanning more files than we need to here -- indirectly outdated files are bIsOutdated=true by this point (for example basemost includes when deeper includes are dirty)
                    if (bIsOutdated && RootAction.ActionType == ActionType.Compile)	// @todo fastubt: Does this work with RC files?  See above too.
                    {
                        Log.TraceVerbose("Outdated action: {0}", RootAction.StatusDescription);
                        foreach (FileItem PrerequisiteItem in RootAction.PrerequisiteItems)
                        {
                            if (PrerequisiteItem.CachedCPPIncludeInfo != null)
                            {
                                if (!IsCPPFile(PrerequisiteItem))
                                {
                                    throw new BuildException("Was only expecting C++ files to have CachedCPPEnvironments!");
                                }
                                Log.TraceVerbose("  -> DEEP include scan: {0}", PrerequisiteItem.AbsolutePath);

                                List<FileItem> OutdatedPrerequisites;
                                if (!TargetToOutdatedPrerequisitesMap.TryGetValue(Target, out OutdatedPrerequisites))
                                {
                                    OutdatedPrerequisites = new List<FileItem>();
                                    TargetToOutdatedPrerequisitesMap.Add(Target, OutdatedPrerequisites);
                                }

                                OutdatedPrerequisites.Add(PrerequisiteItem);
                            }
                            else if (IsCPPImplementationFile(PrerequisiteItem) || IsCPPResourceFile(PrerequisiteItem))
                            {
                                if (PrerequisiteItem.CachedCPPIncludeInfo == null)
                                {
                                    Log.TraceVerbose("  -> WARNING: No CachedCPPEnvironment: {0}", PrerequisiteItem.AbsolutePath);
                                }
                            }
                        }
                    }

                    if (BuildConfiguration.bPrintPerformanceInfo)
                    {
                        double DeepIncludeScanTime = (DateTime.UtcNow - DeepIncludeScanStartTime).TotalSeconds;
                        STBuildTool.TotalDeepIncludeScanTime += DeepIncludeScanTime;
                    }
                }

                // Cache the outdated-ness of this action.
                OutdatedActionDictionary.Add(RootAction, bIsOutdated);
            }

            return bIsOutdated;
        }


        /**
         * Builds a dictionary containing the actions from AllActions that are outdated by calling
         * IsActionOutdated.
         */
        static void GatherAllOutdatedActions(STBuildTarget Target, ActionHistory ActionHistory, ref Dictionary<Action, bool> OutdatedActions, Dictionary<STBuildTarget, List<FileItem>> TargetToOutdatedPrerequisitesMap)
        {
            var CheckOutdatednessStartTime = DateTime.UtcNow;

            foreach (var Action in AllActions)
            {
                IsActionOutdated(Target, Action, ref OutdatedActions, ActionHistory, TargetToOutdatedPrerequisitesMap);
            }

            if (BuildConfiguration.bPrintPerformanceInfo)
            {
                var CheckOutdatednessTime = (DateTime.UtcNow - CheckOutdatednessStartTime).TotalSeconds;
                Log.TraceInformation("Checking actions for " + Target.GetTargetName() + " took " + CheckOutdatednessTime + "s");
            }
        }

        /**
         * Deletes all the items produced by actions in the provided outdated action dictionary. 
         * 
         * @param	OutdatedActionDictionary	Dictionary of outdated actions
         * @param	bShouldDeleteAllFiles		Whether to delete all files associated with outdated items or just ones required
         */
        static void DeleteOutdatedProducedItems(Dictionary<Action, bool> OutdatedActionDictionary, bool bShouldDeleteAllFiles)
        {
            foreach (KeyValuePair<Action, bool> OutdatedActionInfo in OutdatedActionDictionary)
            {
                if (OutdatedActionInfo.Value)
                {
                    Action OutdatedAction = OutdatedActionInfo.Key;
                    foreach (FileItem ProducedItem in OutdatedActionInfo.Key.ProducedItems)
                    {
                        if (ProducedItem.bExists
                        && (bShouldDeleteAllFiles
                            // Delete PDB files as incremental updates are slower than full ones.
                            || (!BuildConfiguration.bUseIncrementalLinking && ProducedItem.AbsolutePath.EndsWith(".PDB", StringComparison.InvariantCultureIgnoreCase))
                            || OutdatedAction.bShouldDeleteProducedItems))
                        {
                            Log.TraceVerbose("Deleting outdated item: {0}", ProducedItem.AbsolutePath);
                            ProducedItem.Delete();
                        }
                    }
                }
            }
        }

        /**
         * Creates directories for all the items produced by actions in the provided outdated action
         * dictionary.
         */
        static void CreateDirectoriesForProducedItems(Dictionary<Action, bool> OutdatedActionDictionary)
        {
            foreach (KeyValuePair<Action, bool> OutdatedActionInfo in OutdatedActionDictionary)
            {
                if (OutdatedActionInfo.Value)
                {
                    foreach (FileItem ProducedItem in OutdatedActionInfo.Key.ProducedItems)
                    {
                        if (ProducedItem.bIsRemoteFile)
                        {
                            // we don't need to do this in the SSH mode, the action will have an output file, and it will use that to make the directory while executing the command
                            if (RemoteToolChain.bUseRPCUtil)
                            {
                                try
                                {
                                    RPCUtilHelper.MakeDirectory(Path.GetDirectoryName(ProducedItem.AbsolutePath).Replace("\\", "/"));
                                }
                                catch (System.Exception Ex)
                                {
                                    throw new BuildException(Ex, "Error while creating remote directory for '{0}'.  (Exception: {1})", ProducedItem.AbsolutePath, Ex.Message);
                                }
                            }
                        }
                        else
                        {
                            string DirectoryPath = Path.GetDirectoryName(ProducedItem.AbsolutePath);
                            if (!Directory.Exists(DirectoryPath))
                            {
                                Log.TraceVerbose("Creating directory for produced item: {0}", DirectoryPath);
                                Directory.CreateDirectory(DirectoryPath);
                            }
                        }
                    }
                }
            }
        }



        /// <summary>
        /// Checks if the specified file is a C++ source implementation file (e.g., .cpp)
        /// </summary>
        /// <param name="FileItem">The file to check</param>
        /// <returns>True if this is a C++ source file</returns>
        private static bool IsCPPImplementationFile(FileItem FileItem)
        {
            return (FileItem.AbsolutePath.EndsWith(".cpp", StringComparison.InvariantCultureIgnoreCase) ||
                    FileItem.AbsolutePath.EndsWith(".c", StringComparison.InvariantCultureIgnoreCase) ||
                    FileItem.AbsolutePath.EndsWith(".mm", StringComparison.InvariantCultureIgnoreCase));
        }


        /// <summary>
        /// Checks if the specified file is a C++ source header file (e.g., .h or .inl)
        /// </summary>
        /// <param name="FileItem">The file to check</param>
        /// <returns>True if this is a C++ source file</returns>
        private static bool IsCPPIncludeFile(FileItem FileItem)
        {
            return (FileItem.AbsolutePath.EndsWith(".h", StringComparison.InvariantCultureIgnoreCase) ||
                    FileItem.AbsolutePath.EndsWith(".inl", StringComparison.InvariantCultureIgnoreCase));
        }


        /// <summary>
        /// Checks if the specified file is a C++ resource file (e.g., .rc)
        /// </summary>
        /// <param name="FileItem">The file to check</param>
        /// <returns>True if this is a C++ source file</returns>
        private static bool IsCPPResourceFile(FileItem FileItem)
        {
            return (FileItem.AbsolutePath.EndsWith(".rc", StringComparison.InvariantCultureIgnoreCase));
        }


        /// <summary>
        /// Checks if the specified file is a C++ source file
        /// </summary>
        /// <param name="FileItem">The file to check</param>
        /// <returns>True if this is a C++ source file</returns>
        private static bool IsCPPFile(FileItem FileItem)
        {
            return IsCPPImplementationFile(FileItem) || IsCPPIncludeFile(FileItem) || IsCPPResourceFile(FileItem);
        }



        /// <summary>
        /// Types of action graph visualizations that we can emit
        /// </summary>
        public enum ActionGraphVisualizationType
        {
            OnlyActions,
            ActionsWithFiles,
            ActionsWithFilesAndHeaders,
            OnlyFilesAndHeaders,
            OnlyCPlusPlusFilesAndHeaders
        }



        /// <summary>
        /// Saves the action graph (and include dependency network) to a graph gile
        /// </summary>
        /// <param name="Filename">File name to emit</param>
        /// <param name="Description">Description to be stored in graph metadata</param>
        /// <param name="VisualizationType">Type of graph to create</param>
        /// <param name="Actions">All actions</param>
        /// <param name="IncludeCompileActions">True if we should include compile actions.  If disabled, only the static link actions will be shown, which is useful to see module relationships</param>
        public static void SaveActionGraphVisualization(STBuildTarget Target, string Filename, string Description, ActionGraphVisualizationType VisualizationType, List<Action> Actions, bool IncludeCompileActions = true)
        {
            // True if we should include individual files in the graph network, or false to include only the build actions
            var IncludeFiles = VisualizationType != ActionGraphVisualizationType.OnlyActions;
            var OnlyIncludeCPlusPlusFiles = VisualizationType == ActionGraphVisualizationType.OnlyCPlusPlusFilesAndHeaders;

            // True if want to show actions in the graph, otherwise we're only showing files
            var IncludeActions = VisualizationType != ActionGraphVisualizationType.OnlyFilesAndHeaders && VisualizationType != ActionGraphVisualizationType.OnlyCPlusPlusFilesAndHeaders;

            // True if C++ header dependencies should be expanded into the graph, or false to only have .cpp files
            var ExpandCPPHeaderDependencies = IncludeFiles && (VisualizationType == ActionGraphVisualizationType.ActionsWithFilesAndHeaders || VisualizationType == ActionGraphVisualizationType.OnlyFilesAndHeaders || VisualizationType == ActionGraphVisualizationType.OnlyCPlusPlusFilesAndHeaders);

            var TimerStartTime = DateTime.UtcNow;

            var GraphNodes = new List<GraphNode>();

            var FileToGraphNodeMap = new Dictionary<FileItem, GraphNode>();

            // Filter our list of actions
            var FilteredActions = new List<Action>();
            {
                for (var ActionIndex = 0; ActionIndex < Actions.Count; ++ActionIndex)
                {
                    var Action = Actions[ActionIndex];

                    if (!IncludeActions || IncludeCompileActions || (Action.ActionType != ActionType.Compile))
                    {
                        FilteredActions.Add(Action);
                    }
                }
            }


            var FilesToCreateNodesFor = new HashSet<FileItem>();
            for (var ActionIndex = 0; ActionIndex < FilteredActions.Count; ++ActionIndex)
            {
                var Action = FilteredActions[ActionIndex];

                if (IncludeActions)
                {
                    var GraphNode = new GraphNode()
                    {
                        Id = GraphNodes.Count,

                        // Don't bother including "Link" text if we're excluding compile actions
                        Label = IncludeCompileActions ? (Action.ActionType.ToString() + " " + Action.StatusDescription) : Action.StatusDescription
                    };

                    switch (Action.ActionType)
                    {
                        case ActionType.BuildProject:
                            GraphNode.Color = new GraphColor() { R = 0.3f, G = 1.0f, B = 1.0f, A = 1.0f };
                            GraphNode.Size = 1.1f;
                            break;

                        case ActionType.Compile:
                            GraphNode.Color = new GraphColor() { R = 0.3f, G = 1.0f, B = 0.3f, A = 1.0f };
                            break;

                        case ActionType.Link:
                            GraphNode.Color = new GraphColor() { R = 0.3f, G = 0.3f, B = 1.0f, A = 1.0f };
                            GraphNode.Size = 1.2f;
                            break;
                    }

                    GraphNodes.Add(GraphNode);
                }

                if (IncludeFiles)
                {
                    foreach (var ProducedFileItem in Action.ProducedItems)
                    {
                        if (!OnlyIncludeCPlusPlusFiles || IsCPPFile(ProducedFileItem))
                        {
                            FilesToCreateNodesFor.Add(ProducedFileItem);
                        }
                    }

                    foreach (var PrerequisiteFileItem in Action.PrerequisiteItems)
                    {
                        if (!OnlyIncludeCPlusPlusFiles || IsCPPFile(PrerequisiteFileItem))
                        {
                            FilesToCreateNodesFor.Add(PrerequisiteFileItem);
                        }
                    }
                }
            }


            var OverriddenPrerequisites = new Dictionary<FileItem, List<FileItem>>();

            // Determine the average size of all of the C++ source files
            Int64 AverageCPPFileSize;
            {
                Int64 TotalFileSize = 0;
                int CPPFileCount = 0;
                foreach (var FileItem in FilesToCreateNodesFor)
                {
                    if (IsCPPFile(FileItem))
                    {
                        ++CPPFileCount;
                        TotalFileSize += new FileInfo(FileItem.AbsolutePath).Length;
                    }
                }

                if (CPPFileCount > 0)
                {
                    AverageCPPFileSize = TotalFileSize / CPPFileCount;
                }
                else
                {
                    AverageCPPFileSize = 1;
                }
            }

            var BuildPlatform = STBuildPlatform.GetBuildPlatform(STTargetPlatform.Win64);

            foreach (var FileItem in FilesToCreateNodesFor)
            {
                var FileGraphNode = new GraphNode()
                {
                    Id = GraphNodes.Count,
                    Label = Path.GetFileName(FileItem.AbsolutePath)
                };

                if (FileItem.AbsolutePath.EndsWith(".h", StringComparison.InvariantCultureIgnoreCase) ||
                    FileItem.AbsolutePath.EndsWith(".inl", StringComparison.InvariantCultureIgnoreCase))
                {
                    // Header file
                    FileGraphNode.Color = new GraphColor() { R = 0.9f, G = 0.2f, B = 0.9f, A = 1.0f };
                }
                else if (FileItem.AbsolutePath.EndsWith(".cpp", StringComparison.InvariantCultureIgnoreCase) ||
                         FileItem.AbsolutePath.EndsWith(".c", StringComparison.InvariantCultureIgnoreCase) ||
                         FileItem.AbsolutePath.EndsWith(".mm", StringComparison.InvariantCultureIgnoreCase))
                {
                    // C++ file
                    FileGraphNode.Color = new GraphColor() { R = 1.0f, G = 1.0f, B = 0.3f, A = 1.0f };
                }
                else
                {
                    // Other file
                    FileGraphNode.Color = new GraphColor() { R = 0.4f, G = 0.4f, B = 0.1f, A = 1.0f };
                }

                // Set the size of the file node based on the size of the file on disk
                var bIsCPPFile = IsCPPFile(FileItem);
                if (bIsCPPFile)
                {
                    var MinNodeSize = 0.25f;
                    var MaxNodeSize = 2.0f;
                    var FileSize = new FileInfo(FileItem.AbsolutePath).Length;
                    float FileSizeScale = (float)((double)FileSize / (double)AverageCPPFileSize);

                    var SourceFileSizeScaleFactor = 0.1f;		// How much to make nodes for files bigger or larger based on their difference from the average file's size
                    FileGraphNode.Size = Math.Min(Math.Max(1.0f + SourceFileSizeScaleFactor * FileSizeScale, MinNodeSize), MaxNodeSize);
                }

                //@todo: Testing out attribute support.  Replace with an attribute that is actually useful!
                //if( FileItem.PrecompiledHeaderIncludeFilename != null )
                //{ 
                //FileGraphNode.Attributes[ "PCHFile" ] = Path.GetFileNameWithoutExtension( FileItem.PrecompiledHeaderIncludeFilename );
                //}

                FileToGraphNodeMap[FileItem] = FileGraphNode;
                GraphNodes.Add(FileGraphNode);

                if (ExpandCPPHeaderDependencies && bIsCPPFile)
                {
                    bool HasUObjects;
                    List<DependencyInclude> DirectlyIncludedFilenames = CPPEnvironment.GetDirectIncludeDependencies(Target, FileItem, BuildPlatform, bOnlyCachedDependencies: false, HasUObjects: out HasUObjects);

                    // Resolve the included file name to an actual file.
                    var DirectlyIncludedFiles =
                        DirectlyIncludedFilenames
                        .Where(DirectlyIncludedFilename => !string.IsNullOrEmpty(DirectlyIncludedFilename.IncludeResolvedName))
                        .Select(DirectlyIncludedFilename => DirectlyIncludedFilename.IncludeResolvedName)
                        // Skip same include over and over (.inl files)
                        .Distinct()
                        .Select(FileItem.GetItemByFullPath)
                        .ToList();

                    OverriddenPrerequisites[FileItem] = DirectlyIncludedFiles;
                }
            }


            // Connect everything together
            var GraphEdges = new List<GraphEdge>();

            if (IncludeActions)
            {
                for (var ActionIndex = 0; ActionIndex < FilteredActions.Count; ++ActionIndex)
                {
                    var Action = FilteredActions[ActionIndex];
                    var ActionGraphNode = GraphNodes[ActionIndex];

                    List<FileItem> ActualPrerequisiteItems = Action.PrerequisiteItems;
                    if (IncludeFiles && ExpandCPPHeaderDependencies && Action.ActionType == ActionType.Compile)
                    {
                        // The first prerequisite is always the .cpp file to compile
                        var CPPFile = Action.PrerequisiteItems[0];
                        if (!IsCPPFile(CPPFile))
                        {
                            throw new BuildException("Was expecting a C++ file as the first prerequisite for a Compile action");
                        }

                        ActualPrerequisiteItems = new List<FileItem>();
                        ActualPrerequisiteItems.Add(CPPFile);
                    }


                    foreach (var PrerequisiteFileItem in ActualPrerequisiteItems)
                    {
                        if (IncludeFiles)
                        {
                            GraphNode PrerequisiteFileGraphNode;
                            if (FileToGraphNodeMap.TryGetValue(PrerequisiteFileItem, out PrerequisiteFileGraphNode))
                            {
                                // Connect a file our action is dependent on, to our action itself
                                var GraphEdge = new GraphEdge()
                                {
                                    Id = GraphEdges.Count,
                                    Source = PrerequisiteFileGraphNode,
                                    Target = ActionGraphNode,
                                };

                                GraphEdges.Add(GraphEdge);
                            }
                            else
                            {
                                // Not a file we were tracking
                                // Console.WriteLine( "Unknown file: " + PrerequisiteFileItem.AbsolutePath );
                            }
                        }
                        else if (PrerequisiteFileItem.ProducingAction != null)
                        {
                            // Not showing files, so connect the actions together
                            var ProducingActionIndex = FilteredActions.IndexOf(PrerequisiteFileItem.ProducingAction);
                            if (ProducingActionIndex != -1)
                            {
                                var SourceGraphNode = GraphNodes[ProducingActionIndex];

                                var GraphEdge = new GraphEdge()
                                {
                                    Id = GraphEdges.Count,
                                    Source = SourceGraphNode,
                                    Target = ActionGraphNode,
                                };

                                GraphEdges.Add(GraphEdge);
                            }
                            else
                            {
                                // Our producer action was filtered out
                            }
                        }
                    }

                    foreach (var ProducedFileItem in Action.ProducedItems)
                    {
                        if (IncludeFiles)
                        {
                            if (!OnlyIncludeCPlusPlusFiles || IsCPPFile(ProducedFileItem))
                            {
                                var ProducedFileGraphNode = FileToGraphNodeMap[ProducedFileItem];

                                var GraphEdge = new GraphEdge()
                                {
                                    Id = GraphEdges.Count,
                                    Source = ActionGraphNode,
                                    Target = ProducedFileGraphNode,
                                };

                                GraphEdges.Add(GraphEdge);
                            }
                        }
                    }
                }
            }

            if (IncludeFiles && ExpandCPPHeaderDependencies)
            {
                // Fill in overridden prerequisites
                foreach (var FileAndPrerequisites in OverriddenPrerequisites)
                {
                    var FileItem = FileAndPrerequisites.Key;
                    var FilePrerequisites = FileAndPrerequisites.Value;

                    var FileGraphNode = FileToGraphNodeMap[FileItem];
                    foreach (var PrerequisiteFileItem in FilePrerequisites)
                    {
                        GraphNode PrerequisiteFileGraphNode;
                        if (FileToGraphNodeMap.TryGetValue(PrerequisiteFileItem, out PrerequisiteFileGraphNode))
                        {
                            var GraphEdge = new GraphEdge()
                            {
                                Id = GraphEdges.Count,
                                Source = PrerequisiteFileGraphNode,
                                Target = FileGraphNode,
                            };

                            GraphEdges.Add(GraphEdge);
                        }
                        else
                        {
                            // Some other header that we don't track directly
                            //Console.WriteLine( "File not known: " + PrerequisiteFileItem.AbsolutePath );
                        }
                    }
                }
            }

            GraphVisualization.WriteGraphFile(Filename, Description, GraphNodes, GraphEdges);

            if (BuildConfiguration.bPrintPerformanceInfo)
            {
                var TimerDuration = DateTime.UtcNow - TimerStartTime;
                Log.TraceInformation("Generating and saving ActionGraph took " + TimerDuration.TotalSeconds + "s");
            }
        }
    };
}
