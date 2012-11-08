// InterfaceDuckProxyType.cs
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
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

namespace DeftTech.DuckTyping
{
    /// <summary>
    /// Class that performs the generation of duck proxy types for the dynamic implementation of interfaces.
    /// </summary>
    internal sealed class InterfaceDuckProxyType
    {
        private delegate object WrapDuckDelegate(object duck);

        private Type m_InterfaceType;
        private Type m_DuckType;
        private bool m_IsDuckStatic;

        private Type m_ProxyType;
        private WrapDuckDelegate m_WrapDuck;
        private object m_StaticProxy;

        /// <summary>
        /// Constructs an object.
        /// </summary>
        /// <param name="interfaceType">Type to be casted to.</param>
        /// <param name="duckType">Type of object to be casted.</param>
        /// <param name="isDuckStatic">Whether or not the duck is a static class instead of an object instance.</param>
        public InterfaceDuckProxyType(Type interfaceType, Type duckType, bool isDuckStatic)
        {
            m_InterfaceType = interfaceType;
            m_DuckType = duckType;
            m_IsDuckStatic = isDuckStatic;

            m_ProxyType = null;
        }

        /// <summary>
        /// Gets the interface type to be casted to.
        /// </summary>
        public Type InterfaceType { get { return m_InterfaceType; } }

        /// <summary>
        /// Gets the duck type to be casted.
        /// </summary>
        public Type DuckType { get { return m_DuckType; } }

        /// <summary>
        /// Gets whether the duck is a static class instead of an object instance.
        /// </summary>
        public bool IsDuckStatic { get { return m_IsDuckStatic; } }

