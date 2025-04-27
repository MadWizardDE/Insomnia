using Autofac;
using MadWizard.Insomnia.Service.Configuration;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia
{
    internal static class PluginLoader
    {
        internal static void RegisterPluginModules<T>(this ContainerBuilder builder) where T : Autofac.Module
        {
            var bin = new DirectoryInfo(typeof(PluginLoader).Assembly.Location).Parent!.FullName;
            var plugins = Path.Combine(bin, "plugins");

            if (Directory.Exists(plugins))
            {
                // extract plugins
                foreach (var zipFile in Directory.GetFiles(plugins, "*.plugin.zip"))
                {
                    var name = Path.GetFileNameWithoutExtension(zipFile).Replace(".plugin", string.Empty);
                    ZipFile.ExtractToDirectory(zipFile, Path.Combine(plugins, name));
                    File.Delete(zipFile);
                }

                // register plugins
                foreach (var pluginDir in Directory.GetDirectories(plugins))
                {
                    var pluginName = new DirectoryInfo(pluginDir).Name;
                    var pluginPath = Path.Combine(pluginDir, $"{pluginName}.dll");

                    if (File.Exists(pluginPath))
                    {
                        var pluginContext = new PluginLoadContext(pluginPath);

                        //builder.RegisterAssemblyModules(pluginContext.PluginAssembly);

                        builder.RegisterAssemblyModules<T>(pluginContext.PluginAssembly);
                    }
                }
            }
        }
    }

    file class PluginLoadContext(string path) : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver resolver = new(path);

        protected override Assembly? Load(AssemblyName assemblyName) => resolver.ResolveAssemblyToPath(assemblyName) is string assemblyPath ? LoadFromAssemblyPath(assemblyPath) : null;

        protected override nint LoadUnmanagedDll(string unmanagedDllName) => resolver.ResolveUnmanagedDllToPath(unmanagedDllName) is string libraryPath ? LoadUnmanagedDllFromPath(libraryPath) : 0;

        public Assembly PluginAssembly => this.LoadFromAssemblyName(AssemblyName.GetAssemblyName(path));
    }
}
