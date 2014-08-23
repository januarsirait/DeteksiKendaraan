﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using AForge;
using AForge.Imaging;
using AForge.Imaging.Filters;
using System.Drawing.Imaging;
using System.Reflection;

namespace DeteksiKendaraan
{
    public partial class Form1 : Form
    {

        Bitmap originalImage;
        Bitmap segementImage;
        Bitmap cannyImage;
        Bitmap roiImage;
        Bitmap morfologi;
        ConnectedComponentsLabeling cclFilter;

        // binarization filtering sequence
        private FiltersSequence filter = new FiltersSequence(
            Grayscale.CommonAlgorithms.BT709,
            new Threshold(64)
        );

        private FiltersSequence morfologiFilter = new FiltersSequence(
            new Opening(),
            new Closing()
        );

        public Form1()
        {
            InitializeComponent();
            txtFilename.Text = "";
            colorDialog1.Color = Color.Red;
        }

        private void openFileDialog1_FileOk(object sender, CancelEventArgs e)
        {
            txtFilename.Text = openFileDialog1.FileName;
            pctResult.Image = (Bitmap)Bitmap.FromFile(openFileDialog1.FileName);
            morfologi = null;
        }

        private void DeteksiJalan(Bitmap roi)
        {
            //originalImage = (Bitmap)Bitmap.FromFile("D:\\Kerjaan\\Skripsi\\Ari Usman\\Bahan Penelitian\\II\\ROI.jpg");
            originalImage = roi;


            segementImage = Segmentasi(originalImage);

            AForge.Imaging.Filters.CannyEdgeDetector cannyFilter = new AForge.Imaging.Filters.CannyEdgeDetector();
            cannyImage = cannyFilter.Apply(AForge.Imaging.Filters.Grayscale.CommonAlgorithms.BT709.Apply(segementImage));

            HoughLineTransformation linetransform = new HoughLineTransformation();
            roiImage = HoughTransformation(cannyImage, originalImage);

            pictureBox1.Image = originalImage;
            pictureBox2.Image = segementImage;
            pictureBox3.Image = cannyImage;
            pictureBox5.Image = roiImage;
        }

        private Bitmap Segmentasi(Bitmap original)
        {
            Bitmap temp = (Bitmap)original.Clone();
            Color c;

            for (int i = 0; i < temp.Width; i++)
            {
                for (int j = 0; j < temp.Height; j++)
                {
                    c = temp.GetPixel(i, j);
                    if ((c.R >= 65 && c.R <= 190) && (c.G >= 70 && c.G <= 190) && (c.B >= 80 && c.B <= 190))
                    {
                        temp.SetPixel(i, j, Color.Black);
                    }
                    else
                    {
                        temp.SetPixel(i, j, Color.White);
                    }
                }
            }

            return temp;
        }

