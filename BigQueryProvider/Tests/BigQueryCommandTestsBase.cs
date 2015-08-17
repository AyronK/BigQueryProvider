﻿#if DEBUGTEST
using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Xunit;

namespace DevExpress.DataAccess.BigQuery.Tests {
    public abstract class BigQueryCommandTestsBase : IDisposable {
        readonly BigQueryConnection connection;
        readonly DataTable natalitySchemaTable;
        const string commandText = "SELECT * FROM [testdata." + TestingInfrastructureHelper.NatalityTableName + "] LIMIT 10";
        const string commandTextWithFilter = "SELECT * FROM [testdata." + TestingInfrastructureHelper.Natality2TableName + "] WHERE state = @state LIMIT 10";
        const string injectedViaSingleQuotesValue = "CA' or 1=1--";
        const string injectedViaDoubleQuotesValue = @"CA"" or 1=1--";
        const string injectedViaBackSlashesValue = @"CA\' or 1=1--";
        const string normalValue = "CA";

        protected abstract string GetConnectionString();

        protected BigQueryCommandTestsBase() {
            natalitySchemaTable = new DataTable();
            natalitySchemaTable.Columns.Add("ColumnName", typeof (string));
            natalitySchemaTable.Columns.Add("DataType", typeof (Type));
            natalitySchemaTable.Rows.Add("weight_pounds", typeof (float));
            natalitySchemaTable.Rows.Add("is_male", typeof (bool));

            connection = new BigQueryConnection(GetConnectionString());
            connection.Open();
        }

        [Fact]
        public void ExecuteReaderTest_TypeText() {
            using(var dbCommand = connection.CreateCommand()) {
                dbCommand.CommandText = commandText;
                dbCommand.CommandType = CommandType.Text;
                var dbDataReader = dbCommand.ExecuteReader();
                Assert.NotNull(dbDataReader);
                Assert.Equal(2, dbDataReader.FieldCount);
            }
        }

        [Fact]
        public void ExecuteReaderTest_TypeText_Async() {
            using(var dbCommand = connection.CreateCommand()) {
                dbCommand.CommandText = commandText;
                dbCommand.CommandType = CommandType.Text;
                var task = dbCommand.ExecuteReaderAsync();
                task.Wait();
                var dbDataReader = task.Result;
                Assert.NotNull(dbDataReader);
                Assert.Equal(2, dbDataReader.FieldCount);
            }
        }

        [Fact]
        public void ExecuteReaderTest_TypeTableDirect() {
            using(var dbCommand = connection.CreateCommand()) {
                dbCommand.CommandText = "natality";
                dbCommand.CommandType = CommandType.TableDirect;
                var dbDataReader = dbCommand.ExecuteReader();
                Assert.NotNull(dbDataReader);
                Assert.Equal(2, dbDataReader.FieldCount);
            }
        }

        [Fact]
        public void ExecuteReaderTest_TypeTableDirect_Async() {
            using(var dbCommand = connection.CreateCommand()) {
                dbCommand.CommandText = "natality";
                dbCommand.CommandType = CommandType.TableDirect;
                var tast = dbCommand.ExecuteReaderAsync();
                tast.Wait();
                var dbDataReader = tast.Result;
                Assert.NotNull(dbDataReader);
                Assert.Equal(2, dbDataReader.FieldCount);
            }
        }

        [Fact]
        public void ExecuteReader_TypeStoredProcedure() {
            Assert.Throws<ArgumentOutOfRangeException>(() => {
                using (var dbCommand = connection.CreateCommand()) {
                    dbCommand.CommandType = CommandType.StoredProcedure;
                }
            }
                );
        }

        [Fact]
        public void ExecuteScalarReaderTest() {
            using(var dbCommand = connection.CreateCommand()) {
                dbCommand.CommandText = "select 1 from [testdata.natality]";
                var executeScalarResult = dbCommand.ExecuteScalar();
                Assert.NotNull(executeScalarResult);
                Assert.Equal(1, int.Parse(executeScalarResult.ToString()));
            }
        }

        [Fact]
        public void ExecuteScalarReaderTest_Async() {
            using(var dbCommand = connection.CreateCommand()) {
                dbCommand.CommandText = "select 1 from [testdata.natality]";
                var task = dbCommand.ExecuteScalarAsync();
                task.Wait();
                var executeScalarResult = task.Result;
                Assert.NotNull(executeScalarResult);
                Assert.Equal(1, int.Parse(executeScalarResult.ToString()));
            }
        }

        [Fact]
        public void CommandSchemaBehaviorTest() {
            using(var dbCommand = connection.CreateCommand()) {
                var dbDataReader = dbCommand.ExecuteReader(CommandBehavior.SchemaOnly);
                DataTable schemaTable = dbDataReader.GetSchemaTable();
                Assert.True(DataTableComparer.Equals(natalitySchemaTable, schemaTable));
            }
        }

        [Fact]
        public void CommandCloseConnectionTest() {
            connection.Close();

            Assert.Throws<InvalidOperationException>(() => {
                using (var dbCommand = connection.CreateCommand()) {
                }
            });
        }

        [Theory]
        [InlineData("state", normalValue, true)]
        [InlineData("@state", normalValue, true)]
        [InlineData("state", injectedViaSingleQuotesValue, false)]
        [InlineData("state", injectedViaDoubleQuotesValue, false)]
        [InlineData("state", injectedViaBackSlashesValue, false)]
        public void RunCommandWithParameterTest(string parameterName, object parameterValue, bool exceptedReadResult) {
            using(var dbCommand = connection.CreateCommand()) {
                var param = dbCommand.CreateParameter();
                dbCommand.CommandText = commandTextWithFilter;
                param.ParameterName = parameterName;
                param.Value = parameterValue;
                dbCommand.Parameters.Add(param);
                var reader = dbCommand.ExecuteReader(CommandBehavior.Default);
                Assert.Equal(exceptedReadResult, reader.Read());
            }
        }

        public void Dispose() {
            connection.Close();
        }
    }
}
#endif