        /// <summary>
        /// Determines whether a proxy can be generated for the types given in the constructor.
        /// </summary>
        /// <returns>If a proxy can be generated for the types given in the constructor, true; otherwise, false.</returns>
        public bool CanProxy()
        {
            return DuckImplementsAllMembers();
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
        /// the proxy type with a given duck object.
        /// </summary>
        /// <param name="duck">Duck object that the new proxy object should forward calls to.</param>
        /// <returns>
        /// An instance of the proxy type, which implements the interface type given in the constructor, initialized with 
        /// the given duck object.
        /// </returns>
        public object WrapDuck(object duck)
        {
            if (m_IsDuckStatic) throw new InvalidOperationException("Cannot call WrapDuck when IsDuckStatic is true.");

            // Make sure the proxy type is generated before we try to call it.
            GenerateProxyType();

            // Call wrap method in the proxy type to create an instance of the proxy for the duck and return it.
            return m_WrapDuck(duck);
        }

        /// <summary>
        /// Gets an instance of the proxy type that forwards calls to the static members of the duck type.
        /// </summary>
        /// <returns>
        /// An instance of the proxy type, which implements the interface type given in the constructor and forwards calls to
        /// the static members of the duck type.
        /// </returns>
        public object GetStaticProxy()
        {
            if (!m_IsDuckStatic) throw new InvalidOperationException("Cannot call GetStaticProxy when IsDuckStatic is false.");

            GenerateProxyType();

            return m_StaticProxy;
        }

        /// <summary>
        /// If a proxy type has not been generated yet, generates a proxy type that implements 
        /// the interface type given in the constructor and forwards calls to an object of the 
        /// duck type given in the constructor.
        /// </summary>
        private void GenerateProxyType()
        {
            if (m_ProxyType == null)
            {
                AssemblyBuilderAccess assemblyBuilderAccess;

#if !DEBUG
                assemblyBuilderAccess = AssemblyBuilderAccess.Run;
#else
                assemblyBuilderAccess = AssemblyBuilderAccess.RunAndSave;
#endif

                AppDomain domain = Thread.GetDomain();
                string assemblyName = "DuckInterfaceProxy_" + m_InterfaceType.Name.Replace(".", "_").Replace("+", "-") + "_" + m_DuckType.Name.Replace(".", "_").Replace("+", "-") + ".dll";
                AssemblyBuilder assembly = domain.DefineDynamicAssembly(new AssemblyName(assemblyName), assemblyBuilderAccess);
                ModuleBuilder module = assembly.DefineDynamicModule(assemblyName);

                TypeBuilder proxyType = module.DefineType("DuckProxy");
                proxyType.AddInterfaceImplementation(typeof(IDuckProxy));
                proxyType.AddInterfaceImplementation(m_InterfaceType);

                FieldBuilder duckField;

                ConstructorBuilder constructor;
                ILGenerator constructorIL;

                MethodBuilder wrapMethod;

                if (!m_IsDuckStatic)
                {
                    // Define private field to hold a reference to the duck object to forward calls to.
                    duckField = proxyType.DefineField("m_Duck", m_DuckType, FieldAttributes.Private);

                    // Define private .ctor(duckType duck)
                    constructor = proxyType.DefineConstructor(MethodAttributes.Private, CallingConventions.HasThis, new Type[] { m_DuckType });
                    constructorIL = constructor.GetILGenerator();
                    constructorIL.Emit(OpCodes.Ldarg_0);
                    constructorIL.Emit(OpCodes.Ldarg_1);
                    constructorIL.Emit(OpCodes.Stfld, duckField);
                    // constructorIL is passed to other methods so that they can add code to the constructor.
                    // constructorIL.Emit(OpCodes.Ret); - This code should appear below after other methods are called.

                    // Define public static object Wrap(object duck)
                    wrapMethod = proxyType.DefineMethod("Wrap", MethodAttributes.Public | MethodAttributes.Static, CallingConventions.Standard, typeof(object), new Type[] { typeof(object) });
                    ILGenerator wrapMethodIL = wrapMethod.GetILGenerator();
                    wrapMethodIL.Emit(OpCodes.Ldarg_0);
                    if (m_DuckType.IsValueType)
                    {
                        wrapMethodIL.Emit(OpCodes.Box, m_DuckType);
                    }
                    wrapMethodIL.Emit(OpCodes.Newobj, constructor);
                    wrapMethodIL.Emit(OpCodes.Ret);

                    // Define public object UnwrapDuck() - Implementation of IDuckProxy
                    MethodBuilder unwrapMethod = proxyType.DefineMethod("UnwrapDuck", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final, CallingConventions.HasThis, typeof(object), new Type[0]);
                    ILGenerator unwrapMethodIL = unwrapMethod.GetILGenerator();
                    unwrapMethodIL.Emit(OpCodes.Ldarg_0);
                    unwrapMethodIL.Emit(OpCodes.Ldfld, duckField);
                    if (m_DuckType.IsValueType)
                    {
                        unwrapMethodIL.Emit(OpCodes.Box, m_DuckType);
                    }
                    unwrapMethodIL.Emit(OpCodes.Ret);

                    // Override public bool Equals() method
                    MethodBuilder equalsMethod = proxyType.DefineMethod("Equals", MethodAttributes.Public | MethodAttributes.Virtual, CallingConventions.HasThis, typeof(bool), new Type[] { typeof(object) });
                    MethodInfo objectEqualsMethod = typeof(object).GetMethod("Equals", new Type[] { typeof(object) });
                    proxyType.DefineMethodOverride(equalsMethod, objectEqualsMethod);
                    ILGenerator equalsMethodIL = equalsMethod.GetILGenerator();
                    equalsMethodIL.Emit(OpCodes.Ldarg_0);
                    equalsMethodIL.Emit(OpCodes.Ldfld, duckField);
                    equalsMethodIL.Emit(OpCodes.Ldarg_1);
                    equalsMethodIL.Emit(OpCodes.Call, typeof(DuckTyping).GetMethod("Uncast", new Type[] { typeof(object) }));
                    equalsMethodIL.Emit(OpCodes.Callvirt, objectEqualsMethod);
                    equalsMethodIL.Emit(OpCodes.Ret);

                    // Override public int GetHashCode() method
                    MethodBuilder getHashCodeMethod = proxyType.DefineMethod("GetHashCode", MethodAttributes.Public | MethodAttributes.Virtual, CallingConventions.HasThis, typeof(int), new Type[0]);
                    MethodInfo objectGetHashCodeMethod = typeof(object).GetMethod("GetHashCode", new Type[0]);
                    proxyType.DefineMethodOverride(getHashCodeMethod, objectGetHashCodeMethod);
                    ILGenerator getHashCodeMethodIL = getHashCodeMethod.GetILGenerator();
                    getHashCodeMethodIL.Emit(OpCodes.Ldarg_0);
                    getHashCodeMethodIL.Emit(OpCodes.Ldfld, duckField);
                    getHashCodeMethodIL.Emit(OpCodes.Callvirt, objectGetHashCodeMethod);
                    getHashCodeMethodIL.Emit(OpCodes.Ret);

                    // Override string ToString() method
                    MethodBuilder toStringMethod = proxyType.DefineMethod("ToString", MethodAttributes.Public | MethodAttributes.Virtual, CallingConventions.HasThis, typeof(string), new Type[0]);
                    MethodInfo objectToStringMethod = typeof(object).GetMethod("ToString", new Type[0]);
                    proxyType.DefineMethodOverride(toStringMethod, objectToStringMethod);
                    ILGenerator toStringMethodIL = toStringMethod.GetILGenerator();
                    toStringMethodIL.Emit(OpCodes.Ldarg_0);
                    toStringMethodIL.Emit(OpCodes.Ldfld, duckField);
                    toStringMethodIL.Emit(OpCodes.Callvirt, objectToStringMethod);
                    toStringMethodIL.Emit(OpCodes.Ret);
                }
                else
                {
                    duckField = null;

                    // Define public .ctor()
                    constructor = proxyType.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis, new Type[0]);
                    constructorIL = constructor.GetILGenerator();

                    wrapMethod = null;

                    // Define public object UnwrapDuck() - Implementation of IDuckProxy
                    MethodBuilder unwrapMethod = proxyType.DefineMethod("UnwrapDuck", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final, CallingConventions.HasThis, typeof(object), new Type[0]);
                    ILGenerator unwrapMethodIL = unwrapMethod.GetILGenerator();
                    unwrapMethodIL.Emit(OpCodes.Ldnull);
                    unwrapMethodIL.Emit(OpCodes.Ret);
                }

                // Define all members of m_InterfaceType
                ProxyMemberDictionary proxyMembers = new ProxyMemberDictionary();
                ImplementInterface(proxyType, proxyMembers, duckField, constructorIL, m_InterfaceType);

                // Now that we are done implementing members, finish constructor method body:
                constructorIL.Emit(OpCodes.Ret);

                // Bake it
                m_ProxyType = proxyType.CreateType();

                if (!m_IsDuckStatic)
                {
                    m_WrapDuck = (WrapDuckDelegate)(Delegate.CreateDelegate(typeof(WrapDuckDelegate), m_ProxyType, wrapMethod.Name));
                }
                else
                {
                    m_StaticProxy = Activator.CreateInstance(m_ProxyType);
                }

//#if DEBUG
                // If we're in debug mode, save the assembly so we can disassemble it if we want.
                //assembly.Save(assemblyName);
//#endif
            }
        }

