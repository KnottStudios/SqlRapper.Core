using System;
using System.Reflection;

namespace SqlRapperTests
{
    public static class PrivateMethod
    {
        public static object InvokePrivateMethodWithReturnType<T>(T testObject, string methodName, BindingFlags flags, Type[] genericTypes, object[] parameters) {
            MethodInfo methodInfo = testObject.GetType().GetMethod(methodName, flags);
            if (methodInfo == null) {
                throw new Exception("Unable to find method.");
            }
            MethodInfo method = methodInfo.MakeGenericMethod(genericTypes);
            return method.Invoke(genericTypes, parameters);
        } 
    }
}
