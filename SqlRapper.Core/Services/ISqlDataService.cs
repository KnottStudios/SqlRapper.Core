using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

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
    public interface ISqlDataService
    {
        /// <summary>
        /// For that rare occasion when your DB connection is screwy.
        /// </summary>
        IFileLogger _logger { get; set; }

        string ConnectionString { get; set; }

        int CmdTimeOut { get; set; }

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
        string GetDataJson(string SQL, CommandType commandType, List<SqlParameter> sqlParameterCollection = null);

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
        List<T> GetData<T>(string SQL, CommandType commandType, List<SqlParameter> sqlParameterCollection = null);

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
        /// <param name="tableName">a Table name if your class isn't your table name minus s.</param>
        /// <returns></returns>
        List<T> GetData<T>(string whereClause = null, string tableName = null);

        /// <summary>
        /// Works with Simple Sql objects that mock tables.  
        /// Protected from SQL Injection using parameterized sql.
        /// Populates a T class.
        /// </summary>
        /// <typeparam name="T">A hand built class.</typeparam>
        /// <param name="row"></param>
        /// <param name="tableName">A table name to match the class, if null adds an s to classname.</param>
        /// <returns>bool success</returns>
        bool InsertData<T>(T row, string tableName = null);

        /// <summary>
        /// SqlBulkCopy is allegedly protected from Sql Injection.
        /// Inserts a list of simple sql objects that mock tables.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="rows">A list of rows to insert</param>
        /// <param name="tableName">a Table name if your class isn't your table name minus s.</param>
        /// <returns>bool success</returns>
        bool BulkInsertData<T>(List<T> rows, string tableName = null);

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
        /// <param name="tableName">a Table name if your class isn't your table name minus s.</param>
        /// <returns></returns>
        bool UpdateData<T>(T row, string whereClause = null, string tableName = null);

        /// <summary>
        /// SqlBulkCopy is allegedly protected from Sql Injection.
        /// Updates a list of simple sql objects that mock tables.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="rows">A list of rows to insert</param>
        /// <param name="tableName">a Table name if your class isn't your table name minus s.</param>
        /// <returns>bool success</returns>
        bool BulkUpdateData<T>(List<T> rows, string tableName = null);
    }
}
