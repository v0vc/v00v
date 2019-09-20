﻿using AutoMapper;
using v00v.Services.Database.Models;

namespace v00v.Services.Persistence.Mappers
{
    public class ItemMapProfile : Profile
    {
        #region Constructors

        public ItemMapProfile()
        {
            CreateMap<Item, Model.Entities.Item>().ForMember(dest => dest.Downloaded, opt => opt.MapFrom(src => src.FileName != null))
                .ForMember(dto => dto.Description, o => o.Ignore())
                .ForMember(dto => dto.ChannelTitle, opt => opt.MapFrom(src => src.Channel.Title));

            CreateMap<Model.Entities.Item, Item>();
        }

        #endregion
    }
}
