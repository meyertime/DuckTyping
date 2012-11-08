// InterfaceStaticDuckCaster.cs
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
    /// Static duck casting strategy that casts a static type to an interface.
    /// </summary>
    internal class InterfaceStaticDuckCaster : IStaticDuckCaster
    {
        private static ToTypeFromTypeTree<InterfaceDuckProxyType> s_ProxyTypeTree;

        /// <summary>
        /// Initializes static members.
        /// </summary>
        static InterfaceStaticDuckCaster()
        {
            s_ProxyTypeTree = new ToTypeFromTypeTree<InterfaceDuckProxyType>();
        }

        /// <summary>
        /// Constructs an object.
        /// </summary>
        public InterfaceStaticDuckCaster()
        { }

        public bool ShouldStaticCast(Type toType, Type staticType)
        {
            return (toType.IsInterface && !staticType.IsInterface);
        }

        public bool CanStaticCast(Type toType, Type staticType)
        {
            return ShouldStaticCast(toType, staticType) && new InterfaceDuckProxyType(toType, staticType, true).CanProxy();
        }

        public void PrepareStaticCast(Type toType, Type staticType)
        {
            GetProxyType(toType, staticType).Prepare();
        }

        public object StaticCast(Type toType, Type staticType)
        {
            return GetProxyType(toType, staticType).GetStaticProxy();
        }

        /// <summary>
        /// Gets the InterfaceDuckProxyType object for a given cast.
        /// </summary>
        /// <param name="interfaceType">Type to cast to.</param>
        /// <param name="duckType">Static type being casted.</param>
        /// <returns>The duck proxy type to use to cast or prepare for casting.</returns>
        private InterfaceDuckProxyType GetProxyType(Type interfaceType, Type duckType)
        {
            InterfaceDuckProxyType proxyType = null;

            FromTypeTree<InterfaceDuckProxyType> fromTypeTree = null;

            if (s_ProxyTypeTree.ContainsKey(interfaceType))
            {
                fromTypeTree = s_ProxyTypeTree[interfaceType];

                if (fromTypeTree.ContainsKey(duckType))
                {
                    proxyType = fromTypeTree[duckType];
                }
            }

            if (proxyType == null)
            {
                proxyType = new InterfaceDuckProxyType(interfaceType, duckType, true);
                
                if (fromTypeTree == null)
                {
                    fromTypeTree = new FromTypeTree<InterfaceDuckProxyType>();
                    s_ProxyTypeTree.Add(interfaceType, fromTypeTree);
                }

                fromTypeTree.Add(proxyType.DuckType, proxyType);
            }

            return proxyType;
        }
    }
}
