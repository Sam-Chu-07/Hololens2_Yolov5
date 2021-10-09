using System;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;
using System.Drawing;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.ML;
using Microsoft.ML.Transforms.Image;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using MySql.Data;
using MySql.Data.MySqlClient;
using YOLO.DataStructures;
using static Microsoft.ML.Transforms.Image.ImageResizingEstimator;



public class SynchronousSocketListener
{
    const int BUFSIZE = (int)3e6;
    const string _ipAddress = "192.168.2.100";
    const string inputFolder = @"Assets\Input";
    const string server = "localhost";
    const string database = "mydatabase";
    const string user = "admin";
    const string password = "samchu0619";
    const string port = "3306";
    const string sslM = "none";

    private static YoloV5 YOLO;
    private static MySqlConnection conn;

    
    public static void StartListening()
    {
        string connString = String.Format("server={0}; port={1};user id={2}; password={3}; database={4}; SslMode={5}", server, port, user, password, database, sslM);
        
        YOLO = new YoloV5();
        YOLO.InitModel();

        IPAddress ipAddress = IPAddress.Parse(_ipAddress);
        IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11000);
        Console.WriteLine("Server IP : {0}", localEndPoint);

        // Create a TCP/IP socket. 
        Socket listener = new Socket(ipAddress.AddressFamily,
        SocketType.Stream, ProtocolType.Tcp);

        try
        {
            listener.Bind(localEndPoint);
            listener.Listen(1);
            
            while (true)
            {
                Console.WriteLine("Waiting for connection...");
                Socket handler = listener.Accept();
                Console.WriteLine("Success Connect");
                conn = new MySqlConnection(connString);
                conn.Open();
                Console.WriteLine("Connect To MySQL Successfully!");

                byte[] msg;
                byte[] buffer = new Byte[BUFSIZE];
                System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

                while (true)
                {
                    try
                    {
                        int bytesRec = handler.Receive(buffer);
                        if (bytesRec <= 0)
                            break;
                        Console.WriteLine($"size: {bytesRec}");
                        sw.Reset();//碼表歸零
                        sw.Start();//碼表開始計時

                        var bitmap = ByteArrayToImage(buffer);
                        if (bitmap == null)
                        {
                            Console.WriteLine("Upload Bitmap is NULL");
                            byte[] errMsg = Encoding.UTF8.GetBytes("Error");
                            handler.Send(errMsg);
                            continue;
                        }

                        var yoloResult = YOLO.predictBitmap(bitmap);
                        var ResultJson = ParseResult(bitmap, yoloResult);

                        if (ResultJson != null)
                        {
                            msg = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(ResultJson));
                            Console.WriteLine(JsonConvert.SerializeObject(ResultJson));
                            var ResultMat = BitmapConverter.ToMat(bitmap);
                            Cv2.ImShow("Hololens", ResultMat);
                            var key = Cv2.WaitKey(30);
                        }
                        else
                        {
                            Console.WriteLine("ResultJson is NULL!  PredictBitmap Error!");
                            msg = Encoding.UTF8.GetBytes("Error");
                        }
                        /*var ResultMat = BitmapConverter.ToMat(bitmap);
                        Cv2.ImShow("Hololens", ResultMat);
                        var key = Cv2.WaitKey(30);
                        msg = Encoding.UTF8.GetBytes("Error");*/
                        handler.Send(msg);
                        Console.WriteLine(" ");
                        Console.WriteLine("- - - - - - - - - - ");
                        Console.WriteLine(" ");
                        sw.Stop();//碼錶停止
                        Console.WriteLine("time: " + sw.Elapsed.TotalMilliseconds.ToString() + " ms");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in while loop: {ex.Message} ");
                        break;
                    }
                }

                // Echo the data back to the client.
                Console.WriteLine("");
                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
                conn.Close();
                Console.WriteLine("Close Connection To MySQL");
                Console.WriteLine("\n---------------------------------\n\n   Close Connection To Client \n\n---------------------------------\n");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Server Error: " + e.ToString());
        }
        Console.WriteLine("\nPress ENTER to continue...");
        Console.Read();
    }

    protected static Bitmap ByteArrayToImage(byte[] arr)
    {
        try
        {
            MemoryStream ms = new MemoryStream(arr);
            Bitmap bmp = new Bitmap(ms);
            //bmp.Save(Path.Combine(inputFolder, "input.jpg"), System.Drawing.Imaging.ImageFormat.Jpeg);
            ms.Close();
            return bmp;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
            return null;
        }
    }

    protected static JObject ParseResult(Bitmap bitmap, IReadOnlyList<YoloResult> results)
    {
        try
        {
            JObject yoloResult = new JObject();
            JArray Bboxes = new JArray();

            using (var g = Graphics.FromImage(bitmap))
            {
                yoloResult.Add("BoxNum", results.Count);
                foreach (var res in results)
                {
                    JObject Bbox = new JObject();

                    var x1 = res.BBox[0];
                    var y1 = res.BBox[1];
                    var x2 = res.BBox[2];
                    var y2 = res.BBox[3];

                    Bbox.Add("x", x1);
                    Bbox.Add("y", y1);
                    Bbox.Add("width", x2 - x1);
                    Bbox.Add("height", y2 - y1);
                    Bbox.Add("confidence", res.Confidence);
                    Bbox.Add("class", res.Label);
                    /*
                    string sql = $"SELECT * FROM commodity WHERE class=\'{res.Label}\'";
                    MySqlCommand cmd = new MySqlCommand(sql, conn);
                    using (MySqlDataReader data = cmd.ExecuteReader())
                    {
                        if (data.HasRows)
                        {
                            data.Read();
                            Bbox.Add("name", data["name"].ToString());
                            Bbox.Add("symbol", data["symbol"].ToString());
                            Bbox.Add("price", data["price"].ToString());
                            Bbox.Add("ingredient", data["ingredient"].ToString());
                            Bbox.Add("manufacturer", data["manufacturer"].ToString());
                            Bbox.Add("calorie", data["calorie"].ToString());
                            Bbox.Add("note", data["note"].ToString());
                        }
                    }*/
                    Bboxes.Add(Bbox);

                    g.DrawRectangle(Pens.Red, x1, y1, x2 - x1, y2 - y1);
                    using (var brushes = new SolidBrush(Color.FromArgb(50, Color.Red)))
                    {
                        g.FillRectangle(brushes, x1, y1, x2 - x1, y2 - y1);
                    }

                    g.DrawString(res.Label + " " + res.Confidence.ToString("0.00"),
                                    new Font("Arial", 12), Brushes.Blue, new PointF(x1, y1));
                    Console.WriteLine($"Label: {res.Label}, Confidence: {res.Confidence.ToString("0.00")}");
                }
                yoloResult.Add("Bbox", Bboxes);
            }
            return yoloResult;
        }
        catch (MySqlException e)
        {
            Console.WriteLine("MySQL Error: " + e.Message);
            return null;
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex.Message);
            return null;
        }
    }

    public static int Main(String[] args)
    {
        StartListening();
        return 0;
    }
}
