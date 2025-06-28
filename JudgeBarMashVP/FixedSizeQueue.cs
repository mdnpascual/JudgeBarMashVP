using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JudgeBarMashVP
{
    public class FixedSizeQueue<T>
    {
        private T[] _buffer;
        private int _head;
        private int _tail;
        private int _count;

        public int Size { get; private set; }

        public FixedSizeQueue(int size)
        {
            Size = size;
            _buffer = new T[size];
            _head = 0;
            _tail = 0;
            _count = 0;
        }

        public void Enqueue(T item)
        {
            // Only enqueue if the queue is empty or the item is not equal to the last added item
            if (_count == 0 || !EqualityComparer<T>.Default.Equals(item, _buffer[(_tail - 1 + Size) % Size]))
            {
                _buffer[_tail] = item;

                if (_count == Size)
                {
                    // If the queue is full, move head to the next position
                    _head = (_head + 1) % Size;
                }
                else
                {
                    _count++;
                }

                _tail = (_tail + 1) % Size;
            }
        }

        public String PrintQueue()
        {
            return string.Join(", ", _buffer);
        }

        public string PrintQueue(int division, int offset)
        {
            if (division <= 0 || offset < 0 || offset >= division)
            {
                return "Invalid division or offset.";
            }

            int itemsPerDivision = Size / division;
            int startIndex = offset * itemsPerDivision;
            int endIndex = startIndex + itemsPerDivision;

            // Adjust endIndex for the last division
            if (offset == division - 1)
            {
                endIndex = Size; // Ensure we go to the end of the queue
            }

            // Create a list to hold the output
            List<T> output = new List<T>();

            for (int i = startIndex; i < endIndex; i++)
            {
                // Calculate the actual index in the circular buffer
                int index = (_head + i) % Size;
                if (i < _count) // Only include valid items
                {
                    output.Add(_buffer[index]);
                }
            }

            return string.Join(", ", output);
        }

        public T PrintNewest()
        {
            if (_count == 0)
            {
                return default;
            }

            // Get the most recent item
            int newestIndex = (_tail - 1 + Size) % Size;
            return _buffer[newestIndex];
        }

        public void ClearAndFillWithZeros()
        {
            for (int i = 0; i < Size; i++)
            {
                _buffer[i] = default; // Set each element to the default value (0 for int)
            }
            _head = 0;
            _tail = 0;
            _count = 0;
        }

        public int GetCount()
        {
            return _count;
        }
    }
}