        #region Proxy type member implementation methods

        private void ImplementInterface(TypeBuilder proxyType, ProxyMemberDictionary proxyMembers, FieldInfo duckField, ILGenerator constructorIL, Type interfaceType)
        {
            ImplementInterface(proxyType, proxyMembers, duckField, constructorIL, interfaceType, new List<Type>());
        }

        private void ImplementInterface(TypeBuilder proxyType, ProxyMemberDictionary proxyMembers, FieldInfo duckField, ILGenerator constructorIL, Type interfaceType, List<Type> implementedInterfaces)
        {
            if (!implementedInterfaces.Contains(interfaceType))
            {
                MemberInfo[] members = interfaceType.GetMembers();
                foreach (MemberInfo member in members)
                {
                    ImplementMember(proxyType, proxyMembers, duckField, constructorIL, member);
                }

                implementedInterfaces.Add(interfaceType);

                Type[] subInterfaces = interfaceType.GetInterfaces();
                foreach (Type subInterfaceType in subInterfaces)
                {
                    ImplementInterface(proxyType, proxyMembers, duckField, constructorIL, subInterfaceType, implementedInterfaces);
                }
            }
        }

        /// <summary>
        /// Implements an interface member in a duck proxy type using a given type builder.
        /// If successful, the implemented member will be added to the given proxy member dictionary.
        /// </summary>
        /// <param name="proxyType">Type builder for the duck proxy type.</param>
        /// <param name="proxyMembers">Dictionary of members of the proxy type.</param>
        /// <param name="duckField">Field that holds a reference to the duck object to forward calls to.</param>
        /// <param name="constructorIL">IL generator to use to add code to the constructor if necessary.</param>
        /// <param name="interfaceMember">The interface member to implement.</param>
        private void ImplementMember(TypeBuilder proxyType, ProxyMemberDictionary proxyMembers, FieldInfo duckField, ILGenerator constructorIL, MemberInfo interfaceMember)
        {
            if (interfaceMember.MemberType == MemberTypes.Method)
            {
                ImplementMethod(proxyType, proxyMembers, duckField, constructorIL, (MethodInfo)interfaceMember);
            }
            else if (interfaceMember.MemberType == MemberTypes.Property)
            {
                ImplementProperty(proxyType, proxyMembers, duckField, constructorIL, (PropertyInfo)interfaceMember);
            }
            else if (interfaceMember.MemberType == MemberTypes.Event)
            {
                ImplementEvent(proxyType, proxyMembers, duckField, constructorIL, (EventInfo)interfaceMember);
            }
            else
            {
                throw new NotSupportedException("Interface defines a member type that is not supported.");
            }
        }

