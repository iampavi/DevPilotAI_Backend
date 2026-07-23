using AutoMapper;
using DevPilotAI.Application.DTOs.Project;
using DevPilotAI.Application.DTOs.Workspace;
using DevPilotAI.Domain.Entities;

namespace DevPilotAI.Application.Common.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Entity to DTO (Read-only mappings - default MemberList.Destination)
        CreateMap<Workspace, WorkspaceDto>();
        CreateMap<Project, ProjectDto>()
            .ForMember(dest => dest.ProjectType, opt => opt.MapFrom(src => src.ProjectType.ToString()));
        CreateMap<ProjectSettings, ProjectSettingsDto>();
        CreateMap<ProjectStatistics, ProjectStatisticsDto>();
        CreateMap<ProjectIndex, ProjectIndexDto>()
            .ForMember(dest => dest.IndexStatus, opt => opt.MapFrom(src => src.IndexStatus.ToString()));

        // DTO to Entity (Write/Create/Update mappings - validated using MemberList.Source)
        CreateMap<CreateWorkspaceDto, Workspace>(MemberList.Source);
        CreateMap<UpdateWorkspaceDto, Workspace>(MemberList.Source);
        CreateMap<CreateProjectDto, Project>(MemberList.Source);
        CreateMap<UpdateProjectDto, Project>(MemberList.Source);
        CreateMap<UpdateProjectSettingsDto, ProjectSettings>(MemberList.Source);
    }
}
