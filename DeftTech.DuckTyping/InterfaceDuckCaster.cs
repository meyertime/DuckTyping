// InterfaceDuckCaster.cs
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

namespace DeftTech.DuckTyping
{
    /// <summary>
    /// Duck casting strategy that casts an object to an interface.
    /// </summary>
    internal class InterfaceDuckCaster : IDuckCaster
    {
        private static ToTypeFromTypeTree<InterfaceDuckProxyType> s_ProxyTypeTree;

        /// <summary>
        /// Initializes static members.
        /// </summary>
        static InterfaceDuckCaster()
        {
            s_ProxyTypeTree = new ToTypeFromTypeTree<InterfaceDuckProxyType>();
        }


        /// <summary>
        /// Constructs an object.
        /// </summary>
        public InterfaceDuckCaster()
        { }

        public bool ShouldCast(Type toType, Type fromType)
        {
            return (toType.IsInterface /*&& !fromType.IsInterface*/);
        }

        public bool ShouldUncast(object duck)
        {
            return (duck is IDuckProxy);
        }

        public bool CanCast(Type toType, Type fromType)
        {
            return ShouldCast(toType, fromType) && new InterfaceDuckProxyType(toType, fromType, false).CanProxy();
        }

        public bool CouldUncast(Type originalType, Type castedType)
        {
            return CanCast(castedType, originalType);
        }

        public void PrepareCast(Type toType, Type fromType)
        {
            GetProxyType(toType, fromType).Prepare();
        }

        public object Cast(Type toType, object duck)
        {
            return GetProxyType(toType, duck.GetType()).WrapDuck(duck);
        }

        public object Uncast(object duck)
        {
            object uncasted = ((IDuckProxy)duck).UnwrapDuck();

            if (uncasted != null) duck = uncasted;

            return duck;
        }

        /// <summary>
        /// Gets the InterfaceDuckProxyType object for a given cast.
        /// </summary>
        /// <remarks>
        /// First, the most general type in the inheritance chain of the given type that implements all the 
        /// members of TInterface is selected.  Then if a proxy type has already been generated, it returns 
        /// it.  Finally, if not, it creates a new object and adds it to the dictionary.
        /// </remarks>
        /// <param name="interfaceType">Type to cast to.</param>
        /// <param name="duckType">Type of object being casted.</param>
        /// <returns>The duck proxy type to use to cast or prepare for casting.</returns>
        private InterfaceDuckProxyType GetProxyType(Type interfaceType, Type duckType)
        {
            InterfaceDuckProxyType proxyType = null;

            FromTypeTree<InterfaceDuckProxyType> fromTypeTree = null;

            // Examine the duck type's inheritance chain.
            List<Type> inheritanceChain = new List<Type>();
            Type baseType = duckType;
            do
            {
                inheritanceChain.Add(baseType);
                baseType = baseType.BaseType;
            }
            while (baseType != null);

            // Include interfaces in the inheritance chain.  We don't know which order, but it will still help.
            /*Type[] interfaces = duckType.GetInterfaces();
            foreach (Type _interface in interfaces)
            {
                inheritanceChain.Add(_interface);
            }*/

            if (s_ProxyTypeTree.ContainsKey(interfaceType))
            {
                fromTypeTree = s_ProxyTypeTree[interfaceType];

                // Iterate the inheritance chain backwards (most general to most specialized) to find a proxy type
                // that has already been generated.
                for (int i = inheritanceChain.Count - 1; i >= 0; i--)
                {
                    Type type = inheritanceChain[i];

                    if (fromTypeTree.ContainsKey(type))
                    {
                        proxyType = fromTypeTree[type];
                        break;
                    }
                }
            }

            if (proxyType == null)
            {
                // Iterate the inheritance chain backwards (most general to most specialized) to find the most 
                // general type that can be casted to TInterface.
                for (int i = inheritanceChain.Count - 1; i >= 0; i--)
                {
                    Type type = inheritanceChain[i];

                    if (CanCast(interfaceType, type))
                    {
                        proxyType = new InterfaceDuckProxyType(interfaceType, type, false);
                        break;
                    }
                }

                if (proxyType == null)
                {
                    // If proxyType is null, then DuckTyping.CanCast<>() returned false for every type in the
                    // inheritance chain.  The following line will cause an exception to be thrown when 
                    // GenerateProxyType() is called.  The exception will indicate why the duck cannot be casted.
                    proxyType = new InterfaceDuckProxyType(interfaceType, duckType, false);
                }
                else
                {
                    // If we got to this point, the cast is valid and it is ok to add the proxy type to the tree.
                    if (fromTypeTree == null)
                    {
                        fromTypeTree = new FromTypeTree<InterfaceDuckProxyType>();
                        s_ProxyTypeTree.Add(interfaceType, fromTypeTree);
                    }

                    fromTypeTree.Add(proxyType.DuckType, proxyType);
                }
            }

            return proxyType;
        }
    }
}
