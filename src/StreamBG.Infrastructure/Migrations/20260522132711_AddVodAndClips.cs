using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreamBG.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVodAndClips : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatorUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StreamerUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SourceStreamId = table.Column<int>(type: "int", nullable: true),
                    SourceVodId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    OffsetSeconds = table.Column<double>(type: "float", nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ThumbnailPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ProcessingError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ViewCount = table.Column<int>(type: "int", nullable: false),
                    IsPublic = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clips_Users_CreatorUserId",
                        column: x => x.CreatorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Clips_Users_StreamerUserId",
                        column: x => x.StreamerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clips_CreatedAt",
                table: "Clips",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Clips_CreatorUserId",
                table: "Clips",
                column: "CreatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Clips_StreamerUserId",
                table: "Clips",
                column: "StreamerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Clips");
        }
    }
}
