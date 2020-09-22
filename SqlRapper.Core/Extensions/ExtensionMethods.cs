using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;

namespace SqlRapper.Extensions
{
    public static class ExtensionMethods
    {
        public static bool NullSafeAny<T>(this IEnumerable<T> collection, Func<T, bool> predicate = null) 
        {
            if (predicate == null) {
                return collection != null && collection.Any();
            }
            return collection != null && collection.Any(predicate);
        }

        public static string ToJson(this object obj) {
            if (obj == null)
            {
                return null;
            }
            return JsonConvert.SerializeObject(obj);
        }
        /// <summary>
        /// Gets the column names of returned rows from a database stored procedure or table.  Pass in a format to format those column names.  
        /// Defaults to ToSting();
        /// </summary>
        /// <param name="reader">SqlDataReader</param>
        /// <param name="format">value => value.ToString().ToLower()</param>
        /// <returns></returns>
        public static List<string> GetColumnNames(this SqlDataReader reader, Func<object, string> format = null) {
            format = format ?? (value => value.ToString());
            return reader.GetSchemaTable().Rows.Cast<DataRow>().Select(row => format(row["ColumnName"])).ToList();
        }
        public static string ConvertToJson<T>(this T obj)
        {
            return JsonConvert.SerializeObject(obj);
        }
        public static T ConvertToObject<T>(this string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return Activator.CreateInstance<T>();
            }
            return JsonConvert.DeserializeObject<T>(json);
        }

        public static T DeserializeXml<T>(this string input, XmlRootAttribute xRoot) where T : class
        {
            XmlSerializer ser = new XmlSerializer(typeof(T), xRoot);

            using (StringReader sr = new StringReader(input))
            {
                return (T)ser.Deserialize(sr);
            }
        }

        public static string SerializeXml<T>(this T objectToSerialize)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(objectToSerialize.GetType());

            using (StringWriter textWriter = new StringWriter())
            {
                xmlSerializer.Serialize(textWriter, objectToSerialize);
                return textWriter.ToString();
            }
        }

        public static string ToCSV<T>(this IEnumerable<T> list) 
        {
            return string.Join(",", list);
        }

        public static string ToCSVWithApostrophes<T>(this IEnumerable<T> list)
        {
            if (list.NullSafeAny())
            {
                return "'" + string.Join("','", list) + "'";
            }
            return "";
        }

        public static bool IsSimple(this Type type)
        {
            var typeInfo = type?.GetTypeInfo();
            if (typeInfo.IsGenericType && typeInfo.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                // nullable type, check if the nested type is simple.
                return IsSimple(typeInfo.GetGenericArguments()[0]);
            }
            return typeInfo.IsPrimitive
              || typeInfo.IsEnum
              || type.Equals(typeof(string))
              || type.Equals(typeof(decimal));
        }
    }
}
