/**
 * Author : Tamashii
 * Package : DLMSoft.DBCS
 * Source : https://github.com/tamashii/Database.cs
 * Copyright : (C) 2018, DLM Soft / Tamashii.
 * Create Time : 2018-10-20
 * Version : 18.10.20
 * Modifies :
 *  - Tamashii : First version @ 18.10.20
 */
#region Using directives
using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;
#endregion
namespace DLMSoft.DBCS {
    /// <summary>
    /// Class of database accesses.
    /// </summary>
    public class Database : IDisposable {
        #region Properties
        /// <summary>
        /// Get the status of Database object disposed.
        /// </summary>
        public bool IsDisposed { get; private set; }
        #endregion

        #region Fields
        IDbConnection connection_;
        IDbTransaction transaction_;
        List<IDbCommand> commands_;
        List<IDbCommand> transaction_commands_;
        #endregion
        
        #region Constructors
        /// <summary>
        /// Construct a Database object.
        /// </summary>
        /// <param name="conn">Database connection.</param>
        public Database(IDbConnection conn) {
            connection_ = conn;
            transaction_ = null;
            commands_ = new List<IDbCommand>();
            transaction_commands_ = null;

            IsDisposed = false;
            
            connection_.Open();
        }
        #endregion

        #region BeginTransaction
        /// <summary>
        /// Begin trasaction.
        /// </summary>
        /// <param name="isolation_level">Isolation level of transcation.</param>
        public void BeginTransaction(IsolationLevel isolation_level = IsolationLevel.Unspecified) {
            if (IsDisposed)
                throw new ObjectDisposedException("db");

            if (transaction_ != null)
                return;

            transaction_ = connection_.BeginTransaction(isolation_level);
            transaction_commands_ = new List<IDbCommand>();
        }
        #endregion

        #region CommitTransaction
        /// <summary>
        /// Commit transcation.
        /// </summary>
        public void CommitTransaction() {
            if (IsDisposed)
                throw new ObjectDisposedException("db");

            if (transaction_ == null)
                return;

            transaction_.Commit();

            foreach (var c in transaction_commands_) {
                c.Dispose();
            }
            transaction_commands_.Clear();

            transaction_commands_ = null;
        }
        #endregion

        #region RollbackTransaction
        /// <summary>
        /// Rollback transcation.
        /// </summary>
        public void RollbackTransaction() {
            if (IsDisposed)
                throw new ObjectDisposedException("db");

            if (transaction_ == null)
                return;

            transaction_.Rollback();

            foreach (var c in transaction_commands_) {
                c.Dispose();
            }
            transaction_commands_.Clear();

            transaction_commands_ = null;
        }
        #endregion

        #region Execute
        /// <summary>
        /// Execute SQL command without query, and return affected rows of SQL command.
        /// </summary>
        /// <param name="sql">SQL command to execute.</param>
        /// <param name="parameters">Parameters of SQL command.</param>
        /// <returns>Return affected rows of SQL command.</returns>
        public int Execute(string sql, object parameters = null) {
            if (IsDisposed)
                throw new ObjectDisposedException("db");

            var command = CreateCommand();
            command.CommandText = sql;

            AddCommandParams(command, parameters);

            return command.ExecuteNonQuery();
        }
        #endregion

