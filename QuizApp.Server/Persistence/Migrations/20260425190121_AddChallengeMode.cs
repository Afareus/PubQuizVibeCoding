using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuizApp.Server.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChallengeMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Challenges",
                columns: table => new
                {
                    ChallengeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatorName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatorTokenHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Challenges", x => x.ChallengeId);
                });

            migrationBuilder.CreateTable(
                name: "ChallengeQuestions",
                columns: table => new
                {
                    ChallengeQuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallengeId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: false),
                    CreatorSelectedOptionKey = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChallengeQuestions", x => x.ChallengeQuestionId);
                    table.ForeignKey(
                        name: "FK_ChallengeQuestions_Challenges_ChallengeId",
                        column: x => x.ChallengeId,
                        principalTable: "Challenges",
                        principalColumn: "ChallengeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChallengeSubmissions",
                columns: table => new
                {
                    ChallengeSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallengeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    MaxScore = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChallengeSubmissions", x => x.ChallengeSubmissionId);
                    table.ForeignKey(
                        name: "FK_ChallengeSubmissions_Challenges_ChallengeId",
                        column: x => x.ChallengeId,
                        principalTable: "Challenges",
                        principalColumn: "ChallengeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChallengeAnswerOptions",
                columns: table => new
                {
                    ChallengeAnswerOptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallengeQuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OptionKey = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChallengeAnswerOptions", x => x.ChallengeAnswerOptionId);
                    table.ForeignKey(
                        name: "FK_ChallengeAnswerOptions_ChallengeQuestions_ChallengeQuestion~",
                        column: x => x.ChallengeQuestionId,
                        principalTable: "ChallengeQuestions",
                        principalColumn: "ChallengeQuestionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChallengeSubmissionAnswers",
                columns: table => new
                {
                    ChallengeSubmissionAnswerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallengeSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallengeQuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedOptionKey = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChallengeSubmissionAnswers", x => x.ChallengeSubmissionAnswerId);
                    table.ForeignKey(
                        name: "FK_ChallengeSubmissionAnswers_ChallengeQuestions_ChallengeQues~",
                        column: x => x.ChallengeQuestionId,
                        principalTable: "ChallengeQuestions",
                        principalColumn: "ChallengeQuestionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChallengeSubmissionAnswers_ChallengeSubmissions_ChallengeSu~",
                        column: x => x.ChallengeSubmissionId,
                        principalTable: "ChallengeSubmissions",
                        principalColumn: "ChallengeSubmissionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeAnswerOptions_ChallengeQuestionId",
                table: "ChallengeAnswerOptions",
                column: "ChallengeQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeQuestions_ChallengeId_OrderIndex",
                table: "ChallengeQuestions",
                columns: new[] { "ChallengeId", "OrderIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Challenges_PublicCode",
                table: "Challenges",
                column: "PublicCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeSubmissionAnswers_ChallengeQuestionId",
                table: "ChallengeSubmissionAnswers",
                column: "ChallengeQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeSubmissionAnswers_ChallengeSubmissionId",
                table: "ChallengeSubmissionAnswers",
                column: "ChallengeSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeSubmissions_ChallengeId",
                table: "ChallengeSubmissions",
                column: "ChallengeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChallengeAnswerOptions");

            migrationBuilder.DropTable(
                name: "ChallengeSubmissionAnswers");

            migrationBuilder.DropTable(
                name: "ChallengeQuestions");

            migrationBuilder.DropTable(
                name: "ChallengeSubmissions");

            migrationBuilder.DropTable(
                name: "Challenges");
        }
    }
}
