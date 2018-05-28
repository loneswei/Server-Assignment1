using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Photon.Hive;
using Photon.Hive.Plugin;
using MySql.Data;
using MySql.Data.MySqlClient;

namespace TestPlugin
{
    public class RaiseEventTestPlugin : PluginBase
    {
        private string connStr;
        private MySqlConnection conn;
        private string RecvdMessage;

        public string ServerString
        {
            get;
            private set;
        }
        public int CallsCount
        {
            get;
            private set;
        }

        public RaiseEventTestPlugin()
        {
            this.UseStrictMode = true;
            this.ServerString = "ServerMessage";
            this.CallsCount = 0;

            // --- Connect to MySQL.
            ConnectToMySQL();
        }

        public override string Name
        {
            get
            {
                return this.GetType().Name;
            }
        }

        public override void OnRaiseEvent(IRaiseEventCallInfo info)
        {
            try
            {
                base.OnRaiseEvent(info);
            }
            catch (Exception e)
            {
                this.PluginHost.BroadcastErrorInfoEvent(e.ToString(), info);
                return;
            }

            if (info.Request.EvCode == 1)
            {
                RecvdMessage = Encoding.Default.GetString((byte[])info.Request.Data);
                string playerName = GetStringDataFromMessage("PlayerName");
                string playerPassword = GetStringDataFromMessage("Password");
                string ReturnMessage = "";

                string search_sql = "SELECT name, password FROM photon.users WHERE name = '" + playerName + "'";
                MySqlCommand cmd = new MySqlCommand(search_sql, conn);
                MySqlDataReader rdr = cmd.ExecuteReader();
                
                if (rdr.HasRows)
                {
                    while (rdr.Read())
                    {
                        // playerPassword does not match with the password in database, UPDATE the password in database
                        if (!rdr[1].Equals(playerPassword))
                        {
                            // Close Select Operation before start Update Operation
                            rdr.Close();
                            string update_sql = "UPDATE photon.users SET password = '" + playerPassword + "' WHERE name = '" + playerName + "'";
                            cmd.CommandText = update_sql;
                            cmd.ExecuteNonQuery();

                            ReturnMessage = playerName + " - LoginResult=PasswordUpdated";

                            // Need to break since already Close reader, can no longer access reader.Read()
                            break;
                        }
                        // playerPassword match with the password in database - worked
                        else
                        {
                            ReturnMessage = playerName + " - LoginResult=OK";
                        }
                    }
                }
                // playerName does not exist in database, INSERT into database
                else
                {
                    // Close Select Operation before start Insert Operation
                    rdr.Close();
                    string insert_sql = "INSERT INTO photon.users (name, password, date_created) VALUES ('" + playerName + "', '" + playerPassword + "', now())";
                    cmd.CommandText = insert_sql;
                    cmd.ExecuteNonQuery();

                    ReturnMessage = playerName + " - LoginResult=NewUser";
                }
               
                this.PluginHost.BroadcastEvent(target: ReciverGroup.All,
                    senderActor: 0,
                    targetGroup: 0,
                    data: new Dictionary<byte, object>() { { (byte)245, ReturnMessage } },
                    evCode: info.Request.EvCode,
                    cacheOp: 0);
            }
        }

        public string GetStringDataFromMessage(string dataTitle)
        {
            dataTitle = dataTitle + "=";
            int index = this.RecvdMessage.IndexOf(dataTitle) + dataTitle.Length;

            // Found
            if (index != -1)
            {
                int index2 = this.RecvdMessage.IndexOf(",", index);
                // Cannot find ,
                if (index2 == -1)
                    index2 = this.RecvdMessage.Length;

                return this.RecvdMessage.Substring(index, index2 - index);
            }
            return null;
        }

        public void ConnectToMySQL()
        {
            // Connect to MySQL
            connStr = "server=localhost;user=root;database=photon;port=3306;password=DM2341sidm";
            conn = new MySqlConnection(connStr);
            try
            {
                conn.Open();
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public void DisconnectFromMySQL()
        {
            conn.Close();
        }
    }
}
