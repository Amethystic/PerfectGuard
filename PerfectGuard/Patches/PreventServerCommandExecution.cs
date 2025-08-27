using HarmonyLib;
using Mirror;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Marioalexsan.PerfectGuard.Patches;

[HarmonyPatch]
internal static class PatchCommands
{
    static IEnumerable<MethodInfo> TargetMethods()
    {
        yield return AccessTools.Method(typeof(HostConsole), nameof(HostConsole.Kick_Peer));
        yield return AccessTools.Method(typeof(HostConsole), nameof(HostConsole.Ban_Peer));
        yield return AccessTools.Method(typeof(HostConsole), nameof(HostConsole.Init_Shutdown));
    }

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        var matcher = new CodeMatcher(code);

        while (true)
        {
            matcher.MatchForward(false,
                new CodeMatch((ins) => ins.Calls(AccessTools.Method(typeof(HostConsole), nameof(HostConsole.Init_ServerMessage))))
                );

            if (matcher.IsInvalid)
                break;

            matcher.RemoveInstruction();
            matcher.Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PreventServerCommandExecution), nameof(PreventServerCommandExecution.ProcessHostConsoleMessage))));
        }

        return matcher.InstructionEnumeration();
    }
}

[HarmonyPatch]
internal static class PreventServerCommandExecution
{
    [HarmonyPatch(typeof(HostConsole), nameof(HostConsole.Init_ServerMessage))]
    [HarmonyPrefix]
    static bool RemoveCommandExecutionFromMessages(HostConsole __instance, string _message)
    {
        ServerMessage message = default;
        message.servMsg = _message;

        NetworkServer.SendToAll(message);
        __instance.New_LogMessage(_message ?? "");

        if (_message != null)
        {
            if (_message.Trim().StartsWith("/"))
            {
                PerfectGuard.Logger.LogWarning("An attempt to execute a command on the host console was done by a client.");
                PerfectGuard.Logger.LogWarning("Offending message: " + _message);
            }
            else if (_message.Contains("/"))
            {
                PerfectGuard.Logger.LogWarning("A suspicious message that might be an attempt to execute commands on the host console was detected.");
                PerfectGuard.Logger.LogWarning("Offending message: " + _message);
            }
        }

        return false;
    }

    [HarmonyPatch(typeof(HostConsole), nameof(HostConsole.Send_ServerMessage))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> ExecuteCommandsFromHostConsole(IEnumerable<CodeInstruction> code)
    {
        var matcher = new CodeMatcher(code);

        matcher.MatchForward(false,
            new CodeMatch((ins) => ins.Calls(AccessTools.Method(typeof(HostConsole), nameof(HostConsole.Init_ServerMessage))))
            );

        if (!matcher.IsValid)
        {
            PerfectGuard.Logger.LogWarning("Failed to patch host console command sending!");
            return code;
        }

        matcher.RemoveInstruction();
        matcher.Insert(
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PreventServerCommandExecution), nameof(ProcessHostConsoleMessage)))
            );

        return matcher.InstructionEnumeration();
    }

    internal static void ProcessHostConsoleMessage(HostConsole instance, string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        if (message.Trim().StartsWith('/'))
        {
            var parts = message.Trim()[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // Vanilla commands only support a command name and an integer parameter

            var command = parts.Length >= 1 ? parts[0] : "";
            var argument = parts.Length >= 2 ? parts[1] : "";

            instance._cmdManager.Init_ConsoleCommand(command, argument);
        }
        else
        {
            instance.Init_ServerMessage(message);
        }
    }
}
