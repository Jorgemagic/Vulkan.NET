using System.Windows.Forms;
using WaveEngine.Bindings.Vulkan;

namespace HelloTriangle
{
    public unsafe partial class HelloTriangle
    {
        const uint WIDTH = 800;
        const uint HEIGHT = 600;       

        private Form window;

        private void InitWindow()
        {
            window = new Form();
            window.Text = "Vulkan";
            window.Size = new System.Drawing.Size((int)WIDTH, (int)HEIGHT);
            window.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            window.Show();
        }

        private void InitVulkan()
        {
            this.CreateInstance();

            this.SetupDebugMessenger();

            this.CreateSurface();

            this.PickPhysicalDevice();

            this.CreateLogicalDevice();

            this.CreateSwapChain();

            this.CreateImageViews();

            this.CreateGraphicsPipeline();
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
            foreach (var imageView in this.swapChainImageViews)
            {
                VulkanNative.vkDestroyImageView(device, imageView, null);
            }

            VulkanNative.vkDestroySwapchainKHR(device, swapChain, null);

            VulkanNative.vkDestroyDevice(device, null);

            this.DestroyDebugMessenger();

            VulkanNative.vkDestroySurfaceKHR(instance, surface, null);

            VulkanNative.vkDestroyInstance(instance, null);

            window.Dispose();
            window.Close();
        }

        public void Run()
        {
            this.InitWindow();

            this.InitVulkan();

            this.MainLoop();

            this.CleanUp();
        }   
    }
}
