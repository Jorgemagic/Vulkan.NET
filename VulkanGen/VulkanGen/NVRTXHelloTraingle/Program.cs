using System;
using System.Diagnostics;

namespace NVRTXHelloTriangle
{
    class Program
    {
        static void Main(string[] args)
        {
            NVRTXHelloTriangle app = new NVRTXHelloTriangle();

            try
            {
                app.Run();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.StackTrace);
            }
        }
    }
}