        /// <summary>
        /// Implements an interface method in a duck proxy type using a given type builder.
        /// If successful, the implemented method will be added to the given proxy member dictionary.
        /// </summary>
        /// <param name="proxyType">Type builder for the duck proxy type.</param>
        /// <param name="proxyMembers">Dictionary of members of the proxy type.</param>
        /// <param name="duckField">Field that holds a reference to the duck object to forward calls to.</param>
        /// <param name="constructorIL">IL generator to use to add code to the constructor if necessary.</param>
        /// <param name="interfaceMethod">The interface method to implement.</param>
        private void ImplementMethod(TypeBuilder proxyType, ProxyMemberDictionary proxyMembers, FieldInfo duckField, ILGenerator constructorIL, MethodInfo interfaceMethod)
        {
            MethodInfo duckMethod = FindDuckMethod(interfaceMethod);
            if (duckMethod == null)
            {
                throw new NotImplementedException("Duck type does not implement a method named \"" + interfaceMethod.Name + "\" with compatible parameters and return type.");
            }

            if (!duckMethod.IsSpecialName || (!duckMethod.Name.StartsWith("add_") && !duckMethod.Name.StartsWith("remove_")))
            {
                ParameterInfo[] interfaceMethodParameters = interfaceMethod.GetParameters();
                ParameterInfo[] duckMethodParameters = duckMethod.GetParameters();

                MethodBuilder proxyMethod = proxyType.DefineMethod(interfaceMethod.Name, MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final, CallingConventions.HasThis, interfaceMethod.ReturnType, GetParameterTypes(interfaceMethodParameters));

                GenericTypeParameterBuilder[] genericParameters;

                if (interfaceMethod.IsGenericMethodDefinition)
                {
                    Type[] interfaceMethodGenericArguments = interfaceMethod.GetGenericArguments();
                    string[] genericParameterNames = new string[interfaceMethodGenericArguments.Length];
                    for (int i = 0; i < interfaceMethodGenericArguments.Length; i++)
                    {
                        genericParameterNames[i] = interfaceMethodGenericArguments[i].Name;
                    }
                    genericParameters = proxyMethod.DefineGenericParameters(genericParameterNames);
                }
                else
                {
                    genericParameters = new GenericTypeParameterBuilder[0];
                }
                
                ILGenerator proxyMethodIL = proxyMethod.GetILGenerator();

                // Emit IL to load the duck object if the method is not static
                if (!duckMethod.IsStatic)
                {
                    // Emit IL to load the proxy instance, then load the value of its duck field
                    proxyMethodIL.Emit(OpCodes.Ldarg_0);
                    proxyMethodIL.Emit(OpCodes.Ldfld, duckField);
                }

                // Emit IL to load method arguments
                for (int i = 0; i < interfaceMethodParameters.Length; i++)
                {
                    // Emit IL to load the argument
                    proxyMethodIL.Emit(OpCodes.Ldarg, 1 + i);

                    // Emit IL to cast the argument if necessary
                    DuckTyping.EmitCastIL(proxyMethodIL, duckMethodParameters[i].ParameterType, interfaceMethodParameters[i].ParameterType);
                }

                MethodInfo methodToCall;
                if (!duckMethod.IsGenericMethodDefinition)
                {
                    methodToCall = duckMethod;
                }
                else
                {
                    methodToCall = duckMethod.MakeGenericMethod((Type[])(genericParameters));
                }

                // Emit IL to call the method
                if (!duckMethod.IsStatic)
                {
                    proxyMethodIL.Emit(OpCodes.Callvirt, methodToCall);
                }
                else
                {
                    proxyMethodIL.Emit(OpCodes.Call, methodToCall);
                }

                // If we are returning something...
                if (duckMethod.ReturnType != typeof(void))
                {
                    // Emit IL to cast the return value if necessary
                    DuckTyping.EmitCastIL(proxyMethodIL, interfaceMethod.ReturnType, duckMethod.ReturnType);
                }

                // Emit IL to return.
                proxyMethodIL.Emit(OpCodes.Ret);

                // Add proxy method to proxy member dictionary
                // (This is so that any associated properties or events can refer to it later)
                if (proxyMembers != null) proxyMembers[duckMethod] = proxyMethod;
            }
        }

