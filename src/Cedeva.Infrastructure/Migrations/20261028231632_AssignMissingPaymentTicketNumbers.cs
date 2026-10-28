using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cedeva.Infrastructure.Migrations
{
    /// <summary>
    /// Data-only: payments recorded by the online-payment webhook (Mollie/Stripe) were saved without a
    /// ticket number and show as "#0" in the accounts. Each one gets the next numbers of its
    /// activity's shared Payment/Expense sequence, in payment-date order. Down is a no-op: the numbers
    /// may already be printed on receipts.
    /// </summary>
    public partial class AssignMissingPaymentTicketNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                WITH highest AS (
                    SELECT a."Id" AS "ActivityId",
                           GREATEST(
                               COALESCE((SELECT max(p."TicketNumber") FROM "Payments" p
                                         JOIN "Bookings" b ON b."Id" = p."BookingId"
                                         WHERE b."ActivityId" = a."Id"), 0),
                               COALESCE((SELECT max(e."TicketNumber") FROM "Expenses" e
                                         WHERE e."ActivityId" = a."Id"), 0)) AS "Highest"
                    FROM "Activities" a
                ),
                numbered AS (
                    SELECT p."Id",
                           h."Highest" + row_number() OVER (PARTITION BY b."ActivityId" ORDER BY p."PaymentDate", p."Id") AS "Ticket"
                    FROM "Payments" p
                    JOIN "Bookings" b ON b."Id" = p."BookingId"
                    JOIN highest h ON h."ActivityId" = b."ActivityId"
                    WHERE p."TicketNumber" = 0
                )
                UPDATE "Payments" p
                SET "TicketNumber" = numbered."Ticket"
                FROM numbered
                WHERE p."Id" = numbered."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
