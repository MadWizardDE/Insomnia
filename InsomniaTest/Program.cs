using Castle.DynamicProxy;
using MadWizard.Insomnia;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;

namespace InsomniaTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            new TestNetwork().Test();
            //new TestGeneric().Test();

            Environment.Exit(0);

            //await TestProxy();

            IDictionary<string, object> dict = new Dictionary<string, object>();

            object obj = "objekt";

            dict.Add("test", obj);

            if (dict.ContainsKey("test"))
                Console.WriteLine("test contained");

            dict.Values.Remove(obj);

            if (dict.ContainsKey("test"))
                Console.WriteLine("test contained");

            //Console.ReadKey();
        }

        static async Task TestProxy()
        {
            ProxyGenerator generator = new ProxyGenerator();

            ITestInterface test = generator.CreateInterfaceProxyWithoutTarget<ITestInterface>(new TestProxy());

            Console.WriteLine(test.Greet("Kevin"));
            Console.WriteLine(await test.GreetAsync("Nicht die Mama"));

            await test.GreetAsyncWithoutReturn("Hitler");
        }

        static void TestSerialize()
        {
            {
                MyObject obj = new MyObject();
                obj.Method = typeof(MyObject).GetMethod("Test");
                IFormatter formatter = new BinaryFormatter();
                Stream stream = new FileStream(@"C:\Users\Kevin\Desktop\file.bin", FileMode.Create, FileAccess.Write, FileShare.None);
                formatter.Serialize(stream, obj);
                stream.Close();
            }

            Console.ReadKey();

            {
                IFormatter formatter = new BinaryFormatter();
                Stream stream = new FileStream(@"C:\Users\Kevin\Desktop\file.bin", FileMode.Open, FileAccess.Read, FileShare.Read);
                MyObject obj = (MyObject)formatter.Deserialize(stream);
                stream.Close();

                // Here's the proof.  
                Console.WriteLine("Method: {0}", obj.Method);
            }
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