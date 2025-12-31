using System;
using System.Management;
using System.Text;

namespace Exterminio_RAT_Cliente
{
    public class InfoDiscosFormatoEspecifico
    {
        public static string ObtenerInformacionDiscosFormateada()
        {
            StringBuilder resultado = new StringBuilder();
            resultado.Append("[DISCOS] | ");

            try
            {
                // Obtener todas las unidades lógicas
                using (ManagementObjectSearcher buscadorDiscos = new ManagementObjectSearcher("SELECT * FROM Win32_LogicalDisk WHERE DriveType = 3"))
                {
                    foreach (ManagementObject discoLogico in buscadorDiscos.Get())
                    {
                        string letraUnidad = discoLogico["DeviceID"]?.ToString() ?? "";
                        string tamañoTotal = discoLogico["Size"] != null ? FormatearBytes(Convert.ToUInt64(discoLogico["Size"])) : "0B";
                        string espacioLibre = discoLogico["FreeSpace"] != null ? FormatearBytes(Convert.ToUInt64(discoLogico["FreeSpace"])) : "0B";

                        // Obtener modelo y tipo de partición
                        string modelo = "Desconocido";
                        string tipoParticion = "Desconocido";

                        // Relación: LogicalDisk -> Partition -> DiskDrive
                        string queryParticion = $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{letraUnidad}'}} WHERE AssocClass = Win32_LogicalDiskToPartition";

                        using (ManagementObjectSearcher buscadorParticiones = new ManagementObjectSearcher(queryParticion))
                        {
                            foreach (ManagementObject particion in buscadorParticiones.Get())
                            {
                                // Obtener tipo de partición (GPT o MBR)
                                tipoParticion = ObtenerTipoParticion(particion);

                                // Obtener disco físico
                                string queryDisco = $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{particion["DeviceID"]}'}} WHERE AssocClass = Win32_DiskDriveToDiskPartition";

                                using (ManagementObjectSearcher buscadorDiscosFisicos = new ManagementObjectSearcher(queryDisco))
                                {
                                    foreach (ManagementObject discoFisico in buscadorDiscosFisicos.Get())
                                    {
                                        modelo = discoFisico["Model"]?.ToString()?.Trim() ?? "Desconocido";
                                        break;
                                    }
                                }
                                break;
                            }
                        }

                        // Construir el formato específico: [C:((Modelo))(475.65GB/83.2GB)(TIPO))]
                        resultado.Append($"[{letraUnidad}(({modelo}))({tamañoTotal}/{espacioLibre})({tipoParticion}))]" + " - ");
                    }
                }
            }
            catch (Exception ex)
            {
                resultado.Append($"[ERROR({ex.Message}))]");
            }

            // Remover el último " - " si existe
            string resultadoFinal = resultado.ToString();
            if (resultadoFinal.EndsWith(" - "))
            {
                resultadoFinal = resultadoFinal.Substring(0, resultadoFinal.Length - 3);
            }

            // Si no hay discos, agregar mensaje
            if (resultadoFinal == "[DISCOS] | ")
            {
                return "[DISCOS [SIN_DISCOS]]";
            }

            Console.WriteLine($"Formato final de discos: '{resultadoFinal}'");
            return resultadoFinal;
        }

        private static string ObtenerTipoParticion(ManagementObject particion)
        {
            try
            {
                string tipo = particion["Type"]?.ToString() ?? "";

                if (tipo.Contains("GPT") || tipo.Contains("gpt") || tipo == "0xEE" || tipo == "0xEF")
                    return "GPT";
                else
                    return "MBR";
            }
            catch
            {
                return "DESCONOCIDO";
            }
        }

        private static string FormatearBytes(ulong bytes)
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
    }
}
