using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace MadWizard.Insomnia.Service.Sessions
{
    [Serializable]
    public abstract class ServiceMessage : SystemMessage
    {
        ServiceIdentifier _identifier;

        public ServiceMessage(Type type)
        {
            ServiceType = type;
        }

        public Type ServiceType
        {
            get => _identifier.Type;
            private set => _identifier = new ServiceIdentifier(value);
        }
    }

    [Serializable]
    public class ServiceControlMessage : ServiceMessage
    {
        internal ServiceControlMessage(Type type, ServiceState state) : base(type)
        {
            State = state;
        }

        public ServiceState State { get; private set; }
    }

    [Serializable]
    public class ServiceStateMessage : ServiceMessage
    {
        public ServiceStateMessage(Type type, ServiceState state) : base(type)
        {
            State = state;
        }

        public ServiceState State { get; private set; }
    }

    [Serializable]
    public class ServiceInvocationMessage : ServiceMessage
    {
        ServicePort _endpoint;

        internal ServiceInvocationMessage(Type type, long id, MethodInfo method, object[] args) : base(type)
        {
            Id = id;
            Method = method;
            Arguments = args;
        }

        public long Id { get; private set; }

        public MethodInfo Method
        {
            get => _endpoint.Method;
            private set => _endpoint = new ServicePort(value);
        }
        public object[] Arguments { get; private set; }

    }
    [Serializable]
    public class ServiceInvocationResultMessage : ServiceMessage
    {
        public ServiceInvocationResultMessage(Type type, long id, object returnValue = null, Exception exceptionValue = null) : base(type)
        {
            Id = id;
            ReturnValue = returnValue;
            ExceptionValue = exceptionValue;
        }

        public long Id { get; private set; }

        public object ReturnValue { get; private set; }
        public Exception ExceptionValue { get; private set; }
    }

    [Serializable]
    public struct ServiceIdentifier
    {
        string _serviceType;

        internal ServiceIdentifier(Type type)
        {
            _serviceType = type.AssemblyQualifiedName;
        }

        public Type Type
        {
            get
            {
                return Type.GetType(_serviceType);
            }
        }
    }
    [Serializable]
    public struct ServicePort
    {
        string _methodType;
        string _methodName;

        string[] _methodParamterTypes;

        internal ServicePort(MethodInfo method)
        {
            _methodType = method.DeclaringType.AssemblyQualifiedName;
            _methodName = method.Name;

            ParameterInfo[] parameterInfos = method.GetParameters();
            _methodParamterTypes = new string[parameterInfos.Length];
            for (int i = 0; i < parameterInfos.Length; i++)
                _methodParamterTypes[i] = parameterInfos[i].ParameterType.AssemblyQualifiedName;

        }

        public MethodInfo Method
        {
            get
            {
                Type methodType = Type.GetType(_methodType);

                Type[] parameterTypes = new Type[_methodParamterTypes.Length];

                for (int i = 0; i < parameterTypes.Length; i++)
                    parameterTypes[i] = Type.GetType(_methodParamterTypes[i]);

                return methodType.GetMethod(_methodName, parameterTypes);
            }
        }
    }

    [Serializable]
    public enum ServiceState
    {
        UNKNOWN = 0,

        STARTED,

        STOPPED
    }
}