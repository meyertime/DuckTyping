// DuckTyping.cs
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
using System.Reflection.Emit;
using System.Reflection;

namespace DeftTech.DuckTyping
{
    /// <summary>
    /// Class for casting objects using "duck typing".  Casting will succeed if a given duck type implements
    /// all the members of an interface even though it does not explicitly implement said interface at 
    /// compile time.  Hence, implementation of interfaces is moved to runtime.  Also supports delegate 
    /// casting.
    /// </summary>
    public static class DuckTyping
    {
        private static List<IDuckCaster> s_DuckCasters;
        private static List<IStaticDuckCaster> s_StaticDuckCasters;

        private static ToTypeFromTypeTree<bool> s_CanCastTree;
        private static ToTypeFromTypeTree<bool> s_CanStaticCastTree;

        [ThreadStatic] private static Stack<Type> t_ToTypeStack;
        [ThreadStatic] private static Stack<Type> t_FromTypeStack;

        /// <summary>
        /// Initializes static members.
        /// </summary>
        static DuckTyping()
        {
            s_DuckCasters = new List<IDuckCaster>();
            s_DuckCasters.Add(new InterfaceDuckCaster());
            s_DuckCasters.Add(new DelegateDuckCaster());
            s_DuckCasters.Add(new EnumStringDuckCaster());
            s_DuckCasters.Add(new GenericParameterDuckCaster());

            s_StaticDuckCasters = new List<IStaticDuckCaster>();
            s_StaticDuckCasters.Add(new InterfaceStaticDuckCaster());

            s_CanCastTree = new ToTypeFromTypeTree<bool>();

            s_CanStaticCastTree = new ToTypeFromTypeTree<bool>();
        }

        private static Stack<Type> ToTypeStack
        {
            get
            {
                if (t_ToTypeStack == null)
                {
                    t_ToTypeStack = new Stack<Type>();
                }

                return t_ToTypeStack;
            }
        }

        private static Stack<Type> FromTypeStack
        {
            get
            {
                if (t_FromTypeStack == null)
                {
                    t_FromTypeStack = new Stack<Type>();
                }

                return t_FromTypeStack;
            }
        }

        /// <summary>
        /// Casts an object using duck typing.
        /// </summary>
        /// <remarks>
        /// This method will use a normal cast if one is possible.
        /// </remarks>
        /// <typeparam name="T">Type to cast to.</typeparam>
        /// <param name="duck">Object to cast.</param>
        /// <returns>A T casting of the given duck object.</returns>
        public static T Cast<T>(object duck)
        {
            return (T)(Cast(typeof(T), duck));
        }

        /// <summary>
        /// Casts an object using duck typing.
        /// </summary>
        /// <remarks>
        /// This method will use a normal cast if one is possible.
        /// </remarks>
        /// <param name="toType">Type to cast to.</param>
        /// <param name="duck">Object to cast.</param>
        /// <returns>A casting of the given duck object to the given type.</returns>
        public static object Cast(Type toType, object duck)
        {
            if (toType == null) throw new ArgumentNullException("toType");
            if (duck == null) return null;

            Type originalDuckType = duck.GetType();

            duck = Uncast(duck);

            Type duckType = duck.GetType();

            object proxy = null;
            if (CanNormalCast(toType, duckType, false))
            {
                proxy = duck;
            }
            else
            {
                bool casted = false;
                foreach (IDuckCaster caster in s_DuckCasters)
                {
                    if (caster.ShouldCast(toType, duckType))
                    {
                        proxy = caster.Cast(toType, duck);
                        casted = true;
                        break;
                    }
                }

                if (!casted) throw new InvalidCastException("A conversion from " + originalDuckType.FullName + " to " + toType.FullName + " does not exist.");
            }

            return proxy;
        }

        /// <summary>
        /// Casts a static type to an object using duck typing.
        /// </summary>
        /// <typeparam name="T">Type to cast to.</typeparam>
        /// <param name="staticType">Static type to cast.</param>
        /// <returns>A casting of the given static type.</returns>
        public static T StaticCast<T>(Type staticType)
        {
            return (T)(StaticCast(typeof(T), staticType));
        }

