// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using Microsoft.Data.SqlClient.TestUtilities;
using System.Runtime.InteropServices;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class Helpers
    {
        internal static void ProcessCommandBatch(Type connType, string constr, string[] batch)
        {
            if (batch.Length > 0)
            {
                object[] activatorArgs = new object[1];
                activatorArgs[0] = constr;
                using (SqlConnection conn = (SqlConnection)Activator.CreateInstance(connType, activatorArgs))
                {
                    // Testing in macOS Azure SQL container requires access token authentication
                    if (DataTestUtility.UseAccessTokenAuth)
                    {
                        string[] credKeys = { "UserID", "Password", "UID", "PWD", "Authentication" };
                        string connectionStringRemovedAuth = DataTestUtility.RemoveKeysInConnStr(conn.ConnectionString, credKeys);
                        conn.ConnectionString = connectionStringRemovedAuth;
                        conn.AccessToken = DataTestUtility.AADAccessToken;
                    }
                    
                    conn.Open();
                    SqlCommand cmd = conn.CreateCommand();

                    ProcessCommandBatch(cmd, batch);
                }
            }
        }

        internal static void ProcessCommandBatch(DbCommand cmd, string[] batch)
        {
            foreach (string cmdtext in batch)
            {
                Helpers.TryExecute(cmd, cmdtext);
            }
        }

        public static int TryDropTable(string dstConstr, string tableName)
        {
            using (SqlConnection dropConn = DataTestUtility.GetSqlConnection(dstConstr))
            using (SqlCommand dropCmd = dropConn.CreateCommand())
            {
                dropConn.Open();
                return Helpers.TryExecute(dropCmd, "drop table " + tableName);
            }
        }

        public static int TryExecute(DbCommand cmd, string strText)
        {
            cmd.CommandText = strText;
            return cmd.ExecuteNonQuery();
        }

        public static int ExecuteNonQueryAzure(string strConnectionString, string strCommand, int commandTimeout = 60)
        {
            using (SqlConnection connection = DataTestUtility.GetSqlConnection(strConnectionString))
            using (SqlCommand command = connection.CreateCommand())
            {
                connection.Open();
                // We need to increase CommandTimeout else you might see the following error:
                // "Timeout expired. The timeout period elapsed prior to completion of the operation or the server is not responding."
                command.CommandTimeout = commandTimeout;
                return Helpers.TryExecute(command, strCommand);
            }
        }

        public static bool VerifyResults(DbConnection conn, string dstTable, int expectedColumns, int expectedRows)
        {
            using (DbCommand cmd = conn.CreateCommand())
            {
                cmd.CommandText = "select * from " + dstTable + "; select count(*) from " + dstTable;
                using (DbDataReader reader = cmd.ExecuteReader())
                {
                    int numColumns = reader.FieldCount;
                    reader.NextResult();
                    reader.Read();
                    int numRows = (int)reader[0];
                    reader.Close();

                    DataTestUtility.AssertEqualsWithDescription(expectedColumns, numColumns, "Unexpected number of columns.");
                    DataTestUtility.AssertEqualsWithDescription(expectedRows, numRows, "Unexpected number of rows.");
                }
            }
            return false;
        }

        public static bool CheckTableRows(DbConnection conn, string table, bool shouldHaveRows)
        {
            string query = "select * from " + table;
            using (DbCommand cmd = conn.CreateCommand())
            {
                cmd.CommandText = query;
                using (DbDataReader reader = cmd.ExecuteReader())
                {
                    DataTestUtility.AssertEqualsWithDescription(shouldHaveRows, reader.HasRows, "Unexpected value for HasRows.");
                }
            }
            return false;
        }
    }
}
