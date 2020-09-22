using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace SqlRapper.Services
{
    public class FileLogger : IFileLogger
    {
        protected readonly object lockObj = new object();
        public string Title { get; set; } = "File Logger for MyBusiness.App";
        public string DirectoryPath { get; set; }
        public string FileName { get; set; }

        /// <summary>
        /// Warning this is get only, set in constructor.
        /// </summary>
        public string Location { get { return DirectoryPath + FileName; } set { throw new NotImplementedException(); } }

        public FileLogger() : this(null, null)
        {
        }

        public FileLogger(string directoryPath, string fileName)
        {
            var dp = directoryPath;
            var fn = fileName;
            try
            {
                if (String.IsNullOrWhiteSpace(directoryPath) || String.IsNullOrWhiteSpace(fileName)) 
                {

                    IConfigurationBuilder builder = new ConfigurationBuilder();

                    builder.AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"));

                    var root = builder.Build();

                    dp = directoryPath ?? root.GetSection("File_Logger_Path").Value;
                    fn = fileName ?? root.GetSection("File_Logger_File").Value;

                }
            }
            catch 
            { }

            DirectoryPath = dp;
            FileName = fn;
        }

        public bool Log(string message, Exception exception = null)
        {
            bool success = CreateLibrary(DirectoryPath);
            success = CreateFile();
            if (success)
            {
                success = WriteToFile(message, exception);
            }
            return success;
        }

        public bool CreateLibrary(string directoryPath) {
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
            }
            catch
            {
                return false;
            }
            return true;
        }
        private bool CreateFile() {
            try
            {
                if (!File.Exists(Location))
                {
                    using (StreamWriter sw = File.CreateText(Location))
                    {
                        sw.WriteLine(Title);
                        sw.WriteLine();
                    }
                }
            } catch {
                return false;
            }
            return true;

        }

        private bool WriteToFile(string message, Exception exception = null) {
            try
            {
                lock (lockObj)
                {

                    using (StreamWriter sw = File.AppendText(Location))
                    {
                        sw.WriteLine($@"{DateTime.Now} : {message}");
                        if (exception != null)
                        {
                            sw.WriteLine($@"EXCEPTION MESSAGE : {exception?.Message}");
                            sw.WriteLine($@"STACKTRACE : {exception?.StackTrace}");
                            sw.WriteLine($@"INNER EXCEPTION : {exception?.InnerException?.Message}");
                        }
                        sw.WriteLine(); //spacer
                        sw.Close();
                    }
                }
            }
            catch
            {
                return false;
            }
            return true;
        }
    }
}