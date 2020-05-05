using System;
using System.Collections.Generic;
using System.Text;
using WaveEngine.Bindings.Vulkan;

namespace HelloTriangle
{
    public unsafe partial class HelloTriangle
    {
        public struct SwapChainSupportDetails
        {
            public VkSurfaceCapabilitiesKHR capabilities;
            public VkSurfaceFormatKHR[] formats;
            public VkPresentModeKHR[] presentModes;
        }

        private SwapChainSupportDetails QuerySwapChainSupport(VkPhysicalDevice physicalDevice)
        {
            SwapChainSupportDetails details = default;

            // Capabilities
            Helpers.CheckErrors(VulkanNative.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(physicalDevice, surface, &details.capabilities));

            // Formats
            uint formatCount;
            Helpers.CheckErrors(VulkanNative.vkGetPhysicalDeviceSurfaceFormatsKHR(physicalDevice, surface, &formatCount, null));

            if (formatCount != 0)
            {
                details.formats = new VkSurfaceFormatKHR[formatCount];
                fixed (VkSurfaceFormatKHR* formatsPtr = &details.formats[0])
                {
                    Helpers.CheckErrors(VulkanNative.vkGetPhysicalDeviceSurfaceFormatsKHR(physicalDevice, surface, &formatCount, formatsPtr));
                }
            }

            // Present Modes
            uint presentModeCount;
            Helpers.CheckErrors(VulkanNative.vkGetPhysicalDeviceSurfacePresentModesKHR(physicalDevice, surface, &presentModeCount, null));

            if (presentModeCount != 0)
            {
                details.presentModes = new VkPresentModeKHR[presentModeCount];
                fixed (VkPresentModeKHR* presentModesPtr = &details.presentModes[0])
                {
                    Helpers.CheckErrors(VulkanNative.vkGetPhysicalDeviceSurfacePresentModesKHR(physicalDevice, surface, &presentModeCount, presentModesPtr));
                }
            }

            return details;
        }
    }
}
