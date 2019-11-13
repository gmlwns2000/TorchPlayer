using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace TorchPlayer
{
    public class UICommand : ICommand
    {
        public event EventHandler CanExecuteChanged;

        Action<object> action;
        Func<object, bool> canExce;
        public UICommand(Action<object> action, Func<object, bool> canExce = null)
        {
            this.action = action;
            this.canExce = canExce;
        }
        public UICommand(Action action, Func<object, bool> canExce = null) : this((o) => { action(); }, canExce) { }

        public bool CanExecute(object parameter)
        {
            if (canExce == null)
                return true;
            return canExce(parameter);
        }

        public void Execute(object parameter)
        {
            action(parameter);
        }
    }
}
