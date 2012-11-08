// DuckProxyType.cs
//
// Copyright (C) 2007 David Meyer
// All Rights Reserved
//
// Website: http://www.deftflux.net/
// E-mail: deftflux@deftflux.net


using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

namespace DeftTech.DuckTyping
{
    /// <summary>
    /// Generic class that performs the generation of duck proxy types.
    /// </summary>
    /// <typeparam name="TInterface">Interface to cast to.</typeparam>
    internal sealed class DuckProxyType<TInterface>
    {
        private delegate TInterface WrapDuckDelegate(object duck);

        private static Dictionary<Type, DuckProxyType<TInterface>> s_ProxyTypes;

        /// <summary>
        /// Initializes static members.
        /// </summary>
        static DuckProxyType()
        {
            s_ProxyTypes = new Dictionary<Type, DuckProxyType<TInterface>>();
        }


        private Type m_DuckType;
        private Type m_ProxyType;
        private WrapDuckDelegate m_WrapDuck;

        /// <summary>
        /// Constructs an object.
        /// </summary>
        /// <param name="duckType">Type of object being casted.</param>
        private DuckProxyType(Type duckType)
        {
            m_DuckType = duckType;
            m_ProxyType = null;
        }


        /// <summary>
        /// If a proxy type has not been generated yet, generates a proxy type that implements 
        /// TInterface and forwards calls to an object of the type given in the constructor.
        /// </summary>
        private void GenerateProxyType()
        {
            if (m_ProxyType == null)
            {
                AppDomain domain = Thread.GetDomain();
                AssemblyBuilder assembly = domain.DefineDynamicAssembly(new AssemblyName("DuckProxy_" + typeof(TInterface).FullName.Replace(".", "_") + "_" + m_DuckType.FullName.Replace(".", "_")), AssemblyBuilderAccess.Run);
                ModuleBuilder module = assembly.DefineDynamicModule("DuckProxy");

                TypeBuilder proxyType = module.DefineType("DuckProxy");
                proxyType.AddInterfaceImplementation(typeof(IDuckProxy));
                proxyType.AddInterfaceImplementation(typeof(TInterface));

                // Define private field to hold a reference to the duck object to forward calls to.
                FieldBuilder duckField = proxyType.DefineField("m_Duck", m_DuckType, FieldAttributes.Private);

                // Define private .ctor(duckType duck)
                ConstructorBuilder constructor = proxyType.DefineConstructor(MethodAttributes.Private, CallingConventions.HasThis, new Type[] { m_DuckType });
                ILGenerator constructorIL = constructor.GetILGenerator();
                constructorIL.Emit(OpCodes.Ldarg_0);
                constructorIL.Emit(OpCodes.Ldarg_1);
                constructorIL.Emit(OpCodes.Stfld, duckField);
                constructorIL.Emit(OpCodes.Ret);

                // Define public static TInterface Wrap(object duck)
                MethodBuilder wrapMethod = proxyType.DefineMethod("Wrap", MethodAttributes.Public | MethodAttributes.Static, CallingConventions.Standard, typeof(TInterface), new Type[] { typeof(object) });
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

                // Define all members of TInterface
                MemberInfo[] members = typeof(TInterface).GetMembers();
                ProxyMemberDictionary proxyMembers = new ProxyMemberDictionary();
                foreach (MemberInfo member in members)
                {
                    ImplementMember(proxyType, proxyMembers, duckField, member);
                }

                m_ProxyType = proxyType.CreateType();
                m_WrapDuck = (WrapDuckDelegate)(Delegate.CreateDelegate(typeof(WrapDuckDelegate), m_ProxyType, wrapMethod.Name));
            }
        }

        #region Proxy type member implementation methods

