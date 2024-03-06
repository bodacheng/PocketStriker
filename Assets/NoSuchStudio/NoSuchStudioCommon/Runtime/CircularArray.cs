using System;

namespace NoSuchStudio.Common {
    public class CircularArray<T> {

        private T[] _array;
        private int _offset;
        private int _capacity;

        public void ForEach(Action<T> a) {
            for (int i = 0; i < _array.Length; i++) {
                a(_array[i]);
            }
        }

        public int Capacity {
            get { return _array.Length; }
        }

        public CircularArray(int capacity) {
            _capacity = capacity;
            _array = new T[capacity];
            _offset = 0;
        }

        public void Rotate(int c) {
            _offset = (_offset + c + _capacity) % _capacity;
        }

        public void RotateRight(int c) {
            Rotate(c);
        }
        public void RotateLeft(int c) {
            Rotate(-c);
        }

        private int ConvertIndex(int i) {
            return (i + _offset + _capacity) % _capacity;
        }

        public T this[int i] {
            get { return _array[ConvertIndex(i)]; }
            set { _array[ConvertIndex(i)] = value; }
        }
    }
}