using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TexChannelPacker
{
    public partial class MainForm : Form
    {
        protected class BMP
        {
            public int Width;
            public int Height;
            public byte[] Data;
            public PixelFormat PixelFormat;
            public int Stride;
        }

        protected BMP bmpAlbedo;
        protected BMP bmpAO;
        protected BMP bmpSpec;
        protected BMP bmpNorm;
        protected BMP bmpDispl;
        protected BMP bmpRough;

        public MainForm()
        {
            InitializeComponent();

            this.Shown += new System.EventHandler(this.MainForm_Shown);
        }

        protected void MainForm_Shown(object sender, EventArgs e)
        {
            Process();

            Application.Exit();
        }

        protected void Process()
        {
            var name = "";
            var newfilepath = "";

            var filepath = GetFilePath("Albedo");

            if(filepath.ToLower().EndsWith(".zip"))
            {
                InputBox("Material Name", "Material Name:", ref name);
                var dir = Path.GetDirectoryName(filepath) + @"\" + name;
                newfilepath = dir;

                dir += "TMP";

                ZipFile.ExtractToDirectory(filepath, dir);
                
                try
                {
                    filepath = FindSupportedImg(dir, "*_Albedo*");
                    var bmp = new Bitmap(filepath);
                    bmpAlbedo = GetData(bmp);
                    bmp.Dispose();

                    filepath = FindSupportedImg(dir, "*_AO*");
                    bmp = new Bitmap(filepath);
                    bmpAO = GetData(bmp);
                    bmp.Dispose();

                    filepath = FindSupportedImg(dir, "*_Specular*");
                    bmp = new Bitmap(filepath);
                    bmpSpec = GetData(bmp);
                    bmp.Dispose();

                    filepath = FindSupportedImg(dir, "*_Normal*");
                    bmp = new Bitmap(filepath);
                    bmpNorm = GetData(bmp);
                    bmp.Dispose();

                    filepath = FindSupportedImg(dir, "*_Displacement*");
                    bmp = new Bitmap(filepath);
                    bmpDispl = GetData(bmp);
                    bmp.Dispose();

                    filepath = FindSupportedImg(dir, "*_Roughness*");
                    bmp = new Bitmap(filepath);
                    bmpRough = GetData(bmp);
                    bmp.Dispose();
                }
                finally
                {
                    Directory.Delete(dir, true);
                }
            }
            else
            {
                bmpAlbedo = GetData(new Bitmap(filepath));

                filepath = GetFilePath("AO");
                bmpAO = GetData(new Bitmap(filepath));

                filepath = GetFilePath("Specular");
                bmpSpec = GetData(new Bitmap(filepath));

                filepath = GetFilePath("Norm");
                bmpNorm = GetData(new Bitmap(filepath));

                filepath = GetFilePath("Displacement");
                bmpDispl = GetData(new Bitmap(filepath));

                filepath = GetFilePath("Roughness");
                bmpRough = GetData(new Bitmap(filepath));

                InputBox("Material Name", "Material Name:", ref name);
                newfilepath = Path.GetDirectoryName(filepath) + @"\" + name;
            }


            var w = bmpAlbedo.Width;
            var h = bmpAlbedo.Height;
            var argb = new byte[w * h * 4];
            var s = w * 4;

            // (RGB)Albedo * AO ; (A)Spec
            for (var x=0 ; x<w ; x++)
                for (var y=0 ; y<h ; y++)
                {
                    var p = x * 4 + y * s;

                    var Albedo = GetPixel(x, y, bmpAlbedo);
                    var AO = GetPixel(x, y, bmpAO);
                    var Spec = GetPixel(x, y, bmpSpec);

                    var fAO = AO[0]/255.0f;
                    var fSpec = Spec[0]/255.0f;
                    fSpec *= fSpec * fAO * fAO;

                    argb[p + 2] = System.Convert.ToByte(Albedo[0] * fAO);
                    argb[p + 1] = System.Convert.ToByte(Albedo[1] * fAO);
                    argb[p + 0] = System.Convert.ToByte(Albedo[2] * fAO);
                    argb[p + 3] = System.Convert.ToByte(255.0f * fSpec);
                }                

            WriteData(newfilepath + "_AAOS.png", argb, w, h);

            // (RG)Norm ; (B)Displ ; (A)Rough
            for (var x=0 ; x<w ; x++)
                for (var y=0 ; y<h ; y++)
                {
                    var p = x * 4 + y * s;

                    var Norm = GetPixel(x, y, bmpNorm);
                    var Displ = GetPixel(x, y, bmpDispl);
                    var Rough = GetPixel(x, y, bmpRough);

                    argb[p + 2] = Norm[0];
                    argb[p + 1] = System.Convert.ToByte(0xFF - Norm[1]);
                    argb[p + 0] = Displ[0];
                    argb[p + 3] = Rough[0];
                }                

            WriteData(newfilepath + "_NDR.png", argb, w, h);
        }

        protected string FindSupportedImg(string dir, string search)
        {
            return Directory.GetFiles(dir, search).Where(x =>
                x.ToLower().EndsWith("png") ||
                x.ToLower().EndsWith("bmp") ||
                x.ToLower().EndsWith("jpg") ||
                x.ToLower().EndsWith("gif")).First();
        }

        public string GetFilePath(string title)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = title; 
                //openFileDialog.InitialDirectory = "c:\\";
                openFileDialog.Filter = "Image Files(*.PNG;*.BMP;*.JPG;*.GIF)|*.PNG;*.BMP;*.JPG;*.GIF|Zip files (*.zip)|*.zip|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 2;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    return openFileDialog.FileName;
                }
            }

            return null;
        }

        protected BMP GetData(Bitmap bmp)
        {
            var newBmp = new BMP();
            newBmp.Width = bmp.Width;
            newBmp.Height = bmp.Height;
            newBmp.PixelFormat = bmp.PixelFormat;

            // Lock the bitmap's bits.  
            Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            System.Drawing.Imaging.BitmapData bmpData =
                bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly,
                bmp.PixelFormat);

            // Get the address of the first line.
            IntPtr ptr = bmpData.Scan0;

            // Declare an array to hold the bytes of the bitmap.
            int bytes  = Math.Abs(bmpData.Stride) * bmp.Height;
            newBmp.Stride = Math.Abs(bmpData.Stride);
            byte[] rgbValues = new byte[bytes];
            newBmp.Data = rgbValues;

            // Copy the RGB values into the array.
            System.Runtime.InteropServices.Marshal.Copy(ptr, rgbValues, 0, bytes);

            // Unlock the bits.
            bmp.UnlockBits(bmpData);

            return newBmp;
        }

        protected void WriteData(string filepath, byte[] argbValues, int width, int height)
        {
            // Create a new bitmap.
            Bitmap bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            // Lock the bitmap's bits.  
            Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            System.Drawing.Imaging.BitmapData bmpData =
                bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly,
                bmp.PixelFormat);

            // Get the address of the first line.
            IntPtr ptr = bmpData.Scan0;

            // Declare an array to hold the bytes of the bitmap.
            int bytes  = Math.Abs(bmpData.Stride) * bmp.Height;

            // Copy the RGB values back to the bitmap
            System.Runtime.InteropServices.Marshal.Copy(argbValues, 0, ptr, bytes);

            // Unlock the bits.
            bmp.UnlockBits(bmpData);

            bmp.Save(filepath, System.Drawing.Imaging.ImageFormat.Png);
        }

        protected byte[] GetPixel(int x, int y, BMP bmp)
        {
            var pixel = new byte[] { 0, 0, 0, 0 };

            var s = bmp.Stride;
            var p = 0;

            switch(bmp.PixelFormat)
            {
                //case Format32bppPArgb

                case PixelFormat.Format24bppRgb:
                    p = x * 3 + y * s;
                    pixel[0] = bmp.Data[p + 2]; // R
                    pixel[1] = bmp.Data[p + 1]; // G
                    pixel[2] = bmp.Data[p + 0]; // B
                    pixel[3] = 1; // A
                    break;

                case PixelFormat.Format32bppArgb:
                    p = x * 4 + y * s;
                    pixel[0] = bmp.Data[p + 2];
                    pixel[1] = bmp.Data[p + 1];
                    pixel[2] = bmp.Data[p + 0];
                    pixel[3] = bmp.Data[p + 3];
                    break;

                case PixelFormat.Format32bppRgb:
                    p = x * 4 + y * s;
                    pixel[0] = bmp.Data[p + 2];
                    pixel[1] = bmp.Data[p + 1];
                    pixel[2] = bmp.Data[p + 0];
                    pixel[3] = 1;
                    break;
            }

            return pixel;
        }

        public static DialogResult InputBox(string title, string promptText, ref string value)
        {
            Form form = new Form();
            Label label = new Label();
            TextBox textBox = new TextBox();
            Button buttonOk = new Button();
            //Button buttonCancel = new Button();

            form.Text = title;
            label.Text = promptText;
            textBox.Text = value;

            buttonOk.Text = "OK";
            // buttonCancel.Text = "Cancel";
            buttonOk.DialogResult = DialogResult.OK;
            // buttonCancel.DialogResult = DialogResult.Cancel;

            label.SetBounds(9, 20, 372, 13);
            textBox.SetBounds(12, 36, 372, 20);
            buttonOk.SetBounds(228, 72, 75, 23);
            // buttonCancel.SetBounds(309, 72, 75, 23);

            label.AutoSize = true;
            textBox.Anchor = textBox.Anchor | AnchorStyles.Right;
            buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            // buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            form.ClientSize = new Size(396, 107);
            form.Controls.AddRange(new Control[] { label, textBox, buttonOk });//, buttonCancel });
            form.ClientSize = new Size(Math.Max(300, label.Right + 10), form.ClientSize.Height);
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.MinimizeBox = false;
            form.MaximizeBox = false;
            form.AcceptButton = buttonOk;
            // form.CancelButton = buttonCancel;

            DialogResult dialogResult = form.ShowDialog();
            value = textBox.Text;
            return dialogResult;
        }
    }
}
