using System.Windows.Forms;
using WaveEngine.Bindings.Vulkan;

namespace HelloTriangle
{
    public unsafe partial class HelloTriangle
    {
        const uint WIDTH = 800;
        const uint HEIGHT = 600;       

        private Form window;

        private void InitVulkan()
        {
            this.CreateInstance();
            this.SetupDebugMessenger();
            this.PickPhysicalDevice();
            this.CreateLogicalDevice();

            window = new Form();
            window.Text = "Vulkan";
            window.Size = new System.Drawing.Size((int)WIDTH, (int)HEIGHT);
            window.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            window.Show();
        }

        private void MainLoop()
        {
            bool isClosing = false;
            window.FormClosing += (s, e) =>
            {
                isClosing = true;
            };

            while (!isClosing)
            {
                Application.DoEvents();
            }
        }

        private void CleanUp()
        {
            VulkanNative.vkDestroyDevice(device, null);
            this.DestroyDebugMessenger();
            VulkanNative.vkDestroyInstance(instance, null);

            window.Dispose();
            window.Close();
        }

        public void Run()
        {
            this.InitVulkan();
            this.MainLoop();
            this.CleanUp();
        }   
    }
}
