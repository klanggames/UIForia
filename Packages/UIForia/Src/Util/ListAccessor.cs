using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace UIForia.Util {

    public struct ListAccessor<T> {

        private static readonly Func<List<T>, T[]> arrayGetter;
        private static readonly Func<List<T>, T[], T[]> arraySetter;
        private static readonly Func<List<T>, int> sizeGetter;
        private static readonly Func<List<T>, int, int> sizeSetter;

        static ListAccessor() {
            arrayGetter = (Func<List<T>, T[]>) CreateFieldGetter("_items");
            arraySetter = (Func<List<T>, T[], T[]>) CreateFieldSetter(typeof(T[]), "_items");
            sizeGetter = (Func<List<T>, int>) CreateFieldGetter("_size");
            sizeSetter = (Func<List<T>, int, int>) CreateFieldSetter(typeof(int), "_size");
        }

        public static void SetArray(List<T> list, T[] array, int count) {
            arraySetter(list, array);
            sizeSetter(list, count);
        }

        public static T[] GetArray(List<T> list) {
            return arrayGetter(list);
        }

        public static void SetCount(List<T> list, int count) {
            sizeSetter(list, count);
        }

        public static int GetSize(List<T> list) {
            return sizeGetter(list);
        }

        public static void AddRange(List<T> target, List<T> source) {
            int length = source.Count;
            int currentSize = target.Count;
            int currentCapacity = target.Capacity;
            T[] targetArray = GetArray(target);
            T[] sourceArray = GetArray(source);

            if (currentCapacity < currentSize + length) {
                Array.Resize(ref targetArray, (currentSize + length) * 2);
            }

            Array.Copy(sourceArray, 0, targetArray, currentSize, length);
            SetArray(target, targetArray, currentSize + length);
        }

        public static void AddRange(List<T> target, T[] source) {
            int length = source.Length;
            int currentSize = target.Count;
            int currentCapacity = target.Capacity;
            T[] targetArray = GetArray(target);

            if (currentCapacity < currentSize + length) {
                Array.Resize(ref targetArray, (currentSize + length) * 2);
            }

            Array.Copy(source, 0, targetArray, currentSize, length);
            SetArray(target, targetArray, currentSize + length);
        }

        public static void Set(List<T> retn, T[] src, int start, int count) {
            T[] targetArray = GetArray(retn);
            if (targetArray.Length <= count) {
                Array.Resize(ref targetArray, count * 2);
            }

            Array.Copy(targetArray, 0, src, start, count);
            SetArray(retn, targetArray, count);
        }

        public static void Set(List<T> retn, List<T> srcList, int start, int count) {
            T[] targetArray = GetArray(retn);
            T[] src = GetArray(srcList);

            if (targetArray.Length <= count) {
                Array.Resize(ref targetArray, count * 2);
            }

            Array.Copy(targetArray, 0, src, start, count);
            SetArray(retn, targetArray, count);
        }

        public static void AddRange(List<T> target, T[] source, int start, int count) {
            int currentSize = target.Count;
            int currentCapacity = target.Capacity;
            T[] targetArray = GetArray(target);

            if (currentCapacity < currentSize + count) {
                Array.Resize(ref targetArray, (currentSize + count) * 2);
            }

            Array.Copy(source, start, targetArray, currentSize, count);
            SetArray(target, targetArray, currentSize + count);
        }

        private static Delegate CreateFieldGetter(string fieldName) {
            ParameterExpression paramExpression = Expression.Parameter(typeof(List<T>));
            Expression fieldGetterExpression = Expression.Field(paramExpression, fieldName);
            return Expression.Lambda(fieldGetterExpression, paramExpression).Compile();
        }

        private static Delegate CreateFieldSetter(Type fieldType, string fieldName) {
            ParameterExpression paramExpression0 = Expression.Parameter(typeof(List<T>));
            ParameterExpression paramExpression1 = Expression.Parameter(fieldType, fieldName);
            MemberExpression fieldGetter = Expression.Field(paramExpression0, fieldName);

            return Expression.Lambda(
                Expression.Assign(fieldGetter, paramExpression1),
                paramExpression0,
                paramExpression1
            ).Compile();
        }

    }

}