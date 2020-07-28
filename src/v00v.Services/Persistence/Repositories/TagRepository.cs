using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using v00v.Model.Entities;
using v00v.Services.Database;
using v00v.Services.Persistence.Helpers;
using v00v.Services.Persistence.Mappers;

namespace v00v.Services.Persistence.Repositories
{
    public class TagRepository : ITagRepository
    {
        #region Static and Readonly Fields

        private readonly IContextFactory _contextFactory;
        private readonly ICommonMapper _mapper;

        #endregion

        #region Constructors

        public TagRepository(IContextFactory contextFactory, ICommonMapper mapper)
        {
            _contextFactory = contextFactory;
            _mapper = mapper;
        }

        #endregion

        #region Methods

        public async Task<int> Add(string text)
        {
            await using var context = _contextFactory.CreateVideoContext();
            await using var transaction = await TransactionHelper.Get(context);
            try
            {
                var res = context.Tags.AddAsync(new Database.Models.Tag { Text = text });
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
                return (await res).Entity.Id;
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<int> DeleteTag(string text)
        {
            await using var context = _contextFactory.CreateVideoContext();
            await using var transaction = await TransactionHelper.Get(context);
            try
            {
                var tag = await context.Tags.AsNoTracking()
                    .FirstOrDefaultAsync(x => string.Equals(x.Text, text, StringComparison.CurrentCultureIgnoreCase));

                if (tag != null)
                {
                    context.Tags.Remove(tag);
                    var res = await context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return res;
                }

                await transaction.RollbackAsync();
                return -1;
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                await transaction.RollbackAsync();
                throw;
            }
        }

        public List<int> GetOrder()
        {
            using var context = _contextFactory.CreateVideoContext();
            try
            {
                var res = context.ChannelTags.AsNoTracking().ToHashSet().GroupBy(x => x.TagId)
                    .Select(x => new Tuple<int, int>(x.Key, x.Count())).OrderByDescending(x => x.Item2).Select(x => x.Item1).ToList();

                res.AddRange(context.Tags.AsNoTracking().Select(x => x.Id).Where(y => !res.Contains(y)));

                return res;
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                throw;
            }
        }

        public IEnumerable<Tag> GetTags()
        {
            using var context = _contextFactory.CreateVideoContext();
            foreach (var tag in context.Tags.AsNoTracking().OrderBy(x => x.Text))
            {
                yield return _mapper.Map<Tag>(tag);
            }
        }

        public IEnumerable<KeyValuePair<int, string>> GetTagsByIds(IEnumerable<int> tagIds)
        {
            using var context = _contextFactory.CreateVideoContext();
            foreach (var tag in context.Tags.AsNoTracking().OrderBy(x => x.Text).Where(x => tagIds.Contains(x.Id)))
            {
                yield return new KeyValuePair<int, string>(tag.Id, tag.Text);
            }
        }

        #endregion
    }
}