        /// <summary>
        /// Implements an interface member in a duck proxy type using a given type builder.
        /// If successful, the implemented member will be added to the given proxy member dictionary.
        /// </summary>
        /// <param name="proxyType">Type builder for the duck proxy type.</param>
        /// <param name="proxyMembers">Dictionary of members of the proxy type.</param>
        /// <param name="duckField">Field that holds a reference to the duck object to forward calls to.</param>
        /// <param name="interfaceMember">The interface member to implement.</param>
        private void ImplementMember(TypeBuilder proxyType, ProxyMemberDictionary proxyMembers, FieldInfo duckField, MemberInfo interfaceMember)
        {
            if (interfaceMember.MemberType == MemberTypes.Method)
            {
                ImplementMethod(proxyType, proxyMembers, duckField, (MethodInfo)interfaceMember);
            }
            else if (interfaceMember.MemberType == MemberTypes.Property)
            {
                ImplementProperty(proxyType, proxyMembers, duckField, (PropertyInfo)interfaceMember);
            }
            else if (interfaceMember.MemberType == MemberTypes.Event)
            {
                ImplementEvent(proxyType, proxyMembers, duckField, (EventInfo)interfaceMember);
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
        /// <param name="interfaceMethod">The interface method to implement.</param>
        private void ImplementMethod(TypeBuilder proxyType, ProxyMemberDictionary proxyMembers, FieldInfo duckField, MethodInfo interfaceMethod)
        {
            MethodInfo duckMethod = FindDuckMethod(interfaceMethod);
            if (duckMethod == null)
            {
                throw new NotImplementedException("Duck type does not implement a method named \"" + interfaceMethod.Name + "\" with the same parameters and return type.");
            }

            ParameterInfo[] interfaceMethodParameters = interfaceMethod.GetParameters();
            ParameterInfo[] duckMethodParameters = duckMethod.GetParameters();

            MethodBuilder proxyMethod = proxyType.DefineMethod(interfaceMethod.Name, MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final, CallingConventions.HasThis, interfaceMethod.ReturnType, GetParameterTypes(interfaceMethodParameters));
            ILGenerator proxyMethodIL = proxyMethod.GetILGenerator();

            // Emit IL to load the proxy instance, then load the value of its duck field
            proxyMethodIL.Emit(OpCodes.Ldarg_0);
            proxyMethodIL.Emit(OpCodes.Ldfld, duckField);

            // Emit IL to load method arguments
            for (int i = 0; i < interfaceMethodParameters.Length; i++)
            {
                // Emit IL to load the argument
                proxyMethodIL.Emit(OpCodes.Ldarg, i + 1);

                // Emit IL to cast the argument if necessary
                DuckTyping.EmitCastIL(proxyMethodIL, duckMethodParameters[i].ParameterType, interfaceMethodParameters[i].ParameterType);
            }

            // Emit IL to call the method
            proxyMethodIL.Emit(OpCodes.Callvirt, duckMethod);

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
            proxyMembers.Add(duckMethod, proxyMethod);
        }

        /// <summary>
        /// Implements an interface property in a duck proxy type using a given type builder.
        /// If successful, the implemented property will be added to the given proxy member dictionary.
        /// </summary>
        /// <param name="proxyType">Type builder for the duck proxy type.</param>
        /// <param name="proxyMembers">Dictionary of members of the proxy type.</param>
        /// <param name="duckField">Field that holds a reference to the duck object to forward calls to.</param>
        /// <param name="interfaceProperty">The interface property to implement.</param>
        private void ImplementProperty(TypeBuilder proxyType, ProxyMemberDictionary proxyMembers, FieldInfo duckField, PropertyInfo interfaceProperty)
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
            proxyMembers.Add(duckProperty, proxyProperty);
        }

        /// <summary>
        /// Implements an interface event in a duck proxy type using a given type builder.
        /// If successful, the implemented event will be added to the given proxy member dictionary.
        /// </summary>
        /// <param name="proxyType">Type builder for the duck proxy type.</param>
        /// <param name="proxyMembers">Dictionary of members of the proxy type.</param>
        /// <param name="duckField">Field that holds a reference to the duck object to forward calls to.</param>
        /// <param name="interfaceEvent">The interface event to implement.</param>
        private void ImplementEvent(TypeBuilder proxyType, ProxyMemberDictionary proxyMembers, FieldInfo duckField, EventInfo interfaceEvent)
        {
            EventInfo duckEvent = FindDuckEvent(interfaceEvent);
            if (duckEvent == null)
            {
                throw new NotImplementedException("Duck type does not implement an event named \"" + interfaceEvent.Name + "\" with the same event handler type.");
            }

            EventBuilder proxyEvent = proxyType.DefineEvent(interfaceEvent.Name, EventAttributes.None, interfaceEvent.EventHandlerType);

            // Associated methods appear before the event in the member array; thus, they have already been 
            // defined and implemented.  The proxy member dictionary is used to refer to these.
            proxyEvent.SetAddOnMethod((MethodBuilder)(proxyMembers[duckEvent.GetAddMethod()]));
            proxyEvent.SetRemoveOnMethod((MethodBuilder)(proxyMembers[duckEvent.GetRemoveMethod()]));
            proxyEvent.SetRaiseMethod((MethodBuilder)(proxyMembers[duckEvent.GetRaiseMethod()]));
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
            // proxyMembers.Add(duckEvent, proxyEvent);
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

            MemberInfo[] members = typeof(TInterface).GetMembers();
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

            MethodInfo[] methods = m_DuckType.GetMethods();
            foreach (MethodInfo method in methods)
            {
                // Must be public and have the same name
                if (method.IsPublic && method.Name == interfaceMethod.Name)
                {
                    // Must have a compatible parameter list
                    ParameterInfo[] parameters = method.GetParameters();
                    ParameterInfo[] interfaceMethodParameters = interfaceMethod.GetParameters();

                    if (parameters.Length == interfaceMethodParameters.Length)
                    {
                        bool areParameterTypesCompatible = true;

                        for (int i = 0; i < parameters.Length; i++)
                        {
                            if (!DuckTyping.AreTypesCompatible(interfaceMethodParameters[i].ParameterType, parameters[i].ParameterType))
                            {
                                areParameterTypesCompatible = false;
                                break;
                            }
                        }

                        if (areParameterTypesCompatible)
                        {
                            // Must have a compatible return type
                            if (DuckTyping.AreTypesCompatible(interfaceMethod.ReturnType, method.ReturnType))
                            {
                                duckMethod = method;
                                break;
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
                    if (DuckTyping.AreTypesCompatible(interfaceEvent.EventHandlerType, duckEvent.EventHandlerType))
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
        /// Calls the static WrapDuck method of the generated proxy type to initialize a new instance of
        /// the proxy type with a given duck object.
        /// </summary>
        /// <param name="duck">Duck object that the new proxy object should forward calls to.</param>
        /// <returns>
        /// An instance of the proxy type, which implements TInterface, initialized with the given duck
        /// object.
        /// </returns>
        internal static TInterface WrapDuck(object duck)
        {
            DuckProxyType<TInterface> proxyType = GetProxyType(duck.GetType());

            // Make sure the proxy type is generated before we try to call it.
            proxyType.GenerateProxyType();

            // Call wrap method in the proxy type to create an instance of the proxy for the duck and return it.
            return proxyType.m_WrapDuck(duck);
        }

        internal static bool ShouldCast(Type duckType)
        {
            return (typeof(TInterface).IsInterface && !duckType.IsInterface);
        }

        /// <summary>
        /// Determines whether a given type can be duck casted to TInterface.
        /// </summary>
        /// <param name="duckType">Type of object being casted.</param>
        /// <returns>If the given type can be duck casted to TInterface, true; otherwise, false.</returns>
        internal static bool CanCast(Type duckType)
        {
            return ShouldCast(duckType) && new DuckProxyType<TInterface>(duckType).DuckImplementsAllMembers();
        }

        /// <summary>
        /// Prepares for a cast by generating the proxy type for a given duck type.
        /// </summary>
        /// <param name="duckType">Type of object being casted.</param>
        internal static void PrepareCast(Type duckType)
        {
            GetProxyType(duckType).GenerateProxyType();
        }

        /// <summary>
        /// Gets the DuckProxyType&lt;TInterface&gt; object for a given duck type.
        /// </summary>
        /// <remarks>
        /// First, the most general type in the inheritance chain of the given type that implements all the 
        /// members of TInterface is selected.  Then if a proxy type has already been generated, it returns 
        /// it.  Finally, if not, it creates a new object and adds it to the dictionary.
        /// </remarks>
        /// <param name="duckType">Type of object being casted.</param>
        /// <returns>The duck proxy type to use to cast or prepare for casting.</returns>
        private static DuckProxyType<TInterface> GetProxyType(Type duckType)
        {
            DuckProxyType<TInterface> proxyType = null;

            // Examine the duck type's inheritance chain.
            List<Type> inheritanceChain = new List<Type>();
            Type baseType = duckType;
            do
            {
                inheritanceChain.Add(baseType);
                baseType = baseType.BaseType;
            }
            while (baseType != null);

            // Iterate the inheritance chain backwards (most general to most specialized) to find a proxy type
            // that has already been generated.
            for (int i = inheritanceChain.Count - 1; i >= 0; i--)
            {
                Type type = inheritanceChain[i];

                if (s_ProxyTypes.ContainsKey(type))
                {
                    proxyType = s_ProxyTypes[type];
                    break;
                }
            }

            if (proxyType == null)
            {
                // Iterate the inheritance chain backwards (most general to most specialized) to find the most 
                // general type that can be casted to TInterface.
                for (int i = inheritanceChain.Count - 1; i >= 0; i--)
                {
                    Type type = inheritanceChain[i];

                    if (CanCast(type))
                    {
                        proxyType = new DuckProxyType<TInterface>(type);
                        break;
                    }
                }

                if (proxyType == null)
                {
                    // If proxyType is null, then DuckTyping.CanCast<>() returned false for every type in the
                    // inheritance chain.  The following line will cause an exception to be thrown when 
                    // GenerateProxyType() is called.  The exception will indicate why the duck cannot be casted.
                    proxyType = new DuckProxyType<TInterface>(duckType);
                }
                else
                {
                    // If we got to this point, the cast is valid and it is ok to add the proxy type to the dictionary.
                    s_ProxyTypes.Add(proxyType.m_DuckType, proxyType);
                }
            }

            return proxyType;
        }

        /// <summary>
        /// Class for keeping track of member builders while implementing proxy members.
        /// The key is the duck member that is called by the proxy.  The value is the proxy member that calls it.
        /// </summary>
        /// <remarks>
        /// This was defined in case it is decided to define a custom class instead of using Dictionary.
        /// </remarks>
        internal class ProxyMemberDictionary : Dictionary<MemberInfo, MemberInfo>
        { }
    }
}
