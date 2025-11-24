using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace JoyDrawBot.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    telegram_id = table.Column<long>(type: "bigint", nullable: false),
                    username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    first_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    last_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.telegram_id);
                });

            migrationBuilder.CreateTable(
                name: "contest_entries",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    source_chat_id = table.Column<long>(type: "bigint", nullable: true),
                    source_chat_title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    source_chat_username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    source_chat_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    source_message_id = table.Column<int>(type: "integer", nullable: true),
                    original_text = table.Column<string>(type: "text", nullable: false),
                    results_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reminder_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contest_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_contest_entries_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "telegram_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contest_channels",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    contest_entry_id = table.Column<int>(type: "integer", nullable: false),
                    label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    url = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contest_channels", x => x.id);
                    table.ForeignKey(
                        name: "fk_contest_channels_contest_entries_contest_entry_id",
                        column: x => x.contest_entry_id,
                        principalTable: "contest_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_contest_channels_contest_entry_id",
                table: "contest_channels",
                column: "contest_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_contest_entries_results_at_reminder_sent_at",
                table: "contest_entries",
                columns: new[] { "results_at", "reminder_sent_at" });

            migrationBuilder.CreateIndex(
                name: "ix_contest_entries_user_id",
                table: "contest_entries",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contest_channels");

            migrationBuilder.DropTable(
                name: "contest_entries");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
