using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevPilotAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddParserEngineMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParsedFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelativePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SizeInBytes = table.Column<long>(type: "bigint", nullable: false),
                    ParserVersion = table.Column<int>(type: "int", nullable: false),
                    Usings = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParsedFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParsedFiles_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectParseJobs",
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
                    table.PrimaryKey("PK_ProjectParseJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectParseJobs_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParsedClasses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParsedFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Namespace = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SymbolType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BaseTypes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Attributes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartLine = table.Column<int>(type: "int", nullable: false),
                    EndLine = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParsedClasses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParsedClasses_ParsedFiles_ParsedFileId",
                        column: x => x.ParsedFileId,
                        principalTable: "ParsedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParsedFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParsedClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AccessModifier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Attributes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartLine = table.Column<int>(type: "int", nullable: false),
                    EndLine = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParsedFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParsedFields_ParsedClasses_ParsedClassId",
                        column: x => x.ParsedClassId,
                        principalTable: "ParsedClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParsedMethods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParsedClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReturnType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AccessModifier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Parameters = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Attributes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartLine = table.Column<int>(type: "int", nullable: false),
                    EndLine = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParsedMethods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParsedMethods_ParsedClasses_ParsedClassId",
                        column: x => x.ParsedClassId,
                        principalTable: "ParsedClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParsedProperties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParsedClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AccessModifier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Attributes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartLine = table.Column<int>(type: "int", nullable: false),
                    EndLine = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParsedProperties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParsedProperties_ParsedClasses_ParsedClassId",
                        column: x => x.ParsedClassId,
                        principalTable: "ParsedClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParsedClasses_FullName",
                table: "ParsedClasses",
                column: "FullName");

            migrationBuilder.CreateIndex(
                name: "IX_ParsedClasses_ParsedFileId",
                table: "ParsedClasses",
                column: "ParsedFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ParsedFields_ParsedClassId",
                table: "ParsedFields",
                column: "ParsedClassId");

            migrationBuilder.CreateIndex(
                name: "IX_ParsedFiles_Language",
                table: "ParsedFiles",
                column: "Language");

            migrationBuilder.CreateIndex(
                name: "IX_ParsedFiles_ProjectId",
                table: "ParsedFiles",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ParsedMethods_ParsedClassId",
                table: "ParsedMethods",
                column: "ParsedClassId");

            migrationBuilder.CreateIndex(
                name: "IX_ParsedProperties_ParsedClassId",
                table: "ParsedProperties",
                column: "ParsedClassId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectParseJobs_ProjectId",
                table: "ProjectParseJobs",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectParseJobs_Status",
                table: "ProjectParseJobs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParsedFields");

            migrationBuilder.DropTable(
                name: "ParsedMethods");

            migrationBuilder.DropTable(
                name: "ParsedProperties");

            migrationBuilder.DropTable(
                name: "ProjectParseJobs");

            migrationBuilder.DropTable(
                name: "ParsedClasses");

            migrationBuilder.DropTable(
                name: "ParsedFiles");
        }
    }
}
