using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InspectionPipeline.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModelVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ModelName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LoadedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InspectionRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ImagePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    FinalResult = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    StageUsed = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AnomalyScore = table.Column<double>(type: "REAL", precision: 18, scale: 6, nullable: false),
                    InferenceTimeMs = table.Column<double>(type: "REAL", precision: 18, scale: 3, nullable: false),
                    ModelVersionId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionRecords_ModelVersions_ModelVersionId",
                        column: x => x.ModelVersionId,
                        principalTable: "ModelVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DefectDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InspectionRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClassName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Confidence = table.Column<double>(type: "REAL", precision: 18, scale: 6, nullable: false),
                    BboxX = table.Column<double>(type: "REAL", precision: 18, scale: 3, nullable: false),
                    BboxY = table.Column<double>(type: "REAL", precision: 18, scale: 3, nullable: false),
                    BboxW = table.Column<double>(type: "REAL", precision: 18, scale: 3, nullable: false),
                    BboxH = table.Column<double>(type: "REAL", precision: 18, scale: 3, nullable: false),
                    MaskArea = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DefectDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DefectDetails_InspectionRecords_InspectionRecordId",
                        column: x => x.InspectionRecordId,
                        principalTable: "InspectionRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DefectDetails_ClassName",
                table: "DefectDetails",
                column: "ClassName");

            migrationBuilder.CreateIndex(
                name: "IX_DefectDetails_InspectionRecordId",
                table: "DefectDetails",
                column: "InspectionRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRecords_FinalResult",
                table: "InspectionRecords",
                column: "FinalResult");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRecords_ModelVersionId",
                table: "InspectionRecords",
                column: "ModelVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRecords_Timestamp",
                table: "InspectionRecords",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionRecords_Timestamp_FinalResult",
                table: "InspectionRecords",
                columns: new[] { "Timestamp", "FinalResult" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelVersions_ModelName",
                table: "ModelVersions",
                column: "ModelName");

            migrationBuilder.CreateIndex(
                name: "IX_ModelVersions_ModelName_Version_Unique",
                table: "ModelVersions",
                columns: new[] { "ModelName", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DefectDetails");

            migrationBuilder.DropTable(
                name: "InspectionRecords");

            migrationBuilder.DropTable(
                name: "ModelVersions");
        }
    }
}
