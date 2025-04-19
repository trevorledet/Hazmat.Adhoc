using System;
using System.Collections.Generic;
using System.Reflection;
using Google.Cloud.Firestore;
using Microsoft.Azure.Cosmos.Linq;

namespace Hazmat.Utilities;

public static class ObjectComparer
{
    public static List<string> CompareProperties<T>(T obj1, T obj2)
    {
        List<string> differences = new List<string>();

        if (obj1 == null || obj2 == null)
        {
            throw new ArgumentNullException("Objects to compare cannot be null");
        }

        PropertyInfo[] properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (PropertyInfo property in properties)
        {
            if (property.PropertyType == typeof(DateTime) || property.PropertyType == typeof(Timestamp) || property.PropertyType == typeof(Guid))
            {
                continue;
            }
            object? value1 = property.GetValue(obj1);
            object? value2 = property.GetValue(obj2);

            if (value1 == null && value2 == null)
            {
                continue;
            }

            if (property.PropertyType == typeof(string[]))
            {
                if (value1 == null || value2 == null || !((string[])value1).SequenceEqual((string[])value2))
                {
                    differences.Add($"{property.Name}: The array elements are different");
                }
            } 
            else if (value1 == null || value2 == null || !value1.Equals(value2))
            {
                differences.Add($"{property.Name}: {value1} != {value2}");
            }
        }

        return differences;
    }
}