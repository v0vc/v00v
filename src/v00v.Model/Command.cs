using System;
using System.Windows.Input;

namespace v00v.Model
{
    public class Command : ICommand
    {
        #region Events

        public event EventHandler CanExecuteChanged;

        #endregion

        #region Static and Readonly Fields

        private readonly Func<bool> _canExecute;

        private readonly Action _execute;

        #endregion

        #region Constructors

        public Command(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        #endregion

        #region Methods

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute();
        }

        public void Execute(object parameter)
        {
            _execute?.Invoke();
        }

        #endregion
    }
}
