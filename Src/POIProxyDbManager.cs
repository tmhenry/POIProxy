using POIProxy.Models;
using MySql.Data.MySqlClient;
using MySql.Data.Entity;

using System;
using System.Collections.Generic;

using System.Data;

using POILibCommunication;
using System.Web.Configuration;

namespace POIProxy
{
    public class POIProxyDbManager
    {
        private string connectionString;
        private string server;
        private string database;
        private string uid;
        private string password;

        private static POIProxyDbManager sharedInstance;

        //Accessing method to get the singleton instance
        public static POIProxyDbManager Instance
        {
            get
            {
                if (sharedInstance == null)
                {
                    sharedInstance = new POIProxyDbManager();
                }

                return sharedInstance;
            }
        }

        private POIProxyDbManager()
        {
            Initialize();
        }

        //Initialize values
        private void Initialize()
        {
            try
            {
                server = WebConfigurationManager.AppSettings["DbHost"];
                database = WebConfigurationManager.AppSettings["DbName"];
                uid = WebConfigurationManager.AppSettings["DbUsername"];
                password = WebConfigurationManager.AppSettings["DbPassword"];
            }
            catch
            {
                POIGlobalVar.POIDebugLog("Error in db init");
            }

            connectionString = "SERVER=" + server + ";" + "DATABASE=" +
            database + ";" + "UID=" + uid + ";" + "PASSWORD=" + password + ";charset=utf8;";
            
        }



        public void testDbFunction(string sql)
        {
            try
            {
                //Test insert 
                Dictionary<string, object> values = new Dictionary<string, object>();
                values["uid"] = "henry";
                values["matched_uid"] = "jiabo";

                insertIntoTable("user_match", values);
                
                //Test update
                Dictionary<string, object> newValues = new Dictionary<string, object>();
                newValues["uid"] = "huan";

                updateTable("user_match", newValues, values);

                //Test delete
                deleteFromTable("user_match", newValues);

                //Test select
                List<string> columns = new List<string>();
                columns.Add("matched_uid");
                columns.Add("uid");

                DataTable table = selectFromTable("user_match", columns, newValues);
                foreach (DataRow row in table.Rows)
                {
                    POIGlobalVar.POIDebugLog(row["uid"]);
                }

                POIGlobalVar.POIDebugLog("hahha");

            }
            catch
            {
                POIGlobalVar.POIDebugLog("Error in db test");
            }
        }

        #region common sql operation wrapper functions

        public string insertIntoTable(string tableName, Dictionary<string, object> values)
        {
            string sql = String.Format("insert into " + tableName + " {0}", getValuesStmtTplFromValues(values));
            sql += "; select LAST_INSERT_ID();";
            string rowId = "-1";

            try
            {
                //Create a connection and open it
                MySqlConnection connection = new MySqlConnection(connectionString);
                connection.Open();

                MySqlCommand cmd = new MySqlCommand(sql, connection);
                rowId = Convert.ToString(cmd.ExecuteScalar());

                //POIGlobalVar.POIDebugLog(rowId);

                connection.Close();
            }
            catch(Exception e)
            {
                POIGlobalVar.POIDebugLog("Error in db insert: " + e.Message);
            }

            return rowId;
        }

        public void deleteFromTable(string tableName, Dictionary<string, object> conditions)
        {
            string sql = String.Format("delete from " + tableName + " {0}", getWhereStmtTplFromConditions(conditions));

            try
            {
                //Create a connection and open it
                MySqlConnection connection = new MySqlConnection(connectionString);
                connection.Open();

                MySqlCommand cmd = new MySqlCommand(sql, connection);
                cmd.ExecuteNonQuery();

                connection.Close();
            }
            catch(Exception e)
            {
                POIGlobalVar.POIDebugLog("Error in db delete: " + e.Message);
            }
        }

