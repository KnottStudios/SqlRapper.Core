using System;

namespace SqlRapper.Services
{
    public interface IFileLogger
    {
        /// <summary>
        /// Connection string or a file directory path.
        /// </summary>
        string Location { get; set; }
        /// <summary>
        /// Send our message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="ex"></param>
        /// <returns></returns>
        bool Log(string message, Exception ex = null);
    }
}
