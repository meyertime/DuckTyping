// DelegateDuckProxyType.cs
//
// Copyright (C) 2007 David Meyer
// All Rights Reserved
//
// Website: http://www.deftflux.net/
// E-mail: deftflux@deftflux.net
//
// This source is licensed to the public via Artistic License 2.0 which should be found in a file
// named license.txt included with the package.  It is also available online at:
// http://www.perlfoundation.org/artistic_license_2_0


using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.IO;

namespace DeftTech.DuckTyping
{
    /// <summary>
    /// Class that performs the generation of duck proxy types for forwarding delegate calls.
    /// </summary>
    internal sealed class DelegateDuckProxyType
    {
        private delegate Delegate WrapDuckDelegate(Delegate duck);

        private Type m_ToDelegateType;
        private Type m_FromDelegateType;
        private Type m_ProxyType;
        private WrapDuckDelegate m_WrapDuck;
        private MethodInfo m_InvokeMethod;

        /// <summary>
        /// Constructs an object.
        /// </summary>
        /// <param name="toDelegateType">Type to be casted to.</param>
        /// <param name="fromDelegateType">Type of delegate to be casted.</param>
        public DelegateDuckProxyType(Type toDelegateType, Type fromDelegateType)
        {
            m_ToDelegateType = toDelegateType;
            m_FromDelegateType = fromDelegateType;
            m_ProxyType = null;
        }

        /// <summary>
        /// Determines whether a proxy can be generated for the types given in the constructor.
        /// </summary>
        /// <returns>If a proxy can be generated for the types given in the constructor, true; otherwise, false.</returns>
        public bool CanProxy()
        {
            bool canProxy = false;

            if (m_ToDelegateType.IsSubclassOf(typeof(Delegate)) && m_FromDelegateType.IsSubclassOf(typeof(Delegate)))
            {
                MethodInfo variantMethod = m_ToDelegateType.GetMethod("Invoke");
                MethodInfo duckMethod = m_FromDelegateType.GetMethod("Invoke");

                // Must have a compatible parameter list
                ParameterInfo[] variantMethodParameters = variantMethod.GetParameters();
                ParameterInfo[] duckMethodParameters = duckMethod.GetParameters();

                if (duckMethodParameters.Length == variantMethodParameters.Length)
                {
                    bool areParameterTypesCompatible = true;

                    for (int i = 0; i < duckMethodParameters.Length; i++)
                    {
                        if (!DuckTyping.AreTypesCompatible(variantMethodParameters[i].ParameterType, duckMethodParameters[i].ParameterType))
                        {
                            areParameterTypesCompatible = false;
                            break;
                        }
                    }

                    if (areParameterTypesCompatible)
                    {
                        // Must have a compatible return type
                        if (DuckTyping.AreTypesCompatible(variantMethod.ReturnType, duckMethod.ReturnType))
                        {
                            canProxy = true;
                        }
                    }
                }
            }

            return canProxy;
        }

        /// <summary>
        /// Prepares by generating the proxy type for the types given in the constructor.
        /// </summary>
        public void Prepare()
        {
            GenerateProxyType();
        }

        /// <summary>
        /// Calls the static WrapDuck method of the generated proxy type to initialize a new instance of
        /// the proxy type with a given duck delegate and return a delegate to its invoke method.
        /// </summary>
        /// <param name="duck">Duck delegate that the new proxy object should forward calls to.</param>
        /// <returns>A delegate of the type given in the constructor that calls the given duck delegate.</returns>
        public Delegate WrapDuck(Delegate duck)
        {
            GenerateProxyType();
            
            return m_WrapDuck(duck);
        }