        /// <summary>
        /// Casts a static type to an object using duck typing.
        /// </summary>
        /// <param name="toType">Type to cast to.</param>
        /// <param name="staticType">Static type to cast.</param>
        /// <returns>A casting of the given static type.</returns>
        public static object StaticCast(Type toType, Type staticType)
        {
            object proxy = null;

            foreach (IStaticDuckCaster caster in s_StaticDuckCasters)
            {
                if (caster.ShouldStaticCast(toType, staticType))
                {
                    proxy = caster.StaticCast(toType, staticType);
                    break;
                }
            }

            if (proxy == null) throw new InvalidCastException("A conversion from static type " + staticType.FullName + " to an object of " + toType.FullName + " does not exist.");

            return proxy;
        }

        /// <summary>
        /// If the given object is a duck casted object, uncasts the object to retrieve the original duck object.
        /// </summary>
        /// <param name="duck">Object that may be duck casted.</param>
        /// <returns>If the given object is duck casted, the original duck object; otherwise, the same object that was given.</returns>
        public static object Uncast(object duck)
        {
            foreach (IDuckCaster caster in s_DuckCasters)
            {
                if (caster.ShouldUncast(duck))
                {
                    duck = caster.Uncast(duck);
                }
            }

            return duck;
        }

        /// <summary>
        /// Determines whether a given object can be casted to a given type.
        /// </summary>
        /// <typeparam name="T">Type to cast to.</typeparam>
        /// <param name="duck">The object to cast.</param>
        /// <returns>If the given object can be casted to the given to type, true; otherwise, false.</returns>
        public static bool CanCast<T>(object duck)
        {
            return CanCast(typeof(T), duck.GetType());
        }

        /// <summary>
        /// Determines whether a given object can be casted to a given type.
        /// </summary>
        /// <param name="toType">Type to cast to.</param>
        /// <param name="duck">The object to cast.</param>
        /// <returns>If the given object can be casted to the given to type, true; otherwise, false.</returns>
        public static bool CanCast(Type toType, object duck)
        {
            return CanCast(toType, duck.GetType());
        }

        /// <summary>
        /// Determines whether a type can be casted to another type.
        /// </summary>
        /// <typeparam name="TTo">Type to cast to.</typeparam>
        /// <typeparam name="TFrom">Type of object to be casted.</typeparam>
        /// <returns>If an object of the given from type can be casted to the given to type, true; otherwise, false.</returns>
        public static bool CanCast<TTo, TFrom>()
        {
            return CanCast(typeof(TTo), typeof(TFrom));
        }

        /// <summary>
        /// Determines whether a type can be casted to type T.
        /// </summary>
        /// <typeparam name="T">Type to cast to.</typeparam>
        /// <param name="duckType">Type of object to be casted.</param>
        /// <returns>If an object of the given type can be casted to T, true; otherwise, false.</returns>
        public static bool CanCast<T>(Type duckType)
        {
            return CanCast(typeof(T), duckType);
        }

        /// <summary>
        /// Determines whether a type can be casted to another type.
        /// </summary>
        /// <param name="toType">Type to cast to.</param>
        /// <param name="fromType">Type of object to be casted.</param>
        /// <returns>If an object of the given from type can be casted to the given to type, true; otherwise, false.</returns>
        public static bool CanCast(Type toType, Type fromType)
        {
            bool canCast;

            FromTypeTree<bool> canCastFromTree;
            if (s_CanCastTree.ContainsKey(toType))
            {
                canCastFromTree = s_CanCastTree[toType];
            }
            else
            {
                canCastFromTree = new FromTypeTree<bool>();
                s_CanCastTree.Add(toType, canCastFromTree);
            }

            if (canCastFromTree.ContainsKey(fromType))
            {
                canCast = canCastFromTree[fromType];
            }
            else
            {
                canCast = CanNormalCast(toType, fromType, true);
                if (!canCast)
                {
                    foreach (IDuckCaster caster in s_DuckCasters)
                    {
                        if (caster.CanCast(toType, fromType))
                        {
                            canCast = true;
                            break;
                        }
                    }
                }

                if (!canCast)
                {
                    foreach (IDuckCaster caster in s_DuckCasters)
                    {
                        if (caster.CouldUncast(toType, fromType))
                        {
                            canCast = true;
                            break;
                        }
                    }
                }

                canCastFromTree.Add(fromType, canCast);
            }

            return canCast;
        }

