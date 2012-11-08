using System;
using System.Collections.Generic;
using System.Text;

namespace DeftTech.DuckTyping
{
    internal class GenericParameterDuckCaster : IDuckCaster
    {
        public GenericParameterDuckCaster()
        { }

        public bool ShouldCast(Type toType, Type fromType)
        {
            return toType.IsGenericParameter && fromType.IsGenericParameter;
        }

        public bool ShouldUncast(object duck)
        {
            return false;
        }

        public bool CanCast(Type toType, Type fromType)
        {
            return ShouldCast(toType, fromType) && toType.GenericParameterPosition == fromType.GenericParameterPosition;
        }

        public bool CouldUncast(Type originalType, Type castedType)
        {
            return false;
        }

        public void PrepareCast(Type toType, Type fromType)
        { }

        public object Cast(Type toType, object duck)
        {
            throw new InvalidOperationException("Cast should never be called since an object cannot exist of a type that is a generic parameter.");
        }

        public object Uncast(object duck)
        {
            throw new InvalidOperationException("Uncast should never be called because ShouldUncast always returns false.");
        }
    }
}