        /// <summary>
        /// Implements an interface property in a duck proxy type using a given type builder.
        /// If successful, the implemented property will be added to the given proxy member dictionary.
        /// </summary>
        /// <param name="proxyType">Type builder for the duck proxy type.</param>
        /// <param name="proxyMembers">Dictionary of members of the proxy type.</param>
        /// <param name="duckField">Field that holds a reference to the duck object to forward calls to.</param>
        /// <param name="constructorIL">IL generator to use to add code to the constructor if necessary.</param>
        /// <param name="interfaceProperty">The interface property to implement.</param>
        private void ImplementProperty(TypeBuilder proxyType, ProxyMemberDictionary proxyMembers, FieldInfo duckField, ILGenerator constructorIL, PropertyInfo interfaceProperty)
        {
            PropertyInfo duckProperty = FindDuckProperty(interfaceProperty);
            if (duckProperty == null)
            {
                throw new NotImplementedException("Duck type does not implement a property named \"" + interfaceProperty.Name + "\" with the same property type and index parameters.");
            }

            PropertyBuilder proxyProperty = proxyType.DefineProperty(interfaceProperty.Name, PropertyAttributes.None, interfaceProperty.PropertyType, GetParameterTypes(interfaceProperty.GetIndexParameters()));

            // Get and set methods appear before associated properties in the member array; thus, they have
            // already been defined and implemented.  The proxy member dictionary is used to refer to these.
            if (interfaceProperty.CanRead)
            {
                proxyProperty.SetGetMethod((MethodBuilder)(proxyMembers[duckProperty.GetGetMethod()]));
            }
            if (interfaceProperty.CanWrite)
            {
                proxyProperty.SetSetMethod((MethodBuilder)(proxyMembers[duckProperty.GetSetMethod()]));
            }

            // Add proxy property to the proxy member dictionary
            // (This is not really necessary, but good to keep things consistent)
            if (proxyMembers != null) proxyMembers[duckProperty] = proxyProperty;
        }

