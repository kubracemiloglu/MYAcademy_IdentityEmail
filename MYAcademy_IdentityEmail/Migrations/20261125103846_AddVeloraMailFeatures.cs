using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MYAcademy_IdentityEmail.Migrations
{
    /// <inheritdoc />
    public partial class AddVeloraMailFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO [Categories] ([CategoryName])
                SELECT v.[Name] FROM (VALUES (N'İş'), (N'Kişisel'), (N'Önemli'), (N'Eğitim'), (N'Finans'), (N'Diğer')) v([Name])
                WHERE NOT EXISTS (SELECT 1 FROM [Categories] c WHERE c.[CategoryName] = v.[Name]);
                """);
            migrationBuilder.CreateTable(
                name: "DraftMessages",
                columns: table => new
                {
                    DraftMessageId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ReceiverEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MessageDetail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DraftMessages", x => x.DraftMessageId);
                    table.ForeignKey(
                        name: "FK_DraftMessages_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DraftMessages_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "CategoryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmailVerificationStates",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FailedAttempts = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailVerificationStates", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_EmailVerificationStates_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MessageUserStates",
                columns: table => new
                {
                    MessageUserStateId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MessageId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsStarred = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsPermanentlyDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageUserStates", x => x.MessageUserStateId);
                    table.ForeignKey(
                        name: "FK_MessageUserStates_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessageUserStates_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "CategoryId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessageUserStates_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "MessageId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DraftMessages_CategoryId",
                table: "DraftMessages",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_DraftMessages_UserId",
                table: "DraftMessages",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageUserStates_CategoryId",
                table: "MessageUserStates",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageUserStates_MessageId",
                table: "MessageUserStates",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageUserStates_UserId_MessageId",
                table: "MessageUserStates",
                columns: new[] { "UserId", "MessageId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DraftMessages");

            migrationBuilder.DropTable(
                name: "EmailVerificationStates");

            migrationBuilder.DropTable(
                name: "MessageUserStates");
        }
    }
}
