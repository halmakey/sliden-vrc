using System;

namespace Chikuwa.Sliden
{
    public class ArrayUtils
    {
        public static object[] Append(object[] values, object value)
        {
            if (value == null) {
                return values;
            }
            object[] copy = new object[values.Length + 1];
            Array.Copy(values, copy, values.Length);
            copy[values.Length] = value;
            return copy;
        }

        public static object[] FilterNonNull(object[] values)
        {
            int size = 0;
            object[] copy = new object[values.Length];
            foreach (var value in values)
            {
                if (value == null)
                {
                    continue;
                }
                copy[size] = value;
                size++;
            }
            object[] result = new object[size];
            Array.Copy(copy, result, size);
            return result;
        }
    }
}
