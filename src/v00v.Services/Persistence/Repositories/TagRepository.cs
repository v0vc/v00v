using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using v00v.Model.Entities;
using v00v.Services.Database;
using v00v.Services.Persistence.Helpers;

namespace v00v.Services.Persistence.Repositories
{
    public class TagRepository : ITagRepository
    {
        #region Static and Readonly Fields

        private readonly IContextFactory _contextFactory;

        private readonly IMapper _mapper;

        #endregion

        #region Constructors

        public TagRepository(IContextFactory contextFactory, IMapper mapper)
        {
            _contextFactory = contextFactory;
            _mapper = mapper;
        }

        #endregion

        #region Methods

        public Task<int> Add(string text)
        {
            using (var context = _contextFactory.CreateVideoContext())
            {
                using (var transaction = TransactionHelper.Get(context))
                {
                    try
                    {
                        var tag = new Database.Models.Tag { Text = text };
                        var res = context.Tags.AddAsync(tag);
                        context.SaveChanges();
                        transaction.Commit();
                        return Task.FromResult(res.Result.Entity.Id);
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception);
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public async Task<int> DeleteTag(string text)
        {
            using (var context = _contextFactory.CreateVideoContext())
            {
                using (var transaction = TransactionHelper.Get(context))
                {
                    try
                    {
                        var tag = await context.Tags.AsNoTracking()
                            .FirstOrDefaultAsync(x => string.Equals(x.Text, text, StringComparison.CurrentCultureIgnoreCase));
                        if (tag != null)
                        {
                            context.Tags.Remove(tag);
                            var res = await context.SaveChangesAsync();
                            transaction.Commit();
                            return res;
                        }

                        transaction.Rollback();
                        return -1;
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception);
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public List<int> GetOrder()
        {
            using (var context = _contextFactory.CreateVideoContext())
            {
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
        }

        public IEnumerable<Tag> GetTags()
        {
            using (var context = _contextFactory.CreateVideoContext())
            {
                foreach (var tag in context.Tags.AsNoTracking().OrderBy(x => x.Text))
                {
                    yield return _mapper.Map<Tag>(tag);
                }
            }
        }

        #endregion
    }
}
