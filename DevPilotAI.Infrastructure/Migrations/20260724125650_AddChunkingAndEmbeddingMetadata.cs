using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevPilotAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChunkingAndEmbeddingMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CodeChunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParsedFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParsedClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParsedMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChunkType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TokenCount = table.Column<int>(type: "int", nullable: false),
                    Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EmbeddingModel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmbeddingVersion = table.Column<int>(type: "int", nullable: false),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CodeChunks_ParsedClasses_ParsedClassId",
                        column: x => x.ParsedClassId,
                        principalTable: "ParsedClasses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CodeChunks_ParsedFiles_ParsedFileId",
                        column: x => x.ParsedFileId,
                        principalTable: "ParsedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CodeChunks_ParsedMethods_ParsedMethodId",
                        column: x => x.ParsedMethodId,
                        principalTable: "ParsedMethods",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CodeChunks_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProjectChunkingJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Progress = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectChunkingJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectChunkingJobs_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CodeChunks_Hash",
                table: "CodeChunks",
                column: "Hash");

            migrationBuilder.CreateIndex(
                name: "IX_CodeChunks_ParsedClassId",
                table: "CodeChunks",
                column: "ParsedClassId");

            migrationBuilder.CreateIndex(
                name: "IX_CodeChunks_ParsedFileId",
                table: "CodeChunks",
                column: "ParsedFileId");

            migrationBuilder.CreateIndex(
                name: "IX_CodeChunks_ParsedMethodId",
                table: "CodeChunks",
                column: "ParsedMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_CodeChunks_ProjectId",
                table: "CodeChunks",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectChunkingJobs_ProjectId",
                table: "ProjectChunkingJobs",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectChunkingJobs_Status",
                table: "ProjectChunkingJobs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CodeChunks");

            migrationBuilder.DropTable(
                name: "ProjectChunkingJobs");
        }
    }
}
