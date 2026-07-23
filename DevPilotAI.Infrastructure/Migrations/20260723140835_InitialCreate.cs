using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevPilotAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SourceLocation = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ProjectType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectIndexes",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IndexVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IndexStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LastIndexedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EmbeddingModel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ChunkCount = table.Column<int>(type: "int", nullable: false),
                    EmbeddingCount = table.Column<int>(type: "int", nullable: false),
                    ParserVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectIndexes", x => x.ProjectId);
                    table.ForeignKey(
                        name: "FK_ProjectIndexes_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectSettings",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExcludedFolders = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExcludedExtensions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaxFileSizeInBytes = table.Column<long>(type: "bigint", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectSettings", x => x.ProjectId);
                    table.ForeignKey(
                        name: "FK_ProjectSettings_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectStatistics",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileCount = table.Column<int>(type: "int", nullable: false),
                    TotalLinesOfCode = table.Column<long>(type: "bigint", nullable: false),
                    TotalBytes = table.Column<long>(type: "bigint", nullable: false),
                    IndexedFileCount = table.Column<int>(type: "int", nullable: false),
                    ControllerCount = table.Column<int>(type: "int", nullable: false),
                    ServiceCount = table.Column<int>(type: "int", nullable: false),
                    RepositoryCount = table.Column<int>(type: "int", nullable: false),
                    ApiCount = table.Column<int>(type: "int", nullable: false),
                    ClassCount = table.Column<int>(type: "int", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectStatistics", x => x.ProjectId);
                    table.ForeignKey(
                        name: "FK_ProjectStatistics_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectIndexes_CreatedAt",
                table: "ProjectIndexes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectIndexes_IndexStatus",
                table: "ProjectIndexes",
                column: "IndexStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectIndexes_LastModifiedAt",
                table: "ProjectIndexes",
                column: "LastModifiedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_CreatedAt",
                table: "Projects",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_LastModifiedAt",
                table: "Projects",
                column: "LastModifiedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Name",
                table: "Projects",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjectType",
                table: "Projects",
                column: "ProjectType");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_WorkspaceId",
                table: "Projects",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSettings_CreatedAt",
                table: "ProjectSettings",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSettings_LastModifiedAt",
                table: "ProjectSettings",
                column: "LastModifiedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_CreatedAt",
                table: "Workspaces",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_LastModifiedAt",
                table: "Workspaces",
                column: "LastModifiedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_Name",
                table: "Workspaces",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectIndexes");

            migrationBuilder.DropTable(
                name: "ProjectSettings");

            migrationBuilder.DropTable(
                name: "ProjectStatistics");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "Workspaces");
        }
    }
}
