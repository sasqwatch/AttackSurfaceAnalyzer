﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using AttackSurfaceAnalyzer.ObjectTypes;
using Microsoft.Data.Sqlite;
using Serilog;

namespace AttackSurfaceAnalyzer.Utils
{
    public static class DatabaseManager
    {
        private static readonly string SQL_CREATE_RUNS = "create table if not exists runs (run_id text, file_system int, ports int, users int, services int, registry int, certificates int, type text, timestamp text, version text, unique(run_id))";
        private static readonly string SQL_CREATE_FILE_MONITORED = "create table if not exists file_system_monitored (run_id text, row_key text, timestamp text, change_type int, path text, old_path text, name text, old_name text, extended_results text, notify_filters text, serialized text)";

        private static readonly string SQL_CREATE_FILE_SYSTEM_COLLECTION = "create table if not exists file_system (run_id text, row_key text, path text, permissions text, size int, hash text, serialized text)";
        private static readonly string SQL_CREATE_OPEN_PORT_COLLECTION = "create table if not exists network_ports (run_id text, row_key text, family text, address text, type text, port int, process_name text, serialized text)";
        private static readonly string SQL_CREATE_SERVICE_COLLECTION = "create table if not exists win_system_service (run_id text, row_key text, service_name text, display_name text, start_type text, current_state text, serialized text)";
        private static readonly string SQL_CREATE_USER_COLLECTION = "create table if not exists user_account (run_id text, row_key text, account_type text, caption text, description text, disabled text, domain text, full_name text, install_date text, local_account text, lockout text, name text, password_changeable text, password_expires text, password_required text, sid text, uid text, gid text, inactive text, home_directory text, shell text, password_storage_algorithm text, properties text data_hash text, serialized text)";
        private static readonly string SQL_CREATE_REGISTRY_COLLECTION = "create table if not exists registry (run_id text, row_key text, key text, value text, subkeys text, permissions text, serialized text)";
        private static readonly string SQL_CREATE_CERTIFICATES_COLLECTION = "create table if not exists certificates (run_id text, row_key text, pkcs12 text, store_location text, store_name text, hash text, hash_plus_store text, cert text, cn text, serialized text)";

        private static readonly string SQL_CREATE_FILE_SYSTEM_ROW_KEY_INDEX = "create index if not exists i_file_system_row_key on file_system(row_key)";
        private static readonly string SQL_CREATE_FILE_SYSTEM_RUN_ID_INDEX = "create index if not exists i_file_system_run_id on file_system(run_id)";
        private static readonly string SQL_CREATE_OPEN_PORT_ROW_KEY_INDEX = "create index if not exists i_network_ports_row_key on network_ports(row_key)";
        private static readonly string SQL_CREATE_OPEN_PORT_RUN_ID_INDEX = "create index if not exists i_network_ports_run_id on network_ports(run_id)";
        private static readonly string SQL_CREATE_SERVICE_ROW_KEY_INDEX = "create index if not exists i_win_system_service_row_key on win_system_service(row_key)";
        private static readonly string SQL_CREATE_SERVICE_RUN_ID_INDEX = "create index if not exists i_win_system_service_run_id on win_system_service(run_id)";
        private static readonly string SQL_CREATE_USER_ROW_KEY_INDEX = "create index if not exists i_user_account_row_key on user_account(row_key)";
        private static readonly string SQL_CREATE_USER_RUN_ID_INDEX = "create index if not exists i_user_account_run_id on user_account(run_id)";
        private static readonly string SQL_CREATE_REGISTRY_ROW_KEY_INDEX = "create index if not exists i_registry_row_key on registry(row_key)";
        private static readonly string SQL_CREATE_REGISTRY_RUN_ID_INDEX = "create index if not exists i_registry_run_id on registry(run_id)";
        private static readonly string SQL_CREATE_CERTIFICATES_ROW_KEY_INDEX = "create index if not exists i_certificates_row_key on certificates(row_key)";
        private static readonly string SQL_CREATE_CERTIFICATES_RUN_ID_INDEX = "create index if not exists i_certificates_run_id on certificates(run_id)";

        private static readonly string SQL_CREATE_FILE_COMBINED_INDEX = "create index if not exists i_filesystem_row_run_combined on file_system(run_id, row_key)";
        private static readonly string SQL_CREATE_REGISTRY_COMBINED_INDEX = "create index if not exists i_registry_row_run_combined on registry(run_id, row_key)";
        private static readonly string SQL_CREATE_COMPARED_COMBINED_INDEX = "create index if not exists i_compared_base_compare_data_combined on compared(base_run_id, compare_run_id, data_type)";

