// DelegateDuckCaster.cs
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
    /// Duck casting strategy used to cast two delegate types.
    /// </summary>
    internal class DelegateDuckCaster : IDuckCaster
    {
        private static ToTypeFromTypeTree<DelegateDuckProxyType> s_ProxyTypeTree;

        /// <summary>
        /// Initializes static members.
        /// </summary>
        static DelegateDuckCaster()
        {
            s_ProxyTypeTree = new ToTypeFromTypeTree<DelegateDuckProxyType>();
        }

        /// <summary>
        /// Constructs an object.
        /// </summary>
        public DelegateDuckCaster()
        { }

        public bool ShouldCast(Type toType, Type fromType)
        {
            return (toType.IsSubclassOf(typeof(Delegate)) && fromType.IsSubclassOf(typeof(Delegate)));
        }

        public bool ShouldUncast(object duck)
        {
            return (duck is Delegate) && (((Delegate)duck).Target is IDuckProxy);
        }

        public bool CanCast(Type toType, Type fromType)
        {
            return ShouldCast(toType, fromType) && GetProxyType(toType, fromType).CanProxy();
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
            return GetProxyType(toType, duck.GetType()).WrapDuck((Delegate)duck);
        }

        public object Uncast(object duck)
        {
            object duckDelegateObj = ((IDuckProxy)(((Delegate)duck).Target)).UnwrapDuck();
            if (duckDelegateObj is Delegate) duck = duckDelegateObj;

            return duck;
        }

        /// <summary>
        /// Gets the DelegateDuckProxyType object for a given duck type.
        /// </summary>
        /// <param name="toType">Type to be casted to.</param>
        /// <param name="fromType">Type of delegate to be casted.</param>
        /// <returns>The duck proxy type to use to cast or prepare for casting.</returns>
        private static DelegateDuckProxyType GetProxyType(Type toType, Type fromType)
        {
            DelegateDuckProxyType proxyType = null;

            FromTypeTree<DelegateDuckProxyType> fromTypeTree = null;
            if (s_ProxyTypeTree.ContainsKey(toType))
            {
                fromTypeTree = s_ProxyTypeTree[toType];

                if (fromTypeTree.ContainsKey(fromType))
                {
                    proxyType = fromTypeTree[fromType];
                }
            }

            if (proxyType == null)
            {
                proxyType = new DelegateDuckProxyType(toType, fromType);

                if (proxyType.CanProxy())
                {
                    if (fromTypeTree == null)
                    {
                        fromTypeTree = new FromTypeTree<DelegateDuckProxyType>();
                        s_ProxyTypeTree.Add(toType, fromTypeTree);
                    }

                    fromTypeTree.Add(fromType, proxyType);
                }
            }

            return proxyType;
        }
    }
}
