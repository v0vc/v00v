using System.Windows.Input;

namespace v00v.Model.Entities
{
    public class Tag : ViewModelBase
    {
        #region Fields

        private bool _isSaved;
        private string _text;

        #endregion

        #region Properties

        public int Id { get; set; }
        public bool IsEditable { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsRemovable { get; set; }

        public bool IsSaved
        {
            get => _isSaved;
            set => Update(ref _isSaved, value);
        }

        public ICommand RemoveCommand { get; set; }

        public string Text
        {
            get => _text;
            set => Update(ref _text, value);
        }

        #endregion
    }
}
