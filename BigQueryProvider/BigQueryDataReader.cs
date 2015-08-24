﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using DevExpress.DataAccess.BigQuery.Native;
using Google;
using Google.Apis.Bigquery.v2;
using Google.Apis.Bigquery.v2.Data;

namespace DevExpress.DataAccess.BigQuery {
    public class BigQueryDataReader : DbDataReader {
        static string PrepareCommandText(BigQueryCommand command) {
            return command.CommandType == CommandType.TableDirect ? string.Format("SELECT * FROM [{0}.{1}]", command.Connection.DataSetId, command.CommandText) : command.CommandText;
        }

        static string PrepareParameterValue(BigQueryParameter parameter) {
            var isString = parameter.BigQueryDbType == BigQueryDbType.String;
            var isTimestamp = parameter.BigQueryDbType == BigQueryDbType.Timestamp;
            if(isString)
                return string.Format("'{0}'", EscapeValue(CroppStringValueBySize(parameter)));
            string format = isTimestamp ? "TIMESTAMP('{0:u}')" : "{0}";
            return string.Format(CultureInfo.InvariantCulture, format, parameter.Value);
        }

        static string CroppStringValueBySize(BigQueryParameter parameter) {
            var value = string.Format(CultureInfo.InvariantCulture, "{0}", parameter.Value);
            return parameter.Size < value.Length
                ? value.Remove(parameter.Size)
                : value;
        }