        #region Query
        /// <summary>
        /// Execute SQL command and return query results.
        /// </summary>
        /// <param name="sql">SQL command to execute.</param>
        /// <param name="parameters">Parameters of SQL command.</param>
        /// <typeparam name="T">Type of query result.</typeparam>
        /// <returns>Return a IEnumerator object contains query results of SQL command.</returns>
        public IEnumerable<T> Query<T>(string sql, object parameters = null) {
            if (IsDisposed)
                throw new ObjectDisposedException("db");

            var command = CreateCommand();
            command.CommandText = sql;

            AddCommandParams(command, parameters);

            if (typeof(T) == typeof(object)) { // typeof(T) will be object when T is dynamic
                var dynamic_result = new List<T>();

                using (var reader = command.ExecuteReader()) {
                    var fields = new string[reader.FieldCount];
                    
                    for (var i = 0; i < fields.Length; i++) {
                        fields[i] = reader.GetName(i);
                    }

                    while (reader.Read()) {
                        var item = new ExpandoObject() as IDictionary<string, object>;

                        for (var i = 0; i < fields.Length; i++) {
                            item.Add(fields[i], reader[i]);
                        }

                        dynamic_result.Add((T)item);
                    }
                }

                return dynamic_result;
            }

            var result_type = typeof(T);
            var result_type_assembly = result_type.Assembly;
            var result_type_properties = result_type.GetProperties().ToDictionary(p => p.Name, p => p);

            List<T> result = new List<T>();

            using (var reader = command.ExecuteReader()) {
                var fields = new System.Reflection.PropertyInfo[reader.FieldCount];

                for (var i = 0; i < fields.Length; i++) {
                    var field_name = reader.GetName(i);
                    if (!result_type_properties.ContainsKey(field_name))
                        continue;

                    fields[i] = result_type_properties[field_name];
                }
                
                while (reader.Read()) {
                    var item = (T)result_type_assembly.CreateInstance(result_type.FullName);
                    
                    for (var i = 0; i < fields.Length; i++) {
                        if (fields[i] == null)
                            continue;

                        var value = reader[i];
                        if (value == DBNull.Value)
                            value = null;

                        fields[i].SetValue(item, value);
                    }

                    result.Add(item);
                }
            }

            return result;
        }
        #endregion

        #region Scalar
        /// <summary>
        /// Execute SQL command and return the first column of the first row in query results.
        /// Extra columns or rows are ignored.
        /// </summary>
        /// <param name="sql">SQL command to execute.</param>
        /// <param name="parameters">Parameters of SQL command.</param>
        /// <typeparam name="T">Type of resul.</typeparam>
        /// <returns>Return the first colunm of the first row in query results.</returns>
        public T Scalar<T> (string sql, object parameters) {
            if (IsDisposed)
                throw new ObjectDisposedException("db");

            var command = CreateCommand();
            command.CommandText = sql;

            AddCommandParams(command, parameters);

            var scalar_result = command.ExecuteScalar();

            if (scalar_result == DBNull.Value)
                return default(T);

            return (T)scalar_result;
        }
        #endregion

        #region Dispose (Implement of IDisposable)
        public void Dispose() {
            if (IsDisposed)
                return;
            
            IsDisposed = true;

            // Dispose of transaction and all commands that depends on it.
            if (transaction_ != null) {
                foreach (var c in transaction_commands_) {
                    c.Dispose();
                }
                transaction_commands_.Clear();
                transaction_commands_ = null;

                transaction_.Dispose();
                transaction_ = null;
            }

            // Dispose of all commands
            foreach (var c in commands_) {
                c.Dispose();
            }
            commands_.Clear();
            commands_ = null;

            connection_.Close();
            connection_ = null;
        }
        #endregion

        #region CreateCommand
        IDbCommand CreateCommand() {
            var result = connection_.CreateCommand();
            
            if (transaction_ != null) {
                result.Transaction = transaction_;
                transaction_commands_.Add(result);
                return result;
            }

            // else
            commands_.Add(result);
            return result;
        }
        #endregion

        #region AddCommandParams
        void AddCommandParams(IDbCommand command, object parameters) {
            if (parameters != null) {
                var parameters_type = parameters.GetType();
                var parameters_properties = parameters_type.GetProperties();

                foreach (var property in parameters_properties) {
                    var value = property.GetValue(parameters);

                    if (value == null)
                        value = DBNull.Value;

                    var db_parameter = command.CreateParameter();
                    db_parameter.ParameterName = property.Name;
                    db_parameter.Value = value;

                    command.Parameters.Add(db_parameter);
                }
            }
        }
        #endregion
    }
}