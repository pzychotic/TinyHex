using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Text;

namespace TinyHex
{
    public class MemoryMappedDataCollection : IList<string>, IList
    {
        private readonly MemoryMappedViewAccessor _accessor;
        private readonly int _recordSize;
        private readonly long _count;

        public MemoryMappedDataCollection(MemoryMappedViewAccessor accessor, int recordSize, long count)
        {
            _accessor = accessor;
            _recordSize = recordSize;
            _count = count;
        }

        public string this[int index]
        {
            get
            {
                if (index < 0 || index >= _count)
                    throw new ArgumentOutOfRangeException();

                byte[] buffer = new byte[_recordSize];
                long fileOffset = (long)index * _recordSize;
                _accessor.ReadArray(fileOffset, buffer, 0, buffer.Length);

                // format line: offset: hex dump | ascii
                var hex = BitConverter.ToString(buffer).Replace("-", " ");
                var ascii = new StringBuilder();
                foreach (byte b in buffer)
                {
                    ascii.Append(b >= 32 && b <= 126 ? (char)b : '.');
                }

                int pad = _recordSize * 3; // each byte " FF" (3 chars)
                return $"{fileOffset:X8}:{hex.PadLeft(pad)} | {ascii}";
            }
            set => throw new NotSupportedException();
        }

        public int Count => (int)Math.Min(_count, int.MaxValue);
        public bool IsReadOnly => true;

        public void Add(string item) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public bool Contains(string item) => false;
        public void CopyTo(string[] array, int arrayIndex) => throw new NotSupportedException();
        public int IndexOf(string item) => throw new NotSupportedException();
        public void Insert(int index, string item) => throw new NotSupportedException();
        public bool Remove(string item) => throw new NotSupportedException();
        public void RemoveAt(int index) => throw new NotSupportedException();

        public IEnumerator<string> GetEnumerator() => Enumerable.Empty<string>().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        object? IList.this[int index]
        {
            get => this[index];
            set => throw new NotSupportedException();
        }
        bool IList.IsReadOnly => true;
        bool IList.IsFixedSize => true;
        int ICollection.Count => Count;
        bool ICollection.IsSynchronized => false;
        object ICollection.SyncRoot => this;

        int IList.Add(object? value) => throw new NotSupportedException();
        void IList.Clear() => throw new NotSupportedException();
        bool IList.Contains(object? value) => false;
        int IList.IndexOf(object? value) => -1;
        void IList.Insert(int index, object? value) => throw new NotSupportedException();
        void IList.Remove(object? value) => throw new NotSupportedException();
        void IList.RemoveAt(int index) => throw new NotSupportedException();
        void ICollection.CopyTo(Array array, int index) => throw new NotSupportedException();
    }
}