using AutoMapper;
using DevPilotAI.Application.DTOs.Identity;
using DevPilotAI.Application.DTOs.Project;
using DevPilotAI.Application.DTOs.Workspace;
using DevPilotAI.Application.DTOs.Chat;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Entities.Identity;

namespace DevPilotAI.Application.Common.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Entity to DTO (Read-only mappings - default MemberList.Destination)
        CreateMap<Workspace, WorkspaceDto>();
        CreateMap<Workspace, WorkspaceBriefDto>();
        CreateMap<Project, ProjectDto>()
            .ForMember(dest => dest.ProjectType, opt => opt.MapFrom(src => src.ProjectType.ToString()));
        CreateMap<Project, ProjectBriefDto>()
            .ForMember(dest => dest.ProjectType, opt => opt.MapFrom(src => src.ProjectType.ToString()))
            .ForMember(dest => dest.IndexStatus, opt => opt.MapFrom(src => src.Index.IndexStatus.ToString()));
        CreateMap<ProjectSettings, ProjectSettingsDto>();
        CreateMap<ProjectStatistics, ProjectStatisticsDto>();
        CreateMap<ProjectIndex, ProjectIndexDto>()
            .ForMember(dest => dest.IndexStatus, opt => opt.MapFrom(src => src.IndexStatus.ToString()));
        CreateMap<ApplicationUser, UserDto>();
        CreateMap<ProjectImportJob, ProjectImportJobDto>()
            .ForMember(dest => dest.ImportType, opt => opt.MapFrom(src => src.ImportType.ToString()))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));

        CreateMap<ProjectParseJob, ProjectParseJobDto>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));
        CreateMap<ParsedFile, ParsedFileDto>();
        CreateMap<ParsedClass, ParsedClassDto>()
            .ForMember(dest => dest.SymbolType, opt => opt.MapFrom(src => src.SymbolType.ToString()));
        CreateMap<ParsedMethod, ParsedMethodDto>();
        CreateMap<ParsedProperty, ParsedPropertyDto>();
        CreateMap<ParsedField, ParsedFieldDto>();

        CreateMap<CodeChunk, CodeChunkDto>()
            .ForMember(dest => dest.StartLine, opt => opt.Ignore())
            .ForMember(dest => dest.EndLine, opt => opt.Ignore())
            .ForMember(dest => dest.RetrievalExplanation, opt => opt.Ignore());
        CreateMap<ProjectChunkingJob, ProjectChunkingJobDto>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));

        // Chat mappings
        CreateMap<ChatSession, ChatSessionDto>();
        CreateMap<ChatMessage, ChatMessageDto>()
            .ForMember(dest => dest.ConfidenceScore, opt => opt.Ignore())
            .ForMember(dest => dest.RetrievedSymbols, opt => opt.Ignore())
            .ForMember(dest => dest.SourceFiles, opt => opt.Ignore())
            .ForMember(dest => dest.RetrievedChunksCount, opt => opt.Ignore())
            .ForMember(dest => dest.SimilarityThreshold, opt => opt.Ignore())
            .ForMember(dest => dest.ChunkCount, opt => opt.Ignore())
            .ForMember(dest => dest.ModelUsed, opt => opt.Ignore())
            .ForMember(dest => dest.Provider, opt => opt.Ignore())
            .ForMember(dest => dest.ResponseTimeMs, opt => opt.Ignore())
            .ForMember(dest => dest.PromptMode, opt => opt.Ignore())
            .AfterMap((src, dest) =>
            {
                if (string.IsNullOrEmpty(src.Metadata)) return;
                try
                {
                    var trimmed = src.Metadata.Trim();
                    if (trimmed.StartsWith("{"))
                    {
                        var metaObj = System.Text.Json.JsonSerializer.Deserialize<ChatMessageMetadataDto>(src.Metadata, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (metaObj != null)
                        {
                            dest.Metadata = System.Text.Json.JsonSerializer.Serialize(metaObj.Sources);
                            dest.ConfidenceScore = metaObj.ConfidenceScore;
                            dest.RetrievedSymbols = metaObj.RetrievedSymbols;
                            dest.SourceFiles = metaObj.SourceFiles;
                            dest.RetrievedChunksCount = metaObj.RetrievedChunksCount;
                            dest.SimilarityThreshold = metaObj.SimilarityThreshold;
                            dest.ChunkCount = metaObj.ChunkCount;
                            dest.ModelUsed = metaObj.ModelUsed;
                            dest.Provider = metaObj.Provider;
                            dest.ResponseTimeMs = metaObj.ResponseTimeMs;
                            dest.PromptMode = metaObj.PromptMode;
                        }
                    }
                }
                catch
                {
                    // Fallback to default direct mapping of Metadata (since destination inherits it from src.Metadata)
                }
            });

        // DTO to Entity (Write/Create/Update mappings - validated using MemberList.Source)
        CreateMap<CreateWorkspaceDto, Workspace>(MemberList.Source);
        CreateMap<UpdateWorkspaceDto, Workspace>(MemberList.Source);
        CreateMap<CreateProjectDto, Project>(MemberList.Source);
        CreateMap<UpdateProjectDto, Project>(MemberList.Source);
        CreateMap<UpdateProjectSettingsDto, ProjectSettings>(MemberList.Source);
    }
}
