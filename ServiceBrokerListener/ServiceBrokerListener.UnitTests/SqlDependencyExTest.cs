﻿namespace ServiceBrokerListener.UnitTests
{
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Text;
    using System.Threading;

    using NUnit.Framework;

    using ServiceBrokerListener.Domain;

    /// <summary>
    /// TODO: 
    /// 1. Performance test.
    /// </summary>
    [TestFixture]
    public class SqlDependencyExTest
    {
        private const string MASTER_CONNECTION_STRING =
            "Data Source=(local);Initial Catalog=master;Integrated Security=True";

        private const string TEST_CONNECTION_STRING =
            "Data Source=(local);Initial Catalog=TestDatabase;User Id=TempLogin;Password=8fdKJl3$nlNv3049jsKK;";

        private const string ADMIN_TEST_CONNECTION_STRING =
            "Data Source=(local);Initial Catalog=TestDatabase;Integrated Security=True";

        private const string INSERT_FORMAT =
            "USE [TestDatabase] INSERT INTO temp.[TestTable] (TestField) VALUES({0})";

        private const string REMOVE_FORMAT =
            "USE [TestDatabase] DELETE FROM temp.[TestTable] WHERE TestField = {0}";

        private const string TEST_DATABASE_NAME = "TestDatabase";

        private const string TEST_TABLE_NAME = "TestTable";

        [SetUp]
        public void TestSetup()
        {
            const string CreateDatabaseScript = @"
                CREATE DATABASE TestDatabase;";
            const string CreateUserScript = @"
                CREATE LOGIN TempLogin 
                WITH PASSWORD = '8fdKJl3$nlNv3049jsKK', DEFAULT_DATABASE=TestDatabase;
                
                USE [TestDatabase];
                CREATE USER TempUser FOR LOGIN TempLogin;

                GRANT CREATE PROCEDURE TO [TempUser];
                GRANT CREATE SERVICE TO [TempUser];
                GRANT CREATE QUEUE  TO [TempUser];
                GRANT REFERENCES ON CONTRACT::[DEFAULT] TO [TempUser]
                GRANT SUBSCRIBE QUERY NOTIFICATIONS TO [TempUser];
                GRANT CONTROL ON SCHEMA::[temp] TO [TempUser];
                ";
            const string CreateTableScript = @"                
                CREATE SCHEMA Temp
                    CREATE TABLE TestTable (TestField int, StrField NVARCHAR(MAX))";
                
            TestCleanup();

            ExecuteNonQuery(CreateDatabaseScript, MASTER_CONNECTION_STRING);
            ExecuteNonQuery(CreateTableScript, ADMIN_TEST_CONNECTION_STRING);
            ExecuteNonQuery(CreateUserScript, MASTER_CONNECTION_STRING);
        }

        [TearDown]
        public void TestCleanup()
        {
            const string DropTestDatabaseScript = @"
                IF EXISTS(select * from sys.databases where name='TestDatabase')
                BEGIN
                    ALTER DATABASE [TestDatabase] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [TestDatabase]
                    DROP LOGIN TempLogin;
                END
                ";
            ExecuteNonQuery(DropTestDatabaseScript, MASTER_CONNECTION_STRING);
        }

        [Test]
        public void NotificationTestWith10ChangesAnd10SecDelay()
        {
            NotificationTest(10, 10);
        }

        [Test]
        public void NotificationTestWith10ChangesAnd60SecDelay()
        {
            NotificationTest(10, 60);
        }

        [Test]
        public void NotificationTestWith10Changes()
        {
            NotificationTest(10);
        }

        [Test]
        public void NotificationTestWith100Changes()
        {
            NotificationTest(100);
        }

        [Test]
        public void NotificationTestWith1000Changes()
        {
            NotificationTest(100);
        }

        [Test]
        public void ResourcesReleasabilityTestWith1000Changes()
        {
            ResourcesReleasabilityTest(100);
        }

        [Test]
        public void ResourcesReleasabilityTestWith100Changes()
        {
            ResourcesReleasabilityTest(100);
        }

        [Test]
        public void ResourcesReleasabilityTestWith10Changes()
        {
            ResourcesReleasabilityTest(10);
        }

        [Test]
        public void DetailsTestWith10ChunkInserts()
        {
            DetailsTest(10);
        }

        [Test]
        public void DetailsTestWith100ChunkInserts()
        {
            DetailsTest(100);
        }

        [Test]
        public void DetailsTestWith1000ChunkInserts()
        {
            DetailsTest(1000);
        }

        [Test]
        public void DetailsTestWith10000ChunkInserts()
        {
            DetailsTest(10000);
        }

        [Test]
        public void DetailsTestWith100000ChunkInserts()
        {
            DetailsTest(100000);
        }

        public void ResourcesReleasabilityTest(int changesCount)
        {
            using (var sqlConnection = new SqlConnection(TEST_CONNECTION_STRING))
            {
                sqlConnection.Open();

                int sqlConversationEndpointsCount = sqlConnection.GetConversationEndpointsCount();
                int sqlConversationGroupsCount = sqlConnection.GetConversationGroupsCount();
                int sqlServiceQueuesCount = sqlConnection.GetServiceQueuesCount();
                int sqlServicesCount = sqlConnection.GetServicesCount();
                int sqlTriggersCount = sqlConnection.GetTriggersCount();
                int sqlProceduresCount = sqlConnection.GetProceduresCount();

                using (SqlDependencyEx sqlDependency = new SqlDependencyEx(
                            TEST_CONNECTION_STRING,
                            TEST_DATABASE_NAME,
                            TEST_TABLE_NAME, "temp"))
                {
                    sqlDependency.Start();

                    // Make sure we've created one queue, sevice, trigger and two procedures.
                    Assert.AreEqual(sqlServicesCount + 1, sqlConnection.GetServicesCount());
                    Assert.AreEqual(
                        sqlServiceQueuesCount + 1,
                        sqlConnection.GetServiceQueuesCount());
                    Assert.AreEqual(sqlTriggersCount + 1, sqlConnection.GetTriggersCount());
                    Assert.AreEqual(sqlProceduresCount + 2, sqlConnection.GetProceduresCount());

                    MakeTableInsertDeleteChanges(changesCount);

                    // Wait a little bit to process all changes.
                    Thread.Sleep(1000);
                }

                // Make sure we've released all resources.
                Assert.AreEqual(sqlServicesCount, sqlConnection.GetServicesCount());
                Assert.AreEqual(
                    sqlConversationGroupsCount,
                    sqlConnection.GetConversationGroupsCount());
                Assert.AreEqual(
                    sqlServiceQueuesCount,
                    sqlConnection.GetServiceQueuesCount());
                Assert.AreEqual(
                    sqlConversationEndpointsCount,
                    sqlConnection.GetConversationEndpointsCount());
                Assert.AreEqual(sqlTriggersCount, sqlConnection.GetTriggersCount());
                Assert.AreEqual(sqlProceduresCount, sqlConnection.GetProceduresCount());
            }
        }

        private void NotificationTest(int changesCount, int changesDelayInSec = 0)
        {
            int changesReceived = 0;

            using (SqlDependencyEx sqlDependency = new SqlDependencyEx(
                        TEST_CONNECTION_STRING,
                        TEST_DATABASE_NAME,
                        TEST_TABLE_NAME, "temp")) 
            {
                sqlDependency.TableChanged += (o, e) => changesReceived++;
                sqlDependency.Start();

                Thread.Sleep(changesDelayInSec * 1000);
                MakeTableInsertDeleteChanges(changesCount);

                // Wait a little bit to receive all changes.
                Thread.Sleep(1000);
            }

            Assert.AreEqual(changesCount, changesReceived);
        }

        private static void DetailsTest(int insertsCount)
        {
            int elementsInDetailsCount = 0;
            int changesReceived = 0;

            using (SqlDependencyEx sqlDependency = new SqlDependencyEx(
                        TEST_CONNECTION_STRING,
                        TEST_DATABASE_NAME,
                        TEST_TABLE_NAME, "temp"))
            {
                sqlDependency.TableChanged += (o, e) =>
                    {
                        changesReceived++;

                        if (e.Data == null) return;

                        if (e.NotificationType == SqlDependencyEx.NotificationTypes.Update)
                        {
                            elementsInDetailsCount +=
                                e.Data.Element("inserted").Elements("row").Count();
                            elementsInDetailsCount +=
                                e.Data.Element("deleted").Elements("row").Count();
                            return;
                        }

                        elementsInDetailsCount += e.NotificationType
                                                  == SqlDependencyEx.NotificationTypes.Insert
                                                      ? e.Data.Element("inserted")
                                                            .Elements("row")
                                                            .Count()
                                                      : e.Data.Element("deleted")
                                                            .Elements("row")
                                                            .Count();
                    };
                sqlDependency.Start();

                MakeChunkedInsertDeleteUpdate(insertsCount);

                // Wait a little bit to receive all changes.
                Thread.Sleep(1000);
            }

            Assert.AreEqual(insertsCount * 2, elementsInDetailsCount);
            Assert.AreEqual(3, changesReceived);
        }

        private static void MakeChunkedInsertDeleteUpdate(int changesCount)
        {
            const string ScriptFormat = "INSERT INTO #TmpTbl VALUES({0}, N'{1}')\r\n";

            // insert unicode statement
            StringBuilder scriptResult = new StringBuilder("SELECT 0 AS Number, N'юникод<>_1000001' AS Str INTO #TmpTbl\r\n");
            for (int i = 1; i < changesCount / 2; i++) scriptResult.Append(string.Format(ScriptFormat, i, "юникод<>_" + i));

            scriptResult.Append(@"INSERT INTO temp.TestTable (TestField, StrField)   
                                            SELECT * FROM #TmpTbl");
            ExecuteNonQuery(scriptResult.ToString(), TEST_CONNECTION_STRING);
            ExecuteNonQuery("UPDATE temp.TestTable SET StrField = NULL", TEST_CONNECTION_STRING);
            ExecuteNonQuery("DELETE FROM temp.TestTable", TEST_CONNECTION_STRING);
        }

        private static void MakeTableInsertDeleteChanges(int changesCount)
        {
            for (int i = 0; i < changesCount / 2; i++)
            {
                ExecuteNonQuery(string.Format(INSERT_FORMAT, i), TEST_CONNECTION_STRING);
                ExecuteNonQuery(string.Format(REMOVE_FORMAT, i), TEST_CONNECTION_STRING);
            }
        }

        private static void ExecuteNonQuery(string commandText, string connectionString)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand(commandText, conn))
            {
                conn.Open();
                command.CommandType = CommandType.Text;
                command.CommandTimeout = 60000;
                command.ExecuteNonQuery();
            }
        }
    }
}
