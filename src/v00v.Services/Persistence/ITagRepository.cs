using System.Collections.Generic;
using System.Threading.Tasks;
using v00v.Model.Entities;

namespace v00v.Services.Persistence
{
    public interface ITagRepository
    {
        #region Methods

        Task<int> Add(string text);

        Task<int> DeleteTag(string text);

        List<int> GetOrder();

        IEnumerable<Tag> GetTags();

        #endregion
    }
}