        private static readonly string SQL_CREATE_ANALYZED_TABLE = "create table if not exists results (base_run_id text, compare_run_id text, status int)";

        private static readonly string SQL_CREATE_FILE_SYSTEM_INDEX = "create index if not exists path_index on file_system(path)";
        private static readonly string SQL_CREATE_REGISTRY_KEY_INDEX = "create index if not exists registry_key_index on registry(key)";

        private static readonly string SQL_CREATE_COMPARE_RESULT_TABLE = "create table if not exists compared (base_run_id text, compare_run_id test, change_type int, base_row_key text, compare_row_key text, data_type int)";
        private static readonly string SQL_CREATE_RESULT_CHANGE_TYPE_INDEX = "create index if not exists i_compared_change_type_index on compared(change_type)";
        private static readonly string SQL_CREATE_RESULT_BASE_RUN_ID_INDEX = "create index if not exists i_compared_base_run_id on compared(base_run_id)";
        private static readonly string SQL_CREATE_RESULT_COMPARE_RUN_ID_INDEX = "create index if not exists i_compared_compare_run_id on compared(compare_run_id)";
        private static readonly string SQL_CREATE_RESULT_BASE_ROW_KEY_INDEX = "create index if not exists i_compared_base_row_key on compared(base_row_key)";
        private static readonly string SQL_CREATE_RESULT_DATA_TYPE_INDEX = "create index if not exists i_compared_data_type_index on compared(data_type)";

        private static readonly string SQL_CREATE_PERSISTED_SETTINGS = "create table if not exists persisted_settings (setting text, value text, unique(setting))";
        private static readonly string SQL_CREATE_DEFAULT_SETTINGS = "insert or ignore into persisted_settings (setting, value) values ('telemetry_opt_out','false'),('schema_version',@schema_version)";

        private static readonly string SQL_GET_RESULT_TYPES_SINGLE = "select * from runs where run_id = @run_id";

        private static readonly string SQL_TRUNCATE_CERTIFICATES = "delete from certificates where run_id=@run_id";
        private static readonly string SQL_TRUNCATE_FILES = "delete from file_system where run_id=@run_id";
        private static readonly string SQL_TRUNCATE_USERS = "delete from user_account where run_id = @run_id";
        private static readonly string SQL_TRUNCATE_SERVICES = "delete from win_system_service where run_id = @run_id";
        private static readonly string SQL_TRUNCATE_REGISTRY = "delete from registry where run_id=@run_id";
        private static readonly string SQL_TRUNCATE_PORTS = "delete from network_ports where run_id = @run_id";
        private static readonly string SQL_TRUNCATE_FILES_MONITORED = "delete from file_system_monitored where run_id=@run_id";
        private static readonly string SQL_TRUNCATE_RUN = "delete from runs where run_id=@run_id";
        private static readonly string SQL_TRUNCATE_RESULTS = "delete from results where base_run_id=@run_id or compare_run_id=@run_id";

        private static readonly string SQL_SELECT_LATEST_N_RUNS = "select run_id from runs where type = @type order by timestamp desc limit 0,@limit;";

        private static readonly string SQL_GET_SCHEMA_VERSION = "select value from persisted_settings where setting = 'schema_version' limit 0,1";
        private static readonly string SQL_GET_NUM_RESULTS = "select count(*) as the_count from @table_name where run_id = @run_id";

        private static readonly string PRAGMAS = "PRAGMA main.auto_vacuum = 1; PRAGMA main.journal_mode = 0; PRAGMA main.temp_store = 0;";

        private static readonly string SCHEMA_VERSION = "1";

        public static SqliteConnection Connection;
        public static SqliteConnection ReadOnlyConnection;

        private static SqliteTransaction _transaction;

        private static bool _firstRun = true;

