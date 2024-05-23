using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IndexerBenchmark.Migrations
{
    /// <inheritdoc />
    public partial class MG2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BlockTable",
                columns: table => new
                {
                    BlockHash = table.Column<string>(type: "text", nullable: false),
                    BlockIndex = table.Column<long>(type: "bigint", nullable: false),
                    BlockTime = table.Column<long>(type: "bigint", nullable: false),
                    PreviousBlockHash = table.Column<string>(type: "text", nullable: false),
                    Bits = table.Column<string>(type: "text", nullable: false),
                    Merkleroot = table.Column<string>(type: "text", nullable: false),
                    Nonce = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockTable", x => x.BlockHash);
                });

            migrationBuilder.CreateTable(
                name: "TransactionTable",
                columns: table => new
                {
                    TXID = table.Column<string>(type: "text", nullable: false),
                    RawTransaction = table.Column<string>(type: "text", nullable: false),
                    BlockHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionTable", x => x.TXID);
                    table.ForeignKey(
                        name: "FK_TransactionTable_BlockTable_BlockHash",
                        column: x => x.BlockHash,
                        principalTable: "BlockTable",
                        principalColumn: "BlockHash",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InputTable",
                columns: table => new
                {
                    TXID = table.Column<string>(type: "text", nullable: false),
                    VOUT = table.Column<long>(type: "bigint", nullable: false),
                    OutpointTXID = table.Column<string>(type: "text", nullable: false),
                    OutpointVOUT = table.Column<long>(type: "bigint", nullable: false),
                    ScriptSig = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InputTable", x => new { x.TXID, x.VOUT });
                    table.ForeignKey(
                        name: "FK_InputTable_TransactionTable_OutpointTXID",
                        column: x => x.OutpointTXID,
                        principalTable: "TransactionTable",
                        principalColumn: "TXID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InputTable_TransactionTable_TXID",
                        column: x => x.TXID,
                        principalTable: "TransactionTable",
                        principalColumn: "TXID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OutputTable",
                columns: table => new
                {
                    TXID = table.Column<string>(type: "text", nullable: false),
                    VOUT = table.Column<long>(type: "bigint", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    ScriptPubKeyHex = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutputTable", x => new { x.TXID, x.VOUT });
                    table.ForeignKey(
                        name: "FK_OutputTable_TransactionTable_TXID",
                        column: x => x.TXID,
                        principalTable: "TransactionTable",
                        principalColumn: "TXID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InputTable_OutpointTXID",
                table: "InputTable",
                column: "OutpointTXID");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionTable_BlockHash",
                table: "TransactionTable",
                column: "BlockHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InputTable");

            migrationBuilder.DropTable(
                name: "OutputTable");

            migrationBuilder.DropTable(
                name: "TransactionTable");

            migrationBuilder.DropTable(
                name: "BlockTable");
        }
    }
}
