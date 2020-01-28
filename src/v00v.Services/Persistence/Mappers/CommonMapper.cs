using System;
using AutoMapper;
using v00v.Services.Persistence.Mappers.Profiles;

namespace v00v.Services.Persistence.Mappers
{
    public class CommonMapper : Mapper, ICommonMapper
    {
        #region Static and Readonly Fields

        private static readonly Lazy<ICommonMapper> s_instance;

        #endregion

        #region Constructors

        static CommonMapper()
        {
            s_instance = new Lazy<ICommonMapper>(CreateMapper);
        }

        private CommonMapper(IConfigurationProvider configurationProvider) : base(configurationProvider)
        {
        }

        #endregion

        #region Static Properties

        public static ICommonMapper Instance => s_instance.Value;

        #endregion

        #region Static Methods

        private static ICommonMapper CreateMapper()
        {
            var configuration = new MapperConfiguration(mc =>
            {
                mc.AddProfile(new ChannelMapProfile());
                mc.AddProfile(new PlaylistMapProfile());
                mc.AddProfile(new ItemMapProfile());
                mc.AddProfile(new TagMapProfile());
            });

            //configuration.AssertConfigurationIsValid();

            return new CommonMapper(configuration);
        }

        #endregion
    }
}
