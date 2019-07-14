using System;
using UnityEngine;

namespace UIForia.Util {

    public class StructStack<T> {

        public T[] array;
        public int size;
        private bool isPooled;

        public int Count => size;

        public StructStack(int capacity = 8) {
            capacity = Mathf.Max(1, capacity);
            this.array = new T[capacity];
            this.size = 0;
            this.isPooled = false;
        }

        public void EnsureCapacity(int capacity) {
            if (array.Length <= capacity) {
                Array.Resize(ref array, capacity * 2);
            }
        }

        public void EnsureAdditionalCapacity(int capacity) {
            if (size + capacity >= array.Length) {
                Array.Resize(ref array, size + capacity);
            }
        }

        public void Push(in T item) {
            if (size + 1 >= array.Length) {
                Array.Resize(ref array, (size + 1) * 2);
            }

            array[size++] = item;
        }

        public T Pop() {
            if (size == 0) return default;
            T obj = array[--size];
            array[size] = default;
            return obj;
        }

        public void PushUnchecked(in T item) {
            array[size++] = item;
        }

        public T PeekAt(int index) {
            return array[index];
        }

        public T PopUnchecked() {
            T obj = array[--size];
            array[size] = default;
            return obj;
        }

        public void Clear() {
            Array.Clear(array, 0, size);
        }

        private static readonly LightList<StructStack<T>> s_Pool = new LightList<StructStack<T>>();

        public static StructStack<T> Get() {
            StructStack<T> retn = s_Pool.Count > 0 ? s_Pool.RemoveLast() : new StructStack<T>();
            retn.isPooled = false;
            return retn;
        }

        public static void Release(ref StructStack<T> toPool) {
            toPool.Clear();
            if (toPool.isPooled) return;
            toPool.isPooled = true;
            s_Pool.Add(toPool);
        }

    }

}