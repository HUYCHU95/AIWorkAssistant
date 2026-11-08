using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIWork.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class V05InboxMemory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", nullable: false),
                    StoredName = table.Column<string>(type: "TEXT", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", nullable: false),
                    LengthBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    ExtractionStatus = table.Column<string>(type: "TEXT", nullable: false),
                    ExtractedText = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InboxItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    ReviewReason = table.Column<string>(type: "TEXT", nullable: false),
                    CaptureKey = table.Column<string>(type: "TEXT", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InboxItems_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InboxItems_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MemoryFacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubjectType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    SubjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Predicate = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ValueText = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EffectiveAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Verification = table.Column<int>(type: "INTEGER", nullable: false),
                    IsCurrent = table.Column<bool>(type: "INTEGER", nullable: false),
                    SupersededById = table.Column<Guid>(type: "TEXT", nullable: true),
                    ConflictsWithId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ConfidenceNotes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoryFacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemoryFacts_MemoryFacts_ConflictsWithId",
                        column: x => x.ConflictsWithId,
                        principalTable: "MemoryFacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemoryFacts_MemoryFacts_SupersededById",
                        column: x => x.SupersededById,
                        principalTable: "MemoryFacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemoryFacts_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MemoryRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FactId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    BeforeJson = table.Column<string>(type: "TEXT", nullable: false),
                    AfterJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoryRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemoryRevisions_MemoryFacts_FactId",
                        column: x => x.FactId,
                        principalTable: "MemoryFacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_SourceId",
                table: "Documents",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_StoredName",
                table: "Documents",
                column: "StoredName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboxItems_CaptureKey",
                table: "InboxItems",
                column: "CaptureKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboxItems_ProjectId",
                table: "InboxItems",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_InboxItems_SourceId",
                table: "InboxItems",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryFacts_ConflictsWithId",
                table: "MemoryFacts",
                column: "ConflictsWithId");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryFacts_SourceId",
                table: "MemoryFacts",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryFacts_SubjectType_SubjectId_Predicate_IsCurrent",
                table: "MemoryFacts",
                columns: new[] { "SubjectType", "SubjectId", "Predicate", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_MemoryFacts_SupersededById",
                table: "MemoryFacts",
                column: "SupersededById");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryRevisions_FactId",
                table: "MemoryRevisions",
                column: "FactId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "InboxItems");

            migrationBuilder.DropTable(
                name: "MemoryRevisions");

            migrationBuilder.DropTable(
                name: "MemoryFacts");
        }
    }
}