        /// <summary>
        /// Determines whether a static type can be casted to another type.
        /// </summary>
        /// <typeparam name="T">Type to cast to.</typeparam>
        /// <param name="staticType">Static type to be casted.</param>
        /// <returns>If the given static type can be casted to the given to type, true; otherwise, false.</returns>
        public static bool CanStaticCast<T>(Type staticType)
        {
            return CanStaticCast(typeof(T), staticType);
        }

        /// <summary>
        /// Determines whether a static type can be casted to another type.
        /// </summary>
        /// <param name="toType">Type to cast to.</param>
        /// <param name="staticType">Static type to be casted.</param>
        /// <returns>If the given static type can be casted to the given to type, true; otherwise, false.</returns>
        public static bool CanStaticCast(Type toType, Type staticType)
        {
            bool canCast;

            FromTypeTree<bool> canCastFromTree;
            if (s_CanStaticCastTree.ContainsKey(toType))
            {
                canCastFromTree = s_CanStaticCastTree[toType];
            }
            else
            {
                canCastFromTree = new FromTypeTree<bool>();
                s_CanStaticCastTree.Add(toType, canCastFromTree);
            }

            if (canCastFromTree.ContainsKey(staticType))
            {
                canCast = canCastFromTree[staticType];
            }
            else
            {
                canCast = false;
                foreach (IStaticDuckCaster caster in s_StaticDuckCasters)
                {
                    if (caster.CanStaticCast(toType, staticType))
                    {
                        canCast = true;
                        break;
                    }
                }

                canCastFromTree.Add(staticType, canCast);
            }

            return canCast;
        }

        /// <summary>
        /// Prepares for a duck casting operation by performing all the necessary overhead.
        /// </summary>
        /// <typeparam name="T">Type to be casted to.</typeparam>
        /// <param name="duck">The object to be casted.</param>
        public static void PrepareCast<T>(object duck)
        {
            PrepareCast(typeof(T), duck.GetType());
        }

        /// <summary>
        /// Prepares for a duck casting operation by performing all the necessary overhead.
        /// </summary>
        /// <param name="toType">Type to be casted to.</param>
        /// <param name="duck">The object to be casted.</param>
        public static void PrepareCast(Type toType, object duck)
        {
            PrepareCast(toType, duck.GetType());
        }

        /// <summary>
        /// Prepares for a duck casting operation by performing all the necessary overhead.
        /// </summary>
        /// <typeparam name="TTo">Type to be casted to.</typeparam>
        /// <typeparam name="TFrom">Type of object to be casted.</typeparam>
        public static void PrepareCast<TTo, TFrom>()
        {
            PrepareCast(typeof(TTo), typeof(TFrom));
        }

        /// <summary>
        /// Prepares for a duck casting operation by performing all the necessary overhead.
        /// </summary>
        /// <typeparam name="T">Type to be casted to.</typeparam>
        /// <param name="duckType">Type of object to be casted.</param>
        public static void PrepareCast<T>(Type duckType)
        {
            PrepareCast(typeof(T), duckType);
        }

