using System.Collections.Generic;
using System.Threading.Tasks;
using v00v.Services.Database.Models;

namespace v00v.Services.Persistence
{
    public interface ITagRepository
    {
        #region Methods

        void Add(Tag[] tags);

        Task<int> Add(string text);

        void DeleteTag(string text);

        Task<IEnumerable<Model.Entities.Tag>> GetTags(bool useOrder);

        #endregion
    }
}
