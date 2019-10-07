using MadWizard.Insomnia.Service.Sessions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static MadWizard.Insomnia.Service.Sessions.ITextMessageService;

namespace MadWizard.Insomnia.Minion.Services
{
    class TextMessageService : ITextMessageService
    {
        Lazy<IUserInterface> _userInterface;

        public TextMessageService(Lazy<IUserInterface> ui)
        {
            _userInterface = ui;
        }

        public void ShowMessage(string text, string caption, TextMessageType type)
        {
            _userInterface.Value.SendAction(() => MessageBox.Show(text, caption, MessageBoxButtons.OK, (MessageBoxIcon)type));
        }

        public Task<TextMesageAnswer> ShowMessage(string text, string caption, TextMessageType type, TextMessageOptions options)
        {
            var answer = (TextMesageAnswer)MessageBox.Show(text, caption, (MessageBoxButtons)options, (MessageBoxIcon)type);

            return Task.FromResult(answer);
        }
    }
}