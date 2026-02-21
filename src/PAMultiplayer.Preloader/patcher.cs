using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Logging;
using Mono.Cecil;

namespace PAMultiplayer.Preloader
{
    public static class Patcher
    {
        // List of assemblies to patch
        public static IEnumerable<string> TargetDLLs { get; } = new[] { "Facepunch.Steamworks.Win64.dll", "DiscordRPC.dll"};
        
        // Patches the assemblies
        public static void Patch(ref AssemblyDefinition assembly)
        {
            switch (assembly.Name.Name)
            {
                case "Assembly-CSharp":
                    break;
                case "DiscordRPC":
                    assembly = AssemblyDefinition.ReadAssembly(
                        $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\\DiscordRPC.dll");
                    break;
                case "Facepunch.Steamworks.Win64":
                    assembly = AssemblyDefinition.ReadAssembly(
                        $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\\Facepunch.Steamworks.Win64.dll");
                    
                    PatchFacepunch(assembly);
                    break;
            }
        }

        private static void PatchFacepunch(AssemblyDefinition assembly)
        {
           ManualLogSource logger = Logger.CreateLogSource("me.ytarame.multiplayer.preloader");
            string path =
                $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\\steam_api64.dll";
            
            foreach (var module in assembly.Modules)
            {
                foreach (var type in module.Types)
                {
                    foreach (var method in type.Methods)
                    {
                        if (!method.IsPInvokeImpl || method.PInvokeInfo == null)
                        {
                            continue;
                        }
                        
                        if (method.PInvokeInfo.Module.Name != "steam_api64")
                        {
                            continue;
                        }

                        logger.LogInfo($"Found steam_api64 import");
                        method.PInvokeInfo.Module.Name = path;
                        return;
                    }
                }
            }
            
            logger.Dispose();
        }
    }
}