        private Bitmap HoughTransformation(Bitmap image, Bitmap originalImage)
        {
            HoughLineTransformation houghLineTransform = new HoughLineTransformation();
            PointLine leftLine = new PointLine();
            PointLine rightLine = new PointLine();

            Bitmap temp = AForge.Imaging.Image.Clone(image, PixelFormat.Format24bppRgb);
            /// lock the source image
            BitmapData sourceData = temp.LockBits(
                new Rectangle(0, 0, temp.Width, temp.Height),
                ImageLockMode.ReadOnly, temp.PixelFormat);
            // binarize the image
            UnmanagedImage binarySource = filter.Apply(new UnmanagedImage(sourceData));
            // apply Hough line transofrm
            houghLineTransform.ProcessImage(binarySource);

            // get lines using relative intensity
            HoughLine[] lines = houghLineTransform.GetLinesByRelativeIntensity(0.5);

            int icr = 1;
            foreach (HoughLine line in lines)
            {

                if (line.Theta > 50 && line.Theta < 130)
                {
                    continue;
                }

                string s = string.Format("Theta = {0}, R = {1}, I = {2} ({3})", line.Theta, line.Radius, line.Intensity, line.RelativeIntensity);
                System.Diagnostics.Debug.WriteLine(s);

                // uncomment to highlight detected lines

                // get line's radius and theta values
                int r = line.Radius;
                double t = line.Theta;

                // check if line is in lower part of the image
                if (r < 0)
                {
                    t += 180;
                    r = -r;
                }

                // convert degrees to radians
                t = (t / 180) * Math.PI;

                // get image centers (all coordinate are measured relative
                // to center)
                int w2 = image.Width / 2;
                int h2 = image.Height / 2;

                double x0 = 0, x1 = 0, y0 = 0, y1 = 0;

                if (line.Theta != 0)
                {
                    // none vertical line
                    x0 = -w2; // most left point
                    x1 = w2;  // most right point

                    // calculate corresponding y values
                    y0 = (-Math.Cos(t) * x0 + r) / Math.Sin(t);
                    y1 = (-Math.Cos(t) * x1 + r) / Math.Sin(t);
                }
                else
                {
                    // vertical line
                    x0 = line.Radius;
                    x1 = line.Radius;

                    y0 = h2;
                    y1 = -h2;
                }

                // draw line on the image
                Drawing.Line(sourceData,
                    new IntPoint((int)x0 + w2, h2 - (int)y0),
                    new IntPoint((int)x1 + w2, h2 - (int)y1),
                    Color.Red);
                System.Diagnostics.Debug.WriteLine(String.Format("Point ({0},{1}),({2},{3})", (int)x0 + w2, h2 - (int)y0, (int)x1 + w2, h2 - (int)y1));

                if (line.Theta >= 25 && line.Theta <= 45)
                {
                    //if (leftLine.Point1 != null) continue;

                    leftLine.Point1 = new IntPoint((int)x0 + w2, h2 - (int)y0);
                    leftLine.Point2 = new IntPoint((int)x1 + w2, h2 - (int)y1);
                }
                else if (line.Theta >= 130 && line.Theta <= 155)
                {
                    //if (rightLine.Point1 != null) continue;

                    rightLine.Point1 = new IntPoint((int)x0 + w2, h2 - (int)y0);
                    rightLine.Point2 = new IntPoint((int)x1 + w2, h2 - (int)y1);
                }


                //if (icr == 3)
                //{
                //    break;
                //}
                //icr++;
            }

            System.Diagnostics.Debug.WriteLine("Found lines: " + houghLineTransform.LinesCount);
            System.Diagnostics.Debug.WriteLine("Max intensity: " + houghLineTransform.MaxIntensity);
            System.Diagnostics.Debug.WriteLine("Houghline: " + lines.Length);

            Bitmap roi = (Bitmap)originalImage.Clone();
            for (int x = 0; x < roi.Width; x++)
            {
                for (int y = 0; y < roi.Height; y++)
                {
                    if (((y * (leftLine.Point2.X - leftLine.Point1.X) - x * (leftLine.Point2.Y - leftLine.Point1.Y)) >
                        (leftLine.Point1.Y * (leftLine.Point2.X - leftLine.Point1.X) - leftLine.Point1.X * (leftLine.Point2.Y - leftLine.Point1.Y))) &&
                        (y * (rightLine.Point2.X - rightLine.Point1.X) - x * (rightLine.Point2.Y - rightLine.Point1.Y)) >
                        (rightLine.Point1.Y * (rightLine.Point2.X - rightLine.Point1.X) - rightLine.Point1.X * (rightLine.Point2.Y - rightLine.Point1.Y)))
                    {
                        // nothing
                    }
                    else
                    {
                        roi.SetPixel(x, y, Color.Black);
                    }
                }
            }

            // unlock source image
            temp.UnlockBits(sourceData);
            // dispose temporary binary source image
            binarySource.Dispose();
            pictureBox4.Image = temp;

            return roi;
        }

