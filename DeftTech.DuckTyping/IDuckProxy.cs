// IDuckProxy.cs
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
    /// All duck proxy types implement this interface.  This interface should normally not be used by code
    /// outside the duck typing library.
    /// </summary>
    public interface IDuckProxy
    {
        /// <summary>
        /// Returns the duck object that the proxy is forwarding calls to.
        /// </summary>
        /// <returns>The duck object that the proxy is forwarding calls to.</returns>
        object UnwrapDuck();
    }
}
