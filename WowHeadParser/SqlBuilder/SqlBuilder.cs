/*
 * SQL builder used by the WowheadParser entity exporters.
 * Text values are emitted as UTF-8 hex literals so quotes, backslashes, and
 * SQL modes such as NO_BACKSLASH_ESCAPES cannot corrupt generated imports.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Sql
{
    public enum SqlQueryType : byte
    {
        None,
        Update,
        InsertOrUpdate,
        Replace,
        Insert,
        InsertIgnore,
        DeleteInsert,
        Max,
    }

    public class SqlBuilder
    {
        public SqlQueryType QueryType { get; private set; }
        public bool AllowNullValue { get; private set; }
        public bool WriteWithoutHeader { get; private set; }

        private readonly string _tableName;
        private readonly string _keyName;
        private readonly List<string> _fields = new List<string>(64);
        public List<SqlItem> _items = new List<SqlItem>(64);
        private readonly StringBuilder _content = new StringBuilder(8196);

        public SqlBuilder(string tableName, string keyName, SqlQueryType type = SqlQueryType.InsertOrUpdate)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("A table name is required.", "tableName");
            if (string.IsNullOrWhiteSpace(keyName))
                throw new ArgumentException("A key column is required.", "keyName");

            _tableName = tableName;
            _keyName = keyName;
            WriteWithoutHeader = false;
            AllowNullValue = true;
            QueryType = type;

            if (QueryType <= SqlQueryType.None || QueryType >= SqlQueryType.Max)
                throw new InvalidQueryTypeException(QueryType);
        }

        public SqlBuilder(string tableName) : this(tableName, "entry") { }

        public void SetFieldsNames(params string[] args)
        {
            if (args == null)
                throw new ArgumentNullException("args");
            foreach (string field in args)
            {
                if (string.IsNullOrWhiteSpace(field))
                    throw new ArgumentException("Field names cannot be empty.", "args");
                _fields.Add(field);
            }
        }

        public void SetFieldsName(string format, params object[] args)
        {
            if (string.IsNullOrEmpty(format) || args == null)
                throw new ArgumentNullException();
            _fields.Add(string.Format(CultureInfo.InvariantCulture, format, args));
        }

        public void AppendFieldsValue(object key, params string[] args)
        {
            if (key == null || args == null)
                throw new ArgumentNullException();
            var values = new List<string>(args.Length);
            foreach (string value in args)
                values.Add(value ?? string.Empty);
            _items.Add(new SqlItem(key, values));
        }

        public void AppendFieldsValue(object key, params object[] args)
        {
            if (key == null || args == null)
                throw new ArgumentNullException();
            var values = new List<string>(args.Length);
            foreach (object value in args)
                values.Add(value == null ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture));
            _items.Add(new SqlItem(key, values));
        }

        public void AppendSqlQuery(string query, params object[] args)
        {
            if (args == null || string.IsNullOrWhiteSpace(query))
                throw new ArgumentNullException();
            _content.AppendLine(string.Format(CultureInfo.InvariantCulture, query, args));
        }

        public bool Empty { get { return _items.Count == 0; } }

        public override string ToString()
        {
            if (Empty)
                return string.Empty;

            _content.Clear();
            switch (QueryType)
            {
                case SqlQueryType.Update:
                    return BuildUpdateQuery();
                case SqlQueryType.InsertOrUpdate:
                    return BuildInsertOrUpdateQuery();
                case SqlQueryType.Replace:
                case SqlQueryType.Insert:
                case SqlQueryType.InsertIgnore:
                case SqlQueryType.DeleteInsert:
                    return BuildReplaceInsertQuery();
                default:
                    return string.Empty;
            }
        }

        private string BuildUpdateQuery()
        {
            foreach (SqlItem item in _items)
            {
                EnsureFieldCount(item);
                var assignments = new List<string>();
                for (int i = 0; i < item.Count; ++i)
                {
                    string value = item[i];
                    if (!AllowNullValue && string.IsNullOrWhiteSpace(value))
                        continue;
                    assignments.Add(QuoteIdentifier(_fields[i]) + " = " + SqlValue(value));
                }
                if (assignments.Count == 0)
                    continue;
                _content.Append("UPDATE ").Append(QuoteIdentifier(_tableName))
                    .Append(" SET ").Append(string.Join(", ", assignments))
                    .Append(" WHERE ").Append(QuoteIdentifier(_keyName)).Append(" = ")
                    .Append(SqlValue(item.Key)).AppendLine(";");
            }
            return _content.ToString();
        }

        private string BuildInsertOrUpdateQuery()
        {
            foreach (SqlItem item in _items)
            {
                EnsureFieldCount(item);
                var columns = new List<string> { QuoteIdentifier(_keyName) };
                var values = new List<string> { SqlValue(item.Key) };
                var updates = new List<string>();

                for (int i = 0; i < item.Count; ++i)
                {
                    string value = item[i];
                    if (!AllowNullValue && string.IsNullOrWhiteSpace(value))
                        continue;
                    string column = QuoteIdentifier(_fields[i]);
                    columns.Add(column);
                    values.Add(SqlValue(value));
                    updates.Add(column + " = VALUES(" + column + ")");
                }
                if (updates.Count == 0)
                    continue;
                _content.Append("INSERT INTO ").Append(QuoteIdentifier(_tableName))
                    .Append(" (").Append(string.Join(", ", columns)).Append(") VALUES (")
                    .Append(string.Join(", ", values)).Append(") ON DUPLICATE KEY UPDATE ")
                    .Append(string.Join(", ", updates)).AppendLine(";");
            }
            return _content.ToString();
        }

        private string BuildReplaceInsertQuery()
        {
            if (QueryType == SqlQueryType.DeleteInsert)
            {
                var deletedKeys = new HashSet<string>(StringComparer.Ordinal);
                foreach (SqlItem item in _items)
                {
                    string key = SqlValue(item.Key);
                    if (!deletedKeys.Add(key))
                        continue;
                    _content.Append("DELETE FROM ").Append(QuoteIdentifier(_tableName))
                        .Append(" WHERE ").Append(QuoteIdentifier(_keyName)).Append(" = ")
                        .Append(key).AppendLine(";");
                }
            }

            string command = QueryType == SqlQueryType.Insert ? "INSERT INTO " :
                QueryType == SqlQueryType.InsertIgnore ? "INSERT IGNORE INTO " :
                QueryType == SqlQueryType.Replace ? "REPLACE INTO " : "INSERT INTO ";
            _content.Append(command).Append(QuoteIdentifier(_tableName));

            var columns = new List<string>();
            if (!WriteWithoutHeader)
            {
                columns.Add(QuoteIdentifier(_keyName));
                foreach (string field in _fields)
                    columns.Add(QuoteIdentifier(field));
                _content.Append(" (").Append(string.Join(", ", columns)).Append(")");
            }

            _content.AppendLine(" VALUES");
            for (int row = 0; row < _items.Count; ++row)
            {
                SqlItem item = _items[row];
                EnsureFieldCount(item);
                if (!WriteWithoutHeader && item.Count != _fields.Count)
                    throw new InvalidOperationException("SQL row has " + item.Count + " values but " + _fields.Count + " columns were configured for " + _tableName + ".");
                var values = new List<string> { SqlValue(item.Key) };
                for (int i = 0; i < item.Count; ++i)
                {
                    string value = item[i];
                    if (!AllowNullValue && string.IsNullOrWhiteSpace(value))
                        value = "0";
                    values.Add(SqlValue(value));
                }
                _content.Append("(").Append(string.Join(", ", values)).Append(")")
                    .AppendLine(row < _items.Count - 1 ? "," : ";");
            }
            return _content.ToString();
        }

        private void EnsureFieldCount(SqlItem item)
        {
            if (item.Count > _fields.Count)
                throw new InvalidOperationException("SQL row has " + item.Count + " values but only " + _fields.Count + " columns were configured for " + _tableName + ".");
        }

        private static string QuoteIdentifier(string identifier)
        {
            return "`" + identifier.Replace("`", "``") + "`";
        }

        private static string SqlValue(object value)
        {
            if (value == null)
                return "''";

            Type valueType = value.GetType();
            if (valueType.IsPrimitive || value is decimal)
            {
                if (value is bool)
                    return (bool)value ? "1" : "0";
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
            return SqlText(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
        }

        private static string SqlValue(string value)
        {
            if (value == null)
                return "''";
            decimal number;
            if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                return number.ToString(CultureInfo.InvariantCulture);
            return SqlText(value);
        }

        private static string SqlText(string value)
        {
            return "'" + value.Replace("'", "''") + "'";
        }
    }
}
