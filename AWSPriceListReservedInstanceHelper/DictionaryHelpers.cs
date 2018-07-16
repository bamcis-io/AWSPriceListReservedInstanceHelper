using System;
using System.Collections.Generic;

namespace BAMCIS.LambdaFunctions.AWSPriceListReservedInstanceHelper
{
    /// <summary>
    /// Supplies some convenience functions for working with dictionaries
    /// </summary>
    public static class DictionaryHelpers
    {
        /// <summary>
        /// Tests if the key exists in the dictionary and that the value of the key is not
        /// null. If the value is a string, also tests that the string is not empty.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="dictionary"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static bool KeyExistsAndValueNotNullOrEmpty<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
        {
            if (typeof(TValue) == typeof(String))
            {
                return dictionary.ContainsKey(key) && !String.IsNullOrEmpty(dictionary[key] as string);
            }
            else
            {
                return dictionary.ContainsKey(key) && dictionary[key] != null;
            }
        }

        /// <summary>
        /// Tests if the key exists in the dictionary and that the value of the key is not
        /// null. If the value is a string, also tests that the string is not empty.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="dictionary"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static bool KeyExistsAndValueNotNullOrEmpty<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key)
        {
            if (typeof(TValue) == typeof(String))
            {
                return dictionary.ContainsKey(key) && !String.IsNullOrEmpty(dictionary[key] as string);
            }
            else
            {
                return dictionary.ContainsKey(key) && dictionary[key] != null;
            }
        }
    }
}