        public Bitmap Substraksi(Bitmap roiImage, Bitmap ujiImage)
        {
            Bitmap temp = (Bitmap)ujiImage.Clone();
            //String pixel = "";
            //using (System.IO.StreamWriter sw = new System.IO.StreamWriter("pixel_.csv"))
            //{
            for (int y = 0; y < roiImage.Height; y++)
            {
                for (int x = 0; x < roiImage.Width; x++)
                {
                    Color roiPixel = roiImage.GetPixel(x, y);
                    Color ujiPixel = ujiImage.GetPixel(x, y);
                    Color newColor;
                    int red = roiPixel.R - ujiPixel.R;
                    int green = roiPixel.G - ujiPixel.G;
                    int blue = roiPixel.B - ujiPixel.B;

                    //sw.Write("(" + red + "," + green + "," + blue + ");");
                    if (red <= 0)
                    {
                        red = 0;
                    }
                    else
                    {
                        red = red + 10;
                    }

                    if (green <= 0)
                    {
                        green = 0;
                    }
                    else
                    {
                        green = green + 10;
                    }

                    if (blue <= 0)
                    {
                        blue = 0;
                    }
                    else
                    {
                        blue = blue + 10;
                    }

                    newColor = Color.FromArgb(red, green, blue);

                    temp.SetPixel(x, y, newColor);
                }
                //sw.WriteLine();
            }

            //    sw.Flush();
            //    sw.Close();
            //}

            return temp;
        }

        private void pctResult_Paint(object sender, PaintEventArgs e)
        {
            if (morfologi != null)
            {
                BlobCounter bc = (BlobCounter)cclFilter.BlobCounter;
                //bc.ProcessImage(morfologi);
                Rectangle[] rects = bc.GetObjectsRectangles();
                int jumlah = 0;

                foreach (Rectangle rect in rects)
                {
                    if (rect.Width > txtMinWidth.Value && rect.Height > txtMinHeight.Value)
                    {
                        Pen pen = new Pen(colorDialog1.Color, float.Parse(txtTebalGaris.Value.ToString()));
                        float X = (float)pctResult.Width / 450;
                        float Y = (float)pctResult.Height / 253;
                        Rectangle newRec = new Rectangle((int)Math.Ceiling(rect.X * X), (int)Math.Ceiling(rect.Y * Y),
                            (int)Math.Ceiling(rect.Width * X), (int)Math.Ceiling(rect.Height * Y));
                        e.Graphics.DrawRectangle(pen, newRec);
                        jumlah++;
                    }
                }

                lblJumlah.Text = jumlah.ToString();
            }
        }

        private void openFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openFileDialog1.ShowDialog();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void aboutToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            AboutForm about = new AboutForm();
            about.ShowDialog();
        }

        private void txtColorPicker_Click(object sender, EventArgs e)
        {
            colorDialog1.Color = Color.FromName(txtColor.Text);
            if (colorDialog1.ShowDialog() == DialogResult.OK)
            {
                txtColor.Text = colorDialog1.Color.Name;
            }
        }

        private void btnDeteksi_Click(object sender, EventArgs e)
        {
            if (txtColor.Text == "")
            {
                MessageBox.Show("Warna harus diisi", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (txtFilename.Text == "")
            {
                MessageBox.Show("Citra uji belum dipilih", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            /* citra uji */
            Bitmap citraUji = (Bitmap)Bitmap.FromFile(openFileDialog1.FileName);
            Bitmap subtraksi = Substraksi(roiImage, citraUji);
            Bitmap filterGT = filter.Apply(subtraksi);

            morfologi = new Opening().Apply(new Closing().Apply(filterGT));

            cclFilter = new ConnectedComponentsLabeling();
            cclFilter.MinHeight = 50000;
            cclFilter.MinWidth = 50000;
            Bitmap ccl = cclFilter.Apply(morfologi);

            Console.WriteLine(cclFilter.ObjectCount);

            pictureBox6.Image = citraUji;
            pictureBox7.Image = subtraksi;
            pictureBox8.Image = filterGT;
            pictureBox9.Image = morfologi;
            pictureBox10.Image = ccl;
            pctResult.Image = citraUji;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Assembly assembly = this.GetType().Assembly;
            
            Bitmap image = new Bitmap(System.IO.Path.GetDirectoryName(assembly.Location) + "\\ROI_3.jpg");
            DeteksiJalan(image);
        }
    }
}
