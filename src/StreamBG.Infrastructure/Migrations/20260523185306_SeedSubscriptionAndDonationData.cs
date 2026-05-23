using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace StreamBG.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedSubscriptionAndDonationData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "AvatarUrl", "Bio", "CreatedAt", "Email", "IsAdmin", "IsBanned", "IsStreamer", "PasswordHash", "Username" },
                values: new object[,]
                {
                    { "seed-donor-1", null, "💰 Тестов донор акаунт за демонстрация", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "donor@example.com", false, false, false, "SEED_ACCOUNT_NOT_FOR_LOGIN", "testdonor" },
                    { "seed-streamer-1", null, "📺 Тестов стриймър акаунт за демонстрация", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "streamer@example.com", false, false, true, "SEED_ACCOUNT_NOT_FOR_LOGIN", "teststreamer" }
                });

            migrationBuilder.InsertData(
                table: "Donations",
                columns: new[] { "Id", "Amount", "CompletedAt", "CreatedAt", "CurrencyCode", "DonorDisplayName", "DonorUserId", "EmojiAnimation", "ExternalPaymentId", "IsAnonymous", "IsShownInChat", "Message", "PaymentProvider", "RecipientUserId", "Status", "StreamId" },
                values: new object[,]
                {
                    { 1, 10.00m, new DateTime(2026, 6, 1, 18, 30, 5, 0, DateTimeKind.Utc), new DateTime(2026, 6, 1, 18, 30, 0, 0, DateTimeKind.Utc), "BGN", "testdonor", "seed-donor-1", "🎉", null, false, true, "Stratoten strim! Produlzhavai taka!", null, "seed-streamer-1", 1, null },
                    { 2, 5.50m, new DateTime(2026, 6, 2, 20, 15, 3, 0, DateTimeKind.Utc), new DateTime(2026, 6, 2, 20, 15, 0, 0, DateTimeKind.Utc), "BGN", "Anonimen fen", null, null, null, true, true, "❤️", null, "seed-streamer-1", 1, null },
                    { 3, 25.00m, new DateTime(2026, 6, 5, 19, 0, 4, 0, DateTimeKind.Utc), new DateTime(2026, 6, 5, 19, 0, 0, 0, DateTimeKind.Utc), "BGN", "testdonor", "seed-donor-1", "🏆", null, false, true, "Za strima na godinata!", null, "seed-streamer-1", 1, null }
                });

            migrationBuilder.InsertData(
                table: "SubscriptionPlans",
                columns: new[] { "Id", "BadgeColor", "BadgeEmoji", "CreatedAt", "CreatorUserId", "CurrencyCode", "Description", "IsActive", "Name", "Perks", "PriceMonthly" },
                values: new object[,]
                {
                    { 1, "#FFD700", "⭐", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed-streamer-1", "BGN", "Базов абонамент на месечна база — емотикони и реклами намалени", true, "Месечен", "[\"\\u0427\\u0430\\u0442 \\u0435\\u043C\\u043E\\u0442\\u0438\\u043A\\u043E\\u043D\\u0438\",\"\\u0411\\u0435\\u0437 \\u0440\\u0435\\u043A\\u043B\\u0430\\u043C\\u0438\"]", 4.99m },
                    { 2, "#9B59B6", "👑", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed-streamer-1", "BGN", "Годишен абонамент с 2 безплатни месеца — VIP бейдж, ексклузивен чат, приоритетен стрийм", true, "Годишен", "[\"\\u0427\\u0430\\u0442 \\u0435\\u043C\\u043E\\u0442\\u0438\\u043A\\u043E\\u043D\\u0438\",\"\\u0411\\u0435\\u0437 \\u0440\\u0435\\u043A\\u043B\\u0430\\u043C\\u0438\",\"\\u0415\\u043A\\u0441\\u043A\\u043B\\u0443\\u0437\\u0438\\u0432\\u0435\\u043D \\u0447\\u0430\\u0442\",\"VIP \\u0431\\u0435\\u0439\\u0434\\u0436\",\"\\u041F\\u0440\\u0438\\u043E\\u0440\\u0438\\u0442\\u0435\\u0442\\u0435\\u043D \\u0441\\u0442\\u0440\\u0438\\u0439\\u043C\"]", 49.99m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Donations",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Donations",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Donations",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: "seed-donor-1");

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: "seed-streamer-1");
        }
    }
}
