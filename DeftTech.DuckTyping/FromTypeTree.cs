// FromTypeTree.cs
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
    /// Tree to refer to T objects by from type.
    /// </summary>
    /// <typeparam name="T">Type of object to store.</typeparam>
    internal class FromTypeTree<T>
    {
        private Dictionary<Type, T> m_FromTypeTree;

        /// <summary>
        /// Constructs an object.
        /// </summary>
        public FromTypeTree()
        {
            m_FromTypeTree = new Dictionary<Type, T>();
        }

        /// <summary>
        /// Determines whether a T object exists for the given from type.
        /// </summary>
        /// <param name="fromType">From type to search for.</param>
        /// <returns>If a T object exists for the given from type, true; otherwise, false.</returns>
        public bool ContainsKey(Type fromType)
        {
            return m_FromTypeTree.ContainsKey(fromType);
        }

        /// <summary>
        /// Adds a T object to the tree.
        /// </summary>
        /// <param name="fromType">From type for the object.</param>
        /// <param name="item">The object to add.</param>
        public void Add(Type fromType, T item)
        {
            m_FromTypeTree.Add(fromType, item);
        }

        /// <summary>
        /// Gets the T object for a given from type.
        /// </summary>
        /// <param name="fromType">From type for the object.</param>
        /// <returns>The object for the given from type.</returns>
        public T this[Type fromType]
        {
            get { return m_FromTypeTree[fromType]; }
        }
    }
}
