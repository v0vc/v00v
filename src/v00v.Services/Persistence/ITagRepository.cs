using System.Collections.Generic;
using System.Threading.Tasks;

namespace v00v.Services.Persistence
{
    public interface ITagRepository
    {
        #region Methods

        Task<int> Add(string text);

        Task<int> DeleteTag(string text);

        List<int> GetOrder();

        List<Model.Entities.Tag> GetTags();

        #endregion
    }
}
