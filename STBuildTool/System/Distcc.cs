﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Xml;
using System.Text.RegularExpressions;
using System.Linq;
using System.Reflection;

namespace STBuildTool
{
    public class Distcc
    {
        public static bool ExecuteActions(List<Action> Actions)
        {
            bool bDistccResult = true;
            if (Actions.Count > 0)
            {
                // Time to sleep after each iteration of the loop in order to not busy wait.
                const float LoopSleepTime = 0.1f;

                int MaxActionsToExecuteInParallel = 0;
                string UserDir = Environment.GetEnvironmentVariable("HOME");
                string HostsInfo = UserDir + "/.dmucs/hosts-info";
                System.IO.StreamReader File = new System.IO.StreamReader(HostsInfo);
                string Line = null;
                while ((Line = File.ReadLine()) != null)
                {
                    var HostInfo = Line.Split(' ');
                    if (HostInfo.Count() == 3)
                    {
                        int NumCPUs = 0;
                        if (System.Int32.TryParse(HostInfo[1], out NumCPUs))
                        {
                            MaxActionsToExecuteInParallel += NumCPUs;
                        }
                    }
                }
                File.Close();

                if (BuildConfiguration.bAllowDistccLocalFallback == false)
                {
                    Environment.SetEnvironmentVariable("DISTCC_FALLBACK", "0");
                }

                string DistccExecutable = BuildConfiguration.DistccExecutablesPath + "/distcc";
                string GetHostExecutable = BuildConfiguration.DistccExecutablesPath + "/gethost";

                Log.TraceInformation("Performing {0} actions ({1} in parallel)", Actions.Count, MaxActionsToExecuteInParallel, DistccExecutable, GetHostExecutable);

                Dictionary<Action, ActionThread> ActionThreadDictionary = new Dictionary<Action, ActionThread>();
                int JobNumber = 1;
                using (ProgressWriter ProgressWriter = new ProgressWriter("Compiling source code...", false))
                {
                    int ProgressValue = 0;
                    while (true)
                    {
                        // Count the number of pending and still executing actions.
                        int NumUnexecutedActions = 0;
                        int NumExecutingActions = 0;
                        foreach (Action Action in Actions)
                        {
                            ActionThread ActionThread = null;
                            bool bFoundActionProcess = ActionThreadDictionary.TryGetValue(Action, out ActionThread);
                            if (bFoundActionProcess == false)
                            {
                                NumUnexecutedActions++;
                            }
                            else if (ActionThread != null)
                            {
                                if (ActionThread.bComplete == false)
                                {
                                    NumUnexecutedActions++;
                                    NumExecutingActions++;
                                }
                            }
                        }

                        // Update the current progress
                        int NewProgressValue = Actions.Count + 1 - NumUnexecutedActions;
                        if (ProgressValue != NewProgressValue)
                        {
                            ProgressWriter.Write(ProgressValue, Actions.Count + 1);
                            ProgressValue = NewProgressValue;
                        }

                        // If there aren't any pending actions left, we're done executing.
                        if (NumUnexecutedActions == 0)
                        {
                            break;
                        }

                        // If there are fewer actions executing than the maximum, look for pending actions that don't have any outdated
                        // prerequisites.
                        foreach (Action Action in Actions)
                        {
                            ActionThread ActionProcess = null;
                            bool bFoundActionProcess = ActionThreadDictionary.TryGetValue(Action, out ActionProcess);
                            if (bFoundActionProcess == false)
                            {
                                if (NumExecutingActions < Math.Max(1, MaxActionsToExecuteInParallel))
                                {
                                    // Determine whether there are any prerequisites of the action that are outdated.
                                    bool bHasOutdatedPrerequisites = false;
                                    bool bHasFailedPrerequisites = false;
                                    foreach (FileItem PrerequisiteItem in Action.PrerequisiteItems)
                                    {
                                        if (PrerequisiteItem.ProducingAction != null && Actions.Contains(PrerequisiteItem.ProducingAction))
                                        {
                                            ActionThread PrerequisiteProcess = null;
                                            bool bFoundPrerequisiteProcess = ActionThreadDictionary.TryGetValue(PrerequisiteItem.ProducingAction, out PrerequisiteProcess);
                                            if (bFoundPrerequisiteProcess == true)
                                            {
                                                if (PrerequisiteProcess == null)
                                                {
                                                    bHasFailedPrerequisites = true;
                                                }
                                                else if (PrerequisiteProcess.bComplete == false)
                                                {
                                                    bHasOutdatedPrerequisites = true;
                                                }
                                                else if (PrerequisiteProcess.ExitCode != 0)
                                                {
                                                    bHasFailedPrerequisites = true;
                                                }
                                            }
                                            else
                                            {
                                                bHasOutdatedPrerequisites = true;
                                            }
                                        }
                                    }

                                    // If there are any failed prerequisites of this action, don't execute it.
                                    if (bHasFailedPrerequisites)
                                    {
                                        // Add a null entry in the dictionary for this action.
                                        ActionThreadDictionary.Add(Action, null);
                                    }
                                    // If there aren't any outdated prerequisites of this action, execute it.
                                    else if (!bHasOutdatedPrerequisites)
                                    {
                                        if ((Action.ActionType == ActionType.Compile || Action.ActionType == ActionType.Link) && DistccExecutable != null && GetHostExecutable != null)
                                        {
                                            string NewCommandArguments = "--wait -1 \"" + DistccExecutable + "\" " + Action.CommandPath + " " + Action.CommandArguments;
                                            Action.CommandPath = GetHostExecutable;
                                            Action.CommandArguments = NewCommandArguments;
                                        }

                                        ActionThread ActionThread = new ActionThread(Action, JobNumber, Actions.Count);
                                        JobNumber++;
                                        ActionThread.Run();

                                        ActionThreadDictionary.Add(Action, ActionThread);

                                        NumExecutingActions++;
                                    }
                                }
                            }
                        }

                        System.Threading.Thread.Sleep(TimeSpan.FromSeconds(LoopSleepTime));
                    }
                }

                Log.WriteLineIf(BuildConfiguration.bLogDetailedActionStats, TraceEventType.Information, "-------- Begin Detailed Action Stats ----------------------------------------------------------");
                Log.WriteLineIf(BuildConfiguration.bLogDetailedActionStats, TraceEventType.Information, "^Action Type^Duration (seconds)^Tool^Task^Using PCH");

                double TotalThreadSeconds = 0;

                // Check whether any of the tasks failed and log action stats if wanted.
                foreach (KeyValuePair<Action, ActionThread> ActionProcess in ActionThreadDictionary)
                {
                    Action Action = ActionProcess.Key;
                    ActionThread ActionThread = ActionProcess.Value;

                    // Check for pending actions, preemptive failure
                    if (ActionThread == null)
                    {
                        bDistccResult = false;
                        continue;
                    }
                    // Check for executed action but general failure
                    if (ActionThread.ExitCode != 0)
                    {
                        bDistccResult = false;
                    }
                    // Log CPU time, tool and task.
                    double ThreadSeconds = Action.Duration.TotalSeconds;

                    Log.WriteLineIf(BuildConfiguration.bLogDetailedActionStats,
                        TraceEventType.Information,
                        "^{0}^{1:0.00}^{2}^{3}^{4}",
                        Action.ActionType.ToString(),
                        ThreadSeconds,
                        Path.GetFileName(Action.CommandPath),
                        Action.StatusDescription,
                        Action.bIsUsingPCH);

                    // Update statistics
                    switch (Action.ActionType)
                    {
                        case ActionType.BuildProject:
                            STBuildTool.TotalBuildProjectTime += ThreadSeconds;
                            break;

                        case ActionType.Compile:
                            STBuildTool.TotalCompileTime += ThreadSeconds;
                            break;

                        case ActionType.CreateAppBundle:
                            STBuildTool.TotalCreateAppBundleTime += ThreadSeconds;
                            break;

                        case ActionType.GenerateDebugInfo:
                            STBuildTool.TotalGenerateDebugInfoTime += ThreadSeconds;
                            break;

                        case ActionType.Link:
                            STBuildTool.TotalLinkTime += ThreadSeconds;
                            break;

                        default:
                            STBuildTool.TotalOtherActionsTime += ThreadSeconds;
                            break;
                    }

                    // Keep track of total thread seconds spent on tasks.
                    TotalThreadSeconds += ThreadSeconds;
                }

                Log.TraceInformation("-------- End Detailed Actions Stats -----------------------------------------------------------");

                // Log total CPU seconds and numbers of processors involved in tasks.
                Log.WriteLineIf(BuildConfiguration.bLogDetailedActionStats || BuildConfiguration.bPrintDebugInfo,
                    TraceEventType.Information, "Cumulative thread seconds ({0} processors): {1:0.00}", System.Environment.ProcessorCount, TotalThreadSeconds);
            }
            return bDistccResult;
        }
    }
}
