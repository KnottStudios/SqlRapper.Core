using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlRapper.Core.Tests;
using SqlRapper.Services;
using SqlRapperTests.ExampleSqlModel;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace SqlRapperTests
{
    [TestClass]
    public class SqlDataServiceTests
    {
        [TestMethod]
        public void CanWriteToSqlDb()
        {
            var success = true;
            //this really writes to the db.  So it is disabled.  
            /* */
            var conString = AppSettingsReader.GetSettings().GetSection("Sql_Con_String").Value;
            SqlDataService db = new SqlDataService(AppSettingsReader.GetSettings().GetSection("Sql_Con_String").Value, new FileLogger());
            var log = new Log() {
                Message = "Test",
                ApplicationId = int.Parse(AppSettingsReader.GetSettings().GetSection("ApplicationId").Value)
            };
            Assert.AreEqual(log.ApplicationId, 3);
            success = db.InsertData(log);
            
            Assert.IsTrue(success);
        }
        [TestMethod]
        public void CanWriteToSqlDbQuickly()
        {
            var success = true;
            //this really writes to the db.  So it is disabled.  
            /*             */
            Stopwatch sw = new Stopwatch();

            SqlDataService db = new SqlDataService(AppSettingsReader.GetSettings().GetSection("Sql_Con_String").Value, new FileLogger());
            var log = new Log() {
                Message = "Test",
                ApplicationId = int.Parse(AppSettingsReader.GetSettings().GetSection("ApplicationId").Value)
            };
            Assert.AreEqual(log.ApplicationId, 3);
            sw.Start();
            success = db.InsertData(log);
            sw.Stop();
            Assert.IsTrue(sw.ElapsedMilliseconds <= 1000);
            

            Assert.IsTrue(success);
        }
        [TestMethod]
        public void CanInsertMultipleRowsToDbQuickly()
        {
            var success = true;
            //this really writes to the db.  So it is disabled.  
            /*    */
            Stopwatch sw = new Stopwatch();

            SqlDataService db = new SqlDataService(AppSettingsReader.GetSettings().GetSection("Sql_Con_String").Value, new FileLogger());
            var log1 = new Log() {
                Message = "Test1",
                ApplicationId = int.Parse(AppSettingsReader.GetSettings().GetSection("ApplicationId").Value)
            };

            var log2 = new Log()
            {
                Message = "Test2",
                ApplicationId = int.Parse(AppSettingsReader.GetSettings().GetSection("ApplicationId").Value)
            };
            var logs = new List<Log>() { log1, log2 };
            Assert.AreEqual(log1.ApplicationId, 3);
            Assert.AreEqual(log2.ApplicationId, 3);
            sw.Start();
            success = db.BulkInsertData(logs);
            sw.Stop();
            Assert.IsTrue(sw.ElapsedMilliseconds <= 3000);
            

            Assert.IsTrue(success);
        }


        [TestMethod]
        public void CanGetDbInfoQuickly()
        {
            var success = true;
            //this really reads from the db.  It is disabled.  

            Stopwatch sw = new Stopwatch();

            SqlDataService db = new SqlDataService(AppSettingsReader.GetSettings().GetSection("Sql_Con_String").Value, new FileLogger());
            /**/
            sw.Start();
            var logs = db.GetData<Log>();
            sw.Stop();
            
            Assert.IsTrue(sw.ElapsedMilliseconds <= 5000);
                       
            Stopwatch sw2 = new Stopwatch();
            sw2.Start();
            var condensedLogs = db.GetData<Log>("WHERE ApplicationId = 3", "Logs");
            sw2.Stop();

            Assert.IsTrue(sw2.ElapsedMilliseconds <= 5000);
             
            Assert.IsTrue(success);
        }

        [TestMethod]
        public void CanGetDbInfoWithString()
        {
            var success = true;
            //this really reads from the db.  It is disabled.  

            Stopwatch sw = new Stopwatch();

            ISqlDataService db = new SqlDataService(AppSettingsReader.GetSettings().GetSection("Sql_Con_String").Value, new FileLogger());
            /**/
            sw.Start();
            var sprocs = db.GetData<string>($@"Select Distinct ExceptionAsJson
                                                                From [MyAwesome].[dbo].[Logs]
                                                                WHERE ApplicationId = 3", System.Data.CommandType.Text);
            sw.Stop();

            Assert.IsTrue(sw.ElapsedMilliseconds <= 2000);

            Assert.IsTrue(success);
        }


        [TestMethod]
        public void AbleToGetColumnsFromObject()
        {
            SqlDataService dbService = new SqlDataService("fake", null);
            MethodInfo getColumns = dbService.GetType().GetMethod("GetColumns", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Instance);
            if (getColumns == null)
            {
                Assert.Fail("Could not find method");
            }
            getColumns = getColumns.MakeGenericMethod(typeof(Log));

            PropertyInfo[] columns = (PropertyInfo[])getColumns.Invoke(typeof(Log), new object[] { new Log() });

            Assert.IsTrue(columns.Length == 7);
        }
        [TestMethod]
        public void CanGetCustomAttributes()
        {
            SqlDataService dbService = new SqlDataService("fake", null);

            var row = new Log();
            var columns = (PropertyInfo[])PrivateMethod.InvokePrivateMethodWithReturnType(dbService, "GetColumns", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Instance, new Type[] { typeof(Log) }, new object[] { row });

            PrivateType dbTester = new PrivateType(typeof(SqlDataService));

            int customCount = 0;

            foreach (var col in columns)
            {
                var attributes = (object[])PrivateMethod.InvokePrivateMethodWithReturnType(dbService, "GetAttributes", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Instance, new Type[] { typeof(Log) }, new object[] { row, col });

                if ((bool)dbTester.InvokeStatic("IsPrimaryKey", new object[] { attributes }))
                {
                    customCount++;
                }
                var value = row.GetType().GetProperty(col.Name).GetValue(row);
                if ((bool)dbTester.InvokeStatic("IsNullDefaultKey", new object[] { attributes, value }))
                {
                    customCount++;
                }
            }
            Assert.AreEqual(customCount, 2);

            Assert.IsTrue(true);
        }

        [TestMethod]
        public void CanCreateSqlStatementIgnoringOnlyCustomKeys()
        {
            var row = new Log();
            string returnedSql = "";
            using (SqlCommand cmd = new SqlCommand())
            {
                var tableName = row.GetType().Name + "s";
                Type[] types = new Type[] { typeof(Log) };
                var parameters = new object[] { row, tableName, cmd };
                returnedSql = PrivateMethod.InvokePrivateMethodWithReturnType(new SqlDataService("fake", null), "GetInsertableRows", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Instance, types, parameters).ToString();
            }
            string expectedSql = "INSERT INTO Logs (ApplicationId,Message,StackTrace,ExceptionAsJson,ExceptionMessage) OUTPUT INSERTED.LogId VALUES (@ApplicationId,@Message,@StackTrace,@ExceptionAsJson,@ExceptionMessage)";

            Assert.AreEqual(returnedSql, expectedSql);
        }

        [TestMethod]
        public void CanUpdateToSqlDb()
        {
            var success = true;
            //this really writes to the db.  So it is disabled.  
            /* */
            SqlDataService db = new SqlDataService(AppSettingsReader.GetSettings().GetSection("Sql_Con_String").Value, new FileLogger());
            Random rand = new Random();
            var log = new Log()
            {
                LogId = 32,
                Message = "Test " + rand.Next(0, 100),
                ApplicationId = int.Parse(AppSettingsReader.GetSettings().GetSection("ApplicationId").Value)
            };
            success = db.UpdateData(log);

            var newLog = db.GetData<Log>("Where Logid = 32");
            Assert.AreEqual(newLog.FirstOrDefault().Message, log.Message);
            Assert.IsTrue(success);
        }
        [TestMethod]
        public void CanUpdateToSqlDbUsingWhereClause()
        {
            var success = true;
            //this really writes to the db.  So it is disabled.  
            /* */
            SqlDataService db = new SqlDataService(AppSettingsReader.GetSettings().GetSection("Sql_Con_String").Value, new FileLogger());
            Random rand = new Random();
            var log = new Log()
            {
                Message = "Test " + rand.Next(0, 100),
                ApplicationId = int.Parse(AppSettingsReader.GetSettings().GetSection("ApplicationId").Value)
            };
            success = db.UpdateData(log, "Where LogId = 32", "Logs");

            var newLog = db.GetData<Log>("Where Logid = 32");
            Assert.AreEqual(newLog.FirstOrDefault().Message, log.Message);
            Assert.IsTrue(success);
        }
        [TestMethod]
        public void CanUpdateMultipleRowsToDbQuickly()
        {
            var success = true;
            //this really writes to the db.  So it is disabled.  
            /*    */
            Stopwatch sw = new Stopwatch();

            SqlDataService db = new SqlDataService(AppSettingsReader.GetSettings().GetSection("Sql_Con_String").Value, new FileLogger());
            var log1 = new Log() {
                LogId = 66,
                Message = "Test1 Bulk Update ID 66",
                ApplicationId = int.Parse(AppSettingsReader.GetSettings().GetSection("ApplicationId").Value)
            };

            var log2 = new Log()
            {
                LogId = 67,
                Message = "Test2 Bulk Update ID 67",
                ExceptionMessage = "Fake Exception Message, man. 2k length but it comes in from Max.",
                ApplicationId = int.Parse(AppSettingsReader.GetSettings().GetSection("ApplicationId").Value)
            };
            var logs = new List<Log>() { log1, log2 };
            Assert.AreEqual(log1.ApplicationId, 3);
            Assert.AreEqual(log2.ApplicationId, 3);
            sw.Start();
            success = db.BulkUpdateData(logs);
            sw.Stop();
            Assert.IsTrue(sw.ElapsedMilliseconds <= 6000);
            

            Assert.IsTrue(success);
        }
        [TestMethod]
        public void DoesNotOverwriteSetFieldsWithUnspecifiedFields()
        {
            var success = true;
            //this really writes to the db.  So it is disabled.  
            /* */
            SqlDataService db = new SqlDataService(AppSettingsReader.GetSettings().GetSection("Sql_Con_String").Value, new FileLogger());
            Random rand = new Random();
            var roll = rand.Next(0, 100);
            var log = new Log()
            {
                LogId = 33,
                Message = "Test " + roll,
                ApplicationId = int.Parse(AppSettingsReader.GetSettings().GetSection("ApplicationId").Value),
                ExceptionMessage = "Test " + roll
            };
            db.UpdateData(log);

            var roll2 = 101;
            //exception is null, it won't set.
            var log2 = new Log()
            {
                LogId = 33,
                Message = "Test " + roll2,
                ApplicationId = int.Parse(AppSettingsReader.GetSettings().GetSection("ApplicationId").Value),
            };
            db.UpdateData(log2);


            var newLog = db.GetData<Log>("Where Logid = 33");
            Assert.AreEqual(newLog.FirstOrDefault().Message, log2.Message);
            Assert.AreEqual(newLog.FirstOrDefault().ExceptionMessage, log.ExceptionMessage);

            Assert.IsTrue(success);
        }
        [TestMethod]
        public void UpdateThrowsExceptionWhenNoWhereandNoPrimaryKey()
        {
            var success = false;
            //this really writes to the db.  So it is disabled.  
            /* */
            
            SqlDataService db = new SqlDataService(AppSettingsReader.GetSettings().GetSection("Sql_Con_String").Value, new FileLogger());
            Random rand = new Random();
            var log = new Log()
            {
                Message = "Test " + rand.Next(0, 100),
                ApplicationId = int.Parse(AppSettingsReader.GetSettings().GetSection("ApplicationId").Value)
            };
            try
            {
                success = db.UpdateData(log);
            }
            catch (SqlException e) {
                Assert.IsTrue(false);
            }
            catch
            {
                Assert.IsTrue(true);
            }
            Assert.IsFalse(success);
        }

    }
}
