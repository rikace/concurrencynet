namespace CSharp.Parallelx
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Reflection;
    using System.Linq.Expressions;

    public static class Functional
    {
        #region Currying

        public static Func<T1, Func<T2, TR>> Curry<T1, T2, TR>(this Func<T1, T2, TR> func)
        {
            return p1 => p2 => func(p1, p2);
        }

        public static Func<T1, Func<T2, Func<T3, TR>>> Curry<T1, T2, T3, TR>(this Func<T1, T2, T3, TR> func)
        {
            return p1 => p2 => p3 => func(p1, p2, p3);
        }

        public static Func<T1, Func<T2, Func<T3, Func<T4, TR>>>> Curry<T1, T2, T3, T4, TR>(
            this Func<T1, T2, T3, T4, TR> func)
        {
            return p1 => p2 => p3 => p4 => func(p1, p2, p3, p4);
        }

        public static Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, TR>>>>> Curry<T1, T2, T3, T4, T5, TR>(
            this Func<T1, T2, T3, T4, T5, TR> func)
        {
            return p1 => p2 => p3 => p4 => p5 => func(p1, p2, p3, p4, p5);
        }

        public static Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, TR>>>>>> Curry<T1, T2, T3, T4, T5, T6, TR>(
            this Func<T1, T2, T3, T4, T5, T6, TR> func)
        {
            return p1 => p2 => p3 => p4 => p5 => p6 => func(p1, p2, p3, p4, p5, p6);
        }

        public static Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, TR>>>>>>> Curry<T1, T2, T3, T4, T5,
            T6,
            T7, TR>(this Func<T1, T2, T3, T4, T5, T6, T7, TR> func)
        {
            return p1 => p2 => p3 => p4 => p5 => p6 => p7 => func(p1, p2, p3, p4, p5, p6, p7);
        }

        public static Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, TR>>>>>>>> Curry<T1, T2,
            T3,
            T4, T5, T6, T7, T8, TR>(this Func<T1, T2, T3, T4, T5, T6, T7, T8, TR> func)
        {
            return p1 => p2 => p3 => p4 => p5 => p6 => p7 => p8 => func(p1, p2, p3, p4, p5, p6, p7, p8);
        }

        public static Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, TR>>>>>>>>>
            Curry<T1,
                T2, T3, T4, T5, T6, T7, T8, T9, TR>(this Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR> func)
        {
            return p1 => p2 => p3 => p4 => p5 => p6 => p7 => p8 => p9 => func(p1, p2, p3, p4, p5, p6, p7, p8, p9);
        }

        public static Func<T1, Action<T2>> Curry<T1, T2>(this Action<T1, T2> action)
        {
            return p1 => p2 => action(p1, p2);
        }

        public static Func<T1, Func<T2, Action<T3>>> Curry<T1, T2, T3>(this Action<T1, T2, T3> action)
        {
            return p1 => p2 => p3 => action(p1, p2, p3);
        }

        public static Func<T1, Func<T2, Func<T3, Action<T4>>>> Curry<T1, T2, T3, T4>(this Action<T1, T2, T3, T4> action)
        {
            return p1 => p2 => p3 => p4 => action(p1, p2, p3, p4);
        }

        public static Func<T1, Func<T2, Func<T3, Func<T4, Action<T5>>>>> Curry<T1, T2, T3, T4, T5>(
            this Action<T1, T2, T3, T4, T5> action)
        {
            return p1 => p2 => p3 => p4 => p5 => action(p1, p2, p3, p4, p5);
        }

        public static Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Action<T6>>>>>> Curry<T1, T2, T3, T4, T5, T6>(
            this Action<T1, T2, T3, T4, T5, T6> action)
        {
            return p1 => p2 => p3 => p4 => p5 => p6 => action(p1, p2, p3, p4, p5, p6);
        }

        public static Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Action<T7>>>>>>> Curry<T1, T2, T3, T4, T5,
            T6,
            T7>(this Action<T1, T2, T3, T4, T5, T6, T7> action)
        {
            return p1 => p2 => p3 => p4 => p5 => p6 => p7 => action(p1, p2, p3, p4, p5, p6, p7);
        }

        public static Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Action<T8>>>>>>>> Curry<T1, T2, T3,
            T4,
            T5, T6, T7, T8>(this Action<T1, T2, T3, T4, T5, T6, T7, T8> action)
        {
            return p1 => p2 => p3 => p4 => p5 => p6 => p7 => p8 => action(p1, p2, p3, p4, p5, p6, p7, p8);
        }

        public static Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Action<T9>>>>>>>>> Curry<
            T1,
            T2, T3, T4, T5, T6, T7, T8, T9>(this Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> action)
        {
            return p1 => p2 => p3 => p4 => p5 => p6 => p7 => p8 => p9 => action(p1, p2, p3, p4, p5, p6, p7, p8, p9);
        }

        public static Func<T2, Func<T1, R>> Swap<T1, T2, R>(Func<T1, Func<T2, R>> func)
        {
            return p2 => p1 => func(p1)(p2);
        }

        #endregion

        #region Uncurrying

        public static Func<T1, T2, TR> Uncurry<T1, T2, TR>(this Func<T1, Func<T2, TR>> func)
        {
            return (p1, p2) => func(p1)(p2);
        }

        public static Func<T1, T2, T3, TR> Uncurry<T1, T2, T3, TR>(this Func<T1, Func<T2, Func<T3, TR>>> func)
        {
            return (p1, p2, p3) => func(p1)(p2)(p3);
        }

        public static Func<T1, T2, T3, T4, TR> Uncurry<T1, T2, T3, T4, TR>(
            this Func<T1, Func<T2, Func<T3, Func<T4, TR>>>> func)
        {
            return (p1, p2, p3, p4) => func(p1)(p2)(p3)(p4);
        }

        public static Func<T1, T2, T3, T4, T5, TR> Uncurry<T1, T2, T3, T4, T5, TR>(
            this Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, TR>>>>> func)
        {
            return (p1, p2, p3, p4, p5) => func(p1)(p2)(p3)(p4)(p5);
        }

        public static Func<T1, T2, T3, T4, T5, T6, TR> Uncurry<T1, T2, T3, T4, T5, T6, TR>(
            this Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, TR>>>>>> func)
        {
            return (p1, p2, p3, p4, p5, p6) => func(p1)(p2)(p3)(p4)(p5)(p6);
        }

        public static Func<T1, T2, T3, T4, T5, T6, T7, TR> Uncurry<T1, T2, T3, T4, T5, T6, T7, TR>(
            this Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, TR>>>>>>> func)
        {
            return (p1, p2, p3, p4, p5, p6, p7) => func(p1)(p2)(p3)(p4)(p5)(p6)(p7);
        }

        public static Func<T1, T2, T3, T4, T5, T6, T7, T8, TR> Uncurry<T1, T2, T3, T4, T5, T6, T7, T8, TR>(
            this Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, TR>>>>>>>> func)
        {
            return (p1, p2, p3, p4, p5, p6, p7, p8) => func(p1)(p2)(p3)(p4)(p5)(p6)(p7)(p8);
        }

        public static Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR> Uncurry<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR>(
            this Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, TR>>>>>>>>> func)
        {
            return (p1, p2, p3, p4, p5, p6, p7, p8, p9) => func(p1)(p2)(p3)(p4)(p5)(p6)(p7)(p8)(p9);
        }

        public static Action<T1, T2> Uncurry<T1, T2>(this Func<T1, Action<T2>> func)
        {
            return (p1, p2) => func(p1)(p2);
        }

        public static Action<T1, T2, T3> Uncurry<T1, T2, T3>(this Func<T1, Func<T2, Action<T3>>> func)
        {
            return (p1, p2, p3) => func(p1)(p2)(p3);
        }

        public static Action<T1, T2, T3, T4> Uncurry<T1, T2, T3, T4>(this Func<T1, Func<T2, Func<T3, Action<T4>>>> func)
        {
            return (p1, p2, p3, p4) => func(p1)(p2)(p3)(p4);
        }

        public static Action<T1, T2, T3, T4, T5> Uncurry<T1, T2, T3, T4, T5>(
            this Func<T1, Func<T2, Func<T3, Func<T4, Action<T5>>>>> func)
        {
            return (p1, p2, p3, p4, p5) => func(p1)(p2)(p3)(p4)(p5);
        }

        public static Action<T1, T2, T3, T4, T5, T6> Uncurry<T1, T2, T3, T4, T5, T6>(
            this Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Action<T6>>>>>> func)
        {
            return (p1, p2, p3, p4, p5, p6) => func(p1)(p2)(p3)(p4)(p5)(p6);
        }

        public static Action<T1, T2, T3, T4, T5, T6, T7> Uncurry<T1, T2, T3, T4, T5, T6, T7>(
            this Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Action<T7>>>>>>> func)
        {
            return (p1, p2, p3, p4, p5, p6, p7) => func(p1)(p2)(p3)(p4)(p5)(p6)(p7);
        }

        public static Action<T1, T2, T3, T4, T5, T6, T7, T8> Uncurry<T1, T2, T3, T4, T5, T6, T7, T8>(
            this Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Action<T8>>>>>>>> func)
        {
            return (p1, p2, p3, p4, p5, p6, p7, p8) => func(p1)(p2)(p3)(p4)(p5)(p6)(p7)(p8);
        }

        public static Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> Uncurry<T1, T2, T3, T4, T5, T6, T7, T8, T9>(
            this Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Action<T9>>>>>>>>> func)
        {
            return (p1, p2, p3, p4, p5, p6, p7, p8, p9) => func(p1)(p2)(p3)(p4)(p5)(p6)(p7)(p8)(p9);
        }

        #endregion


        #region Func, 2 params

        public static Func<T2, TR> Apply<T1, T2, TR>(Func<T1, T2, TR> function, T1 arg)
        {
            return arg2 => function(arg, arg2);
        }

        #endregion

        #region Func, 3 params

        public static Func<T2, T3, TR> Apply<T1, T2, T3, TR>(Func<T1, T2, T3, TR> function, T1 arg)
        {
            return (arg2, arg3) => function(arg, arg2, arg3);
        }

        public static Func<T3, TR> Apply<T1, T2, T3, TR>(Func<T1, T2, T3, TR> function, T1 arg, T2 arg2)
        {
            return arg3 => function(arg, arg2, arg3);
        }

        #endregion

        #region Func, 4 params

        public static Func<T2, T3, T4, TR> Apply<T1, T2, T3, T4, TR>(Func<T1, T2, T3, T4, TR> function, T1 arg)
        {
            return (arg2, arg3, arg4) => function(arg, arg2, arg3, arg4);
        }

        public static Func<T3, T4, TR> Apply<T1, T2, T3, T4, TR>(Func<T1, T2, T3, T4, TR> function, T1 arg, T2 arg2)
        {
            return (arg3, arg4) => function(arg, arg2, arg3, arg4);
        }

        public static Func<T4, TR> Apply<T1, T2, T3, T4, TR>(Func<T1, T2, T3, T4, TR> function, T1 arg, T2 arg2,
            T3 arg3)
        {
            return arg4 => function(arg, arg2, arg3, arg4);
        }

        #endregion

        #region Func, 5 params

        public static Func<T2, T3, T4, T5, TR> Apply<T1, T2, T3, T4, T5, TR>(Func<T1, T2, T3, T4, T5, TR> function,
            T1 arg)
        {
            return (arg2, arg3, arg4, arg5) => function(arg, arg2, arg3, arg4, arg5);
        }

        public static Func<T3, T4, T5, TR> Apply<T1, T2, T3, T4, T5, TR>(Func<T1, T2, T3, T4, T5, TR> function, T1 arg,
            T2 arg2)
        {
            return (arg3, arg4, arg5) => function(arg, arg2, arg3, arg4, arg5);
        }

        public static Func<T4, T5, TR> Apply<T1, T2, T3, T4, T5, TR>(Func<T1, T2, T3, T4, T5, TR> function, T1 arg,
            T2 arg2,
            T3 arg3)
        {
            return (arg4, arg5) => function(arg, arg2, arg3, arg4, arg5);
        }

        public static Func<T5, TR> Apply<T1, T2, T3, T4, T5, TR>(Func<T1, T2, T3, T4, T5, TR> function, T1 arg, T2 arg2,
            T3 arg3, T4 arg4)
        {
            return arg5 => function(arg, arg2, arg3, arg4, arg5);
        }

        #endregion

        #region Func, 6 params

        public static Func<T2, T3, T4, T5, T6, TR> Apply<T1, T2, T3, T4, T5, T6, TR>(
            Func<T1, T2, T3, T4, T5, T6, TR> function, T1 arg)
        {
            return (arg2, arg3, arg4, arg5, arg6) => function(arg, arg2, arg3, arg4, arg5, arg6);
        }

        public static Func<T3, T4, T5, T6, TR> Apply<T1, T2, T3, T4, T5, T6, TR>(
            Func<T1, T2, T3, T4, T5, T6, TR> function,
            T1 arg, T2 arg2)
        {
            return (arg3, arg4, arg5, arg6) => function(arg, arg2, arg3, arg4, arg5, arg6);
        }

        public static Func<T4, T5, T6, TR> Apply<T1, T2, T3, T4, T5, T6, TR>(Func<T1, T2, T3, T4, T5, T6, TR> function,
            T1 arg, T2 arg2, T3 arg3)
        {
            return (arg4, arg5, arg6) => function(arg, arg2, arg3, arg4, arg5, arg6);
        }

        public static Func<T5, T6, TR> Apply<T1, T2, T3, T4, T5, T6, TR>(Func<T1, T2, T3, T4, T5, T6, TR> function,
            T1 arg,
            T2 arg2, T3 arg3, T4 arg4)
        {
            return (arg5, arg6) => function(arg, arg2, arg3, arg4, arg5, arg6);
        }

        public static Func<T6, TR> Apply<T1, T2, T3, T4, T5, T6, TR>(Func<T1, T2, T3, T4, T5, T6, TR> function, T1 arg,
            T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            return arg6 => function(arg, arg2, arg3, arg4, arg5, arg6);
        }

        #endregion

        #region Func, 7 params

        public static Func<T2, T3, T4, T5, T6, T7, TR> Apply<T1, T2, T3, T4, T5, T6, T7, TR>(
            Func<T1, T2, T3, T4, T5, T6, T7, TR> function, T1 arg)
        {
            return (arg2, arg3, arg4, arg5, arg6, arg7) => function(arg, arg2, arg3, arg4, arg5, arg6, arg7);
        }

        public static Func<T3, T4, T5, T6, T7, TR> Apply<T1, T2, T3, T4, T5, T6, T7, TR>(
            Func<T1, T2, T3, T4, T5, T6, T7, TR> function, T1 arg, T2 arg2)
        {
            return (arg3, arg4, arg5, arg6, arg7) => function(arg, arg2, arg3, arg4, arg5, arg6, arg7);
        }

        public static Func<T4, T5, T6, T7, TR> Apply<T1, T2, T3, T4, T5, T6, T7, TR>(
            Func<T1, T2, T3, T4, T5, T6, T7, TR> function, T1 arg, T2 arg2, T3 arg3)
        {
            return (arg4, arg5, arg6, arg7) => function(arg, arg2, arg3, arg4, arg5, arg6, arg7);
        }

        public static Func<T5, T6, T7, TR> Apply<T1, T2, T3, T4, T5, T6, T7, TR>(
            Func<T1, T2, T3, T4, T5, T6, T7, TR> function, T1 arg, T2 arg2, T3 arg3, T4 arg4)
        {
            return (arg5, arg6, arg7) => function(arg, arg2, arg3, arg4, arg5, arg6, arg7);
        }

        public static Func<T6, T7, TR> Apply<T1, T2, T3, T4, T5, T6, T7, TR>(
            Func<T1, T2, T3, T4, T5, T6, T7, TR> function,
            T1 arg, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            return (arg6, arg7) => function(arg, arg2, arg3, arg4, arg5, arg6, arg7);
        }

        public static Func<T7, TR> Apply<T1, T2, T3, T4, T5, T6, T7, TR>(Func<T1, T2, T3, T4, T5, T6, T7, TR> function,
            T1 arg, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        {
            return arg7 => function(arg, arg2, arg3, arg4, arg5, arg6, arg7);
        }

        #endregion

        #region Func, 8 params

        public static Func<T2, T3, T4, T5, T6, T7, T8, TR> Apply<T1, T2, T3, T4, T5, T6, T7, T8, TR>(
            Func<T1, T2, T3, T4, T5, T6, T7, T8, TR> function, T1 arg)
        {
            return (arg2, arg3, arg4, arg5, arg6, arg7, arg8) =>
                function(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        }

        public static Func<T3, T4, T5, T6, T7, T8, TR> Apply<T1, T2, T3, T4, T5, T6, T7, T8, TR>(
            Func<T1, T2, T3, T4, T5, T6, T7, T8, TR> function, T1 arg, T2 arg2)
        {
            return (arg3, arg4, arg5, arg6, arg7, arg8) => function(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        }

        public static Func<T4, T5, T6, T7, T8, TR> Apply<T1, T2, T3, T4, T5, T6, T7, T8, TR>(
            Func<T1, T2, T3, T4, T5, T6, T7, T8, TR> function, T1 arg, T2 arg2, T3 arg3)
        {
            return (arg4, arg5, arg6, arg7, arg8) => function(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        }

        public static Func<T5, T6, T7, T8, TR> Apply<T1, T2, T3, T4, T5, T6, T7, T8, TR>(
            Func<T1, T2, T3, T4, T5, T6, T7, T8, TR> function, T1 arg, T2 arg2, T3 arg3, T4 arg4)
        {
            return (arg5, arg6, arg7, arg8) => function(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        }

        public static Func<T6, T7, T8, TR> Apply<T1, T2, T3, T4, T5, T6, T7, T8, TR>(
            Func<T1, T2, T3, T4, T5, T6, T7, T8, TR> function, T1 arg, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            return (arg6, arg7, arg8) => function(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        }

        public static Func<T7, T8, TR> Apply<T1, T2, T3, T4, T5, T6, T7, T8, TR>(
            Func<T1, T2, T3, T4, T5, T6, T7, T8, TR> function, T1 arg, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        {
            return (arg7, arg8) => function(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        }

        public static Func<T8, TR> Apply<T1, T2, T3, T4, T5, T6, T7, T8, TR>(
            Func<T1, T2, T3, T4, T5, T6, T7, T8, TR> function, T1 arg, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6,
            T7 arg7)
        {
            return arg8 => function(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        }

        #endregion

        #region Func, 9 params

        public static Func<T2, T3, T4, T5, T6, T7, T8, T9, TR> Apply<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR>(
            Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR> function, T1 arg)
        {
            return (arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9) =>
                function(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        }

        public static Func<T3, T4, T5, T6, T7, T8, T9, TR> Apply<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR>(
            Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR> function, T1 arg, T2 arg2)
        {
            return (arg3, arg4, arg5, arg6, arg7, arg8, arg9) =>
                function(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        }

        public static Func<T4, T5, T6, T7, T8, T9, TR> Apply<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR>(
            Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR> function, T1 arg, T2 arg2, T3 arg3)
        {
            return (arg4, arg5, arg6, arg7, arg8, arg9) =>
                function(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        }

        public static Func<T5, T6, T7, T8, T9, TR> Apply<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR>(
            Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR> function, T1 arg, T2 arg2, T3 arg3, T4 arg4)
        {
            return (arg5, arg6, arg7, arg8, arg9) => function(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        }

        public static Func<T6, T7, T8, T9, TR> Apply<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR>(
            Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR> function, T1 arg, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            return (arg6, arg7, arg8, arg9) => function(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        }

        public static Func<T7, T8, T9, TR> Apply<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR>(
            Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR> function, T1 arg, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        {
            return (arg7, arg8, arg9) => function(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        }

        public static Func<T8, T9, TR> Apply<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR>(
            Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR> function, T1 arg, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6,
            T7 arg7)
        {
            return (arg8, arg9) => function(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        }

        public static Func<T9, TR> Apply<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR>(
            Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR> function, T1 arg, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6,
            T7 arg7, T8 arg8)
        {
            return arg9 => function(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        }

        #endregion

        #region Action, 2 params

        public static Action<T2> Apply<T1, T2>(Action<T1, T2> action, T1 arg)
        {
            return arg2 => action(arg, arg2);
        }

        #endregion

        #region Action, 3 params

        public static Action<T2, T3> Apply<T1, T2, T3>(Action<T1, T2, T3> action, T1 arg)
        {
            return (arg2, arg3) => action(arg, arg2, arg3);
        }

        public static Action<T3> Apply<T1, T2, T3>(Action<T1, T2, T3> action, T1 arg, T2 arg2)
        {
            return arg3 => action(arg, arg2, arg3);
        }

        #endregion

        #region Action, 4 params

        public static Action<T2, T3, T4> Apply<T1, T2, T3, T4>(Action<T1, T2, T3, T4> action, T1 arg)
        {
            return (arg2, arg3, arg4) => action(arg, arg2, arg3, arg4);
        }

        public static Action<T3, T4> Apply<T1, T2, T3, T4>(Action<T1, T2, T3, T4> action, T1 arg, T2 arg2)
        {
            return (arg3, arg4) => action(arg, arg2, arg3, arg4);
        }

        public static Action<T4> Apply<T1, T2, T3, T4>(Action<T1, T2, T3, T4> action, T1 arg, T2 arg2, T3 arg3)
        {
            return arg4 => action(arg, arg2, arg3, arg4);
        }

        #endregion

        #region Action, 5 params

        public static Action<T2, T3, T4, T5> Apply<T1, T2, T3, T4, T5>(Action<T1, T2, T3, T4, T5> action, T1 arg)
        {
            return (arg2, arg3, arg4, arg5) => action(arg, arg2, arg3, arg4, arg5);
        }

        public static Action<T3, T4, T5> Apply<T1, T2, T3, T4, T5>(Action<T1, T2, T3, T4, T5> action, T1 arg, T2 arg2)
        {
            return (arg3, arg4, arg5) => action(arg, arg2, arg3, arg4, arg5);
        }

        public static Action<T4, T5> Apply<T1, T2, T3, T4, T5>(Action<T1, T2, T3, T4, T5> action, T1 arg, T2 arg2,
            T3 arg3)
        {
            return (arg4, arg5) => action(arg, arg2, arg3, arg4, arg5);
        }

        public static Action<T5> Apply<T1, T2, T3, T4, T5>(Action<T1, T2, T3, T4, T5> action, T1 arg, T2 arg2, T3 arg3,
            T4 arg4)
        {
            return arg5 => action(arg, arg2, arg3, arg4, arg5);
        }

        #endregion

        #region Action, 6 params

        public static Action<T2, T3, T4, T5, T6> Apply<T1, T2, T3, T4, T5, T6>(Action<T1, T2, T3, T4, T5, T6> action,
            T1 arg)
        {
            return (arg2, arg3, arg4, arg5, arg6) => action(arg, arg2, arg3, arg4, arg5, arg6);
        }

        public static Action<T3, T4, T5, T6> Apply<T1, T2, T3, T4, T5, T6>(Action<T1, T2, T3, T4, T5, T6> action,
            T1 arg,
            T2 arg2)
        {
            return (arg3, arg4, arg5, arg6) => action(arg, arg2, arg3, arg4, arg5, arg6);
        }

        public static Action<T4, T5, T6> Apply<T1, T2, T3, T4, T5, T6>(Action<T1, T2, T3, T4, T5, T6> action, T1 arg,
            T2 arg2, T3 arg3)
        {
            return (arg4, arg5, arg6) => action(arg, arg2, arg3, arg4, arg5, arg6);
        }

        public static Action<T5, T6> Apply<T1, T2, T3, T4, T5, T6>(Action<T1, T2, T3, T4, T5, T6> action, T1 arg,
            T2 arg2,
            T3 arg3, T4 arg4)
        {
            return (arg5, arg6) => action(arg, arg2, arg3, arg4, arg5, arg6);
        }

        public static Action<T6> Apply<T1, T2, T3, T4, T5, T6>(Action<T1, T2, T3, T4, T5, T6> action, T1 arg, T2 arg2,
            T3 arg3, T4 arg4, T5 arg5)
        {
            return arg6 => action(arg, arg2, arg3, arg4, arg5, arg6);
        }

        #endregion

        #region Action, 7 params

        public static Action<T2, T3, T4, T5, T6, T7> Apply<T1, T2, T3, T4, T5, T6, T7>(
            Action<T1, T2, T3, T4, T5, T6, T7> action, T1 arg)
        {
            return (arg2, arg3, arg4, arg5, arg6, arg7) => action(arg, arg2, arg3, arg4, arg5, arg6, arg7);
        }

        public static Action<T3, T4, T5, T6, T7> Apply<T1, T2, T3, T4, T5, T6, T7>(
            Action<T1, T2, T3, T4, T5, T6, T7> action, T1 arg, T2 arg2)
        {
            return (arg3, arg4, arg5, arg6, arg7) => action(arg, arg2, arg3, arg4, arg5, arg6, arg7);
        }

        public static Action<T4, T5, T6, T7> Apply<T1, T2, T3, T4, T5, T6, T7>(
            Action<T1, T2, T3, T4, T5, T6, T7> action,
            T1 arg, T2 arg2, T3 arg3)
        {
            return (arg4, arg5, arg6, arg7) => action(arg, arg2, arg3, arg4, arg5, arg6, arg7);
        }

        public static Action<T5, T6, T7> Apply<T1, T2, T3, T4, T5, T6, T7>(Action<T1, T2, T3, T4, T5, T6, T7> action,
            T1 arg, T2 arg2, T3 arg3, T4 arg4)
        {
            return (arg5, arg6, arg7) => action(arg, arg2, arg3, arg4, arg5, arg6, arg7);
        }

        public static Action<T6, T7> Apply<T1, T2, T3, T4, T5, T6, T7>(Action<T1, T2, T3, T4, T5, T6, T7> action,
            T1 arg,
            T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            return (arg6, arg7) => action(arg, arg2, arg3, arg4, arg5, arg6, arg7);
        }

        public static Action<T7> Apply<T1, T2, T3, T4, T5, T6, T7>(Action<T1, T2, T3, T4, T5, T6, T7> action, T1 arg,
            T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        {
            return arg7 => action(arg, arg2, arg3, arg4, arg5, arg6, arg7);
        }

        #endregion

        #region Action, 8 params

        public static Action<T2, T3, T4, T5, T6, T7, T8> Apply<T1, T2, T3, T4, T5, T6, T7, T8>(
            Action<T1, T2, T3, T4, T5, T6, T7, T8> action, T1 arg)
        {
            return (arg2, arg3, arg4, arg5, arg6, arg7, arg8) => action(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        }

        public static Action<T3, T4, T5, T6, T7, T8> Apply<T1, T2, T3, T4, T5, T6, T7, T8>(
            Action<T1, T2, T3, T4, T5, T6, T7, T8> action, T1 arg, T2 arg2)
        {
            return (arg3, arg4, arg5, arg6, arg7, arg8) => action(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        }

        public static Action<T4, T5, T6, T7, T8> Apply<T1, T2, T3, T4, T5, T6, T7, T8>(
            Action<T1, T2, T3, T4, T5, T6, T7, T8> action, T1 arg, T2 arg2, T3 arg3)
        {
            return (arg4, arg5, arg6, arg7, arg8) => action(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        }

        public static Action<T5, T6, T7, T8> Apply<T1, T2, T3, T4, T5, T6, T7, T8>(
            Action<T1, T2, T3, T4, T5, T6, T7, T8> action, T1 arg, T2 arg2, T3 arg3, T4 arg4)
        {
            return (arg5, arg6, arg7, arg8) => action(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        }

        public static Action<T6, T7, T8> Apply<T1, T2, T3, T4, T5, T6, T7, T8>(
            Action<T1, T2, T3, T4, T5, T6, T7, T8> action, T1 arg, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            return (arg6, arg7, arg8) => action(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        }

        public static Action<T7, T8> Apply<T1, T2, T3, T4, T5, T6, T7, T8>(
            Action<T1, T2, T3, T4, T5, T6, T7, T8> action,
            T1 arg, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        {
            return (arg7, arg8) => action(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        }

        public static Action<T8> Apply<T1, T2, T3, T4, T5, T6, T7, T8>(Action<T1, T2, T3, T4, T5, T6, T7, T8> action,
            T1 arg, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
        {
            return arg8 => action(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        }

        #endregion

        #region Action, 9 params

        public static Action<T2, T3, T4, T5, T6, T7, T8, T9> Apply<T1, T2, T3, T4, T5, T6, T7, T8, T9>(
            Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> action, T1 arg)
        {
            return (arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9) =>
                action(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        }

        public static Action<T3, T4, T5, T6, T7, T8, T9> Apply<T1, T2, T3, T4, T5, T6, T7, T8, T9>(
            Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> action, T1 arg, T2 arg2)
        {
            return (arg3, arg4, arg5, arg6, arg7, arg8, arg9) =>
                action(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        }

        public static Action<T4, T5, T6, T7, T8, T9> Apply<T1, T2, T3, T4, T5, T6, T7, T8, T9>(
            Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> action, T1 arg, T2 arg2, T3 arg3)
        {
            return (arg4, arg5, arg6, arg7, arg8, arg9) => action(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        }

        public static Action<T5, T6, T7, T8, T9> Apply<T1, T2, T3, T4, T5, T6, T7, T8, T9>(
            Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> action, T1 arg, T2 arg2, T3 arg3, T4 arg4)
        {
            return (arg5, arg6, arg7, arg8, arg9) => action(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        }

        public static Action<T6, T7, T8, T9> Apply<T1, T2, T3, T4, T5, T6, T7, T8, T9>(
            Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> action, T1 arg, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            return (arg6, arg7, arg8, arg9) => action(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        }

        public static Action<T7, T8, T9> Apply<T1, T2, T3, T4, T5, T6, T7, T8, T9>(
            Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> action, T1 arg, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        {
            return (arg7, arg8, arg9) => action(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        }

        public static Action<T8, T9> Apply<T1, T2, T3, T4, T5, T6, T7, T8, T9>(
            Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> action, T1 arg, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6,
            T7 arg7)
        {
            return (arg8, arg9) => action(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        }

        public static Action<T9> Apply<T1, T2, T3, T4, T5, T6, T7, T8, T9>(
            Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> action, T1 arg, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6,
            T7 arg7,
            T8 arg8)
        {
            return arg9 => action(arg, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        }

        #endregion

     
        #region 2 functions

        public static Func<TSource, TEndResult> Compose<TSource, TIntermediateResult, TEndResult>(
            this Func<TSource, TIntermediateResult> func1, Func<TIntermediateResult, TEndResult> func2)
        {
            return sourceParam => func2(func1(sourceParam));
        }

        public static Func<T1, T2, TEndResult> Compose<T1, T2, TIntermediateResult, TEndResult>(
            this Func<T1, T2, TIntermediateResult> func1, Func<TIntermediateResult, TEndResult> func2)
        {
            return (p1, p2) => func2(func1(p1, p2));
        }

        public static Func<T1, T2, T3, TEndResult> Compose<T1, T2, T3, TIntermediateResult, TEndResult>(
            this Func<T1, T2, T3, TIntermediateResult> func1, Func<TIntermediateResult, TEndResult> func2)
        {
            return (p1, p2, p3) => func2(func1(p1, p2, p3));
        }

        public static Func<T1, T2, T3, T4, TEndResult> Compose<T1, T2, T3, T4, TIntermediateResult, TEndResult>(
            this Func<T1, T2, T3, T4, TIntermediateResult> func1, Func<TIntermediateResult, TEndResult> func2)
        {
            return (p1, p2, p3, p4) => func2(func1(p1, p2, p3, p4));
        }

        public static Func<T1, T2, T3, T4, T5, TEndResult> Compose<T1, T2, T3, T4, T5, TIntermediateResult, TEndResult>(
            this Func<T1, T2, T3, T4, T5, TIntermediateResult> func1, Func<TIntermediateResult, TEndResult> func2)
        {
            return (p1, p2, p3, p4, p5) => func2(func1(p1, p2, p3, p4, p5));
        }

        public static Func<T1, T2, T3, T4, T5, T6, TEndResult> Compose<T1, T2, T3, T4, T5, T6, TIntermediateResult,
            TEndResult>(
            this Func<T1, T2, T3, T4, T5, T6, TIntermediateResult> func1, Func<TIntermediateResult, TEndResult> func2)
        {
            return (p1, p2, p3, p4, p5, p6) => func2(func1(p1, p2, p3, p4, p5, p6));
        }

        public static Func<T1, T2, T3, T4, T5, T6, T7, TEndResult> Compose<T1, T2, T3, T4, T5, T6, T7,
            TIntermediateResult,
            TEndResult>(
            this Func<T1, T2, T3, T4, T5, T6, T7, TIntermediateResult> func1,
            Func<TIntermediateResult, TEndResult> func2)
        {
            return (p1, p2, p3, p4, p5, p6, p7) => func2(func1(p1, p2, p3, p4, p5, p6, p7));
        }

        public static Func<T1, T2, T3, T4, T5, T6, T7, T8, TEndResult> Compose<T1, T2, T3, T4, T5, T6, T7, T8,
            TIntermediateResult, TEndResult>(
            this Func<T1, T2, T3, T4, T5, T6, T7, T8, TIntermediateResult> func1,
            Func<TIntermediateResult, TEndResult> func2)
        {
            return (p1, p2, p3, p4, p5, p6, p7, p8) => func2(func1(p1, p2, p3, p4, p5, p6, p7, p8));
        }

        public static Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TEndResult> Compose<T1, T2, T3, T4, T5, T6, T7, T8, T9,
            TIntermediateResult, TEndResult>(
            this Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TIntermediateResult> func1,
            Func<TIntermediateResult, TEndResult> func2)
        {
            return (p1, p2, p3, p4, p5, p6, p7, p8, p9) => func2(func1(p1, p2, p3, p4, p5, p6, p7, p8, p9));
        }

        #endregion

        #region 3 functions

        public static Func<TSource, TEndResult> Compose<TSource, TIR1, TIR2, TEndResult>(
            Func<TSource, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TEndResult> func3)
        {
            return sourceParam => func3(func2(func1(sourceParam)));
        }

        public static Func<T1, T2, TEndResult> Compose<T1, T2, TIR1, TIR2, TEndResult>(
            this Func<T1, T2, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TEndResult> func3)
        {
            return (p1, p2) => func3(func2(func1(p1, p2)));
        }

        public static Func<T1, T2, T3, TEndResult> Compose<T1, T2, T3, TIR1, TIR2, TEndResult>(
            this Func<T1, T2, T3, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TEndResult> func3)
        {
            return (p1, p2, p3) => func3(func2(func1(p1, p2, p3)));
        }

        public static Func<T1, T2, T3, T4, TEndResult> Compose<T1, T2, T3, T4, TIR1, TIR2, TEndResult>(
            this Func<T1, T2, T3, T4, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TEndResult> func3)
        {
            return (p1, p2, p3, p4) => func3(func2(func1(p1, p2, p3, p4)));
        }

        public static Func<T1, T2, T3, T4, T5, TEndResult> Compose<T1, T2, T3, T4, T5, TIR1, TIR2, TEndResult>(
            this Func<T1, T2, T3, T4, T5, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TEndResult> func3)
        {
            return (p1, p2, p3, p4, p5) => func3(func2(func1(p1, p2, p3, p4, p5)));
        }

        public static Func<T1, T2, T3, T4, T5, T6, TEndResult> Compose<T1, T2, T3, T4, T5, T6, TIR1, TIR2, TEndResult>(
            this Func<T1, T2, T3, T4, T5, T6, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TEndResult> func3)
        {
            return (p1, p2, p3, p4, p5, p6) => func3(func2(func1(p1, p2, p3, p4, p5, p6)));
        }

        public static Func<T1, T2, T3, T4, T5, T6, T7, TEndResult> Compose<T1, T2, T3, T4, T5, T6, T7, TIR1, TIR2,
            TEndResult>(
            this Func<T1, T2, T3, T4, T5, T6, T7, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TEndResult> func3)
        {
            return (p1, p2, p3, p4, p5, p6, p7) => func3(func2(func1(p1, p2, p3, p4, p5, p6, p7)));
        }

        public static Func<T1, T2, T3, T4, T5, T6, T7, T8, TEndResult> Compose<T1, T2, T3, T4, T5, T6, T7, T8, TIR1,
            TIR2,
            TEndResult>(
            this Func<T1, T2, T3, T4, T5, T6, T7, T8, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TEndResult> func3)
        {
            return (p1, p2, p3, p4, p5, p6, p7, p8) => func3(func2(func1(p1, p2, p3, p4, p5, p6, p7, p8)));
        }

        public static Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TEndResult> Compose<T1, T2, T3, T4, T5, T6, T7, T8, T9,
            TIR1,
            TIR2, TEndResult>(
            this Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TIR1> func1, Func<TIR1, TIR2> func2,
            Func<TIR2, TEndResult> func3)
        {
            return (p1, p2, p3, p4, p5, p6, p7, p8, p9) => func3(func2(func1(p1, p2, p3, p4, p5, p6, p7, p8, p9)));
        }

        #endregion

        #region 4 functions

        public static Func<TSource, TEndResult> Compose<TSource, TIR1, TIR2, TIR3, TEndResult>(
            Func<TSource, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3, Func<TIR3, TEndResult> func4)
        {
            return sourceParam => func4(func3(func2(func1(sourceParam))));
        }

        public static Func<T1, T2, TEndResult> Compose<T1, T2, TIR1, TIR2, TIR3, TEndResult>(
            this Func<T1, T2, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3, Func<TIR3, TEndResult> func4)
        {
            return (p1, p2) => func4(func3(func2(func1(p1, p2))));
        }

        public static Func<T1, T2, T3, TEndResult> Compose<T1, T2, T3, TIR1, TIR2, TIR3, TEndResult>(
            this Func<T1, T2, T3, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3,
            Func<TIR3, TEndResult> func4)
        {
            return (p1, p2, p3) => func4(func3(func2(func1(p1, p2, p3))));
        }

        public static Func<T1, T2, T3, T4, TEndResult> Compose<T1, T2, T3, T4, TIR1, TIR2, TIR3, TEndResult>(
            this Func<T1, T2, T3, T4, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3,
            Func<TIR3, TEndResult> func4)
        {
            return (p1, p2, p3, p4) => func4(func3(func2(func1(p1, p2, p3, p4))));
        }

        public static Func<T1, T2, T3, T4, T5, TEndResult> Compose<T1, T2, T3, T4, T5, TIR1, TIR2, TIR3, TEndResult>(
            this Func<T1, T2, T3, T4, T5, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3,
            Func<TIR3, TEndResult> func4)
        {
            return (p1, p2, p3, p4, p5) => func4(func3(func2(func1(p1, p2, p3, p4, p5))));
        }

        public static Func<T1, T2, T3, T4, T5, T6, TEndResult>
            Compose<T1, T2, T3, T4, T5, T6, TIR1, TIR2, TIR3, TEndResult>(
                this Func<T1, T2, T3, T4, T5, T6, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3,
                Func<TIR3, TEndResult> func4)
        {
            return (p1, p2, p3, p4, p5, p6) => func4(func3(func2(func1(p1, p2, p3, p4, p5, p6))));
        }

        public static Func<T1, T2, T3, T4, T5, T6, T7, TEndResult> Compose<T1, T2, T3, T4, T5, T6, T7, TIR1, TIR2, TIR3,
            TEndResult>(
            this Func<T1, T2, T3, T4, T5, T6, T7, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3,
            Func<TIR3, TEndResult> func4)
        {
            return (p1, p2, p3, p4, p5, p6, p7) => func4(func3(func2(func1(p1, p2, p3, p4, p5, p6, p7))));
        }

        public static Func<T1, T2, T3, T4, T5, T6, T7, T8, TEndResult> Compose<T1, T2, T3, T4, T5, T6, T7, T8, TIR1,
            TIR2,
            TIR3, TEndResult>(
            this Func<T1, T2, T3, T4, T5, T6, T7, T8, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3,
            Func<TIR3, TEndResult> func4)
        {
            return (p1, p2, p3, p4, p5, p6, p7, p8) => func4(func3(func2(func1(p1, p2, p3, p4, p5, p6, p7, p8))));
        }

        public static Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TEndResult> Compose<T1, T2, T3, T4, T5, T6, T7, T8, T9,
            TIR1,
            TIR2, TIR3, TEndResult>(
            this Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3,
            Func<TIR3, TEndResult> func4)
        {
            return (p1, p2, p3, p4, p5, p6, p7, p8, p9) =>
                func4(func3(func2(func1(p1, p2, p3, p4, p5, p6, p7, p8, p9))));
        }

        #endregion

        #region 5 functions

        public static Func<TSource, TEndResult> Compose<TSource, TIR1, TIR2, TIR3, TIR4, TEndResult>(
            Func<TSource, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3, Func<TIR3, TIR4> func4,
            Func<TIR4, TEndResult> func5)
        {
            return sourceParam => func5(func4(func3(func2(func1(sourceParam)))));
        }

        public static Func<T1, T2, TEndResult> Compose<T1, T2, TIR1, TIR2, TIR3, TIR4, TEndResult>(
            this Func<T1, T2, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3, Func<TIR3, TIR4> func4,
            Func<TIR4, TEndResult> func5)
        {
            return (p1, p2) => func5(func4(func3(func2(func1(p1, p2)))));
        }

        public static Func<T1, T2, T3, TEndResult> Compose<T1, T2, T3, TIR1, TIR2, TIR3, TIR4, TEndResult>(
            this Func<T1, T2, T3, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3, Func<TIR3, TIR4> func4,
            Func<TIR4, TEndResult> func5)
        {
            return (p1, p2, p3) => func5(func4(func3(func2(func1(p1, p2, p3)))));
        }

        public static Func<T1, T2, T3, T4, TEndResult> Compose<T1, T2, T3, T4, TIR1, TIR2, TIR3, TIR4, TEndResult>(
            this Func<T1, T2, T3, T4, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3,
            Func<TIR3, TIR4> func4,
            Func<TIR4, TEndResult> func5)
        {
            return (p1, p2, p3, p4) => func5(func4(func3(func2(func1(p1, p2, p3, p4)))));
        }

        public static Func<T1, T2, T3, T4, T5, TEndResult> Compose<T1, T2, T3, T4, T5, TIR1, TIR2, TIR3, TIR4,
            TEndResult>(
            this Func<T1, T2, T3, T4, T5, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3,
            Func<TIR3, TIR4> func4, Func<TIR4, TEndResult> func5)
        {
            return (p1, p2, p3, p4, p5) => func5(func4(func3(func2(func1(p1, p2, p3, p4, p5)))));
        }

        public static Func<T1, T2, T3, T4, T5, T6, TEndResult> Compose<T1, T2, T3, T4, T5, T6, TIR1, TIR2, TIR3, TIR4,
            TEndResult>(
            this Func<T1, T2, T3, T4, T5, T6, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3,
            Func<TIR3, TIR4> func4, Func<TIR4, TEndResult> func5)
        {
            return (p1, p2, p3, p4, p5, p6) => func5(func4(func3(func2(func1(p1, p2, p3, p4, p5, p6)))));
        }

        public static Func<T1, T2, T3, T4, T5, T6, T7, TEndResult> Compose<T1, T2, T3, T4, T5, T6, T7, TIR1, TIR2, TIR3,
            TIR4, TEndResult>(
            this Func<T1, T2, T3, T4, T5, T6, T7, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3,
            Func<TIR3, TIR4> func4, Func<TIR4, TEndResult> func5)
        {
            return (p1, p2, p3, p4, p5, p6, p7) => func5(func4(func3(func2(func1(p1, p2, p3, p4, p5, p6, p7)))));
        }

        public static Func<T1, T2, T3, T4, T5, T6, T7, T8, TEndResult> Compose<T1, T2, T3, T4, T5, T6, T7, T8, TIR1,
            TIR2,
            TIR3, TIR4, TEndResult>(
            this Func<T1, T2, T3, T4, T5, T6, T7, T8, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3,
            Func<TIR3, TIR4> func4, Func<TIR4, TEndResult> func5)
        {
            return (p1, p2, p3, p4, p5, p6, p7, p8) =>
                func5(func4(func3(func2(func1(p1, p2, p3, p4, p5, p6, p7, p8)))));
        }

        public static Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TEndResult> Compose<T1, T2, T3, T4, T5, T6, T7, T8, T9,
            TIR1,
            TIR2, TIR3, TIR4, TEndResult>(
            this Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3,
            Func<TIR3, TIR4> func4, Func<TIR4, TEndResult> func5)
        {
            return (p1, p2, p3, p4, p5, p6, p7, p8, p9) =>
                func5(func4(func3(func2(func1(p1, p2, p3, p4, p5, p6, p7, p8, p9)))));
        }

        #endregion

        #region 1 function, 1 action

        public static Action<TSource> Compose<TSource, TIntermediateResult>(
            this Func<TSource, TIntermediateResult> func1, Action<TIntermediateResult> action)
        {
            return sourceParam => action(func1(sourceParam));
        }

        public static Action<T1, T2> Compose<T1, T2, TIntermediateResult>(
            this Func<T1, T2, TIntermediateResult> func1, Action<TIntermediateResult> action)
        {
            return (p1, p2) => action(func1(p1, p2));
        }

        public static Action<T1, T2, T3> Compose<T1, T2, T3, TIntermediateResult>(
            this Func<T1, T2, T3, TIntermediateResult> func1, Action<TIntermediateResult> action)
        {
            return (p1, p2, p3) => action(func1(p1, p2, p3));
        }

        public static Action<T1, T2, T3, T4> Compose<T1, T2, T3, T4, TIntermediateResult>(
            this Func<T1, T2, T3, T4, TIntermediateResult> func1, Action<TIntermediateResult> action)
        {
            return (p1, p2, p3, p4) => action(func1(p1, p2, p3, p4));
        }

        public static Action<T1, T2, T3, T4, T5> Compose<T1, T2, T3, T4, T5, TIntermediateResult>(
            this Func<T1, T2, T3, T4, T5, TIntermediateResult> func1, Action<TIntermediateResult> action)
        {
            return (p1, p2, p3, p4, p5) => action(func1(p1, p2, p3, p4, p5));
        }

        public static Action<T1, T2, T3, T4, T5, T6> Compose<T1, T2, T3, T4, T5, T6, TIntermediateResult>(
            this Func<T1, T2, T3, T4, T5, T6, TIntermediateResult> func1, Action<TIntermediateResult> action)
        {
            return (p1, p2, p3, p4, p5, p6) => action(func1(p1, p2, p3, p4, p5, p6));
        }

        public static Action<T1, T2, T3, T4, T5, T6, T7> Compose<T1, T2, T3, T4, T5, T6, T7, TIntermediateResult>(
            this Func<T1, T2, T3, T4, T5, T6, T7, TIntermediateResult> func1, Action<TIntermediateResult> action)
        {
            return (p1, p2, p3, p4, p5, p6, p7) => action(func1(p1, p2, p3, p4, p5, p6, p7));
        }

        public static Action<T1, T2, T3, T4, T5, T6, T7, T8> Compose<T1, T2, T3, T4, T5, T6, T7, T8,
            TIntermediateResult>(
            this Func<T1, T2, T3, T4, T5, T6, T7, T8, TIntermediateResult> func1, Action<TIntermediateResult> action)
        {
            return (p1, p2, p3, p4, p5, p6, p7, p8) => action(func1(p1, p2, p3, p4, p5, p6, p7, p8));
        }

        public static Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> Compose<T1, T2, T3, T4, T5, T6, T7, T8, T9,
            TIntermediateResult>(
            this Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TIntermediateResult> func1,
            Action<TIntermediateResult> action)
        {
            return (p1, p2, p3, p4, p5, p6, p7, p8, p9) => action(func1(p1, p2, p3, p4, p5, p6, p7, p8, p9));
        }

        #endregion

        #region 2 functions, 1 action

        public static Action<TSource> Compose<TSource, TIR1, TIR2>(
            Func<TSource, TIR1> func1, Func<TIR1, TIR2> func2, Action<TIR2> action)
        {
            return sourceParam => action(func2(func1(sourceParam)));
        }

        public static Action<T1, T2> Compose<T1, T2, TIR1, TIR2>(
            this Func<T1, T2, TIR1> func1, Func<TIR1, TIR2> func2, Action<TIR2> action)
        {
            return (p1, p2) => action(func2(func1(p1, p2)));
        }

        public static Action<T1, T2, T3> Compose<T1, T2, T3, TIR1, TIR2>(
            this Func<T1, T2, T3, TIR1> func1, Func<TIR1, TIR2> func2, Action<TIR2> action)
        {
            return (p1, p2, p3) => action(func2(func1(p1, p2, p3)));
        }

        public static Action<T1, T2, T3, T4> Compose<T1, T2, T3, T4, TIR1, TIR2>(
            this Func<T1, T2, T3, T4, TIR1> func1, Func<TIR1, TIR2> func2, Action<TIR2> action)
        {
            return (p1, p2, p3, p4) => action(func2(func1(p1, p2, p3, p4)));
        }

        public static Action<T1, T2, T3, T4, T5> Compose<T1, T2, T3, T4, T5, TIR1, TIR2>(
            this Func<T1, T2, T3, T4, T5, TIR1> func1, Func<TIR1, TIR2> func2, Action<TIR2> action)
        {
            return (p1, p2, p3, p4, p5) => action(func2(func1(p1, p2, p3, p4, p5)));
        }

        public static Action<T1, T2, T3, T4, T5, T6> Compose<T1, T2, T3, T4, T5, T6, TIR1, TIR2>(
            this Func<T1, T2, T3, T4, T5, T6, TIR1> func1, Func<TIR1, TIR2> func2, Action<TIR2> action)
        {
            return (p1, p2, p3, p4, p5, p6) => action(func2(func1(p1, p2, p3, p4, p5, p6)));
        }

        public static Action<T1, T2, T3, T4, T5, T6, T7> Compose<T1, T2, T3, T4, T5, T6, T7, TIR1, TIR2>(
            this Func<T1, T2, T3, T4, T5, T6, T7, TIR1> func1, Func<TIR1, TIR2> func2, Action<TIR2> action)
        {
            return (p1, p2, p3, p4, p5, p6, p7) => action(func2(func1(p1, p2, p3, p4, p5, p6, p7)));
        }

        public static Action<T1, T2, T3, T4, T5, T6, T7, T8> Compose<T1, T2, T3, T4, T5, T6, T7, T8, TIR1, TIR2>(
            this Func<T1, T2, T3, T4, T5, T6, T7, T8, TIR1> func1, Func<TIR1, TIR2> func2, Action<TIR2> action)
        {
            return (p1, p2, p3, p4, p5, p6, p7, p8) => action(func2(func1(p1, p2, p3, p4, p5, p6, p7, p8)));
        }

        public static Action<T1, T2, T3, T4, T5, T6, T7, T8, T9>
            Compose<T1, T2, T3, T4, T5, T6, T7, T8, T9, TIR1, TIR2>(
                this Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TIR1> func1, Func<TIR1, TIR2> func2, Action<TIR2> action)
        {
            return (p1, p2, p3, p4, p5, p6, p7, p8, p9) => action(func2(func1(p1, p2, p3, p4, p5, p6, p7, p8, p9)));
        }

        #endregion

        #region 3 functions, 1 action

        public static Action<TSource> Compose<TSource, TIR1, TIR2, TIR3>(
            Func<TSource, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3, Action<TIR3> action)
        {
            return sourceParam => action(func3(func2(func1(sourceParam))));
        }

        public static Action<T1, T2> Compose<T1, T2, TIR1, TIR2, TIR3>(
            this Func<T1, T2, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3, Action<TIR3> action)
        {
            return (p1, p2) => action(func3(func2(func1(p1, p2))));
        }

        public static Action<T1, T2, T3> Compose<T1, T2, T3, TIR1, TIR2, TIR3>(
            this Func<T1, T2, T3, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3, Action<TIR3> action)
        {
            return (p1, p2, p3) => action(func3(func2(func1(p1, p2, p3))));
        }

        public static Action<T1, T2, T3, T4> Compose<T1, T2, T3, T4, TIR1, TIR2, TIR3>(
            this Func<T1, T2, T3, T4, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3, Action<TIR3> action)
        {
            return (p1, p2, p3, p4) => action(func3(func2(func1(p1, p2, p3, p4))));
        }

        public static Action<T1, T2, T3, T4, T5> Compose<T1, T2, T3, T4, T5, TIR1, TIR2, TIR3>(
            this Func<T1, T2, T3, T4, T5, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3,
            Action<TIR3> action)
        {
            return (p1, p2, p3, p4, p5) => action(func3(func2(func1(p1, p2, p3, p4, p5))));
        }

        public static Action<T1, T2, T3, T4, T5, T6> Compose<T1, T2, T3, T4, T5, T6, TIR1, TIR2, TIR3>(
            this Func<T1, T2, T3, T4, T5, T6, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3,
            Action<TIR3> action)
        {
            return (p1, p2, p3, p4, p5, p6) => action(func3(func2(func1(p1, p2, p3, p4, p5, p6))));
        }

        public static Action<T1, T2, T3, T4, T5, T6, T7> Compose<T1, T2, T3, T4, T5, T6, T7, TIR1, TIR2, TIR3>(
            this Func<T1, T2, T3, T4, T5, T6, T7, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3,
            Action<TIR3> action)
        {
            return (p1, p2, p3, p4, p5, p6, p7) => action(func3(func2(func1(p1, p2, p3, p4, p5, p6, p7))));
        }

        public static Action<T1, T2, T3, T4, T5, T6, T7, T8> Compose<T1, T2, T3, T4, T5, T6, T7, T8, TIR1, TIR2, TIR3>(
            this Func<T1, T2, T3, T4, T5, T6, T7, T8, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3,
            Action<TIR3> action)
        {
            return (p1, p2, p3, p4, p5, p6, p7, p8) => action(func3(func2(func1(p1, p2, p3, p4, p5, p6, p7, p8))));
        }

        public static Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> Compose<T1, T2, T3, T4, T5, T6, T7, T8, T9, TIR1, TIR2,
            TIR3>(
            this Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3,
            Action<TIR3> action)
        {
            return (p1, p2, p3, p4, p5, p6, p7, p8, p9) =>
                action(func3(func2(func1(p1, p2, p3, p4, p5, p6, p7, p8, p9))));
        }

        #endregion

        #region 4 functions, 1 action

        public static Action<TSource> Compose<TSource, TIR1, TIR2, TIR3, TIR4>(
            Func<TSource, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3, Func<TIR3, TIR4> func4,
            Action<TIR4> action)
        {
            return sourceParam => action(func4(func3(func2(func1(sourceParam)))));
        }

        public static Action<T1, T2> Compose<T1, T2, TIR1, TIR2, TIR3, TIR4>(
            this Func<T1, T2, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3, Func<TIR3, TIR4> func4,
            Action<TIR4> action)
        {
            return (p1, p2) => action(func4(func3(func2(func1(p1, p2)))));
        }

        public static Action<T1, T2, T3> Compose<T1, T2, T3, TIR1, TIR2, TIR3, TIR4>(
            this Func<T1, T2, T3, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3, Func<TIR3, TIR4> func4,
            Action<TIR4> action)
        {
            return (p1, p2, p3) => action(func4(func3(func2(func1(p1, p2, p3)))));
        }

        public static Action<T1, T2, T3, T4> Compose<T1, T2, T3, T4, TIR1, TIR2, TIR3, TIR4>(
            this Func<T1, T2, T3, T4, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3,
            Func<TIR3, TIR4> func4,
            Action<TIR4> action)
        {
            return (p1, p2, p3, p4) => action(func4(func3(func2(func1(p1, p2, p3, p4)))));
        }

        public static Action<T1, T2, T3, T4, T5> Compose<T1, T2, T3, T4, T5, TIR1, TIR2, TIR3, TIR4>(
            this Func<T1, T2, T3, T4, T5, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3,
            Func<TIR3, TIR4> func4, Action<TIR4> action)
        {
            return (p1, p2, p3, p4, p5) => action(func4(func3(func2(func1(p1, p2, p3, p4, p5)))));
        }

        public static Action<T1, T2, T3, T4, T5, T6> Compose<T1, T2, T3, T4, T5, T6, TIR1, TIR2, TIR3, TIR4>(
            this Func<T1, T2, T3, T4, T5, T6, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3,
            Func<TIR3, TIR4> func4, Action<TIR4> action)
        {
            return (p1, p2, p3, p4, p5, p6) => action(func4(func3(func2(func1(p1, p2, p3, p4, p5, p6)))));
        }

        public static Action<T1, T2, T3, T4, T5, T6, T7> Compose<T1, T2, T3, T4, T5, T6, T7, TIR1, TIR2, TIR3, TIR4>(
            this Func<T1, T2, T3, T4, T5, T6, T7, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3,
            Func<TIR3, TIR4> func4, Action<TIR4> action)
        {
            return (p1, p2, p3, p4, p5, p6, p7) => action(func4(func3(func2(func1(p1, p2, p3, p4, p5, p6, p7)))));
        }

        public static Action<T1, T2, T3, T4, T5, T6, T7, T8>
            Compose<T1, T2, T3, T4, T5, T6, T7, T8, TIR1, TIR2, TIR3, TIR4>(
                this Func<T1, T2, T3, T4, T5, T6, T7, T8, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3,
                Func<TIR3, TIR4> func4, Action<TIR4> action)
        {
            return (p1, p2, p3, p4, p5, p6, p7, p8) =>
                action(func4(func3(func2(func1(p1, p2, p3, p4, p5, p6, p7, p8)))));
        }

        public static Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> Compose<T1, T2, T3, T4, T5, T6, T7, T8, T9, TIR1, TIR2,
            TIR3, TIR4>(
            this Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TIR1> func1, Func<TIR1, TIR2> func2, Func<TIR2, TIR3> func3,
            Func<TIR3, TIR4> func4, Action<TIR4> action)
        {
            return (p1, p2, p3, p4, p5, p6, p7, p8, p9) =>
                action(func4(func3(func2(func1(p1, p2, p3, p4, p5, p6, p7, p8, p9)))));
        }

        #endregion


        #region Basic Func, none or one parameter

        public static Func<TR> Lambda<TR>(Func<TR> f)
        {
            return f;
        }

        public static Func<TArg, TR> Lambda<TArg, TR>(Func<TArg, TR> f)
        {
            return f;
        }

        #endregion

        #region Curried Func, up to 9 parameters

        public static Func<T1, Func<T2, TR>> Lambda<T1, T2, TR>(Func<T1, Func<T2, TR>> f)
        {
            return f;
        }

        public static Func<T1, Func<T2, Func<T3, TR>>> Lambda<T1, T2, T3, TR>(Func<T1, Func<T2, Func<T3, TR>>> f)
        {
            return f;
        }

        public static Func<T1, Func<T2, Func<T3, Func<T4, TR>>>> Lambda<T1, T2, T3, T4, TR>(
            Func<T1, Func<T2, Func<T3, Func<T4, TR>>>> f)
        {
            return f;
        }

        public static Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, TR>>>>> Lambda<T1, T2, T3, T4, T5, TR>(
            Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, TR>>>>> f)
        {
            return f;
        }

        public static Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, TR>>>>>> Lambda<T1, T2, T3, T4, T5, T6, TR>(
            Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, TR>>>>>> f)
        {
            return f;
        }

        public static Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, TR>>>>>>>
            Lambda<T1, T2, T3, T4, T5, T6, T7, TR>(
                Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, TR>>>>>>> f)
        {
            return f;
        }

        public static Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, TR>>>>>>>>
            Lambda<T1, T2, T3, T4, T5, T6, T7, T8, TR>(
                Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, TR>>>>>>>> f)
        {
            return f;
        }

        public static Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, TR>>>>>>>>>
            Lambda<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR>(
                Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Func<T9, TR>>>>>>>>> f)
        {
            return f;
        }

        #endregion

        #region Non-curried Func, up to 9 parameters

        public static Func<T1, T2, TR> Lambda<T1, T2, TR>(Func<T1, T2, TR> f)
        {
            return f;
        }

        public static Func<T1, T2, T3, TR> Lambda<T1, T2, T3, TR>(Func<T1, T2, T3, TR> f)
        {
            return f;
        }

        public static Func<T1, T2, T3, T4, TR> Lambda<T1, T2, T3, T4, TR>(Func<T1, T2, T3, T4, TR> f)
        {
            return f;
        }

        public static Func<T1, T2, T3, T4, T5, TR> Lambda<T1, T2, T3, T4, T5, TR>(Func<T1, T2, T3, T4, T5, TR> f)
        {
            return f;
        }

        public static Func<T1, T2, T3, T4, T5, T6, TR> Lambda<T1, T2, T3, T4, T5, T6, TR>(
            Func<T1, T2, T3, T4, T5, T6, TR> f)
        {
            return f;
        }

        public static Func<T1, T2, T3, T4, T5, T6, T7, TR> Lambda<T1, T2, T3, T4, T5, T6, T7, TR>(
            Func<T1, T2, T3, T4, T5, T6, T7, TR> f)
        {
            return f;
        }

        public static Func<T1, T2, T3, T4, T5, T6, T7, T8, TR> Lambda<T1, T2, T3, T4, T5, T6, T7, T8, TR>(
            Func<T1, T2, T3, T4, T5, T6, T7, T8, TR> f)
        {
            return f;
        }

        public static Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR> Lambda<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR>(
            Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR> f)
        {
            return f;
        }

        #endregion

        #region Basic Action, none or one parameter

        public static Action Lambda(Action f)
        {
            return f;
        }

        public static Action<TArg> Lambda<TArg>(Action<TArg> f)
        {
            return f;
        }

        #endregion

        #region Curried Action, up to 9 parameters

        public static Func<T1, Action<T2>> Lambda<T1, T2>(Func<T1, Action<T2>> f)
        {
            return f;
        }

        public static Func<T1, Func<T2, Action<T3>>> Lambda<T1, T2, T3>(Func<T1, Func<T2, Action<T3>>> f)
        {
            return f;
        }

        public static Func<T1, Func<T2, Func<T3, Action<T4>>>> Lambda<T1, T2, T3, T4>(
            Func<T1, Func<T2, Func<T3, Action<T4>>>> f)
        {
            return f;
        }

        public static Func<T1, Func<T2, Func<T3, Func<T4, Action<T5>>>>> Lambda<T1, T2, T3, T4, T5>(
            Func<T1, Func<T2, Func<T3, Func<T4, Action<T5>>>>> f)
        {
            return f;
        }

        public static Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Action<T6>>>>>> Lambda<T1, T2, T3, T4, T5, T6>(
            Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Action<T6>>>>>> f)
        {
            return f;
        }

        public static Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Action<T7>>>>>>>
            Lambda<T1, T2, T3, T4, T5, T6, T7>(Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Action<T7>>>>>>> f)
        {
            return f;
        }

        public static Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Action<T8>>>>>>>>
            Lambda<T1, T2, T3, T4, T5, T6, T7, T8>(
                Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Action<T8>>>>>>>> f)
        {
            return f;
        }

        public static Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Action<T9>>>>>>>>>
            Lambda<T1, T2, T3, T4, T5, T6, T7, T8, T9>(
                Func<T1, Func<T2, Func<T3, Func<T4, Func<T5, Func<T6, Func<T7, Func<T8, Action<T9>>>>>>>>> f)
        {
            return f;
        }

        #endregion

        #region Non-curried Action, up to 9 parameters

        public static Action<T1, T2> Lambda<T1, T2>(Action<T1, T2> f)
        {
            return f;
        }

        public static Action<T1, T2, T3> Lambda<T1, T2, T3>(Action<T1, T2, T3> f)
        {
            return f;
        }

        public static Action<T1, T2, T3, T4> Lambda<T1, T2, T3, T4>(Action<T1, T2, T3, T4> f)
        {
            return f;
        }

        public static Action<T1, T2, T3, T4, T5> Lambda<T1, T2, T3, T4, T5>(Action<T1, T2, T3, T4, T5> f)
        {
            return f;
        }

        public static Action<T1, T2, T3, T4, T5, T6> Lambda<T1, T2, T3, T4, T5, T6>(Action<T1, T2, T3, T4, T5, T6> f)
        {
            return f;
        }

        public static Action<T1, T2, T3, T4, T5, T6, T7> Lambda<T1, T2, T3, T4, T5, T6, T7>(
            Action<T1, T2, T3, T4, T5, T6, T7> f)
        {
            return f;
        }

        public static Action<T1, T2, T3, T4, T5, T6, T7, T8> Lambda<T1, T2, T3, T4, T5, T6, T7, T8>(
            Action<T1, T2, T3, T4, T5, T6, T7, T8> f)
        {
            return f;
        }

        public static Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> Lambda<T1, T2, T3, T4, T5, T6, T7, T8, T9>(
            Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> f)
        {
            return f;
        }

        #endregion
    }
}
