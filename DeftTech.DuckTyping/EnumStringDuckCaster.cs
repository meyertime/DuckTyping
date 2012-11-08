// EnumStringDuckCaster.cs
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
    /// Duck casting strategy used to cast between enums and strings.
    /// </summary>
    internal class EnumStringDuckCaster : IDuckCaster
    {
        /// <summary>
        /// Constructs an object.
        /// </summary>
        public EnumStringDuckCaster()
        { }

        public bool ShouldCast(Type toType, Type fromType)
        {
            return ((toType.IsSubclassOf(typeof(Enum)) && fromType == typeof(string)) || (toType == typeof(string) && fromType.IsSubclassOf(typeof(Enum))));
        }

        public bool ShouldUncast(object duck)
        {
            return false;
        }

        public bool CanCast(Type toType, Type fromType)
        {
            return ShouldCast(toType, fromType);
        }

        public bool CouldUncast(Type originalType, Type castedType)
        {
            return CanCast(castedType, originalType);
        }

        public void PrepareCast(Type toType, Type fromType)
        { }

        public object Cast(Type toType, object duck)
        {
            object casted;

            if (duck is string)
            {
                casted = Enum.Parse(toType, (string)duck);
            }
            else
            {
                casted = duck.ToString();
            }

            return casted;
        }

        public object Uncast(object duck)
        {
            return duck;
        }
    }
}
