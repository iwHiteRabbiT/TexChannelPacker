using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text.RegularExpressions;

namespace TexChannelPacker
{
    public partial class MainForm : Form
    {
        public const string xNormal = @"C:\Softs\xNormal\3.19.3\x64\xNormal.exe";

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
        protected BMP bmpCav;
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

            var meshfile = "";

            if(filepath.ToLower().EndsWith(".zip"))
            {
                InputBox("Material Name", "Material Name:", ref name);
                newfilepath = Path.GetDirectoryName(filepath);
                var dir = newfilepath + @"\" + name + "TMP";

                ZipFile.ExtractToDirectory(filepath, dir);
                
                try
                {
                    filepath = FindSupportedImg(dir, "*_Albedo*");
                    var bmp = new Bitmap(filepath);
                    bmpAlbedo = GetData(bmp);
                    bmp.Dispose();

                    filepath = FindSupportedImg(dir, "*_AO*");
                    if (!String.IsNullOrEmpty(filepath))
                    {
                        bmp = new Bitmap(filepath);
                        bmpAO = GetData(bmp);
                        bmp.Dispose();
                    }

                    filepath = FindSupportedImg(dir, "*_Cavity*");
                    bmp = new Bitmap(filepath);
                    bmpCav = GetData(bmp);
                    bmp.Dispose();

                    // filepath = FindSupportedImg(dir, "*_Specular*");
                    // bmp = new Bitmap(filepath);
                    // bmpSpec = GetData(bmp);
                    // bmp.Dispose();

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

                    var fbxes = Directory.GetFiles(dir, "*").Where(x => x.ToLower().EndsWith("fbx")).ToList();
                    if (fbxes.Count > 0)
                    {
                        newfilepath += @"\" + name + @"\";
                        Directory.CreateDirectory(newfilepath);

                        foreach(var fp in fbxes)
                        {
                            var fn = Path.GetFileName(fp);
                            fn = Regex.Replace(fn, "(.+)(_LOD.+)", name + "$2");
                            File.Copy(fp, newfilepath + fn);

                            if (Regex.IsMatch(fn, ".+_LOD0.+"))
                                meshfile = newfilepath + fn;
                        }
                    }
                    else
                        newfilepath += @"\2K\";

                    newfilepath += name;
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

                filepath = GetFilePath("Cavity");
                bmpCav = GetData(new Bitmap(filepath));

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

            var resize = !String.IsNullOrEmpty(meshfile);

            var nC = 3;
            var w = bmpAlbedo.Width;
            var h = bmpAlbedo.Height;
            var rgb = new byte[w * h * nC];
            var s = w * nC;

            // (RGB)Albedo * AO
            for (var x=0 ; x<w ; x++)
                for (var y=0 ; y<h ; y++)
                {
                    var p = x * nC + y * s;

                    var Albedo = GetPixel(x, y, bmpAlbedo);

                    var fAO = 1.0f;

                    if (bmpAO != null)
                    {
                        var AO = GetPixel(x, y, bmpAO);
                        // var Spec = GetPixel(x, y, bmpSpec);

                        fAO = AO[0]/255.0f;
                        // var fSpec = Spec[0]/255.0f;
                        // fSpec *= fSpec * fAO * fAO;
                    }

                    rgb[p + 2] = System.Convert.ToByte(Albedo[0] * fAO);
                    rgb[p + 1] = System.Convert.ToByte(Albedo[1] * fAO);
                    rgb[p + 0] = System.Convert.ToByte(Albedo[2] * fAO);
                }                

            WriteData(newfilepath + "_AAO.png", rgb, w, h, 2048, resize);

            // (RGB)Norm for xNormal
            for (var x=0 ; x<w ; x++)
                for (var y=0 ; y<h ; y++)
                {
                    var p = x * nC + y * s;

                    var Norm = GetPixel(x, y, bmpNorm);

                    rgb[p + 2] = Norm[0];
                    rgb[p + 1] = Norm[1];
                    rgb[p + 0] = Norm[2];
                }                

            WriteData(newfilepath + "_N.png", rgb, w, h, 2048, resize);

            if (!String.IsNullOrEmpty(meshfile))
                xNormalConverter(meshfile, newfilepath + "_N.png", newfilepath + "_WSN.png");

            // (RGB)Norm
            for (var x=0 ; x<w ; x++)
                for (var y=0 ; y<h ; y++)
                {
                    var p = x * nC + y * s;

                    var Norm = GetPixel(x, y, bmpNorm);

                    rgb[p + 2] = Norm[0];
                    rgb[p + 1] = System.Convert.ToByte(0xFF - Norm[1]);
                    rgb[p + 0] = Norm[2];
                }                

            WriteData(newfilepath + "_N.png", rgb, w, h, 2048, resize);

            // (R)Displ ; (G)Rough ; (B)Cavity
            for (var x=0 ; x<w ; x++)
                for (var y=0 ; y<h ; y++)
                {
                    var p = x * nC + y * s;

                    var Displ = GetPixel(x, y, bmpDispl);
                    var Rough = GetPixel(x, y, bmpRough);
                    var Cav = GetPixel(x, y, bmpCav);

                    rgb[p + 2] = Displ[0];
                    rgb[p + 1] = Rough[0];
                    rgb[p + 0] = Cav[0];
                }                

            WriteData(newfilepath + "_DRC.png", rgb, w, h, 1024, resize);
        }

        protected static string FindSupportedImg(string dir, string search)
        {
            return Directory.GetFiles(dir, search).Where(x =>
                x.ToLower().EndsWith("png") ||
                x.ToLower().EndsWith("bmp") ||
                x.ToLower().EndsWith("jpg") ||
                x.ToLower().EndsWith("gif")).FirstOrDefault();
        }

        public static string GetFilePath(string title)
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

        protected static BMP GetData(Bitmap bmp)
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

        protected static void WriteData(string filepath, byte[] rgbValues, int width, int height, int newSize = 0, bool resize = false)
        {
            // Create a new bitmap.
            Bitmap bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

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
            System.Runtime.InteropServices.Marshal.Copy(rgbValues, 0, ptr, bytes);

            // Unlock the bits.
            bmp.UnlockBits(bmpData);

            if (resize)
            {
                var finalImg = ResizeImage(bmp, newSize);

                finalImg.Save(filepath, System.Drawing.Imaging.ImageFormat.Png);
            }
            else
                bmp.Save(filepath, System.Drawing.Imaging.ImageFormat.Png);
        }

        protected static byte[] GetPixel(int x, int y, BMP bmp)
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

        public static Image ResizeImage(Image image, int size, bool preserveAspectRatio = false)
        {
            int newWidth;
            int newHeight;
            if (preserveAspectRatio)
            {
                int originalWidth = image.Width;
                int originalHeight = image.Height;
                float percentWidth = (float)size / (float)originalWidth;
                float percentHeight = (float)size / (float)originalHeight;
                float percent = percentHeight < percentWidth ? percentHeight : percentWidth;
                newWidth = (int)(originalWidth * percent);
                newHeight = (int)(originalHeight * percent);
            }
            else
            {
                newWidth = size;
                newHeight = size;
            }

            Image newImage = new Bitmap(newWidth, newHeight);
            using (Graphics graphicsHandle = Graphics.FromImage(newImage))
            {
                graphicsHandle.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphicsHandle.DrawImage(image, 0, 0, newWidth, newHeight);
            }
            return newImage;
        }

        public static void xNormalConverter(string objpath, string npath, string wsnpath)
        {
            var param = string.Format("-os2ts {0} true {1} {2} false 16", objpath, npath, wsnpath);

            var proc = System.Diagnostics.Process.Start(xNormal, param);
            proc.WaitForExit();
            proc.Close();

            var bmp = new Bitmap(wsnpath);
            var bmpWSN = GetData(bmp);
            bmp.Dispose();

            // (RGB)Norm
            var nC = 3;
            var w = bmpWSN.Width;
            var h = bmpWSN.Height;
            var rgb = new byte[w * h * nC];
            var s = w * nC;

            for (var x=0 ; x<w ; x++)
                for (var y=0 ; y<h ; y++)
                {
                    var p = x * nC + y * s;

                    var Norm = GetPixel(x, y, bmpWSN);

                    rgb[p + 2] = Norm[0];
                    rgb[p + 1] = Norm[2];//System.Convert.ToByte(0xFF - Norm[1]);
                    rgb[p + 0] = Norm[1];
                }                

            WriteData(wsnpath, rgb, w, h);
        }
    }
}
