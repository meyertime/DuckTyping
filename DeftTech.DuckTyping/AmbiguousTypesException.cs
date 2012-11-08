using System;
using System.Collections.Generic;
using System.Text;

namespace DeftTech.DuckTyping
{
    /// <summary>
    /// Exception thrown when two types cannot be distinguished from one another in a way that enables comparison.
    /// </summary>
    public sealed class AmbiguousTypesException : Exception
    {
        private Type m_Type1;
        private Type m_Type2;

        /// <summary>
        /// Constructs an object.
        /// </summary>
        /// <param name="type1">First type.</param>
        /// <param name="type2">Second type.</param>
        public AmbiguousTypesException(Type type1, Type type2)
            : base("Types cannot be distinguished from one another in a way that enables comparison.\r\n\r\n" 
                 + "Type 1: " + type1.FullName + "\r\n" 
                 + "Type 2: " + type2.FullName + "\r\n\r\n" 
                 + "NOTE: This exception indicates an internal problem in the duck typing library.  Please let the author know if "
                 + "this exception is encountered.  (http://www.deftflux.net)  Developers: For more information on the types "
                 + "involved, refer to the Type1 and Type2 properties of this exception.")
        {
            m_Type1 = type1;
            m_Type2 = type2;
        }

        /// <summary>
        /// Gets the first type.
        /// </summary>
        public Type Type1 { get { return m_Type1; } }

        /// <summary>
        /// Gets the second type.
        /// </summary>
        public Type Type2 { get { return m_Type2; } }
    }
}