        public void updateTable(string tableName, Dictionary<string, object> values, Dictionary<string, object> conditions)
        {
            string sql = String.Format("update " + tableName + " {0} {1}",
                getSetStmtTplFromValues(values), getWhereStmtTplFromConditions(conditions));

            try
            {
                //Create a connection and open it
                MySqlConnection connection = new MySqlConnection(connectionString);
                connection.Open();

                MySqlCommand cmd = new MySqlCommand(sql, connection);
                cmd.ExecuteNonQuery();

                connection.Close();
            }
            catch(Exception e)
            {
                POIGlobalVar.POIDebugLog("Error in db update: " + e.Message);
            }
        }

        public DataRow selectSingleRowFromTable(string tableName, List<string> columns, Dictionary<string, object> conditions)
        {
            DataTable result = selectFromTable(tableName, columns, conditions);
            if (result != null && result.Rows.Count > 0)
            {
                return result.Rows[0];
            }
            else
            {
                return null;
            }
        }

        public DataTable selectFromTable(string tableName, List<string> columns, Dictionary<string, object> conditions)
        {
            string sql = String.Format("select {0} from " + tableName + " {1}",
                getColumnStmtFromColumnList(columns), getWhereStmtTplFromConditions(conditions));

            DataSet ds = new DataSet();

            try
            {
                //Create a connection and open it
                MySqlConnection connection = new MySqlConnection(connectionString);
                connection.Open();

                MySqlDataAdapter da = new MySqlDataAdapter(sql, connection);
                da.Fill(ds);

                connection.Close();
            }
            catch(Exception e)
            {
                POIGlobalVar.POIDebugLog("Error in db update: " + e.Message);
            }

            return ds.Tables[0];
        }

        #endregion
        
        #region utility functions

        private string getWhereStmtTplFromConditions(Dictionary<string, object> conditions)
        {
            string whereClause;

            if (conditions == null || conditions.Count == 0)
            {
                whereClause = "";
            }
            else
            {
                int count = 0;
                whereClause = "";

                foreach (KeyValuePair<string, object> item in conditions)
                {
                    if (count == 0)
                    {
                        whereClause = "where " + item.Key + " = '" + item.Value + "'";
                        count++;
                    }
                    else
                    {
                        whereClause += " and " + item.Key + " = '" + item.Value + "'";
                    }
                }
            }

            return whereClause;
        }

        private string getSetStmtTplFromValues(Dictionary<string, object> values)
        {
            string setClause;

            if (values == null || values.Count == 0)
            {
                setClause = "";
            }
            else
            {
                int count = 0;
                setClause = "";

                foreach (KeyValuePair<string, object> item in values)
                {
                    if (count == 0)
                    {
                        setClause = "set " + item.Key + " = '" + item.Value + "'";
                        count++;
                    }
                    else
                    {
                        setClause += ", " + item.Key + " = '" + item.Value + "'";
                    }
                }
            }

            return setClause;
        }

        private string getColumnStmtFromColumnList(List<string> columns)
        {
            string columnList;

            if (columns == null || columns.Count == 0)
            {
                columnList = "*";
            }
            else
            {
                columnList = columns[0];
                for (int i = 1; i < columns.Count; i++)
                {
                    columnList += ", " + columns[i];
                }
            }

            return columnList;
        }

        private string getValuesStmtTplFromValues(Dictionary<string, object> values)
        {
            string valueClause;

            if (values == null || values.Count == 0)
            {
                valueClause = "";
            }
            else
            {
                int count = 0;
                string keyClause = "", valClause = "";

                foreach (KeyValuePair<string, object> item in values)
                {
                    if (count == 0)
                    {
                        keyClause = item.Key;
                        valClause = "'" + item.Value + "'";
                        count++;
                    }
                    else
                    {
                        keyClause += ", " + item.Key;
                        valClause += ", '" + item.Value + "'";
                    }
                }

                valueClause = String.Format("({0}) values ({1})", keyClause, valClause);
            }

            return valueClause;
        }

        #endregion
        
    }
}