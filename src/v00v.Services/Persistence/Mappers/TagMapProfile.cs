using AutoMapper;
using v00v.Services.Database.Models;

namespace v00v.Services.Persistence.Mappers
{
    public class TagMapProfile : Profile
    {
        #region Constructors

        public TagMapProfile()
        {
            CreateMap<Tag, Model.Entities.Tag>().ForMember(dto => dto.IsSaved, opt => opt.MapFrom(src => true));
            CreateMap<Model.Entities.Tag, Tag>();
        }

        #endregion
    }
}
