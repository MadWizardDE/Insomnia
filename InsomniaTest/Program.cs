using Cassia;
using Castle.DynamicProxy;
using MadWizard.Insomnia;
using MadWizard.Insomnia.Service.SleepWatch.Detector;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.DirectoryServices.AccountManagement;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace InsomniaTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            TestNetworkSessions();
        }

        private static void TestNetworkSessions()
        {
            //var sessions = NetworkSessions.EnumerateSessions();

            //Console.ReadKey();
        }


        private static async Task<double> GetCpuUsageForProcess(Process proc)
        {
            var startTime = DateTime.UtcNow;
            var startCpuUsage = proc.TotalProcessorTime;
            await Task.Delay(5000);

            var endTime = DateTime.UtcNow;
            var endCpuUsage = proc.TotalProcessorTime;
            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
            return cpuUsageTotal * 100;
        }

        static void TestPrincipal(string user)
        {
            var currentIdentity = WindowsIdentity.GetCurrent();
            var userIdetitiy = new WindowsIdentity(currentIdentity.Token);

            //WindowsPrincipal prinz = new WindowsIdentity(user));
            WindowsPrincipal prinz = new WindowsPrincipal(userIdetitiy);

            if (prinz.IsInRole(WindowsBuiltInRole.Administrator))
                Console.WriteLine($"{prinz.Identity.Name} is Administrator");
            else
                Console.WriteLine($"{prinz.Identity.Name} is NOT Administrator");
        }

        static WindowsIdentity GetWindowsIdentity(string userName)
        {
            PrincipalContext ctx = new PrincipalContext(ContextType.Machine);

            return null;
        }


        private static bool IsAdmin
        {
            get => new WindowsPrincipal(WindowsIdentity.GetCurrent()).UserClaims.Where(c => c.Value.Contains("S-1-5-32-544")).Count() > 0;
        }

        static void TestCassia()
        {
            var tsManager = new TerminalServicesManager();
            var tsServer = tsManager.GetLocalServer();

            var sessions = tsServer.GetSessions();

            Console.ReadKey();
        }

        static void TestProcess()
        {
            SecureString password = new SecureString();
            password.AppendChar('c');
            password.AppendChar('h');
            password.AppendChar('a');
            password.AppendChar('n');
            password.AppendChar('g');
            password.AppendChar('e');
            password.AppendChar('i');
            password.AppendChar('t');

            using (Process compiler = new Process())
            {
                compiler.StartInfo.UserName = "Johannes";
                compiler.StartInfo.Password = password;
                compiler.StartInfo.FileName = @"notepad";
                compiler.StartInfo.UseShellExecute = false;
                compiler.StartInfo.RedirectStandardOutput = true;
                compiler.StartInfo.WorkingDirectory = @"C:\";
                compiler.Start();

                Console.WriteLine(compiler.StandardOutput.ReadToEnd());

                compiler.WaitForExit();
            }

        }

        static async Task TestProxy()
        {
            ProxyGenerator generator = new ProxyGenerator();

            ITestInterface test = generator.CreateInterfaceProxyWithoutTarget<ITestInterface>(new TestProxy());

            Console.WriteLine(test.Greet("Kevin"));
            Console.WriteLine(await test.GreetAsync("Nicht die Mama"));

            await test.GreetAsyncWithoutReturn("Hitler");
        }

        static async Task IdleTest()
        {
            while (true)
            {
                Console.WriteLine($"Idle-Time: {Win32API.IdleTime}");

                await Task.Delay(2000);
            }
        }
    }

    public class TestProxy : IInterceptor
    {
        void IInterceptor.Intercept(IInvocation invocation)
        {
            Type typeTask = typeof(Task);
            Type typeTaskString = typeof(Task<string>);

            if (invocation.Method.ReturnType.BaseType == typeof(Task))
            {
                if (invocation.Method.ReturnType.GenericTypeArguments[0] == typeof(string))
                    invocation.ReturnValue = InterceptTask(invocation.Arguments);
            }
            if (invocation.Method.ReturnType == typeof(Task))
            {
                Console.WriteLine("Async Hallo " + invocation.Arguments[0]);
                invocation.ReturnValue = Task.CompletedTask;
            }
            if (invocation.Method.ReturnType == typeof(string))
                invocation.ReturnValue = "Sync Hallo " + invocation.Arguments[0];

        }

        async Task<string> InterceptTask(object[] args)
        {
            await Task.Delay(5000);

            return "Async<string> Hallo " + args[0];
        }
    }

    public interface ITestInterface
    {
        string Greet(string name);

        Task<string> GreetAsync(string name);

        Task GreetAsyncWithoutReturn(string name);
    }

    [Serializable]
    public class MyObject : ISerializable
    {
        public MethodInfo Method { get; set; }

        public void Test(string text)
        {
            Console.WriteLine(text);
        }

        public MyObject()
        {

        }

        public MyObject(SerializationInfo info, StreamingContext context)
        {
            Type methodType = Type.GetType(info.GetString("methodType"));
            string methodName = info.GetString("methodName");
            //Type methodReturnType = Type.GetType(info.GetString("methodReturnType"));

            Type[] parameterTypes = new Type[info.GetInt32("methodParameterCount")];

            for (int i = 0; i < parameterTypes.Length; i++)
                parameterTypes[i] = Type.GetType(info.GetString("methodParameterType" + i));

            Method = methodType.GetMethod(methodName, parameterTypes);
        }

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            ParameterInfo[] parameterInfos = Method.GetParameters();

            info.AddValue("methodType", Method.DeclaringType.AssemblyQualifiedName);

            info.AddValue("methodName", Method.Name);
            //info.AddValue("methodReturnType", Method.ReturnType.AssemblyQualifiedName);
            info.AddValue("methodParameterCount", parameterInfos.Length);
            for (int i = 0; i < parameterInfos.Length; i++)
                info.AddValue("methodParameterType" + i, parameterInfos[i].ParameterType.AssemblyQualifiedName);


        }
    }
}
