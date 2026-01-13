using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mono.Cecil;

namespace PAMultiplayer.Preloader
{
    public static class Patcher
    {
        // List of assemblies to patch
        public static IEnumerable<string> TargetDLLs { get; } = new[] {"Facepunch.Steamworks.Win64.dll", "DiscordRPC.dll"};

        // Patches the assemblies
        public static void Patch(ref AssemblyDefinition assembly)
        {
            switch (assembly.Name.Name)
            {
                case "DiscordRPC":
                    assembly = AssemblyDefinition.ReadAssembly(
                        $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\\DiscordRPC.dll");
                    break;
                case "Facepunch.Steamworks.Win64":
                    assembly = AssemblyDefinition.ReadAssembly(
                        $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\\Facepunch.Steamworks.Win64.dll");
                    break;
            }
        }
    }
}