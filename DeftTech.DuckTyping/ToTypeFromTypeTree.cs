// ToTypeFromTypeTree.cs
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
    /// Tree to refer to from type trees by to type.
    /// </summary>
    /// <typeparam name="T">Type of object to store.</typeparam>
    internal class ToTypeFromTypeTree<T>
    {
        private Dictionary<Type, FromTypeTree<T>> m_ToTypeTree;

        /// <summary>
        /// Constructs an object.
        /// </summary>
        public ToTypeFromTypeTree()
        {
            m_ToTypeTree = new Dictionary<Type, FromTypeTree<T>>();
        }

        /// <summary>
        /// Determines whether a from type tree exists for the given from type.
        /// </summary>
        /// <param name="toType">To type to search for.</param>
        /// <returns>If a from type tree exists for the given to type, true; otherwise, false.</returns>
        public bool ContainsKey(Type toType)
        {
            return m_ToTypeTree.ContainsKey(toType);
        }

        /// <summary>
        /// Adds a from type tree to the to type tree.
        /// </summary>
        /// <param name="toType">To type for the from type tree.</param>
        /// <param name="fromTypeTree">The from type tree to add.</param>
        public void Add(Type toType, FromTypeTree<T> fromTypeTree)
        {
            m_ToTypeTree.Add(toType, fromTypeTree);
        }

        /// <summary>
        /// Gets the from type tree for a given to type.
        /// </summary>
        /// <param name="toType">To type for the object.</param>
        /// <returns>The from type tree for the given from type.</returns>
        public FromTypeTree<T> this[Type toType]
        {
            get { return m_ToTypeTree[toType]; }
        }
    }
}
