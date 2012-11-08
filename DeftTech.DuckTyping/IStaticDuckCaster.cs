// IStaticDuckCaster.cs
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
    /// Interface for strategies of duck casting a static type.
    /// </summary>
    internal interface IStaticDuckCaster
    {
        /// <summary>
        /// Determines whether this duck caster should be used to perform the given static cast.
        /// </summary>
        /// <param name="toType">Type to be casted to.</param>
        /// <param name="staticType">Static type to be casted.</param>
        /// <returns>If this duck caster should be used to perform the given static cast, true; otherwise, false.</returns>
        bool ShouldStaticCast(Type toType, Type staticType);

        /// <summary>
        /// Determines whether a given static cast is valid.
        /// </summary>
        /// <param name="toType">Type to be casted to.</param>
        /// <param name="staticType">Static type to be casted.</param>
        /// <returns>If the given static cast is valid, true; otherwise, false.</returns>
        bool CanStaticCast(Type toType, Type staticType);

        /// <summary>
        /// Prepares for a given static cast by performing all necessary overhead.
        /// </summary>
        /// <param name="toType">Type to be casted to.</param>
        /// <param name="staticType">Static type to be casted.</param>
        void PrepareStaticCast(Type toType, Type staticType);

        /// <summary>
        /// Performs a given static cast.
        /// </summary>
        /// <param name="toType">Type to be casted to.</param>
        /// <param name="staticType">Static type to cast.</param>
        /// <returns>The casted static type.</returns>
        object StaticCast(Type toType, Type staticType);
    }
}
