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
            string ReturnMessage = "";

            // Login System
            if (info.Request.EvCode == 1)
            {
                RecvdMessage = Encoding.Default.GetString((byte[])info.Request.Data);
                string playerName = GetStringDataFromMessage("PlayerName");
                string playerPassword = GetStringDataFromMessage("Password");

                string search_sql = "SELECT name, password FROM users WHERE name = '" + playerName + "'";
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
                            string update_sql = "UPDATE users SET password = '" + playerPassword + "' WHERE name = '" + playerName + "'";
                            cmd.CommandText = update_sql;
                            cmd.ExecuteNonQuery();

                            ReturnMessage = playerName + " - LoginResult=PasswordUpdated";

                            // Need to break since already Close reader, can no longer access reader.Read()
                            break;
                        }
                        // playerPassword match with the password in database - worked
                        else
                        {
                            rdr.Close();
                            ReturnMessage = playerName + " - LoginResult=OK";
                            break;
                        }
                    }
                }
                // playerName does not exist in database, INSERT into database
                else
                {
                    // Close Select Operation before start Insert Operation
                    rdr.Close();
                    string insert_sql = "INSERT INTO users (name, password, date_created) VALUES ('" + playerName + "', '" + playerPassword + "', now())";
                    cmd.CommandText = insert_sql;
                    cmd.ExecuteNonQuery();

                    ReturnMessage = playerName + " - LoginResult=NewUser";
                }

                BroadcastEvent(info, ReturnMessage);
            }
            // Read position from DB
            else if (info.Request.EvCode == 2)
            {
                RecvdMessage = Encoding.Default.GetString((byte[])info.Request.Data);
                string playerName = GetStringDataFromMessage("PlayerName");

                string search_sql = "SELECT name, PlayerX, PlayerY, PlayerZ, PetX, PetY, PetZ FROM users WHERE name = '" + playerName + "'";
                MySqlCommand cmd = new MySqlCommand(search_sql, conn);
                MySqlDataReader rdr = cmd.ExecuteReader();

                if (rdr.HasRows)
                {
                    while (rdr.Read())
                    {
                        // all x,y,z are not null, send the value from DB to client
                        if (!DBNull.Value.Equals(rdr[1]) && !DBNull.Value.Equals(rdr[2]) && !DBNull.Value.Equals(rdr[3]) &&
                            !DBNull.Value.Equals(rdr[4]) && !DBNull.Value.Equals(rdr[5]) && !DBNull.Value.Equals(rdr[6]))
                        {
                            ReturnMessage = playerName + " - Result=NotNull, - PlayerX=" + rdr[1].ToString() +
                                ", PlayerY=" + rdr[2].ToString() + ", PlayerZ=" + rdr[3].ToString() +

                                ", PetX=" + rdr[4].ToString() + ", PetY=" + rdr[5].ToString() +
                                ", PetZ=" + rdr[6].ToString();

                            // Close Select Operation
                            rdr.Close();

                            // Need to break since already Close reader, can no longer access reader.Read()
                            break;
                        }
                        // one/all of x,y,z is/are null, tell client to load default position instead
                        else
                        {
                            rdr.Close();
                            ReturnMessage = playerName + " - Result=Null";
                            break;
                        }
                    }
                }
                // playerName does not exist in database
                else
                {
                    // Close Select Operation
                    rdr.Close();
                }

                SendEvent(info, ReturnMessage, new List<int> { info.ActorNr });

                // prevent raiseevent from being sent to all players
                info.Cancel();
            }
            // Update position in DB
            else if (info.Request.EvCode == 3)
            {
                RecvdMessage = Encoding.Default.GetString((byte[])info.Request.Data);
                string playerName = GetStringDataFromMessage("PlayerName");
                string playerX = GetStringDataFromMessage("PlayerX");
                string playerY = GetStringDataFromMessage("PlayerY");
                string playerZ = GetStringDataFromMessage("PlayerZ");
                string petX = GetStringDataFromMessage("PetX");
                string petY = GetStringDataFromMessage("PetY");
                string petZ = GetStringDataFromMessage("PetZ");

                string update_sql = "UPDATE users SET PlayerX = '" + playerX + "', PlayerY = '" + playerY + "', PlayerZ = '" + playerZ
                    + "', PetX = '" + petX + "', PetY = '" + petY + "', PetZ = '" + petZ + "' WHERE name = '" + playerName + "'";
                MySqlCommand cmd = new MySqlCommand(update_sql, conn);
                cmd.ExecuteNonQuery();

                ReturnMessage = playerName + " - Result=PositionUpdated";

                BroadcastEvent(info, ReturnMessage);
            }
            //Receive & broadcast the attacking player's name and their position
            else if (info.Request.EvCode == 4)
            {
                RecvdMessage = Encoding.Default.GetString((byte[])info.Request.Data);
                string playerName = GetStringDataFromMessage("PlayerName");
                string playerX = GetStringDataFromMessage("X");
                string playerY = GetStringDataFromMessage("Y");
                string playerZ = GetStringDataFromMessage("Z");

                ReturnMessage = "Player=" + playerName + " attacked (" + playerX + ',' + playerY + ',' + playerZ + ')';

                BroadcastEvent(info, ReturnMessage);
            }
            //Receive & broadcast which player is defending or has stoppped defending
            else if (info.Request.EvCode == 5)
            {
                RecvdMessage = Encoding.Default.GetString((byte[])info.Request.Data);
                string playerName = GetStringDataFromMessage("PlayerName");

                if (RecvdMessage.Last() == '1')
                {
                    ReturnMessage = "Player " + playerName + " is currently DEFENDING";
                }
                else
                {
                    ReturnMessage = "Player " + playerName + " has STOPPED DEFENDING";
                }

                BroadcastEvent(info, ReturnMessage);
            }

            info.Continue();
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

        public void BroadcastEvent(IRaiseEventCallInfo info, string ReturnMessage)
        {
            this.PluginHost.BroadcastEvent(target: ReciverGroup.All, senderActor: 0, targetGroup: 0, data: new Dictionary<byte, object>()
            {
                {
                        (byte)245, ReturnMessage
                }
            },
     evCode: info.Request.EvCode,
     cacheOp: 0);
        }

        public void SendEvent(IRaiseEventCallInfo info, object ReturnMessage, List<int> targets)
        {
            this.PluginHost.BroadcastEvent(recieverActors: targets, senderActor: 0, evCode: info.Request.EvCode, data: new Dictionary<byte, object>()
            {
                {
                        (byte)245, ReturnMessage
                }
            },
     cacheOp: 0);
        }

        public void ConnectToMySQL()
        {
            // Connect to MySQL
            //connStr = "server=localhost;user=root;database=photon;port=3306;password=DM2341sidm";
            connStr = "server=localhost;user=root;database=photon;port=3306;password=Shihwei123";
            conn = new MySqlConnection(connStr);
            try
            {
                conn.Open();
            }
            catch (Exception ex)
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