        /// <summary>
        /// Implements an interface event in a duck proxy type using a given type builder.
        /// If successful, the implemented event will be added to the given proxy member dictionary.
        /// </summary>
        /// <param name="proxyType">Type builder for the duck proxy type.</param>
        /// <param name="proxyMembers">Dictionary of members of the proxy type.</param>
        /// <param name="duckField">Field that holds a reference to the duck object to forward calls to.</param>
        /// <param name="constructorIL">IL generator to use to add code to the constructor if necessary.</param>
        /// <param name="interfaceEvent">The interface event to implement.</param>
        private void ImplementEvent(TypeBuilder proxyType, ProxyMemberDictionary proxyMembers, FieldInfo duckField, ILGenerator constructorIL, EventInfo interfaceEvent)
        {
            EventInfo duckEvent = FindDuckEvent(interfaceEvent);
            if (duckEvent == null)
            {
                throw new NotImplementedException("Duck type does not implement an event named \"" + interfaceEvent.Name + "\" with the same event handler type.");
            }

            EventBuilder proxyEvent = proxyType.DefineEvent(interfaceEvent.Name, EventAttributes.None, interfaceEvent.EventHandlerType);

            Type interfaceEventHandlerType = interfaceEvent.EventHandlerType;
            Type duckEventHandlerType = duckEvent.EventHandlerType;

            // Define event add method
            MethodBuilder addMethod = proxyType.DefineMethod("add_" + interfaceEvent.Name, MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final, CallingConventions.HasThis, typeof(void), new Type[] { interfaceEventHandlerType });
            ILGenerator addMethodIL = addMethod.GetILGenerator();

            // Define event remove method
            MethodBuilder removeMethod = proxyType.DefineMethod("remove_" + interfaceEvent.Name, MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final, CallingConventions.HasThis, typeof(void), new Type[] { interfaceEventHandlerType });
            ILGenerator removeMethodIL = removeMethod.GetILGenerator();

            if (interfaceEventHandlerType == duckEventHandlerType)
            {
                // If the event handler types are the same, we can just forward calls to add and remove like normal.

                MethodInfo duckAddMethod = duckEvent.GetAddMethod();
                if (!duckAddMethod.IsStatic)
                {
                    addMethodIL.Emit(OpCodes.Ldarg_0);
                    addMethodIL.Emit(OpCodes.Ldfld, duckField);
                    addMethodIL.Emit(OpCodes.Ldarg_1);
                    addMethodIL.Emit(OpCodes.Callvirt, duckAddMethod);
                }
                else
                {
                    addMethodIL.Emit(OpCodes.Ldarg_1);
                    addMethodIL.Emit(OpCodes.Call, duckAddMethod);
                }

                MethodInfo duckRemoveMethod = duckEvent.GetRemoveMethod();
                if (!duckRemoveMethod.IsStatic)
                {
                    removeMethodIL.Emit(OpCodes.Ldarg_0);
                    removeMethodIL.Emit(OpCodes.Ldfld, duckField);
                    removeMethodIL.Emit(OpCodes.Ldarg_1);
                    removeMethodIL.Emit(OpCodes.Callvirt, duckRemoveMethod);
                }
                else
                {
                    removeMethodIL.Emit(OpCodes.Ldarg_1);
                    removeMethodIL.Emit(OpCodes.Call, duckRemoveMethod);
                }
            }
            else
            {
                // If the event handler types are different, we need some special behavior.  Specifically, the event handlers
                // added using the interface are kept as a seperate multicast delegate in the proxy class and an event handler
                // is added to the duck that calls the proxy event delegate.

                // Define the event multicast delegate field
                FieldBuilder eventHandlerField = proxyType.DefineField(interfaceEvent.Name, interfaceEventHandlerType, FieldAttributes.Private);

                // Implement the add method
                addMethodIL.Emit(OpCodes.Ldarg_0);
                addMethodIL.Emit(OpCodes.Ldarg_0);
                addMethodIL.Emit(OpCodes.Ldfld, eventHandlerField);
                addMethodIL.Emit(OpCodes.Ldarg_1);
                addMethodIL.Emit(OpCodes.Call, typeof(Delegate).GetMethod("Combine", new Type[] { typeof(Delegate), typeof(Delegate) }));
                addMethodIL.Emit(OpCodes.Castclass, interfaceEventHandlerType);
                addMethodIL.Emit(OpCodes.Stfld, eventHandlerField);

                // Implement the remove method
                removeMethodIL.Emit(OpCodes.Ldarg_0);
                removeMethodIL.Emit(OpCodes.Ldarg_0);
                removeMethodIL.Emit(OpCodes.Ldfld, eventHandlerField);
                removeMethodIL.Emit(OpCodes.Ldarg_1);
                removeMethodIL.Emit(OpCodes.Call, typeof(Delegate).GetMethod("Remove", new Type[] { typeof(Delegate), typeof(Delegate) }));
                removeMethodIL.Emit(OpCodes.Castclass, interfaceEventHandlerType);
                removeMethodIL.Emit(OpCodes.Stfld, eventHandlerField);

                // Set some local variables for later use...
                MethodInfo interfaceInvokeMethod = interfaceEventHandlerType.GetMethod("Invoke");
                Type[] interfaceInvokeMethodParameterTypes = GetParameterTypes(interfaceInvokeMethod.GetParameters());
                
                MethodInfo duckInvokeMethod = duckEventHandlerType.GetMethod("Invoke");
                Type[] duckInvokeMethodParameterTypes = GetParameterTypes(duckInvokeMethod.GetParameters());


                // Define the method that will serve as an event handler to the duck which will invoke the proxy's delegate
                MethodBuilder proxyInvokeMethod = proxyType.DefineMethod("Invoke" + interfaceEvent.Name, MethodAttributes.Private | MethodAttributes.Virtual | MethodAttributes.Final, CallingConventions.HasThis, duckInvokeMethod.ReturnType, duckInvokeMethodParameterTypes);
                ILGenerator proxyInvokeMethodIL = proxyInvokeMethod.GetILGenerator();

                // First, check if the proxy event handler is null
                Label ifEventHandlerIsNullLabel = proxyInvokeMethodIL.DefineLabel();
                proxyInvokeMethodIL.Emit(OpCodes.Ldarg_0);
                proxyInvokeMethodIL.Emit(OpCodes.Ldfld, eventHandlerField);
                proxyInvokeMethodIL.Emit(OpCodes.Ldnull);
                proxyInvokeMethodIL.Emit(OpCodes.Ceq);
                proxyInvokeMethodIL.Emit(OpCodes.Brtrue_S, ifEventHandlerIsNullLabel);
                
                // If the proxy event handler is not null, invoke it
                proxyInvokeMethodIL.Emit(OpCodes.Ldarg_0);
                proxyInvokeMethodIL.Emit(OpCodes.Ldfld, eventHandlerField);
                for (int i = 0; i < interfaceInvokeMethodParameterTypes.Length; i++)
                {
                    proxyInvokeMethodIL.Emit(OpCodes.Ldarg, i + 1);
                    DuckTyping.EmitCastIL(proxyInvokeMethodIL, interfaceInvokeMethodParameterTypes[i], duckInvokeMethodParameterTypes[i]);
                }
                proxyInvokeMethodIL.Emit(OpCodes.Callvirt, interfaceInvokeMethod);

                // If the proxy event handler is null, execution jumps here
                proxyInvokeMethodIL.MarkLabel(ifEventHandlerIsNullLabel);

                // Return
                proxyInvokeMethodIL.Emit(OpCodes.Ret);


                // Add code to the constructor to add the event handler to the duck.
                MethodInfo duckAddMethod = duckEvent.GetAddMethod();
                if (!duckAddMethod.IsStatic)
                {
                    constructorIL.Emit(OpCodes.Ldarg_0);
                    constructorIL.Emit(OpCodes.Ldfld, duckField);
                }
                constructorIL.Emit(OpCodes.Ldarg_0);
                constructorIL.Emit(OpCodes.Ldftn, proxyInvokeMethod);
                constructorIL.Emit(OpCodes.Newobj, duckEventHandlerType.GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
                if (!duckAddMethod.IsStatic)
                {
                    constructorIL.Emit(OpCodes.Callvirt, duckAddMethod);
                }
                else
                {
                    constructorIL.Emit(OpCodes.Call, duckAddMethod);
                }
            }

            // Finish add method and set it for the event
            addMethodIL.Emit(OpCodes.Ret);
            proxyEvent.SetAddOnMethod(addMethod);

            // Finish remove method and set it for the event
            removeMethodIL.Emit(OpCodes.Ret);
            proxyEvent.SetRemoveOnMethod(removeMethod);

            // Other associated methods appear before the event in the member array; thus, they have already been 
            // defined and implemented.  The proxy member dictionary is used to refer to these.
            MethodInfo raiseMethod = duckEvent.GetRaiseMethod();
            if (raiseMethod != null) proxyEvent.SetRaiseMethod((MethodBuilder)(proxyMembers[raiseMethod]));

            MethodInfo[] otherDuckMethods = duckEvent.GetOtherMethods();
            foreach (MethodInfo otherDuckMethod in otherDuckMethods)
            {
                if (proxyMembers.ContainsKey(otherDuckMethod))
                {
                    proxyEvent.AddOtherMethod((MethodBuilder)(proxyMembers[otherDuckMethod]));
                }
            }

            // Add proxy event to the proxy member dictionary
            // (This is not really necessary, but good to keep things consistent)
            // if (proxyMembers != null) proxyMembers[duckEvent] =  proxyEvent;
            // For some reason, EventBuilder does not inherit MemberInfo, so it cannot be added.
        }

        #endregion

        #region Duck type implementation determination and search methods

        /// <summary>
        /// Determines whether the type given in the constructor implements all the members of TInterface.
        /// </summary>
        /// <returns>If the type given in the constructor implements all the members of TInterface, true; otherwise, false.</returns>
        private bool DuckImplementsAllMembers()
        {
            bool implementsAll = true;

            MemberInfo[] members = m_InterfaceType.GetMembers();
            foreach (MemberInfo member in members)
            {
                if (!DuckImplementsMember(member))
                {
                    implementsAll = false;
                    break;
                }
            }

            return implementsAll;
        }

        /// <summary>
        /// Determines whether the type given in the constructor implements a given interface member.
        /// </summary>
        /// <remarks>
        /// This method is used in determining whether an object of a given type can be duck typed to TInterface.
        /// </remarks>
        /// <param name="interfaceMember">Interface member to check for.</param>
        /// <returns>If the type given in the constructor implements the given interface member, true; otherwise, false.</returns>
        private bool DuckImplementsMember(MemberInfo interfaceMember)
        {
            bool implementsMember;

            if (interfaceMember.MemberType == MemberTypes.Method)
            {
                implementsMember = (FindDuckMethod((MethodInfo)interfaceMember) != null);
            }
            else if (interfaceMember.MemberType == MemberTypes.Property)
            {
                implementsMember = (FindDuckProperty((PropertyInfo)interfaceMember) != null);
            }
            else if (interfaceMember.MemberType == MemberTypes.Event)
            {
                implementsMember = (FindDuckEvent((EventInfo)interfaceMember) != null);
            }
            else
            {
                throw new NotSupportedException("Interface defines a member type that is not supported.");
            }

            return implementsMember;
        }

        /// <summary>
        /// Finds the duck implementation for a given interface method.
        /// </summary>
        /// <param name="interfaceMethod">Interface method to find the implementation of.</param>
        /// <returns>If the type given in the constructor implements the given interface method, such method; otherwise, null.</returns>
        private MethodInfo FindDuckMethod(MethodInfo interfaceMethod)
        {
            MethodInfo duckMethod = null;
            int bestParameterCompatibilityScore = int.MinValue;

            MethodInfo[] methods = m_DuckType.GetMethods();
            foreach (MethodInfo method in methods)
            {
                // If casting a static class, must be static; if not, must not be static
                if (method.IsStatic == m_IsDuckStatic)
                {
                    // Must be public and have the same name
                    if (method.IsPublic && method.Name == interfaceMethod.Name)
                    {
                        // Must both be non-generic or both generic
                        if (method.IsGenericMethodDefinition == interfaceMethod.IsGenericMethodDefinition)
                        {
                            // Must have compatible generic arguments
                            bool genericArgumentsMatch = false;

                            if (method.IsGenericMethodDefinition)
                            {
                                Type[] genericArguments = method.GetGenericArguments();
                                Type[] interfaceGenericArguments = interfaceMethod.GetGenericArguments();

                                if (genericArguments.Length == interfaceGenericArguments.Length)
                                {
                                    // TODO: Check generic argument constraints.

                                    genericArgumentsMatch = true;
                                }
                            }
                            else
                            {
                                genericArgumentsMatch = true;
                            }

                            if (genericArgumentsMatch)
                            {
                                // Must have a compatible parameter list
                                int parameterCompatibilityScore = 0;

                                ParameterInfo[] parameters = method.GetParameters();
                                ParameterInfo[] interfaceMethodParameters = interfaceMethod.GetParameters();

                                if (parameters.Length == interfaceMethodParameters.Length)
                                {
                                    bool areParameterTypesCompatible = true;

                                    for (int i = 0; i < parameters.Length; i++)
                                    {
                                        if (!DuckTyping.AreTypesCompatible(parameters[i].ParameterType, interfaceMethodParameters[i].ParameterType, true))
                                        {
                                            areParameterTypesCompatible = false;
                                            break;
                                        }

                                        parameterCompatibilityScore += DuckTyping.GetTypeCompatibilityScore(parameters[i].ParameterType, interfaceMethodParameters[i].ParameterType);
                                    }

                                    if (areParameterTypesCompatible)
                                    {
                                        // Must have a compatible return type
                                        if (DuckTyping.AreTypesCompatible(interfaceMethod.ReturnType, method.ReturnType, true))
                                        {
                                            parameterCompatibilityScore += DuckTyping.GetTypeCompatibilityScore(interfaceMethod.ReturnType, method.ReturnType);

                                            // Since there may be multiple method overloads that are compatible with the
                                            // interface method, we want to choose the best one.
                                            if (parameterCompatibilityScore > bestParameterCompatibilityScore)
                                            {
                                                duckMethod = method;
                                                bestParameterCompatibilityScore = parameterCompatibilityScore;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return duckMethod;
        }

        /// <summary>
        /// Finds the duck implementation for a given interface property.
        /// </summary>
        /// <param name="interfaceProperty">Interface property to find the implementation of.</param>
        /// <returns>If the type given in the constructor implements the given interface property, such property; otherwise, null.</returns>
        private PropertyInfo FindDuckProperty(PropertyInfo interfaceProperty)
        {
            PropertyInfo duckProperty = null;

            PropertyInfo[] properties = m_DuckType.GetProperties();
            foreach (PropertyInfo property in properties)
            {
                // Must have the same name
                if (property.Name == interfaceProperty.Name)
                {
                    // Must have a compatible property type
                    if (DuckTyping.AreTypesCompatible(interfaceProperty.PropertyType, property.PropertyType))
                    {
                        // Must implement the capabilities of the interface property
                        if ((property.CanRead || !interfaceProperty.CanRead) && (property.CanWrite || !interfaceProperty.CanWrite)
                            && (!interfaceProperty.CanRead || property.GetGetMethod().IsPublic) && (!interfaceProperty.CanWrite || property.GetSetMethod().IsPublic))
                        {
                            // Must have a compatible index parameter list
                            ParameterInfo[] indexParameters = property.GetIndexParameters();
                            ParameterInfo[] interfacePropertyIndexParameters = interfaceProperty.GetIndexParameters();

                            if (indexParameters.Length == interfacePropertyIndexParameters.Length)
                            {
                                bool areParameterTypesCompatible = true;

                                for (int i = 0; i < indexParameters.Length; i++)
                                {
                                    if (!DuckTyping.AreTypesCompatible(interfacePropertyIndexParameters[i].ParameterType, indexParameters[i].ParameterType))
                                    {
                                        areParameterTypesCompatible = false;
                                        break;
                                    }
                                }

                                if (areParameterTypesCompatible)
                                {
                                    duckProperty = property;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            return duckProperty;
        }

        /// <summary>
        /// Finds the duck implementation for a given interface event.
        /// </summary>
        /// <param name="interfaceEvent">Interface event to find the implementation of.</param>
        /// <returns>If the type given in the constructor implements the given interface event, such event; otherwise, null.</returns>
        private EventInfo FindDuckEvent(EventInfo interfaceEvent)
        {
            EventInfo duckEvent = null;

            EventInfo[] events = m_DuckType.GetEvents();
            foreach (EventInfo _event in events)
            {
                // Must have the same name
                if (_event.Name == interfaceEvent.Name)
                {
                    // Must have a compatible event handler type
                    if (DuckTyping.AreTypesCompatible(interfaceEvent.EventHandlerType, _event.EventHandlerType))
                    {
                        duckEvent = _event;
                        break;
                    }
                }
            }

            return duckEvent;
        }

        #endregion

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

        /// <summary>
        /// Class for keeping track of member builders while implementing proxy members.
        /// The key is the duck member that is called by the proxy.  The value is the proxy member that calls it.
        /// </summary>
        /// <remarks>
        /// This was defined in case it is decided to change the class later.
        /// </remarks>
        private class ProxyMemberDictionary : Dictionary<MemberInfo, MemberInfo>
        { }
    }
}
