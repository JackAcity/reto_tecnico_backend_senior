using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistencia.Migraciones
{
    /// <summary>
    /// §4.15 — procedimientos almacenados. Solo dos, y solo donde el motor gana de verdad:
    /// atomicidad real (advisory lock) e inserción set-based. Sin procedimientos de adorno.
    /// </summary>
    public partial class Procedimientos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------------------------------------------------------------------
            // sp_resolver_periodo — decide en UNA operación atómica si un periodo
            // puede procesarse. Un SELECT seguido de un INSERT sería un TOCTOU:
            // dos cargas simultáneas del mismo periodo pasarían ambas (design.md §C9).
            // ---------------------------------------------------------------------
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION sp_resolver_periodo(p_carga_id int, p_periodo varchar)
                RETURNS varchar
                LANGUAGE plpgsql
                AS $$
                DECLARE
                    v_estado_otra varchar(20);
                BEGIN
                    -- Serializa a los competidores por ESTE periodo hasta el fin de la transacción.
                    PERFORM pg_advisory_xact_lock(hashtext(p_periodo));

                    -- Una carga que murió sin terminar no puede reservar el periodo para siempre.
                    -- Su reserva pasa a 'Bloqueado' (que es literalmente lo que le ocurrió) y el
                    -- índice único parcial queda libre para la carga nueva.
                    UPDATE carga_periodo cp
                       SET estado = 'Bloqueado'
                      FROM carga_archivo ca
                     WHERE cp.carga_archivo_id = ca.id
                       AND cp.periodo = p_periodo
                       AND cp.estado = 'Aceptado'
                       AND cp.carga_archivo_id <> p_carga_id
                       AND ca.estado IN ('Fallida', 'Rechazada', 'Bloqueada');

                    -- §C2: excluye la propia carga. Implementado literal, el consumidor
                    -- encontraría su propia fila Pendiente y se auto-bloquearía.
                    SELECT ca.estado INTO v_estado_otra
                      FROM carga_periodo cp
                      JOIN carga_archivo ca ON ca.id = cp.carga_archivo_id
                     WHERE cp.periodo = p_periodo
                       AND cp.estado = 'Aceptado'
                       AND cp.carga_archivo_id <> p_carga_id
                     LIMIT 1;

                    IF v_estado_otra IN ('Cargado', 'Finalizado', 'Notificado') THEN
                        RETURN 'YaCargado';
                    ELSIF v_estado_otra IS NOT NULL THEN
                        -- Otra carga Pendiente/EnProceso ya tomó el periodo.
                        RETURN 'Bloqueado';
                    END IF;

                    -- ON CONFLICT: si el mensaje se reentrega (§C8), la reserva propia ya
                    -- existe y la operación es inofensiva.
                    INSERT INTO carga_periodo (carga_archivo_id, periodo, estado, filas_insertadas)
                    VALUES (p_carga_id, p_periodo, 'Aceptado', 0)
                    ON CONFLICT DO NOTHING;

                    RETURN 'Libre';
                END;
                $$;
                """);

            // ---------------------------------------------------------------------
            // sp_insertar_data_procesada — inserción masiva set-based con unnest:
            // un round trip en vez de N. El ON CONFLICT sobre la clave de negocio
            // (Periodo, CodigoProducto) es el Idempotent Consumer de Richardson
            // apoyado en la llave natural, sin tabla de mensajes procesados (§C8).
            // ---------------------------------------------------------------------
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION sp_insertar_data_procesada(
                    p_carga_id  int,
                    p_periodos  varchar[],
                    p_codigos   varchar[],
                    p_nombres   varchar[],
                    p_precios   numeric[]
                )
                RETURNS int
                LANGUAGE plpgsql
                AS $$
                DECLARE
                    v_insertadas int;
                BEGIN
                    INSERT INTO data_procesada
                        (periodo, codigo_producto, nombre_producto, precio, carga_archivo_id, fecha_registro)
                    SELECT t.periodo, t.codigo, t.nombre, t.precio, p_carga_id, now()
                      FROM unnest(p_periodos, p_codigos, p_nombres, p_precios)
                        AS t(periodo, codigo, nombre, precio)
                    ON CONFLICT (periodo, codigo_producto) DO NOTHING;

                    GET DIAGNOSTICS v_insertadas = ROW_COUNT;

                    -- La diferencia contra el tamaño del lote son los duplicados que ya
                    -- estaban en base: el llamador los reporta como 'Existente'.
                    RETURN v_insertadas;
                END;
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_insertar_data_procesada(int, varchar[], varchar[], varchar[], numeric[]);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_resolver_periodo(int, varchar);");
        }
    }
}