        /// <summary>
        /// If a proxy type has not been generated yet, generates a proxy type that defines a method 
        /// matching the method signature of the to delegate type given in the constructor which forwards 
        /// calls to a delegate of the from type given in the constructor.
        /// </summary>
        private void GenerateProxyType()
        {
            if (m_ProxyType == null)
            {
                if (!CanProxy())
                {
                    throw new ArgumentException(m_FromDelegateType.FullName + " is not compatible with " + m_ToDelegateType.FullName + ".");
                }

                AssemblyBuilderAccess assemblyBuilderAccess;

//#if !DEBUG
                assemblyBuilderAccess = AssemblyBuilderAccess.Run;
//#else
                //assemblyBuilderAccess = AssemblyBuilderAccess.RunAndSave;
//#endif

                AppDomain domain = Thread.GetDomain();
                string assemblyName = "DuckDelegateProxy_" + m_ToDelegateType.Name.Replace(".", "_").Replace("+", "-") + "_" + m_FromDelegateType.Name.Replace(".", "_").Replace("+", "-") + ".dll";
                AssemblyBuilder assembly = domain.DefineDynamicAssembly(new AssemblyName(assemblyName), assemblyBuilderAccess);
                ModuleBuilder module = assembly.DefineDynamicModule(assemblyName);

                TypeBuilder proxyType = module.DefineType("DuckDelegateProxy");
                proxyType.AddInterfaceImplementation(typeof(IDuckProxy));

                // Define private field to hold a reference to the duck delegate to forward calls to.
                FieldBuilder duckDelegateField = proxyType.DefineField("m_DuckDelegate", m_FromDelegateType, FieldAttributes.Private);

                // Define private .ctor(duckDelegateType duckDelegate)
                ConstructorBuilder constructor = proxyType.DefineConstructor(MethodAttributes.Private, CallingConventions.HasThis, new Type[] { m_FromDelegateType });
                ILGenerator constructorIL = constructor.GetILGenerator();
                constructorIL.Emit(OpCodes.Ldarg_0);
                constructorIL.Emit(OpCodes.Ldarg_1);
                constructorIL.Emit(OpCodes.Stfld, duckDelegateField);
                constructorIL.Emit(OpCodes.Ret);

                // Define Invoke method
                MethodBuilder invokeMethod = ImplementInvokeMethod(proxyType, duckDelegateField);

                // Define public static Delegate Wrap(Delegate duck)
                MethodBuilder wrapMethod = proxyType.DefineMethod("Wrap", MethodAttributes.Public | MethodAttributes.Static, CallingConventions.Standard, typeof(Delegate), new Type[] { typeof(Delegate) });
                ILGenerator wrapMethodIL = wrapMethod.GetILGenerator();
                wrapMethodIL.Emit(OpCodes.Ldarg_0);
                wrapMethodIL.Emit(OpCodes.Newobj, constructor);
                wrapMethodIL.Emit(OpCodes.Ldftn, invokeMethod);
                wrapMethodIL.Emit(OpCodes.Newobj, m_ToDelegateType.GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
                wrapMethodIL.Emit(OpCodes.Ret);

                // Define public object UnwrapDuck() - Implementation of IDuckProxy
                MethodBuilder unwrapMethod = proxyType.DefineMethod("UnwrapDuck", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final, CallingConventions.HasThis, typeof(object), new Type[0]);
                ILGenerator unwrapMethodIL = unwrapMethod.GetILGenerator();
                unwrapMethodIL.Emit(OpCodes.Ldarg_0);
                unwrapMethodIL.Emit(OpCodes.Ldfld, duckDelegateField);
                unwrapMethodIL.Emit(OpCodes.Ret);

                // Bake it
                m_ProxyType = proxyType.CreateType();
                m_WrapDuck = (WrapDuckDelegate)(Delegate.CreateDelegate(typeof(WrapDuckDelegate), m_ProxyType, wrapMethod.Name));
                m_InvokeMethod = m_ProxyType.GetMethod("Invoke");

//#if DEBUG
                // If we're in debug mode, save the assembly so we can disassemble it if we want.
                //assembly.Save(assemblyName);
//#endif
            }
        }

        /// <summary>
        /// Implements the method of the proxy type that invokes the delegate given in its constructor.
        /// </summary>
        /// <param name="proxyType">Type builder to define the invoke method in.</param>
        /// <param name="duckDelegateField">Field that stores a reference to the delegate to call.</param>
        /// <returns>The implemented invoke method.</returns>
        private MethodBuilder ImplementInvokeMethod(TypeBuilder proxyType, FieldInfo duckDelegateField)
        {
            MethodInfo variantMethod = m_ToDelegateType.GetMethod("Invoke");
            MethodInfo duckMethod = m_FromDelegateType.GetMethod("Invoke");

            ParameterInfo[] variantMethodParameters = variantMethod.GetParameters();
            ParameterInfo[] duckMethodParameters = duckMethod.GetParameters();

            MethodBuilder proxyMethod = proxyType.DefineMethod("Invoke", MethodAttributes.Public | MethodAttributes.HideBySig, CallingConventions.HasThis, variantMethod.ReturnType, GetParameterTypes(variantMethodParameters));
            ILGenerator proxyMethodIL = proxyMethod.GetILGenerator();

            // Emit IL to load the proxy instance, then load the value of its duck delegate field
            proxyMethodIL.Emit(OpCodes.Ldarg_0);
            proxyMethodIL.Emit(OpCodes.Ldfld, duckDelegateField);

            // Emit IL to load method arguments
            for (int i = 0; i < variantMethodParameters.Length; i++)
            {
                // Emit IL to load the argument
                proxyMethodIL.Emit(OpCodes.Ldarg, i + 1);

                // Emit IL to cast the argument if necessary
                DuckTyping.EmitCastIL(proxyMethodIL, duckMethodParameters[i].ParameterType, variantMethodParameters[i].ParameterType);
            }

            // Emit IL to call the delegate
            proxyMethodIL.Emit(OpCodes.Callvirt, duckMethod);

            // If we are returning something...
            if (duckMethod.ReturnType != typeof(void))
            {
                // Emit IL to cast the return value if necessary
                DuckTyping.EmitCastIL(proxyMethodIL, variantMethod.ReturnType, duckMethod.ReturnType);
            }

            // Emit IL to return.
            proxyMethodIL.Emit(OpCodes.Ret);

            return proxyMethod;
        }

        /// <summary>
        /// Utility to create an array of parameter types from an array of parameters.
        /// </summary>
        /// <param name="parameters">Array of parameters to return the parameter types of.</param>
        /// <returns>An array of parameter types of the given method.</returns>
        private Type[] GetParameterTypes(ParameterInfo[] parameters)
        {
            Type[] parameterTypes = new Type[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                parameterTypes[i] = parameters[i].ParameterType;
            }

            return parameterTypes;
        }        
    }
}
