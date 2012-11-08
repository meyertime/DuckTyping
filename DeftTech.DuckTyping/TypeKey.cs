using System;
using System.Collections.Generic;
using System.Text;

namespace DeftTech.DuckTyping
{
    internal sealed class TypeKey : IComparable<TypeKey>
    {
        private Type m_Type;

        public TypeKey(Type type)
        {
            m_Type = type;
        }

        public override string ToString()
        {
            return m_Type.ToString();
        }

        public int CompareTo(TypeKey other)
        {
            Type type1 = m_Type;
            Type type2 = other.m_Type;

            int result;

            // If the types are equal, immediately return 0.
            if (type1 == type2)
            {
                result = 0;
            }
            else
            {
                // At this point, we know that the types are not equal, so we need some way to compare them.

                // First, compare their GUIDs.  This should work most of the time.
                if ((result = type1.GUID.CompareTo(type2.GUID)) == 0)
                {
                    // If the GUIDs are the same somehow, compare the assembly qualified name.  This should also work most of the time.
                    if (type1.AssemblyQualifiedName != null && type2.AssemblyQualifiedName != null)
                    {
                        result = type1.AssemblyQualifiedName.CompareTo(type2.AssemblyQualifiedName);
                    }

                    if (result == 0)
                    {
                        // The only known case where the GUID and assembly qualified name of two types are equal is when both types
                        // are generic parameters of the same name.  So let's check for that:
                        if (type1.IsGenericParameter && type2.IsGenericParameter)
                        {
                            // If the generic parameters belong to generic methods...
                            if (type1.DeclaringMethod != null && type2.DeclaringMethod != null)
                            {
                                // Compare the types that define the generic methods:
                                if ((result = new TypeKey(type1.DeclaringMethod.DeclaringType).CompareTo(new TypeKey(type2.DeclaringMethod.DeclaringType))) == 0)
                                {
                                    // If the generic methods are declared in the same type, compare the name of the generic methods:
                                    if ((result = type1.DeclaringMethod.Name.CompareTo(type2.DeclaringMethod.Name)) == 0)
                                    {
                                        // If the generic parameters belong to the same generic method, compare their position in
                                        // the list of generic parameters.
                                        result = type1.GenericParameterPosition.CompareTo(type2.GenericParameterPosition);
                                    }
                                }
                            }
                            // If the generic parameters belong to a generic type...
                            else if (type1.DeclaringType != null && type2.DeclaringType != null)
                            {
                                // Compare the generic types:
                                if ((result = new TypeKey(type1.DeclaringType).CompareTo(new TypeKey(type2.DeclaringType))) == 0)
                                {
                                    // If they belong to the same generic type, compare their position in the list of generic 
                                    // parameters:
                                    result = type1.GenericParameterPosition.CompareTo(type2.GenericParameterPosition);
                                }
                            }
                        }

                        // If we still haven't distinguished the types, I suppose it's possible that they are two generic types,
                        // perhaps of the same generic type definition.
                        if (result == 0 && type1.IsGenericType && type2.IsGenericType)
                        {
                            Type[] type1GenericArguments = type1.GetGenericArguments();
                            Type[] type2GenericArguments = type2.GetGenericArguments();

                            // Compare the length of the generic argument lists:
                            if ((result = type1GenericArguments.Length.CompareTo(type2GenericArguments.Length)) == 0)
                            {
                                // If the lists are the same length, compare each argument:
                                int i = 0;
                                do
                                {
                                    result = new TypeKey(type1GenericArguments[i]).CompareTo(new TypeKey(type2GenericArguments[i]));

                                    i++;
                                }
                                while (i < type1GenericArguments.Length && result == 0);
                            }
                        }

                        // If we still haven't distinguished the types, yet we know that they are not equal, we have no choice but
                        // to throw an exception.  Theoretically, this should never happen, but in case it does, we'll know what
                        // the problem is and have some information to work with.
                        if (result == 0)
                        {
                            throw new AmbiguousTypesException(type1, type2);
                        }
                    }
                }
            }

            return result;
        }
    }
}
