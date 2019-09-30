using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using v00v.Services.Database;
using v00v.Services.Database.Models;
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

        public void Add(Tag[] tags)
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                using (IDbContextTransaction transaction = TransactionHelper.Get(context))
                {
                    try
                    {
                        context.Tags.AddRange(tags);
                        context.SaveChanges();
                        transaction.Commit();
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

        public Task<int> Add(string text)
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                using (IDbContextTransaction transaction = TransactionHelper.Get(context))
                {
                    try
                    {
                        var tag = new Tag { Text = text };
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

        public void DeleteTag(string text)
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                using (IDbContextTransaction transaction = TransactionHelper.Get(context))
                {
                    try
                    {
                        var tag = context.Tags.AsNoTracking()
                            .FirstOrDefaultAsync(x => string.Equals(x.Text, text, StringComparison.CurrentCultureIgnoreCase)).Result;
                        if (tag != null)
                        {
                            context.Tags.Remove(tag);
                            context.SaveChanges();
                            transaction.Commit();
                        }
                        else
                        {
                            transaction.Rollback();
                        }
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

        public async Task<IEnumerable<Model.Entities.Tag>> GetTags(bool useOrder)
        {
            using (VideoContext context = _contextFactory.CreateVideoContext())
            {
                try
                {
                    var res = await context.Tags.AsNoTracking().OrderBy(x => x.Text).ToListAsync();

                    if (!useOrder)
                    {
                        return res.Select(x => _mapper.Map<Model.Entities.Tag>(x));
                    }

                    var dres = (await context.ChannelTags.AsNoTracking().ToListAsync()).GroupBy(x => x.TagId)
                        .Select(x => new Tuple<int, int>(x.Key, x.Count())).OrderByDescending(x => x.Item2)
                        .Select(x => new Tag { Id = x.Item1, Text = res.First(y => y.Id == x.Item1).Text }).ToList();

                    dres.AddRange(res.Where(x => !dres.Select(y => y.Id).Contains(x.Id)));

                    return dres.Select(x => _mapper.Map<Model.Entities.Tag>(x));
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    throw;
                }
            }
        }

        #endregion
    }
}
