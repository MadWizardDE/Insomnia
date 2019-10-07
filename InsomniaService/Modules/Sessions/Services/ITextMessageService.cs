using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Service.Sessions
{
    public interface ITextMessageService
    {
        void ShowMessage(string text, string caption = null, TextMessageType type = TextMessageType.None);

        Task<TextMesageAnswer> ShowMessage(string text, string caption = null, TextMessageType type = TextMessageType.None, TextMessageOptions options = TextMessageOptions.OK);

        [Serializable]
        public enum TextMessageType
        {
            None = 0,
            Info = 64,
            Warning = 48,
            Error = 16
        }

        [Serializable]
        public enum TextMessageOptions
        {
            OK = 0,
            OKCancel = 1,
            AbortRetryIgnore = 2,
            YesNoCancel = 3,
            YesNo = 4,
            RetryCancel = 5
        }

        [Serializable]
        public enum TextMesageAnswer
        {
            None = 0,
            OK = 1,
            Cancel = 2,
            Abort = 3,
            Retry = 4,
            Ignore = 5,
            Yes = 6,
            No = 7
        }
    }
}
