using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using SqlRapper.CustomAttributes;
using SqlRapper.Extensions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SqlRapper.Services
{
    /// <summary>
    /// Creates sql commands that function like an ORM.  
    /// To utilize this like an ORM, pass a specified class into the T objects.  
    /// If the specified classname + s = tablename, it will automatically find the table, otherwise, pass in a table name.
    /// Properties will be mapped to columns that share a name with them. 
    /// During Inserts custom Attributes can be added to the class properties.
    /// The primary key should always be nullable to allow for inserts using the [PrimaryKey] attribute.
    /// [PrimaryKey] designates a property as a primary key and it will be automatically generated in the sql.
    /// [DefaultKey] designates a property that will default in sql during insert if it is null and therefore is not required.  
    /// </summary>
    public class SqlDataService : ISqlDataService
    {
        #region Fields And Properties
        /// <summary>
        /// For that rare occasion when your DB connection is screwy.
        /// </summary>
        public IFileLogger _logger { get; set; }
        public int CmdTimeOut { get; set; } = 30;
        public string ConnectionString { get; set; }
        #endregion
        #region Constructors
        /// <summary>
        /// Creates sql commands that function like an ORM.  
        /// To utilize this like an ORM, pass a specified class into the T objects.  
        /// If the specified classname + s = tablename, it will automatically find the table, otherwise, pass in a table name.
        /// Properties will be mapped to columns that share a name with them. 
        /// During Inserts custom Attributes can be added to the class properties.
        /// The primary key should always be nullable to allow for inserts using the [PrimaryKey] attribute.
        /// [PrimaryKey] designates a property as a primary key and it will be automatically generated in the sql.
        /// [DefaultKey] designates a property that will default in sql during insert if it is null and therefore is not required.  
        /// </summary>
        public SqlDataService() : this(null)
        {
        }
        /// <summary>
        /// Creates sql commands that function like an ORM.  
        /// To utilize this like an ORM, pass a specified class into the T objects.  
        /// If the specified classname + s = tablename, it will automatically find the table, otherwise, pass in a table name.
        /// Properties will be mapped to columns that share a name with them. 
        /// During Inserts custom Attributes can be added to the class properties.
        /// The primary key should always be nullable to allow for inserts using the [PrimaryKey] attribute.
        /// [PrimaryKey] designates a property as a primary key and it will be automatically generated in the sql.
        /// [DefaultKey] designates a property that will default in sql during insert if it is null and therefore is not required.  
        /// </summary>
        /// <param name="connectionString">A connection string to the sql database.</param>
        /// <param name="logger">When SqlDataService fails, it needs to report its failure, however, a sql logger may recall the sql data service to write the error, possibly causing another error, causing a loop. So, we write to a file.  </param>
        public SqlDataService(string connectionString, IFileLogger logger)
        {
            var cs = connectionString;
            try
            {
                if (String.IsNullOrWhiteSpace(connectionString))
                {
                    IConfigurationBuilder builder = new ConfigurationBuilder();
                    builder.AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"));

                    var root = builder.Build();
                    cs = root.GetSection("Sql_Con_String").Value;
                }
            }
            catch 
            { 
            }

            ConnectionString = cs;
            _logger = logger;
        }
        /// <summary>
        /// Creates sql commands that function like an ORM.  
        /// To utilize this like an ORM, pass a specified class into the T objects.  
        /// If the specified classname + s = tablename, it will automatically find the table, otherwise, pass in a table name.
        /// Properties will be mapped to columns that share a name with them. 
        /// During Inserts custom Attributes can be added to the class properties.
        /// The primary key should always be nullable to allow for inserts using the [PrimaryKey] attribute.
        /// [PrimaryKey] designates a property as a primary key and it will be automatically generated in the sql.
        /// [DefaultKey] designates a property that will default in sql during insert if it is null and therefore is not required.  
        /// </summary>
        /// <param name="connectionString">A connection string to the sql database.</param>
        public SqlDataService(string connectionString) : this(connectionString, null)
        {
        }
        #endregion
        #region Public Methods
        /// <summary>
        /// WARNING: THE SQL string here can open this statement to SQL Injection, use this only with known inside information.
        /// USE $@"SprocName" and CommandType.StoredProcedure to use a stored procedure.
        /// USE "Parameterized SQL" 
        /// EXAMPLE: SELECT * FROM Table WHERE param = @paramName, CommandType.Text, and add SqlParameter("@paramName", myValue) to guard against sql injection.
        /// A simple wrapper to get data back in the form of a string.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="SQL"></param>
        /// <param name="commandType"></param>
        /// <param name="sqlParameterCollection"></param>
        /// <returns>a specified object T</returns>
        public string GetDataJson(string SQL, CommandType commandType, List<SqlParameter> sqlParameterCollection = null)
        {
            var results = new List<Dictionary<string, object>>();
            if (string.IsNullOrEmpty(SQL))
            {
                throw new Exception("SQL statement was null or empty");
            }
            using (SqlConnection con = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand(SQL, con))
                {
                    cmd.CommandType = commandType;
                    cmd.Parameters.Clear();
                    cmd.CommandTimeout = CmdTimeOut;
                    try
                    {
                        if (sqlParameterCollection.NullSafeAny())
                        {
                            cmd.Parameters.AddRange(sqlParameterCollection.ToArray());
                        }

                        con.Open();
                        SqlDataReader reader = cmd.ExecuteReader();

                        var columns = reader.GetColumnNames(value => value.ToString());
                        while (reader.Read())
                        {
                            results.Add(SerializeRow(columns, reader));
                        }
                    }
                    catch (Exception ex)
                    {
                        if (con.State != ConnectionState.Closed)
                        {
                            con.Close();
                        }
                        if (_logger != null)
                        {
                            _logger.Log("Failed to Read Sql.", ex);
                        }
                        throw ex;
                    }
                }
            }
            return JsonConvert.SerializeObject(results, Formatting.Indented);
        }

        /// <summary>
        /// WARNING:  The where clause opens this statement to sql injection, only use whereClause with internally created strings.
        /// Gets data from a table matching T + s or a specified table name.  The properties map to the table columns.  
        /// Custom sql can be put into the call through the whereClause, this allows for most customization.
        /// Ways to use: 
        /// 1.  GetData&lt;tableNameSingular&gt;() automatically selects all records in that table.
        /// 2.  GetData&lt;anyclass&gt;(tableName: sqlTableName) populates a table to a specified class.
        /// 3.  GetData&lt;tableNameSingular&gt;("Where x = 1") select * data from the tableNameSingulars table where X = 1.  
        /// Pretty much, this can be a shortcut to only write a where clause or Select * from a table.  You should use 
        /// GetData&lt;T&gt;(sql, commandType.Text, sqlParameterCollection) for more difficult or dangerous queries.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="whereClause"></param>
        /// <param name="tableName">Just specifying a table name here will </param>
        /// <returns></returns>
        public List<T> GetData<T>(string whereClause = null, string tableName = null)
        {
            var row = Activator.CreateInstance<T>();
            var objProps = row.GetType().GetProperties();
            var returnList = new List<T>();
            using (SqlConnection con = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandTimeout = CmdTimeOut;
                    cmd.Connection = con;
                    StringBuilder sb1 = new StringBuilder();
                    string tn = tableName ?? row.GetType().Name + "s";

                    sb1.Append($@"SELECT * FROM {tn} {whereClause}");
                    cmd.CommandText = sb1.ToString();
                    try
                    {
                        con.Open();
                        SqlDataReader reader = cmd.ExecuteReader();
                        var columns = reader.GetColumnNames(value => value.ToString().ToLower());
                        while (reader.Read())
                        {
                            var thisRow = Activator.CreateInstance<T>();
                            foreach (var prop in objProps)
                            {
                                if (columns.Contains(prop.Name.ToLower()))
                                {
                                    var val = reader[prop.Name];
                                    if (val != DBNull.Value)
                                    {
                                        prop.SetValue(thisRow, val);
                                    }
                                    else
                                    {
                                        prop.SetValue(thisRow, null);
                                    }
                                }
                            }
                            returnList.Add(thisRow);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (con.State != ConnectionState.Closed)
                        {
                            con.Close();
                        }
                        if (_logger != null)
                        {
                            _logger.Log("Failed to Read Sql.", ex);
                        }
                        throw ex;
                    }
                }
            }
            return returnList;
        }

        /// <summary>
        /// This method can accept simple objects.  
        /// WARNING: THE SQL string here can open this statement to SQL Injection, use this only with known inside information.
        /// USE: $@"SprocName" and CommandType.StoredProcedure to use a stored procedure.
        /// USE "Parameterized SQL" 
        /// EXAMPLE: SELECT * FROM Table WHERE param = @paramName, CommandType.Text, and add SqlParameter("@paramName", myValue) to guard against sql injection.
        /// A simple wrapper to get data back in the form of an object.  
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="SQL"></param>
        /// <param name="commandType"></param>
        /// <param name="sqlParameterCollection"></param>
        /// <returns>a specified object T</returns>
        public List<T> GetData<T>(string SQL, CommandType commandType, List<SqlParameter> sqlParameterCollection = null)
        {
            if (typeof(T) == typeof(string))
            {
                return GetSimpleObject<T>(SQL, commandType, sqlParameterCollection);
            }

            var objType = Activator.CreateInstance<T>().GetType();
            if (objType.IsSimple())
            {
                return GetSimpleObject<T>(SQL, commandType, sqlParameterCollection);
            }

            var returnList = new List<T>();
            var objProps = objType.GetProperties();

            if (string.IsNullOrEmpty(SQL))
            {
                throw new Exception("SQL statement was null or empty");
            }
            using (SqlConnection con = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand(SQL, con))
                {
                    cmd.CommandType = commandType;
                    cmd.Parameters.Clear();
                    cmd.CommandTimeout = CmdTimeOut;
                    try
                    {
                        if (sqlParameterCollection.NullSafeAny())
                        {
                            cmd.Parameters.AddRange(sqlParameterCollection.ToArray());
                        }

                        con.Open();
                        SqlDataReader reader = cmd.ExecuteReader();
                        var columns = reader.GetColumnNames(value => value.ToString().ToLower());
                        while (reader.Read())
                        {
                            var thisRow = Activator.CreateInstance<T>();
                            foreach (var prop in objProps)
                            {
                                if (columns.Contains(prop.Name.ToLower()))
                                {
                                    var val = reader[prop.Name];
                                    if (val != DBNull.Value)
                                    {
                                        prop.SetValue(thisRow, val);
                                    }
                                    else
                                    {
                                        prop.SetValue(thisRow, null);
                                    }
                                }
                            }
                            returnList.Add(thisRow);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (con.State != ConnectionState.Closed)
                        {
                            con.Close();
                        }
                        if (_logger != null)
                        {
                            _logger.Log("Failed to Read Sql.", ex);
                        }
                        throw ex;
                    }
                }
            }
            return returnList;
        }

        /// <summary>
        /// Works with Simple Sql objects that mock tables.  
        /// Protected from SQL Injection using parameterized sql.
        /// Populates a T class.
        /// </summary>
        /// <typeparam name="T">A hand built class.</typeparam>
        /// <param name="row"></param>
        /// <param name="tableName">A table name to match the class, if null adds an s to classname.</param>
        /// <returns>bool success</returns>
        public bool InsertData<T>(T row, string tableName = null)
        {
            string tn = tableName ?? row.GetType().Name + "s";
            int inserted = 0;
            using (SqlConnection con = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    StringBuilder sb = GetInsertableRows(row, tn, cmd);
                    cmd.CommandText = sb.ToString();
                    cmd.Connection = con;
                    cmd.CommandTimeout = CmdTimeOut;
                    try
                    {
                        con.Open();
                        inserted = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                    catch (Exception ex)
                    {
                        if (con.State != ConnectionState.Closed)
                        {
                            con.Close();
                        }
                        //well logging to sql might not work... we could try... but could cause infinite loop if it fails.
                        //So Lets write to a local file.
                        if (_logger != null)
                        {
                            _logger.Log("Failed to Write to Sql.", ex);
                        }
                        throw ex;
                    }
                }
            }
            if (inserted > 0)
            {
                SetPrimaryColumnValue(row, inserted);
                return true;
            }
            return false;
        }

        /// <summary>
        /// SqlBulkCopy is allegedly protected from Sql Injection.
        /// Inserts a list of simple sql objects that mock tables.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="rows">A list of rows to insert</param>
        /// <param name="tableName">A table name to match the class, if null adds an s to classname.</param>
        /// <returns>bool success</returns>
        public bool BulkInsertData<T>(List<T> rows, string tableName = null)
        {
            var template = rows.FirstOrDefault();
            string tn = tableName ?? template.GetType().Name + "s";
            int inserted = 0;
            using (SqlConnection con = new SqlConnection(ConnectionString))
            {
                using (SqlBulkCopy sbc = new SqlBulkCopy(ConnectionString))
                {
                    var dt = new DataTable();
                    var columns = GetColumns(template);
                    int rowNum = 0;
                    foreach (var row in rows)
                    {
                        dt.Rows.Add();
                        int colNum = 0;
                        foreach (var col in columns)
                        {
                            if (rowNum == 0)
                            {
                                dt.Columns.Add(new DataColumn(col.Name));
                                SqlBulkCopyColumnMapping map = new SqlBulkCopyColumnMapping(col.Name, col.Name);
                                sbc.ColumnMappings.Add(map);
                            }
                            var attributes = GetAttributes(row, col);
                            bool skip = IsPrimaryKey(attributes);
                            var value = row.GetType().GetProperty(col.Name).GetValue(row);
                            skip = skip ? skip : IsNullDefaultKey(attributes, value);
                            if (skip)
                            {
                                dt.Rows[rowNum][colNum] = DBNull.Value;
                                colNum++;
                                continue;
                            }
                            dt.Rows[rowNum][colNum] = value ?? DBNull.Value;
                            colNum++;
                        }
                        rowNum++;
                    }
                    try
                    {
                        con.Open();
                        sbc.DestinationTableName = tn;
                        sbc.WriteToServer(dt);
                        inserted = 1;
                    }
                    catch (Exception ex)
                    {
                        if (con.State != ConnectionState.Closed)
                        {
                            con.Close();
                        }
                        //well logging to sql might not work... we could try... but could cause infinite loop if it fails.
                        //So Lets write to a local file.
                        if (_logger != null)
                        {
                            _logger.Log($"Failed to Bulk Copy to Sql:  {rows.ToCSV()}", ex);
                        }
                        throw ex;
                    }
                }
            }
            if (inserted > 0)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// Ignores the defaultkey attribute. 
        /// Creates a SqlCommand to update a row in a table using the class provided.
        /// If no whereClause is given, the where clause becomes the primary key attribute.  
        /// If no primary key and no where clause exists, throws an exception.
        /// You cannot use both a where clause and a primary key attribute.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="row"></param>
        /// <param name="whereClause">Dangerous.</param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public bool UpdateData<T>(T row, string whereClause = null, string tableName = null)
        {
            string tn = tableName ?? row.GetType().Name + "s";
            int inserted = 0;
            using (SqlConnection con = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    try
                    {
                        StringBuilder sb = GetUpdateableRows(row, tn, cmd, whereClause);
                        cmd.CommandText = sb.ToString();
                        cmd.Connection = con;
                        cmd.CommandTimeout = CmdTimeOut;
                        con.Open();
                        inserted = cmd.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        if (con.State != ConnectionState.Closed)
                        {
                            con.Close();
                        }
                        //well logging to sql might not work... we could try... but could cause infinite loop if it fails.
                        //So Lets write to a local file.
                        if (_logger != null)
                        {
                            _logger.Log("Failed to update to Sql.", ex);
                        }
                        throw ex;
                    }
                }
            }
            if (inserted > 0)
            {
                return true;
            }
            return false;
        }


        /// <summary>
        /// SqlBulkCopy is allegedly protected from Sql Injection.
        /// Updates a list of simple sql objects that mock tables.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="rows">A list of rows to insert</param>
        /// <param name="tableName">a Table name if your class isn't your table name minus s.</param>
        /// <returns>bool success</returns>
        public bool BulkUpdateData<T>(List<T> rows, string tableName = null)
        {
            var template = rows.FirstOrDefault();
            string tn = tableName ?? template.GetType().Name + "s";
            int updated = 0;
            using (SqlConnection con = new SqlConnection(ConnectionString))
            {
                using (SqlCommand command = new SqlCommand("", con))
                {
                    using (SqlBulkCopy sbc = new SqlBulkCopy(con))
                    {
                        var dt = new DataTable();
                        var columns = GetColumns(template);
                        var colNames = new List<string>();
                        string keyName = "";
                        var setStatement = new StringBuilder();
                        int rowNum = 0;
                        foreach (var row in rows)
                        {
                            dt.Rows.Add();
                            int colNum = 0;
                            foreach (var col in columns)
                            {
                                var attributes = GetAttributes(row, col);
                                bool isPrimary = IsPrimaryKey(attributes);
                                var value = row.GetType().GetProperty(col.Name).GetValue(row);

                                if (rowNum == 0)
                                {
                                    colNames.Add($"{col.Name} {GetSqlDataType(col.PropertyType, isPrimary)}");
                                    dt.Columns.Add(new DataColumn(col.Name, Nullable.GetUnderlyingType(col.PropertyType) ?? col.PropertyType));
                                    if (!isPrimary)
                                    {
                                        setStatement.Append($" ME.{col.Name} = T.{col.Name},");
                                    }

                                }
                                if (isPrimary)
                                {
                                    keyName = col.Name;
                                    if (value == null)
                                    {
                                        throw new Exception("Trying to update a row whose primary key is null; use insert instead.");
                                    }
                                }
                                dt.Rows[rowNum][colNum] = value ?? DBNull.Value;
                                colNum++;
                            }
                            rowNum++;
                        }
                        setStatement.Length--;
                        try
                        {
                            con.Open();

                            command.CommandText = $"CREATE TABLE [dbo].[#TmpTable]({String.Join(",", colNames)})";
                            //command.CommandTimeout = CmdTimeOut;
                            command.ExecuteNonQuery();

                            sbc.DestinationTableName = "[dbo].[#TmpTable]";
                            sbc.BulkCopyTimeout = CmdTimeOut * 3;
                            sbc.WriteToServer(dt);
                            sbc.Close();

                            command.CommandTimeout = CmdTimeOut * 3;
                            command.CommandText = $"UPDATE ME SET {setStatement} FROM {tn} as ME INNER JOIN #TmpTable AS T on ME.{keyName} = T.{keyName}; DROP TABLE #TmpTable;";
                            updated = command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            if (con.State != ConnectionState.Closed)
                            {
                                sbc.Close();
                                con.Close();
                            }
                            //well logging to sql might not work... we could try... but no.
                            //So Lets write to a local file.
                            _logger.Log($"Failed to Bulk Update to Sql:  {rows.ToCSV()}", ex);
                            throw ex;
                        }
                    }
                }
            }
            return (updated > 0) ? true : false;
        }

        #endregion
        #region Private methods

        private List<T> GetSimpleObject<T>(string SQL, CommandType commandType, List<SqlParameter> sqlParameterCollection)
        {
            var returnList = new List<T>();
            using (SqlConnection con = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand(SQL, con))
                {
                    cmd.CommandType = commandType;
                    cmd.Parameters.Clear();
                    cmd.CommandTimeout = CmdTimeOut;
                    try
                    {
                        if (sqlParameterCollection.NullSafeAny())
                        {
                            cmd.Parameters.AddRange(sqlParameterCollection.ToArray());
                        }

                        con.Open();
                        SqlDataReader reader = cmd.ExecuteReader();
                        var isString = (typeof(T) == typeof(string));
                        while (reader.Read())
                        {
                            var val = reader.GetValue(0);
                            if(isString)
                            {
                                if (val == DBNull.Value)
                                {
                                    returnList.Add(default);
                                }
                                else
                                {
                                    returnList.Add((T)val);
                                }
                                continue;
                            }
                            var thisRow = Activator.CreateInstance<T>();
                            if (val != DBNull.Value)
                            {
                                thisRow = (T)val;
                            }
                            returnList.Add(thisRow);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (con.State != ConnectionState.Closed)
                        {
                            con.Close();
                        }
                        if (_logger != null)
                        {
                            _logger.Log("Failed to Read Sql.", ex);
                        }
                        throw ex;
                    }
                }
            }
            return returnList;
        }

        private static StringBuilder GetUpdateableRows<T>(T row, string table, SqlCommand cmd, string whereClause = null)
        {
            StringBuilder sb1 = new StringBuilder();
            sb1.Append($"Update {table} Set ");
            string primaryKey = "";
            object primaryValue = "";
            var columns = GetColumns(row);
            foreach (var col in columns)
            {
                var attributes = GetAttributes(row, col);
                var value = row.GetType().GetProperty(col.Name).GetValue(row);
                if (IsPrimaryKey(attributes))
                {
                    primaryKey = col.Name;
                    primaryValue = value;
                    continue;
                }
                if (value != null)
                {
                    sb1.Append($"{col.Name} = @{col.Name},");
                    cmd.Parameters.AddWithValue($"@{col.Name}", value);
                }
            }
            sb1.Length--;
            if (!string.IsNullOrWhiteSpace(whereClause))
            {
                sb1.Append($" {whereClause}");
            }
            else if (!string.IsNullOrWhiteSpace(primaryKey) && primaryValue != null)
            {
                sb1.Append($" WHERE {primaryKey} = @{primaryKey}");
                cmd.Parameters.AddWithValue($"@{primaryKey}", primaryValue);
            }
            else
            {
                throw new Exception("A where clause was not able to be derived from the class due to no primary key and a where clause was not provided.");
            }
            return sb1;
        }
        private static void SetPrimaryColumnValue<T>(T row, int idVal)
        {
            var columns = GetColumns(row);
            foreach (var col in columns)
            {
                var attributes = GetAttributes(row, col);
                bool isThis = IsPrimaryKey(attributes);
                if (isThis)
                {
                    row.GetType().GetProperty(col.Name).SetValue(row, idVal);
                    break;
                }
            }
        }

        private static StringBuilder GetInsertableRows<T>(T row, string table, SqlCommand cmd)
        {
            StringBuilder sb1 = new StringBuilder();
            sb1.Append($"INSERT INTO {table} (");
            StringBuilder sb2 = new StringBuilder();
            sb2.Append($" VALUES (");
            string primColName = "";
            var columns = GetColumns(row);
            foreach (var col in columns)
            {
                var attributes = GetAttributes(row, col);
                bool skip = IsPrimaryKey(attributes);
                if (skip)
                {
                    primColName = col.Name;
                    continue;
                }
                var value = row.GetType().GetProperty(col.Name).GetValue(row);
                skip = skip ? skip : IsNullDefaultKey(attributes, value);
                if (skip)
                {
                    continue;
                }
                sb1.Append($"{col.Name},");
                value = value ?? DBNull.Value;
                cmd.Parameters.AddWithValue($"@{col.Name}", value);
                sb2.Append($"@{col.Name},");
            }
            sb1.Length--;
            sb2.Length--;
            sb1.Append(")");
            sb2.Append(")");
            if (!string.IsNullOrWhiteSpace(primColName))
            {
                sb1.Append($" OUTPUT INSERTED.{primColName}");
            }
            sb1.Append(sb2);
            return sb1;
        }

        private static object[] GetAttributes<T>(T row, PropertyInfo col)
        {
            return row.GetType().GetProperty(col.Name).GetCustomAttributes(false);
        }

        private static PropertyInfo[] GetColumns<T>(T row)
        {
            return row.GetType().GetProperties();
        }

        private static bool IsPrimaryKey(object[] attributes)
        {
            bool skip = false;
            foreach (var attr in attributes)
            {
                if (attr.GetType() == typeof(PrimaryKeyAttribute))
                {
                    skip = true;
                }
            }

            return skip;
        }
        private static bool IsNullDefaultKey(object[] attributes, object value)
        {
            bool skip = false;
            foreach (var attr in attributes)
            {
                if (attr.GetType() == typeof(DefaultKeyAttribute) && value == null)
                {
                    skip = true;
                }
            }

            return skip;
        }

        private Dictionary<string, object> SerializeRow(IEnumerable<string> cols, SqlDataReader reader)
        {
            var result = new Dictionary<string, object>();
            foreach (var col in cols)
                result.Add(col, reader[col]);
            return result;
        }

        private string GetSqlDataType(Type type, bool isPrimary = false)
        {
            var sqlType = new StringBuilder();
            var isNullable = false;
            if (Nullable.GetUnderlyingType(type) != null)
            {
                isNullable = true;
                type = Nullable.GetUnderlyingType(type);
            }
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.String:
                    isNullable = true;
                    sqlType.Append("nvarchar(MAX)");
                    break;
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Int16:
                    sqlType.Append("int");
                    break;
                case TypeCode.Boolean:
                    sqlType.Append("bit");
                    break;
                case TypeCode.DateTime:
                    sqlType.Append("datetime");
                    break;
                case TypeCode.Decimal:
                case TypeCode.Double:
                    sqlType.Append("decimal");
                    break;
            }
            if (!isNullable || isPrimary)
            {
                sqlType.Append(" NOT NULL");
            }
            return sqlType.ToString();
        }

        #endregion
    }
}