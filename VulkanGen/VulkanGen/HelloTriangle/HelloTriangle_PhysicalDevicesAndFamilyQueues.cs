using System;
using WaveEngine.Bindings.Vulkan;

namespace HelloTriangle
{
    public unsafe partial class HelloTriangle
    {
        private VkPhysicalDevice physicalDevice;

        private void PickPhysicalDevice()
        {
            uint deviceCount = 0;
            Helpers.CheckErrors(VulkanNative.vkEnumeratePhysicalDevices(instance, &deviceCount, null));
            if (deviceCount == 0)
            {
                throw new Exception("Failed to find GPUs with Vulkan support!");
            }

            VkPhysicalDevice* devices = stackalloc VkPhysicalDevice[(int)deviceCount];
            Helpers.CheckErrors(VulkanNative.vkEnumeratePhysicalDevices(instance, &deviceCount, devices));

            for (int i = 0; i < deviceCount; i++)
            {
                var device = devices[i];
                if (this.IsPhysicalDeviceSuitable(device))
                {
                    this.physicalDevice = device;
                    break;
                }
            }

            if (this.physicalDevice == default)
            {
                throw new Exception("failed to find a suitable GPU!");
            }
        }

        private bool IsPhysicalDeviceSuitable(VkPhysicalDevice device)
        {
            // acquire Raytracing features
            VkPhysicalDeviceRayTracingFeaturesKHR rayTracingFeatures = new VkPhysicalDeviceRayTracingFeaturesKHR()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_RAY_TRACING_FEATURES_KHR,
                pNext = null,
            };

            VkPhysicalDeviceFeatures2 deviceFeatures2 = new VkPhysicalDeviceFeatures2()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_FEATURES_2,
                pNext = &rayTracingFeatures,
            };
            VulkanNative.vkGetPhysicalDeviceFeatures2(device, &deviceFeatures2);

            return rayTracingFeatures.rayTracing;
        }

        public struct QueueFamilyIndices
        {
            public uint? graphicsFamily;

            public bool IsComplete()
            {
                return graphicsFamily.HasValue;
            }
        }

        private QueueFamilyIndices FindQueueFamilies(VkPhysicalDevice physicalDevice)
        {
            QueueFamilyIndices indices = default;

            uint queueFamilyCount = 0;
            VulkanNative.vkGetPhysicalDeviceQueueFamilyProperties(physicalDevice, &queueFamilyCount, null);

            VkQueueFamilyProperties* queueFamilies = stackalloc VkQueueFamilyProperties[(int)queueFamilyCount];
            VulkanNative.vkGetPhysicalDeviceQueueFamilyProperties(physicalDevice, &queueFamilyCount, queueFamilies);

            for (uint i = 0; i < queueFamilyCount; i++)
            {
                var queueFamily = queueFamilies[i];
                if ((queueFamily.queueFlags & VkQueueFlagBits.VK_QUEUE_GRAPHICS_BIT) == 0)
                {
                    indices.graphicsFamily = i;
                }

                if (indices.IsComplete())
                {
                    break;
                }
            }

            return indices;
        }

        private bool IsDeviceSuitable(VkPhysicalDevice physicalDevice)
        {
            QueueFamilyIndices indices = this.FindQueueFamilies(physicalDevice);

            return indices.IsComplete();
        }
    }
}