        public static bool Setup()
        {
            if (Connection == null)
            {
                Log.Debug("Starting database setup");
                Connection = new SqliteConnection($"Filename=" + _SqliteFilename);
                Connection.Open();

                using (var cmd = new SqliteCommand(SQL_CREATE_RUNS, DatabaseManager.Connection, DatabaseManager.Transaction))
                {
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = PRAGMAS;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_FILE_MONITORED;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_FILE_SYSTEM_COLLECTION;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_OPEN_PORT_COLLECTION;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_SERVICE_COLLECTION;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_USER_COLLECTION;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_REGISTRY_COLLECTION;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_CERTIFICATES_COLLECTION;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_COMPARE_RESULT_TABLE;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_ANALYZED_TABLE;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_PERSISTED_SETTINGS;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_DEFAULT_SETTINGS;
                    cmd.Parameters.AddWithValue("@schema_version", SCHEMA_VERSION);
                    _firstRun &= cmd.ExecuteNonQuery() != 0;        

                    cmd.CommandText = SQL_CREATE_FILE_SYSTEM_INDEX;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_REGISTRY_KEY_INDEX;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_FILE_SYSTEM_ROW_KEY_INDEX;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_FILE_SYSTEM_RUN_ID_INDEX;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_OPEN_PORT_ROW_KEY_INDEX;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_OPEN_PORT_RUN_ID_INDEX;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_USER_ROW_KEY_INDEX;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_USER_RUN_ID_INDEX;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_SERVICE_ROW_KEY_INDEX;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_SERVICE_RUN_ID_INDEX;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_REGISTRY_ROW_KEY_INDEX;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_REGISTRY_RUN_ID_INDEX;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_CERTIFICATES_ROW_KEY_INDEX;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_CERTIFICATES_RUN_ID_INDEX;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_RESULT_CHANGE_TYPE_INDEX;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_RESULT_BASE_RUN_ID_INDEX;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_RESULT_COMPARE_RUN_ID_INDEX;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_RESULT_BASE_ROW_KEY_INDEX;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_RESULT_DATA_TYPE_INDEX;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_FILE_COMBINED_INDEX;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_REGISTRY_COMBINED_INDEX;
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = SQL_CREATE_COMPARED_COMBINED_INDEX;
                    cmd.ExecuteNonQuery();
                }

                DatabaseManager.Transaction.Commit();
                _transaction = null;
                Log.Debug("Done with database setup");
                return true;
            }
            return false;
        }

        public static bool IsFirstRun()
        {
            return _firstRun;
        }

        public static void VerifySchemaVersion()
        {
            using (var cmd = new SqliteCommand(SQL_GET_SCHEMA_VERSION, DatabaseManager.Connection))
                using (var reader = cmd.ExecuteReader())
                {
                    reader.Read();
                    if (!reader["value"].ToString().Equals(SCHEMA_VERSION))
                    {
                        Log.Fatal("Schema version of database is {0} but {1} is required. Use config --reset-database to delete the incompatible database.", reader["value"].ToString(), SCHEMA_VERSION);
                        Environment.Exit(-1);
                    }
                }
        }

        public static List<string> GetLatestRunIds(int numberOfIds, string type)
        {
            List<string> output = new List<string>();
            using (var cmd = new SqliteCommand(SQL_SELECT_LATEST_N_RUNS, DatabaseManager.Connection))
            {
                cmd.Parameters.AddWithValue("@type", type);
                cmd.Parameters.AddWithValue("@limit", numberOfIds);
                try
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            output.Add(reader["run_id"].ToString());
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Debug(e.GetType().ToString());
                    Log.Debug(e.Message);
                    Log.Debug("Couldn't determine latest {0} run ids.",numberOfIds);
                }
            }
            return output;
        }

        public static int GetNumResults(RESULT_TYPE ResultType, string runId)
        {
            try
            {
                using (var cmd = new SqliteCommand(SQL_GET_NUM_RESULTS.Replace("@table_name", Helpers.ResultTypeToTableName(ResultType)), DatabaseManager.Connection, DatabaseManager.Transaction))
                {
                    cmd.Parameters.AddWithValue("@run_id", runId);

                    using (var reader = cmd.ExecuteReader())
                    {

                        while (reader.Read())
                        {
                            return int.Parse(reader["the_count"].ToString());
                        }
                    }
                }
            }
            catch(Exception e)
            {
                Log.Debug(e.GetType().ToString());
                Log.Debug(e.Message);
            }
            return -1;
        }


        public static SqliteTransaction Transaction
        {
            get
            {
                if (_transaction == null)
                {
                    _transaction = Connection.BeginTransaction();
                }
                return _transaction;
            }
        }

        public static void Commit()
        {
            try
            {
                if (_transaction != null)
                {
                    _transaction.Commit();
                }
            }
            catch (Exception)
            {
                Log.Debug("Commit collision");
            }

            _transaction = null;
        }

        private static string _SqliteFilename = "asa.sqlite";

        public static bool _ReadOnly;