        static object EscapeValue(object value) {
            var valueAsString = value as string;
            if(valueAsString == null)
                return value;
            return valueAsString.Replace(@"\", @"\\")
                .Replace("'", @"\'")
                .Replace("\"", @"""");
        }

        static DateTime UnixTimeStampToDateTime(object timestamp) {
            return UnixTimeStampToDateTime(double.Parse(timestamp.ToString()));
        }

        static DateTime UnixTimeStampToDateTime(double unixTimeStamp) {
            DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp);
            return dtDateTime;
        }

        const char parameterPrefix = '@';

        readonly BigQueryCommand bigQueryCommand;
        readonly BigqueryService bigQueryService;
        readonly CommandBehavior behavior;
        IEnumerator<TableRow> enumerator;
        IEnumerator<TableList.TablesData> tables;
        IList<TableRow> rows;
        TableSchema schema;
        int fieldsCount;
        bool disposed;

        internal BigQueryDataReader(CommandBehavior behavior, BigQueryCommand command, BigqueryService service) {
            this.behavior = behavior;
            bigQueryService = service;
            bigQueryCommand = command;
        }

        public override void Close() {
            Dispose();
        }

        public override Task<T> GetFieldValueAsync<T>(int ordinal, CancellationToken cancellationToken) {
            return Task.Run(() => GetFieldValue<T>(ordinal), cancellationToken);
        }

        public override T GetFieldValue<T>(int ordinal) {
            object value = GetValue(ordinal);
            return ChangeValueType<T>(value, ordinal);
        }

        public override DataTable GetSchemaTable() {
            DisposeCheck();
            if(tables.Current == null)
                return null;
            string projectId = bigQueryCommand.Connection.ProjectId;
            string dataSetId = bigQueryCommand.Connection.DataSetId;
            string tableId = tables.Current.TableReference.TableId;

            DataTable dataTable = new DataTable { TableName = tableId };
            dataTable.Columns.Add("ColumnName", typeof(string));
            dataTable.Columns.Add("DataType", typeof(Type));

            try {
                Table tableSchema = bigQueryService.Tables.Get(projectId, dataSetId, tableId).Execute();
                foreach(var tableFieldSchema in tableSchema.Schema.Fields) {
                    dataTable.Rows.Add(tableFieldSchema.Name, BigQueryTypeConverter.ToType(tableFieldSchema.Type));
                }
            }
            catch(GoogleApiException e) {
                throw e.Wrap();
            }

            return dataTable;
        }

        public override Task<bool> NextResultAsync(CancellationToken cancellationToken) {
            return Task.Run(() => NextResult(), cancellationToken);
        }

        public override bool NextResult() {
            DisposeCheck();
            if(behavior == CommandBehavior.SchemaOnly) {
                return tables.MoveNext();
            }
            return false;
        }

        public override Task<bool> ReadAsync(CancellationToken cancellationToken) {
            return Task.Run(() => Read(), cancellationToken);
        }

        public override bool Read() {
            DisposeCheck();
            return enumerator.MoveNext();
        }

        public override int Depth {
            get { return 0; }
        }

        public override bool IsClosed {
            get { return disposed; }
        }

        public override int RecordsAffected {
            get { return 0; }
        }

        public override bool GetBoolean(int ordinal) {
            object value = GetValue(ordinal);
            return ChangeValueType<bool>(value, ordinal);
        }

        public override byte GetByte(int ordinal) {
            throw new NotSupportedException();
        }

        public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length) {
            throw new NotSupportedException();
        }

        public override char GetChar(int ordinal) {
            throw new NotSupportedException();
        }

        public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length) {
            throw new NotSupportedException();
        }

        public override Guid GetGuid(int ordinal) {
            throw new NotSupportedException();
        }

        public override short GetInt16(int ordinal) {
            object value = GetValue(ordinal);
            return ChangeValueType<short>(value, ordinal);
        }

        public override int GetInt32(int ordinal) {
            object value = GetValue(ordinal);
            return ChangeValueType<int>(value, ordinal);
        }

        public override long GetInt64(int ordinal) {
            object value = GetValue(ordinal);
            return ChangeValueType<long>(value, ordinal);
        }

        public override DateTime GetDateTime(int ordinal) {
            object value = GetValue(ordinal);
            return ChangeValueType<DateTime>(value, ordinal);
        }

        public override string GetString(int ordinal) {
            object value = GetValue(ordinal);
            return ChangeValueType<string>(value, ordinal);
        }

        public override decimal GetDecimal(int ordinal) {
            throw new NotSupportedException();
        }

        public override double GetDouble(int ordinal) {
            throw new NotSupportedException();
        }

        public override float GetFloat(int ordinal) {
            object value = GetValue(ordinal);
            return ChangeValueType<float>(value, ordinal);
        }

        public override string GetName(int ordinal) {
            DisposeCheck();
            RangeCheck(ordinal);
            return schema.Fields[ordinal].Name;
        }

        public override int GetProviderSpecificValues(object[] values) {
            return GetValues(values);
        }

        public override int GetValues(object[] values) {
            DisposeCheck();
            for(int i = 0; i < fieldsCount; i++) {
                values[i] = ChangeValueType(GetValue(i), i);
            }
            return values.Length;
        }

        public override Task<bool> IsDBNullAsync(int ordinal, CancellationToken cancellationToken) {
            return Task.Run(() => IsDBNull(ordinal), cancellationToken);
        }

        public override bool IsDBNull(int ordinal) {
            DisposeCheck();
            RangeCheck(ordinal);
            return GetValue(ordinal) == null;
        }

        public override int VisibleFieldCount {
            get { return FieldCount; }
        }

        public override int FieldCount {
            get {
                DisposeCheck();
                return fieldsCount;
            }
        }

        public override object this[int ordinal] {
            get {
                DisposeCheck();
                return GetValue(ordinal);
            }
        }

        public override object this[string name] {
            get {
                DisposeCheck();
                int ordinal = GetOrdinal(name);
                return GetValue(ordinal);
            }
        }

        public override bool HasRows {
            get {
                DisposeCheck();
                return rows != null && rows.Count > 0;
            }
        }

        public override int GetOrdinal(string name) {
            DisposeCheck();
            for(int i = 0; i < schema.Fields.Count; i++) {
                if(schema.Fields[i].Name == name)
                    return i;
            }
            return -1;
        }

        public override string GetDataTypeName(int ordinal) {
            DisposeCheck();
            return GetFieldType(ordinal).Name;
        }

        public override Type GetProviderSpecificFieldType(int ordinal) {
            return GetFieldType(ordinal);
        }

        public override Type GetFieldType(int ordinal) {
            DisposeCheck();
            RangeCheck(ordinal);
            string type = schema.Fields[ordinal].Type;
            Type fieldType = BigQueryTypeConverter.ToType(type);
            if(fieldType != null)
                return fieldType;
            throw new ArgumentOutOfRangeException("ordinal", ordinal, "No field with ordinal");
        }

        public override object GetProviderSpecificValue(int ordinal) {
            return GetValue(ordinal);
        }

        public override object GetValue(int ordinal) {
            DisposeCheck();
            RangeCheck(ordinal);
            return enumerator.Current.F[ordinal].V;
        }

        public override IEnumerator GetEnumerator() {
            DisposeCheck();
            return enumerator;
        }

        internal async Task InitializeAsync() {
            try {
                if(behavior == CommandBehavior.SchemaOnly) {
                    TableList tableList = await bigQueryService.Tables.List(bigQueryCommand.Connection.ProjectId, bigQueryCommand.Connection.DataSetId).ExecuteAsync().ConfigureAwait(false);
                    tables = tableList.Tables.GetEnumerator();
                    tables.MoveNext();
                } else {
                    ((BigQueryParameterCollection)bigQueryCommand.Parameters).Validate();
                    JobsResource.QueryRequest request = CreateRequest();
                    QueryResponse queryResponse = await request.ExecuteAsync().ConfigureAwait(false);
                    ProcessQueryResponse(queryResponse);
                }
            }
            catch(GoogleApiException e) {
                throw e.Wrap();
            }
        }

        protected override void Dispose(bool disposing) {
            if(disposed)
                return;
            if(disposing) {
                if(enumerator != null) {
                    enumerator.Dispose();
                }
            }
            disposed = true;
            base.Dispose(disposing);
        }

        JobsResource.QueryRequest CreateRequest() {
            BigQueryParameterCollection collection = (BigQueryParameterCollection)bigQueryCommand.Parameters;
            foreach(BigQueryParameter parameter in collection) {
                bigQueryCommand.CommandText = bigQueryCommand.CommandText.Replace(parameterPrefix + parameter.ParameterName.TrimStart(parameterPrefix), PrepareParameterValue(parameter));
            }
            QueryRequest queryRequest = new QueryRequest { Query = PrepareCommandText(bigQueryCommand), TimeoutMs = bigQueryCommand.CommandTimeout != 0 ? (int)TimeSpan.FromSeconds(bigQueryCommand.CommandTimeout).TotalMilliseconds : int.MaxValue };
            JobsResource.QueryRequest request = bigQueryService.Jobs.Query(queryRequest, bigQueryCommand.Connection.ProjectId);
            return request;
        }

        void ProcessQueryResponse(QueryResponse queryResponse) {
            if(queryResponse.JobComplete.HasValue && !queryResponse.JobComplete.Value) {
                throw new BigQueryException("Timeout is reached");
            }
            rows = queryResponse.Rows;
            schema = queryResponse.Schema;
            fieldsCount = schema.Fields.Count;
            if(rows != null) {
                enumerator = rows.GetEnumerator();
            } else {
                rows = new TableRow[] { };
                enumerator = rows.GetEnumerator();
            }
        }

        T ChangeValueType<T>(object value, int ordinal) {
            return (T)ChangeValueType(value, ordinal);
        }

        object ChangeValueType(object value, int ordinal) {
            if(value == null)
                return null;
            Type conversionType = BigQueryTypeConverter.ToType(schema.Fields[ordinal].Type);
            if(conversionType == typeof(DateTime))
                return UnixTimeStampToDateTime(value);
            return Convert.ChangeType(value, conversionType);
        }

        void DisposeCheck() {
            if(disposed)
                throw new ObjectDisposedException("DataReader disposed");
        }

        void RangeCheck(int index) {
            if(index < 0 || fieldsCount <= index)
                throw new IndexOutOfRangeException("index out of range");
        }

        ~BigQueryDataReader() {
            Dispose(false);
        }
    }
}
