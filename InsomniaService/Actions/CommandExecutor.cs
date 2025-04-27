using Autofac;
using Castle.Core.Logging;
using MadWizard.Insomnia.Manager.Processes;
using MadWizard.Insomnia.Session;
using MadWizard.Insomnia.Session.Manager;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Service.Actions
{
    class CommandExecutor : Actor
    {
        public required ILogger<CommandExecutor> Logger { protected get; init; }

        [ActionHandler("exec")]
        internal void HandleActionExec(string command, string? arguments, ISession? session = null)
        {
            var start = DetermineStartInfo(command, arguments);

            Logger.LogInformation($"Executing: '{start.FileName}' with {start.ArgumentsToQuotedString()} as {session?.ToString() ?? "SYSTEM"}");

            if (session != null)
            {
                session.LaunchProcess(start);
            }
            else
            {
                Process.Start(start);
            }
        }

        private ProcessStartInfo DetermineStartInfo(string command, string? arguments)
        {
            command = Path.GetFullPath(Environment.ExpandEnvironmentVariables(command));

            var startInfo = new ProcessStartInfo()
            {
                UseShellExecute = false,
            };

            if (File.Exists(command))
            {
                startInfo.WorkingDirectory = Path.GetDirectoryName(command);

                switch (Path.GetExtension(command).ToLowerInvariant())
                {
                    case ".exe":
                    case ".com":
                        startInfo.FileName = command;
                        startInfo.Arguments = arguments;
                        break;

                    case ".bat":
                    case ".cmd":
                        startInfo.FileName = "cmd.exe";
                        startInfo.AddArguments("/C", command);
                        if (arguments != null)
                            startInfo.AddArguments(arguments);
                        startInfo.CreateNoWindow = true;
                        break;
                    case ".ps1":
                        startInfo.FileName = "powershell.exe";
                        startInfo.AddArguments("-ExecutionPolicy", "Bypass");
                        startInfo.AddArguments("-File", command);
                        if (arguments != null)
                            startInfo.AddArguments(arguments);
                        startInfo.CreateNoWindow = true;
                        break;

                    default:
                        throw new NotSupportedException($"Unsupported file type '{Path.GetExtension(command)}'");
                }
            }
            else
                throw new FileNotFoundException($"File not found: {command}", command);

            return startInfo;
        }

    }
}
