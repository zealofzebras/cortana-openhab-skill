using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace openHAbot
{
    public class LoginFlow
    {
        // Identifies the last question asked.
        public enum Question
        {
            UseCustomServer,
            ServerName,
            Username,
            Password,
            None,  // Our last action did not involve a question.
        }

        // The last question asked.
        public Question LastQuestionAsked { get; set; } = Question.None;
    }
}