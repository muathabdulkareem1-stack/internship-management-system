using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUniqueStudentInternshipConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Internships_StudentId",
                table: "Internships");

            migrationBuilder.CreateIndex(
                name: "IX_Internships_StudentId",
                table: "Internships",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Internships_StudentId",
                table: "Internships");

            migrationBuilder.CreateIndex(
                name: "IX_Internships_StudentId",
                table: "Internships",
                column: "StudentId",
                unique: true);
        }
    }
}
