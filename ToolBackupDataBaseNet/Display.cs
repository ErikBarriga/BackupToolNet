using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ToolBackupDataBaseNet
{
    internal static class Display
    {
        public enum TypeText {
            Default = ConsoleColor.White,
            Partial = ConsoleColor.DarkGray,
            OK = ConsoleColor.Blue,
            Error = ConsoleColor.Red
        }


        /// <summary>
        /// Set the default ForegroundColor in white
        /// </summary>
        public static void Reset()
        {
            Console.ResetColor();
        }
        /// <summary>
        /// Initialize the Console
        /// </summary>
        /// <param name="text">string for the Console title</param>
        public static void Title(string text)
        {
            Console.Title = text;
            Console.ForegroundColor = (ConsoleColor)TypeText.Default;
        }

        /// <summary>
        /// Print a text using the color, string format and arguments refered
        /// </summary>
        /// /// <param name="text">string to be shown in the Console</param>
        /// <param name="text">string to be shown in the Console</param>
        /// <param name="arg">parameters to be included in text </param>
        public static void Write(TypeText color, string text, bool goBackOneLine, params object[] arg)
        {
            Write(color, string.Format(text, arg), goBackOneLine);
        }
        /// <summary>
        /// Print a text using the color, string refered
        /// </summary>
        /// /// <param name="text">string to be shown in the Console</param>
        /// <param name="text">string to be shown in the Console</param>
        public static void Write(TypeText color, string text, bool goBackOneLine)
        {
            if (goBackOneLine)
                Console.SetCursorPosition(0, Console.CursorTop - 1);
            Console.ForegroundColor = (ConsoleColor)color;
            Console.WriteLine(text);
        }
    }
}