        /// <summary>
        /// Prepares for a duck casting operation by performing all the necessary overhead.
        /// </summary>
        /// <param name="toType">Type to be casted to.</param>
        /// <param name="fromType">Type of object to be casted.</param>
        public static void PrepareCast(Type toType, Type fromType)
        {
            if (!CanNormalCast(toType, fromType, false))
            {
                foreach (IDuckCaster caster in s_DuckCasters)
                {
                    if (caster.ShouldCast(toType, fromType))
                    {
                        caster.PrepareCast(toType, fromType);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Prepares for a static duck casting operation by performing all the necessary overhead.
        /// </summary>
        /// <typeparam name="T">Type to be casted to.</typeparam>
        /// <param name="staticType">Static type to be casted.</param>
        public static void PrepareStaticCast<T>(Type staticType)
        {
            PrepareStaticCast(typeof(T), staticType);
        }

        /// <summary>
        /// Prepares for a static duck casting operation by performing all the necessary overhead.
        /// </summary>
        /// <param name="toType">Type to be casted to.</param>
        /// <param name="staticType">Static type to be casted.</param>
        public static void PrepareStaticCast(Type toType, Type staticType)
        {
            foreach (IStaticDuckCaster caster in s_StaticDuckCasters)
            {
                if (caster.ShouldStaticCast(toType, staticType))
                {
                    caster.PrepareStaticCast(toType, staticType);
                    break;
                }
            }
        }

        /// <summary>
        /// Determines whether two types are compatible.
        /// </summary>
        /// <param name="toType">Type to be casted to.</param>
        /// <param name="fromType">Type of object to be casted.</param>
        /// <param name="bidirectionalContext">Whether to also check the reverse of the given conversion when checking for an infinite loop.</param>
        /// <returns>If the given types are compatible, true; otherwise, false.</returns>
        internal static bool AreTypesCompatible(Type toType, Type fromType, bool bidirectionalContext)
        {
            bool areCompatible = false;

            // This code prevents an infinite loop.  ToTypeStack and FromTypeStack are thread static Type stacks that keep track of
            // what conversions are currently being checked in this thread.  If the given conversion is already being checked, we
            // can assume that the types are compatible since they are as long as all the other members are.

            Type[] toTypes = ToTypeStack.ToArray();
            Type[] fromTypes = FromTypeStack.ToArray();

            for (int i = 0; i < toTypes.Length; i++)
            {
                if ((toType == toTypes[i] && fromType == fromTypes[i]) || (bidirectionalContext && toType == fromTypes[i] && fromType == toTypes[i]))
                {
                    areCompatible = true;
                    break;
                }
            }

            // If the current conversion is not in the stack, proceed with normal check:
            if (!areCompatible)
            {
                ToTypeStack.Push(toType);
                FromTypeStack.Push(fromType);

                areCompatible = CanCast(toType, fromType);

                ToTypeStack.Pop();
                FromTypeStack.Pop();
            }

            return areCompatible;
        }

        /// <summary>
        /// Determines whether two types are compatible.
        /// </summary>
        /// <param name="toType">Type to be casted to.</param>
        /// <param name="fromType">Type of object to be casted.</param>
        /// <returns>If the given types are compatible, true; otherwise, false.</returns>
        internal static bool AreTypesCompatible(Type toType, Type fromType)
        {
            return AreTypesCompatible(toType, fromType, false);
        }

        /// <summary>
        /// Assuming that the two types are compatible, scores how well the types match.  0 is the best 
        /// match, worse matches are less than 0.
        /// </summary>
        /// <param name="toType">Type to be casted to.</param>
        /// <param name="fromType">Type of object to be casted.</param>
        /// <returns>A number indicating how well the given types match.  0 is the best match, worse matches are less than 0.</returns>
        internal static int GetTypeCompatibilityScore(Type toType, Type fromType)
        {
            int score;

            if (toType == fromType)
            {
                // If the types are equal, the score is 0 (the best).
                score = 0;
            }
            else
            {
                Type baseType = null;
                Type subType = null;

                // Determine which type is the subclass of the other.
                if (toType.IsSubclassOf(fromType))
                {
                    baseType = fromType;
                    subType = toType;
                }
                else if (fromType.IsSubclassOf(toType))
                {
                    baseType = toType;
                    subType = fromType;
                }

                if (baseType != null && subType != null)
                {
                    // If one of the types is the subclass of the other, the score is the additive inverse
                    // of how many levels down the inheritance chain the base class is.  Thus, the closer
                    // the base class is to the subclass, the higher the score.
                    score = 0;
                    do
                    {
                        score--;
                        subType = subType.BaseType;
                    }
                    while (subType != baseType && subType != null);

                    // I guess if one type is an interface that the other implements, IsSubclassOf will 
                    // return true, but the interface will, of course, not be found in the type's 
                    // inheritance chain.  We want the score to be -1 in this case:
                    if (subType == null)
                    {
                        score = -1;
                    }
                }
                else
                {
                    // If one of the types is not the subclass of the other, then, assuming that the types
                    // are compatible, they are so because one type is an interface that the other type 
                    // implements or the types can be duck casted.  In both cases, the inheritance is 
                    // logically one level deep, so we will return a score of -1.
                    score = -1;
                }
            }

            return score;
        }

        /// <summary>
        /// Helps maintain consistent support for typing class members by centralizing the generation of IL
        /// that converts types.  Emits IL to perform whatever conversion is necessary from one type to 
        /// another.
        /// </summary>
        /// <param name="il">IL generator to use to emit the IL.</param>
        /// <param name="toType">Type to be casted to.</param>
        /// <param name="fromType">Type of object to be casted.</param>
        internal static void EmitCastIL(ILGenerator il, Type toType, Type fromType)
        {
            if (toType != typeof(void) && fromType != typeof(void))
            {
                if (CanNormalCast(toType, fromType, true))
                {
                    // Emit IL to box or unbox a value type if necessary
                    if (fromType.IsValueType && !toType.IsValueType)
                    {
                        il.Emit(OpCodes.Box, fromType);
                    }
                    else if (!fromType.IsValueType && toType.IsValueType)
                    {
                        il.Emit(OpCodes.Unbox_Any, toType);
                    }

                    // Emit IL to cast the object if necessary
                    if (fromType != toType && !toType.IsValueType)
                    {
                        il.Emit(OpCodes.Castclass, toType);
                    }
                }
                else if (ShouldDuckCast(toType, fromType) || ShouldDuckCast(fromType, toType))
                {
                    // Emit IL to box a value type if necessary
                    if (fromType.IsValueType)
                    {
                        il.Emit(OpCodes.Box, fromType);
                    }

                    // Emit IL to call the DuckTyping.Cast<> method
                    MethodInfo castMethod = typeof(DuckTyping).GetMethod("Cast", new Type[] { typeof(object) }).MakeGenericMethod(toType);
                    il.Emit(OpCodes.Call, castMethod);
                }
                else
                {
                    // This exception should never be thrown
                    throw new NotImplementedException("EmitCastIL method does not know how to handle the type conversion.  This exception indicates an inconsistency in the program; the AreTypesCompatible method accounts for a possible type conversion that has not been implemented in the EmitCastIL method.");
                }
            }
        }

        /// <summary>
        /// Determines whether a given type can be casted to another type using normal casting.
        /// </summary>
        /// <param name="toType">Type to be casted to.</param>
        /// <param name="fromType">Type to be casted from.</param>
        /// <param name="bidirectional">If true, true will also be returned if normal casting is possible in reverse.</param>
        /// <returns>If the given type can normally be casted to the other given type, true; otherwise, false.</returns>
        private static bool CanNormalCast(Type toType, Type fromType, bool bidirectional)
        {
            return toType == fromType || toType.IsAssignableFrom(fromType) || (bidirectional && fromType.IsAssignableFrom(toType));
        }

        /// <summary>
        /// Determines whether duck typing should be used to perform the given cast.  (As opposed to normal casting.)
        /// </summary>
        /// <param name="toType">Type to be casted to.</param>
        /// <param name="fromType">Type of object being casted.</param>
        /// <returns></returns>
        private static bool ShouldDuckCast(Type toType, Type fromType)
        {
            bool shouldDuckCast = false;
            foreach (IDuckCaster caster in s_DuckCasters)
            {
                if (caster.ShouldCast(toType, fromType))
                {
                    shouldDuckCast = true;
                    break;
                }
            }

            return shouldDuckCast;
        }
    }
}
