using UnityEngine;
using System;
using System.IO;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;

[Serializable]
public class RangoFechaPieza
{
    public string tipoPieza = "WHITE";
    [Tooltip("Formato obligatorio: dd/MM/yyyy HH:mm:ss (Escribe la hora LOCAL de tu PC, ej: 27/07/2026 14:45:25)")]
    public string fechaHoraInicio = "27/07/2026 14:45:25";
    [Tooltip("Formato obligatorio: dd/MM/yyyy HH:mm:ss (Escribe la hora LOCAL de tu PC, ej: 27/07/2026 14:47:45)")]
    public string fechaHoraFin = "27/07/2026 14:47:45";
    public string nombreArchivoJson = "datosPiezaBlanca.json";
}

public class GenerarJSONSimulacion : MonoBehaviour
{
    [Header("--- Carpeta de Guardado ---")]
    public string carpetaGuardado = "Assets/DatosSimulacion";

    [Header("--- Configuración Rango de Fechas por Pieza (Hora Local PC) ---")]
    public RangoFechaPieza piezaBlanca = new RangoFechaPieza
    {
        tipoPieza = "WHITE",
        fechaHoraInicio = "27/07/2026 14:45:25",
        fechaHoraFin = "27/07/2026 14:47:45",
        nombreArchivoJson = "datosPiezaBlanca.json"
    };

    public RangoFechaPieza piezaRoja = new RangoFechaPieza
    {
        tipoPieza = "RED",
        fechaHoraInicio = "29/07/2026 12:35:05",
        fechaHoraFin = "29/07/2026 12:37:20",
        nombreArchivoJson = "datosPiezaRoja.json"
    };

    public RangoFechaPieza piezaAzul = new RangoFechaPieza
    {
        tipoPieza = "BLUE",
        fechaHoraInicio = "29/07/2026 14:26:40",
        fechaHoraFin = "29/07/2026 14:29:05",
        nombreArchivoJson = "datosPiezaAzul.json"
    };

    // =========================================================================
    // MENÚ CONTEXTUAL (Haz clic derecho sobre este componente en el Inspector)
    // =========================================================================

    [ContextMenu("▶ 1. Generar TODOS los JSONs desde InfluxDB")]
    public void GenerarTodosLosArchivos()
    {
        StartCoroutine(ProcesarTodos());
    }

    [ContextMenu("▶ 2. Generar Solo Pieza BLANCA")]
    public void GenerarJSONBlanca() => StartCoroutine(ProcesarYGuardarPieza(piezaBlanca));

    [ContextMenu("▶ 3. Generar Solo Pieza ROJA")]
    public void GenerarJSONRoja() => StartCoroutine(ProcesarYGuardarPieza(piezaRoja));

    [ContextMenu("▶ 4. Generar Solo Pieza AZUL")]
    public void GenerarJSONAzul() => StartCoroutine(ProcesarYGuardarPieza(piezaAzul));

    private IEnumerator ProcesarTodos()
    {
        yield return StartCoroutine(ProcesarYGuardarPieza(piezaBlanca));
        yield return StartCoroutine(ProcesarYGuardarPieza(piezaRoja));
        yield return StartCoroutine(ProcesarYGuardarPieza(piezaAzul));
        Debug.Log("<color=green><b>🎉 ¡Proceso finalizado! Todos los JSONs fueron descargados y guardados.</b></color>");
    }

    private IEnumerator ProcesarYGuardarPieza(RangoFechaPieza config)
    {
        // 🟢 Interpreta la hora introducida como Hora Local de tu PC y la convierte a UTC para InfluxDB
        DateTime inicioUTC = ParsearFecha(config.fechaHoraInicio);
        DateTime finUTC = ParsearFecha(config.fechaHoraFin);

        if (inicioUTC == DateTime.MinValue || finUTC == DateTime.MinValue)
        {
            Debug.LogError($"❌ Error en formato de fecha para [{config.tipoPieza}]. Usa 'dd/MM/yyyy HH:mm:ss'");
            yield break;
        }

        InfluxDBClient influxClient = InfluxDBClient.Instance != null ? InfluxDBClient.Instance : FindFirstObjectByType<InfluxDBClient>();
        if (influxClient == null)
        {
            Debug.LogError("❌ InfluxDBClient no está activo en la escena.");
            yield break;
        }

        Debug.Log($"<color=yellow>⏳ Descargando InfluxDB para [{config.tipoPieza}]\n• Hora PC introducida: {config.fechaHoraInicio} a {config.fechaHoraFin}\n• Consulta convertida a UTC: {inicioUTC:dd/MM/yyyy HH:mm:ss} a {finUTC:dd/MM/yyyy HH:mm:ss}</color>");

        SecuenciaPiezaData secuenciaData = new SecuenciaPiezaData { tipoPieza = config.tipoPieza };
        bool descargaCompletada = false;
        List<InfluxDBClient.RegistroInflux> eventosObtenidos = null;

        yield return StartCoroutine(
            influxClient.DescargarHistoricoSinReproducir(
                inicioUTC,
                finUTC,
                (registros) =>
                {
                    eventosObtenidos = registros;
                    descargaCompletada = true;
                }
            )
        );

        while (!descargaCompletada)
        {
            yield return null;
        }

        if (eventosObtenidos != null && eventosObtenidos.Count > 0)
        {
            // Ordenar eventos por fecha
            eventosObtenidos.Sort((a, b) => a.timestamp.CompareTo(b.timestamp));
            DateTime tiempoBase = eventosObtenidos[0].timestamp.ToUniversalTime();

            foreach (var reg in eventosObtenidos)
            {
                float segundosRelativos = (float)(reg.timestamp.ToUniversalTime() - tiempoBase).TotalSeconds;

                secuenciaData.eventos.Add(new EventoHistoricoPieza
                {
                    tiempoRelativoSegundos = (float)Math.Round(segundosRelativos, 3),
                    topic = reg.topic,
                    payloadJson = reg.payloadJson
                });
            }
        }

        if (!Directory.Exists(carpetaGuardado))
        {
            Directory.CreateDirectory(carpetaGuardado);
        }

        string rutaCompleta = Path.Combine(carpetaGuardado, config.nombreArchivoJson);
        string jsonTexto = JsonUtility.ToJson(secuenciaData, true);

        File.WriteAllText(rutaCompleta, jsonTexto);

        Debug.Log($"<color=green>✅ Archivo guardado: <b>{rutaCompleta}</b> ({secuenciaData.eventos.Count} eventos guardados)</color>");

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
    }

    /// <summary>
    /// Interpreta la fecha introducida en el Inspector como HORA LOCAL de tu PC 
    /// y la convierte a UTC para consultar a InfluxDB.
    /// </summary>
    private DateTime ParsearFecha(string fechaTexto)
    {
        string[] formatos = { "dd/MM/yyyy HH:mm:ss", "dd-MM-yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss" };

        // 1. Interpretar la fecha escrita como Hora Local del sistema operativo (AssumeLocal)
        if (DateTime.TryParseExact(fechaTexto.Trim(), formatos, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime dtLocal))
        {
            // 2. Convertir automáticamente a UTC para la consulta de InfluxDB
            return dtLocal.ToUniversalTime();
        }

        if (DateTime.TryParse(fechaTexto, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out dtLocal))
        {
            return dtLocal.ToUniversalTime();
        }

        return DateTime.MinValue;
    }
}