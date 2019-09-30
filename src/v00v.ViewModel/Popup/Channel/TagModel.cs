using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using v00v.Model.Entities;
using v00v.ViewModel.Core;

namespace v00v.ViewModel.Popup.Channel
{
    public class TagModel : ViewModelBase
    {
        private string _tagText;
        private bool _isSaved;

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

        public string TagText
        {
            get => _tagText;
            set => Update(ref _tagText, value);
        }

        #endregion

        #region Static Methods

        public static TagModel FromDbTag(Tag tag, IEnumerable<Tag> channelTags)
        {
            var t = new TagModel
            {
                Id = tag.Id,
                TagText = tag.Text,
                IsEditable = false,
                IsRemovable = false,
                IsSaved = true,
            };
            if (channelTags != null)
            {
                t.IsEnabled = channelTags.Select(x => x.Id).Contains(tag.Id);
            }

            return t;
        }

        public static TagModel FromTag(Tag tag)
        {
            return new TagModel
            {
                Id = tag.Id,
                TagText = tag.Text,
                IsRemovable = true,
                IsEditable = true,
                IsSaved = false
            };
        }

        public static Tag ToTag(TagModel tag)
        {
            return new Tag { Text = tag.TagText, Id = tag.Id };
        }

        #endregion
    }
}
