using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "carga_archivo",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre_archivo = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ruta_archivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tamano_bytes = table.Column<long>(type: "bigint", nullable: false),
                    usuario = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    fecha_registro = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha_fin = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    total_filas = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    filas_insertadas = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    filas_rechazadas = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    mensaje_error = table.Column<string>(type: "text", nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_carga_archivo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuario",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    rol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "carga_periodo",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    carga_archivo_id = table.Column<int>(type: "integer", nullable: false),
                    periodo = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    filas_insertadas = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_carga_periodo", x => x.id);
                    table.ForeignKey(
                        name: "fk_carga_periodo_carga_archivo_carga_archivo_id",
                        column: x => x.carga_archivo_id,
                        principalTable: "carga_archivo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "data_procesada",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    periodo = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    codigo_producto = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nombre_producto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    precio = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    carga_archivo_id = table.Column<int>(type: "integer", nullable: false),
                    fecha_registro = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_procesada", x => x.id);
                    table.ForeignKey(
                        name: "fk_data_procesada_carga_archivo_carga_archivo_id",
                        column: x => x.carga_archivo_id,
                        principalTable: "carga_archivo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "detalle_carga_error",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    carga_archivo_id = table.Column<int>(type: "integer", nullable: false),
                    numero_fila = table.Column<int>(type: "integer", nullable: false),
                    periodo = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    codigo_producto = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    columna = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    motivo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    valor_crudo = table.Column<string>(type: "text", nullable: true),
                    fecha_registro = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_detalle_carga_error", x => x.id);
                    table.ForeignKey(
                        name: "fk_detalle_carga_error_carga_archivo_carga_archivo_id",
                        column: x => x.carga_archivo_id,
                        principalTable: "carga_archivo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_token",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    usuario_id = table.Column<int>(type: "integer", nullable: false),
                    token = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    expira_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revocado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reemplazado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_token", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_token_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_carga_archivo_estado",
                table: "carga_archivo",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "ix_carga_periodo_carga_archivo_id",
                table: "carga_periodo",
                column: "carga_archivo_id");

            migrationBuilder.CreateIndex(
                name: "ux_carga_periodo_activo",
                table: "carga_periodo",
                column: "periodo",
                unique: true,
                filter: "estado = 'Aceptado'");

            migrationBuilder.CreateIndex(
                name: "ix_data_procesada_carga_archivo_id",
                table: "data_procesada",
                column: "carga_archivo_id");

            migrationBuilder.CreateIndex(
                name: "ux_data_procesada_periodo_codigo",
                table: "data_procesada",
                columns: new[] { "periodo", "codigo_producto" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_detalle_carga_error_carga_archivo_id",
                table: "detalle_carga_error",
                column: "carga_archivo_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_token_token",
                table: "refresh_token",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_token_usuario_id",
                table: "refresh_token",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_email",
                table: "usuario",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "carga_periodo");

            migrationBuilder.DropTable(
                name: "data_procesada");

            migrationBuilder.DropTable(
                name: "detalle_carga_error");

            migrationBuilder.DropTable(
                name: "refresh_token");

            migrationBuilder.DropTable(
                name: "carga_archivo");

            migrationBuilder.DropTable(
                name: "usuario");
        }
    }
}
