using System;
using System.Windows.Input;

namespace v00v.Model.Core
{
    public class Command : ICommand
    {
        #region Events

        public event EventHandler CanExecuteChanged;

        #endregion

        #region Static and Readonly Fields

        private readonly Predicate<object> _canExecute;
        private readonly Action<object> _execute;

        #endregion

        #region Constructors

        public Command(Action<object> execute = null, Predicate<object> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        #endregion

        #region Methods

        public virtual void NotifyCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute?.Invoke(parameter) ?? true;
        }

        public void Execute(object parameter)
        {
            _execute?.Invoke(parameter);
        }

        #endregion
    }
}