        public static string SqliteFilename
        {
            get
            {
                return _SqliteFilename;
            }
            set
            {
                if (_SqliteFilename != value)
                {
                    try
                    {
                        if (Connection != null)
                        {
                            CloseDatabase();
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Debug("{0}:: {1}: {2}", System.Reflection.MethodBase.GetCurrentMethod().Name, e.GetType().ToString(), e.Message);
                    }

                    try
                    {
                        _SqliteFilename = value;
                        Setup();
                    }
                    catch (Exception e)
                    {
                        Log.Fatal(e, "'{0}' {0}:: {1}: {2}", value, System.Reflection.MethodBase.GetCurrentMethod().Name, e.GetType().ToString(), e.Message);
                    }
                }

            }
        }

        public static void CloseDatabase()
        {
            _transaction.Commit();
            Connection.Close();
            Connection = null;
        }

        public static void DeleteRun(string runid)
        {
            using (var cmd = new SqliteCommand(SQL_GET_RESULT_TYPES_SINGLE, DatabaseManager.Connection, DatabaseManager.Transaction))
            {
                cmd.Parameters.AddWithValue("@run_id", runid);
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.HasRows)
                    {
                        Log.Warning("That Run ID wasn't found in the database");
                        return;
                    }
                    while (reader.Read())
                    {
                        using (var inner_cmd = new SqliteCommand(SQL_TRUNCATE_RUN, DatabaseManager.Connection, DatabaseManager.Transaction))
                        {
                            inner_cmd.Parameters.AddWithValue("@run_id", runid);
                            inner_cmd.ExecuteNonQuery();
                        }
                        if (reader["type"].ToString() == "monitor")
                        {
                            if ((int.Parse(reader["file_system"].ToString()) != 0))
                            {
                                using (var inner_cmd = new SqliteCommand(SQL_TRUNCATE_FILES_MONITORED, DatabaseManager.Connection, DatabaseManager.Transaction))
                                {
                                    inner_cmd.Parameters.AddWithValue("@run_id", runid);
                                    inner_cmd.ExecuteNonQuery();
                                }
                            }
                        }
                        else
                        {
                            using (var inner_cmd = new SqliteCommand(SQL_TRUNCATE_RESULTS, DatabaseManager.Connection, DatabaseManager.Transaction))
                            {
                                inner_cmd.Parameters.AddWithValue("@run_id", runid);
                                inner_cmd.ExecuteNonQuery();
                            }

                            if ((int.Parse(reader["file_system"].ToString()) != 0))
                            {
                                using (var inner_cmd = new SqliteCommand(SQL_TRUNCATE_FILES, DatabaseManager.Connection, DatabaseManager.Transaction))
                                {
                                    inner_cmd.Parameters.AddWithValue("@run_id", runid);
                                    inner_cmd.ExecuteNonQuery();
                                }
                            }
                            if ((int.Parse(reader["ports"].ToString()) != 0))
                            {
                                using (var inner_cmd = new SqliteCommand(SQL_TRUNCATE_PORTS, DatabaseManager.Connection, DatabaseManager.Transaction))
                                {
                                    inner_cmd.Parameters.AddWithValue("@run_id", runid);
                                    inner_cmd.ExecuteNonQuery();
                                }
                            }


                            if ((int.Parse(reader["users"].ToString()) != 0))
                            {
                                using (var inner_cmd = new SqliteCommand(SQL_TRUNCATE_USERS, DatabaseManager.Connection, DatabaseManager.Transaction))
                                {
                                    inner_cmd.Parameters.AddWithValue("@run_id", runid);
                                    inner_cmd.ExecuteNonQuery();
                                }
                            }
                            if ((int.Parse(reader["services"].ToString()) != 0))
                            {
                                using (var inner_cmd = new SqliteCommand(SQL_TRUNCATE_SERVICES, DatabaseManager.Connection, DatabaseManager.Transaction))
                                {
                                    inner_cmd.Parameters.AddWithValue("@run_id", runid);
                                    inner_cmd.ExecuteNonQuery();
                                }
                            }
                            if ((int.Parse(reader["registry"].ToString()) != 0))
                            {
                                using (var inner_cmd = new SqliteCommand(SQL_TRUNCATE_REGISTRY, DatabaseManager.Connection, DatabaseManager.Transaction))
                                {
                                    inner_cmd.Parameters.AddWithValue("@run_id", runid);
                                    inner_cmd.ExecuteNonQuery();
                                }
                            }
                            if ((int.Parse(reader["certificates"].ToString()) != 0))
                            {
                                using (var inner_cmd = new SqliteCommand(SQL_TRUNCATE_CERTIFICATES, DatabaseManager.Connection, DatabaseManager.Transaction))
                                {
                                    inner_cmd.Parameters.AddWithValue("@run_id", runid);
                                    inner_cmd.ExecuteNonQuery();
                                }
                            }
                        }
                    }
                }

                cmd.CommandText = "VACUUM";
                cmd.ExecuteNonQuery();
            }
            DatabaseManager.Commit();
        }
    }
}
