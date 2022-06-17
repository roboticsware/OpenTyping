using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Threading.Tasks;
using System.Windows;
using OpenTyping.Resources.Lang;
using SQLite;

namespace OpenTyping
{
    public class SqliteProvider
    {
        private SQLiteAsyncConnection _db;
        public static string machineId;

        public SqliteProvider() {
            GetMachineID();
        }

        public async Task<bool> OpenDatabase()
        {
            string filename = Directory.GetCurrentDirectory() + @"\Resources\db.db";
            string password = machineId + "AddYourSalt";
            Debug.WriteLine(password);

            var options = new SQLiteConnectionString(filename, true, key: password);
            _db = new SQLiteAsyncConnection(options);

            if (!File.Exists(filename))
            {
                await _db.CreateTableAsync<User>();
                Debug.WriteLine("New database! Table created!");
            }
            else // Check it's broken DB
            {
                try
                {
                    await _db.GetTableInfoAsync("Users");
                } 
                catch (Exception ex)
                {
                    if (ex.Message.Contains("file is not a database"))
                    {

                        MessageBox.Show(LangStr.ErrMsg24,
                                    LangStr.AppName,
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                        Environment.Exit(-1);
                        return false;
                    }
                }
            }

            await _db.CloseAsync();

            return true;
        } 

        private void GetMachineID()
        {
            ManagementObjectCollection mbsList;
            mbsList= new ManagementObjectSearcher("Select * From Win32_processor").Get();
            
            foreach (ManagementObject mo in mbsList)
            {
                machineId = mo["ProcessorID"].ToString();
            }
        }

        public async Task<int> AddUserAsync(User user)
        {
            return await _db.InsertAsync(user);
        }

        public async Task<List<User>> GetUsersAsync()
        {
            return await _db.QueryAsync<User>("Select * From Users");
        }

        public async Task ReWriteAllAsync(List<User> users)
        {
            await _db.DeleteAllAsync<User>();
            Debug.WriteLine("All records are deleted");

            await _db.InsertAllAsync(users);
            Debug.WriteLine("Rewrited all users");
        }
    }
}
