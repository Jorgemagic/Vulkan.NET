using System;
using System.Runtime.InteropServices;
using WaveEngine.Bindings.Vulkan;

namespace HelloTriangle
{
    public unsafe partial class HelloTriangle
    {
        string[] deviceExtensions = new string[] {
            "VK_KHR_swapchain",
            "VK_KHR_ray_tracing",
            "VK_KHR_pipeline_library",
            "VK_KHR_deferred_host_operations",
        };

        private VkDevice device;
        private VkQueue graphicsQueue;

        private void CreateLogicalDevice()
        {
            QueueFamilyIndices indices = this.FindQueueFamilies(physicalDevice);

            float queuePriority = 1.0f;
            VkDeviceQueueCreateInfo queueCreateInfo = new VkDeviceQueueCreateInfo()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO,
                queueFamilyIndex = indices.graphicsFamily.Value,
                queueCount = 1,
                pQueuePriorities = &queuePriority,
            };

            VkPhysicalDeviceFeatures deviceFeatures = default;           

            // Raytracing extensions
            VkPhysicalDeviceRayTracingFeaturesKHR deviceRayTracingFeatures = new VkPhysicalDeviceRayTracingFeaturesKHR()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_RAY_TRACING_FEATURES_KHR,
                pNext = null,
                rayTracing = true,
            };

            VkPhysicalDeviceVulkan12Features deviceVulkan12Features = new VkPhysicalDeviceVulkan12Features()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_2_FEATURES,
                pNext = &deviceRayTracingFeatures,
                bufferDeviceAddress = true,
            };

            int deviceExtensionsCount = deviceExtensions.Length;
            IntPtr* deviceExtensionsArray = stackalloc IntPtr[deviceExtensionsCount];
            for (int i = 0; i < deviceExtensionsCount; i++)
            {
                string extension = deviceExtensions[i];
                deviceExtensionsArray[i] = Marshal.StringToHGlobalAnsi(extension);
            }

            VkDeviceCreateInfo createInfo = new VkDeviceCreateInfo()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO,
                pNext = &deviceVulkan12Features,
                pQueueCreateInfos = &queueCreateInfo,
                queueCreateInfoCount = 1,
                pEnabledFeatures = &deviceFeatures,
                enabledExtensionCount = (uint)deviceExtensions.Length,
                ppEnabledExtensionNames = (byte**)deviceExtensionsArray,
            };

            fixed (VkDevice* devicePtr = &device)
            {
                Helpers.CheckErrors(VulkanNative.vkCreateDevice(physicalDevice, &createInfo, null, devicePtr));
            }

            fixed (VkQueue* graphicsQueuePtr = &graphicsQueue)
            {
                VulkanNative.vkGetDeviceQueue(device, indices.graphicsFamily.Value, 0, graphicsQueuePtr);
            }
        }
    }
}
