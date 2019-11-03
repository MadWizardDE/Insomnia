using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace InsomniaTest
{
    class TestGeneric
    {
        void HandleMessage<T>(T message)
        {
            Console.WriteLine($"Typ={typeof(T).Name}, message={message.ToString()}");
        }

        public void Test()
        {
            Type stringType = typeof(string);

            var method = typeof(TestGeneric).GetMethod(nameof(HandleMessage), BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(stringType);

            method.Invoke(this, new object[] { "Hallo!" });
        }
    }
}
