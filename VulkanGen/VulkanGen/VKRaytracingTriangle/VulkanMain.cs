using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VulkanRaytracing.Structs;
using WaveEngine.Bindings.Vulkan;

namespace VulkanRaytracing
{
    public unsafe class VulkanMain
    {
        private const int VK_NULL_HANDLE = 0;

        VkDevice device;
        VkInstance instance;
        VkPhysicalDevice physicalDevice;

        VkQueue queue;
        VkCommandPool commandPool;

        VkPipeline pipeline;
        VkPipelineLayout pipelineLayout;

        VkDescriptorSet descriptorSet;
        VkDescriptorPool descriptorPool;
        VkDescriptorSetLayout descriptorSetLayout;

        VkSurfaceKHR surface;
        VkSwapchainKHR swapchain;

        VkSemaphore semaphoreImageAvailable;
        VkSemaphore semaphoreRenderingAvailable;

        VkImage offscreenBuffer;
        VkImageView offscreenBufferView;
        VkDeviceMemory offscreenBufferMemory;

        AccelerationMemory shaderBindingTable;
        uint shaderBindingTableSize = 0;
        uint shaderBindingTableGroupCount = 3;

        VkAccelerationStructureKHR bottomLevelAS;
        ulong bottomLevelASHandle = 0;
        VkAccelerationStructureKHR topLevelAS;
        ulong topLevelASHandle = 0;

        uint desiredWindowWidth = 640;
        uint desiredWindowHeight = 480;
        VkFormat desiredSurfaceFormat = VkFormat.VK_FORMAT_B8G8R8A8_UNORM;

        Form window;

        VkCommandBuffer[] commandBuffers;
        private VkPhysicalDeviceRayTracingPropertiesKHR rayTracingProperties;

        string appName = "VK KHR Raytracing Vulkan Triangle";

        string[] instanceExtensions = new string[] {
            "VK_KHR_surface",
            "VK_KHR_win32_surface",
            "VK_KHR_get_physical_device_properties2",
            "VK_EXT_debug_utils",
        };

        string[] validationLayers = new string[] {
            "VK_LAYER_KHRONOS_validation"
        };

        string[] deviceExtensions = new string[] {
            "VK_KHR_swapchain",
            "VK_KHR_ray_tracing",
            "VK_KHR_pipeline_library",
            "VK_EXT_descriptor_indexing",
            "VK_KHR_buffer_device_address",
            "VK_KHR_deferred_host_operations",
            "VK_KHR_get_memory_requirements2",
        };

        public VkShaderModule CreateShaderModule(byte[] code)
        {
            VkShaderModule shaderModule;
            VkShaderModuleCreateInfo shaderModuleInfo = new VkShaderModuleCreateInfo();

            fixed (byte* sourcePointer = code)
            {
                shaderModuleInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_SHADER_MODULE_CREATE_INFO;
                shaderModuleInfo.pNext = null;
                shaderModuleInfo.codeSize = (UIntPtr)code.Length;
                shaderModuleInfo.pCode = (uint*)sourcePointer;

                var result = VulkanNative.vkCreateShaderModule(device, &shaderModuleInfo, null, &shaderModule);
                Helpers.CheckErrors(result);
            }

            return shaderModule;
        }

        public uint FindMemoryType(uint typeFilter, VkMemoryPropertyFlagBits properties)
        {
            VkPhysicalDeviceMemoryProperties memProperties;
            VulkanNative.vkGetPhysicalDeviceMemoryProperties(physicalDevice, &memProperties);
            for (int ii = 0; ii < memProperties.memoryTypeCount; ii++)
            {
                if (((typeFilter & (1 << ii)) != 0)
                    && (memProperties.GetMemoryType((uint)ii).propertyFlags & properties) == properties)
                {
                    return (uint)ii;
                }
            }
            throw new Exception("failed to find suitable memory type!");
        }

        public ulong GetBufferAddress(VkBuffer buffer)
        {
            VkBufferDeviceAddressInfo bufferAddressInfo = new VkBufferDeviceAddressInfo()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_DEVICE_ADDRESS_INFO,
                pNext = null,
                buffer = buffer,
            };

            return VulkanNative.vkGetBufferDeviceAddress(device, &bufferAddressInfo);
        }

        public AccelerationMemory CreateMappedBuffer<T>(T[] srcData, uint byteLength)
            where T : struct
        {
            GCHandle gcHandle = GCHandle.Alloc(srcData, GCHandleType.Pinned);
            IntPtr srcDataPtr = gcHandle.AddrOfPinnedObject();
            AccelerationMemory accelerationMemory = new AccelerationMemory();

            // Buffer description
            VkBufferCreateInfo bufferInfo = new VkBufferCreateInfo()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO,
                pNext = null,
                size = byteLength,
                usage = VkBufferUsageFlagBits.VK_BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT,
                sharingMode = VkSharingMode.VK_SHARING_MODE_EXCLUSIVE,
                queueFamilyIndexCount = 0,
                pQueueFamilyIndices = null,
            };
            var result = VulkanNative.vkCreateBuffer(device, &bufferInfo, null, &accelerationMemory.buffer);
            Helpers.CheckErrors(result);

            VkMemoryRequirements memoryRequirements = new VkMemoryRequirements();
            VulkanNative.vkGetBufferMemoryRequirements(device, accelerationMemory.buffer, &memoryRequirements);

            VkMemoryAllocateFlagsInfo memAllocFlagsInfo = new VkMemoryAllocateFlagsInfo()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_FLAGS_INFO,
                pNext = null,
                flags = VkMemoryAllocateFlagBits.VK_MEMORY_ALLOCATE_DEVICE_ADDRESS_BIT,
                deviceMask = 0,
            };

            VkMemoryAllocateInfo memAllocInfo = new VkMemoryAllocateInfo()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO,
                pNext = &memAllocFlagsInfo,
                allocationSize = memoryRequirements.size,
                memoryTypeIndex = FindMemoryType(memoryRequirements.memoryTypeBits, VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT | VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_HOST_COHERENT_BIT),
            };

            result = VulkanNative.vkAllocateMemory(device, &memAllocInfo, null, &accelerationMemory.memory);
            Helpers.CheckErrors(result);

            result = VulkanNative.vkBindBufferMemory(device, accelerationMemory.buffer, accelerationMemory.memory, 0);
            Helpers.CheckErrors(result);

            accelerationMemory.memoryAddress = GetBufferAddress(accelerationMemory.buffer);

            void* dstData;
            result = VulkanNative.vkMapMemory(device, accelerationMemory.memory, 0, byteLength, 0, &dstData);
            Helpers.CheckErrors(result);


            Unsafe.CopyBlock((void*)dstData, (void*)srcDataPtr, byteLength);

            VulkanNative.vkUnmapMemory(device, accelerationMemory.memory);
            accelerationMemory.mappedPointer = dstData;

            /*IntPtr dataPtr;
            result = VulkanNative.vkMapMemory(device, accelerationMemory.memory, 0, byteLength, 0, (void**)&dataPtr);
            Helpers.CheckErrors(result);

            for (int i = 0; i < (byteLength / sizeof(uint)); i++)
            {
                uint* pointer = (uint*)(dataPtr + (i * sizeof(uint)));
              
            }            

            VulkanNative.vkUnmapMemory(device, accelerationMemory.memory);*/
            gcHandle.Free();

            return accelerationMemory;
        }

        public VkMemoryRequirements GetAccelerationStructureMemoryRequirements(VkAccelerationStructureKHR acceleration, VkAccelerationStructureMemoryRequirementsTypeKHR type)
        {
            VkMemoryRequirements2 memoryRequirements2 = new VkMemoryRequirements2()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_REQUIREMENTS_2,
            };

            VkAccelerationStructureMemoryRequirementsInfoKHR accelerationMemoryRequirements = new VkAccelerationStructureMemoryRequirementsInfoKHR()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_MEMORY_REQUIREMENTS_INFO_KHR,
                pNext = null,
                type = type,
                buildType = VkAccelerationStructureBuildTypeKHR.VK_ACCELERATION_STRUCTURE_BUILD_TYPE_DEVICE_KHR,
                accelerationStructure = acceleration,
            };


            VulkanNative.vkGetAccelerationStructureMemoryRequirementsKHR(device, &accelerationMemoryRequirements, &memoryRequirements2);

            return memoryRequirements2.memoryRequirements;
        }

        public void BindAccelerationMemory(VkAccelerationStructureKHR acceleration, VkDeviceMemory memory)
        {
            VkBindAccelerationStructureMemoryInfoKHR accelerationMemoryBindInfo = new VkBindAccelerationStructureMemoryInfoKHR()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_BIND_ACCELERATION_STRUCTURE_MEMORY_INFO_KHR,
                pNext = null,
                accelerationStructure = acceleration,
                memory = memory,
                memoryOffset = 0,
                deviceIndexCount = 0,
                pDeviceIndices = null,
            };

            var result = VulkanNative.vkBindAccelerationStructureMemoryKHR(device, 1, &accelerationMemoryBindInfo);
            Helpers.CheckErrors(result);
        }

        public AccelerationMemory CreateAccelerationScratchBuffer(VkAccelerationStructureKHR acceleration, VkAccelerationStructureMemoryRequirementsTypeKHR type)
        {
            AccelerationMemory accelerationMemory = new AccelerationMemory();

            VkMemoryRequirements asRequirements = this.GetAccelerationStructureMemoryRequirements(acceleration, type);

            VkBufferCreateInfo bufferInfo = new VkBufferCreateInfo()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO,
                pNext = null,
                size = asRequirements.size,
                usage = VkBufferUsageFlagBits.VK_BUFFER_USAGE_RAY_TRACING_BIT_KHR | VkBufferUsageFlagBits.VK_BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT,
                sharingMode = VkSharingMode.VK_SHARING_MODE_EXCLUSIVE,
                queueFamilyIndexCount = 0,
                pQueueFamilyIndices = null,
            };
            var result = VulkanNative.vkCreateBuffer(device, &bufferInfo, null, &accelerationMemory.buffer);
            Helpers.CheckErrors(result);

            VkMemoryRequirements bufRequirements = new VkMemoryRequirements();
            VulkanNative.vkGetBufferMemoryRequirements(device, accelerationMemory.buffer, &bufRequirements);

            // buffer requirements can differ to AS requirements, so we max them
            ulong alloctionSize = asRequirements.size > bufRequirements.size ? asRequirements.size : bufRequirements.size;
            // combine AS and buf mem types
            uint allocationMemoryBits = bufRequirements.memoryTypeBits | asRequirements.memoryTypeBits;

            VkMemoryAllocateFlagsInfo memAllocFlagsInfo = new VkMemoryAllocateFlagsInfo()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_FLAGS_INFO,
                pNext = null,
                flags = VkMemoryAllocateFlagBits.VK_MEMORY_ALLOCATE_DEVICE_ADDRESS_BIT,
                deviceMask = 0,
            };

            VkMemoryAllocateInfo memAllocInfo = new VkMemoryAllocateInfo()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO,
                pNext = &memAllocFlagsInfo,
                allocationSize = alloctionSize,
                memoryTypeIndex = FindMemoryType(allocationMemoryBits, VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT),
            };
            result = VulkanNative.vkAllocateMemory(device, &memAllocInfo, null, &accelerationMemory.memory);
            Helpers.CheckErrors(result);

            result = VulkanNative.vkBindBufferMemory(device, accelerationMemory.buffer, accelerationMemory.memory, 0);
            Helpers.CheckErrors(result);

            accelerationMemory.memoryAddress = GetBufferAddress(accelerationMemory.buffer);

            return accelerationMemory;
        }

        public void InsertCommandImageBarrier(VkCommandBuffer commandBuffer,
                               VkImage image,
                               VkAccessFlagBits srcAccessMask,
                               VkAccessFlagBits dstAccessMask,
                               VkImageLayout oldLayout,
                               VkImageLayout newLayout,
                               VkImageSubresourceRange subresourceRange)
        {
            VkImageMemoryBarrier imageMemoryBarrier = new VkImageMemoryBarrier()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER,
                pNext = null,
                srcAccessMask = srcAccessMask,
                dstAccessMask = dstAccessMask,
                oldLayout = oldLayout,
                newLayout = newLayout,
                srcQueueFamilyIndex = VulkanNative.VK_QUEUE_FAMILY_IGNORED,
                dstQueueFamilyIndex = VulkanNative.VK_QUEUE_FAMILY_IGNORED,
                image = image,
                subresourceRange = subresourceRange,
            };

            VulkanNative.vkCmdPipelineBarrier(commandBuffer, VkPipelineStageFlagBits.VK_PIPELINE_STAGE_ALL_COMMANDS_BIT,
                                 VkPipelineStageFlagBits.VK_PIPELINE_STAGE_ALL_COMMANDS_BIT, 0, 0, null, 0, null, 1,
                                 &imageMemoryBarrier);
        }

        public bool IsValidationLayerAvailable(string layerName)
        {
            uint propertyCount = 0;
            VulkanNative.vkEnumerateInstanceLayerProperties(&propertyCount, null);
            VkLayerProperties* properties = stackalloc VkLayerProperties[(int)propertyCount];
            var result = VulkanNative.vkEnumerateInstanceLayerProperties(&propertyCount, properties);
            Helpers.CheckErrors(result);

            // loop through all toggled layers and check if we can enable each
            for (uint ii = 0; ii < propertyCount; ++ii)
            {
                string pLayerName = Helpers.GetString(properties[ii].layerName);
                if (layerName.Equals(pLayerName))
                    return true;
            }

            return false;
        }

        public int Main()
        {
            window = new Form();
            window.Text = appName;
            window.Size = new System.Drawing.Size((int)desiredWindowWidth, (int)desiredWindowHeight);
            window.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            window.Show();

            // check which validation layers are available
            List<string> availableValidationLayers = new List<string>();
            for (uint ii = 0; ii < validationLayers.Length; ++ii)
            {
                if (IsValidationLayerAvailable(validationLayers[ii]))
                {
                    availableValidationLayers.Add(validationLayers[ii]);
                }
                else
                {
                    Debug.WriteLine($"Ignoring layer {validationLayers[ii]} as it is unavailable");
                }
            };

            VkApplicationInfo appInfo = new VkApplicationInfo()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_APPLICATION_INFO,
                pNext = null,
                pApplicationName = appName.ToPointer(),
                applicationVersion = Helpers.Version(1, 0, 0),
                pEngineName = "No Engine".ToPointer(),
                engineVersion = Helpers.Version(1, 0, 0),
                apiVersion = Helpers.Version(1, 2, 0),
            };

            int layersCount = availableValidationLayers.Count;
            IntPtr* layersToEnableArray = stackalloc IntPtr[layersCount];
            for (int i = 0; i < layersCount; i++)
            {
                string layer = availableValidationLayers[i];
                layersToEnableArray[i] = Marshal.StringToHGlobalAnsi(layer);
            }

            int extensionsCount = instanceExtensions.Length;
            IntPtr* extensionsToEnableArray = stackalloc IntPtr[extensionsCount];
            for (int i = 0; i < extensionsCount; i++)
            {
                string extension = instanceExtensions[i];
                extensionsToEnableArray[i] = Marshal.StringToHGlobalAnsi(extension);
            }

            VkInstanceCreateInfo createInfo = new VkInstanceCreateInfo()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO,
                pNext = null,
                pApplicationInfo = &appInfo,
                enabledExtensionCount = (uint)instanceExtensions.Length,
                ppEnabledExtensionNames = (byte**)extensionsToEnableArray,
                enabledLayerCount = (uint)availableValidationLayers.Count,
                ppEnabledLayerNames = (byte**)layersToEnableArray,
            };
            VkResult result;
            fixed (VkInstance* instancePtr = &instance)
            {
                result = VulkanNative.vkCreateInstance(&createInfo, null, instancePtr);
                Helpers.CheckErrors(result);
            }

            this.SetupDebugMessenger();

            uint deviceCount = 0;
            result = VulkanNative.vkEnumeratePhysicalDevices(instance, &deviceCount, null);
            Helpers.CheckErrors(result);

            if (deviceCount <= 0)
            {
                Debug.WriteLine("No physical devices available");
                return 1;
            }

            VkPhysicalDevice[] devices = new VkPhysicalDevice[deviceCount];
            fixed (VkPhysicalDevice* devicesPtr = devices)
            {
                result = VulkanNative.vkEnumeratePhysicalDevices(instance, &deviceCount, devicesPtr);
                Helpers.CheckErrors(result);
            }

            // find RT compatible device
            for (uint ii = 0; ii < devices.Length; ++ii)
            {
                // acquire RT features
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
                VulkanNative.vkGetPhysicalDeviceFeatures2(devices[ii], &deviceFeatures2);

                if (rayTracingFeatures.rayTracing == true)
                {
                    physicalDevice = devices[ii];
                    break;
                }
            };

            if (physicalDevice == null)
            {
                Debug.WriteLine("No ray tracing compatible GPU found");
            }

            VkPhysicalDeviceProperties deviceProperties = new VkPhysicalDeviceProperties();
            VulkanNative.vkGetPhysicalDeviceProperties(physicalDevice, &deviceProperties);
            Debug.WriteLine($"GPU: {Marshal.PtrToStringAnsi((IntPtr)deviceProperties.deviceName)}");

            float queuePriority = 0.0f;

            VkDeviceQueueCreateInfo deviceQueueInfo = new VkDeviceQueueCreateInfo()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO,
                pNext = null,
                queueFamilyIndex = 0,
                queueCount = 1,
                pQueuePriorities = &queuePriority,
            };

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

            VkPhysicalDeviceFeatures deviceFeatures = default;
            VkDeviceCreateInfo deviceInfo = new VkDeviceCreateInfo()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO,
                pNext = &deviceVulkan12Features,
                queueCreateInfoCount = 1,
                pQueueCreateInfos = &deviceQueueInfo,
                enabledExtensionCount = (uint)deviceExtensions.Length,
                ppEnabledExtensionNames = (byte**)deviceExtensionsArray,
                pEnabledFeatures = &deviceFeatures,
            };

            fixed (VkDevice* devicePtr = &device)
            {
                result = VulkanNative.vkCreateDevice(physicalDevice, &deviceInfo, null, devicePtr);
                Helpers.CheckErrors(result);
            }

            fixed (VkQueue* queuePtr = &queue)
            {
                VulkanNative.vkGetDeviceQueue(device, 0, 0, queuePtr);
            }

            VkWin32SurfaceCreateInfoKHR surfaceCreateInfo = new VkWin32SurfaceCreateInfoKHR()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_WIN32_SURFACE_CREATE_INFO_KHR,
                pNext = null,
                flags = 0,
                hinstance = Process.GetCurrentProcess().Handle,
                hwnd = window.Handle,
            };

            fixed (VkSurfaceKHR* surfacePtr = &surface)
            {
                result = VulkanNative.vkCreateWin32SurfaceKHR(instance, &surfaceCreateInfo, null, surfacePtr);
                Helpers.CheckErrors(result);
            }

            VkBool32 surfaceSupport = false;
            result = VulkanNative.vkGetPhysicalDeviceSurfaceSupportKHR(physicalDevice, 0, surface, &surfaceSupport);
            Helpers.CheckErrors(result);

            if (!surfaceSupport)
            {
                Debug.WriteLine("No surface rendering support");
                return 1;
            }

            VkCommandPoolCreateInfo cmdPoolInfo = new VkCommandPoolCreateInfo()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO,
                pNext = null,
                flags = 0,
                queueFamilyIndex = 0,
            };

            fixed (VkCommandPool* commandPoolPtr = &commandPool)
            {
                result = VulkanNative.vkCreateCommandPool(device, &cmdPoolInfo, null, commandPoolPtr);
                Helpers.CheckErrors(result);
            }

            // acquire RT properties
            rayTracingProperties = new VkPhysicalDeviceRayTracingPropertiesKHR()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_RAY_TRACING_PROPERTIES_KHR,
                pNext = null,
            };

            VkPhysicalDeviceProperties2 deviceProperties2;
            fixed (VkPhysicalDeviceRayTracingPropertiesKHR* rayTracingPropertiesPtr = &rayTracingProperties)
            {
                deviceProperties2 = new VkPhysicalDeviceProperties2()
                {
                    sType = VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_PROPERTIES_2,
                    pNext = rayTracingPropertiesPtr,
                };
            }

            VulkanNative.vkGetPhysicalDeviceProperties2(physicalDevice, &deviceProperties2);

            // create bottom-level container
            this.CreateBottomLevelContainer();

            // create top-level container
            this.CreateTopLevelContainer();

            // offscreen buffer
            this.OffscreenBuffer();

            // rt descriptor set layout
            this.RTDescriptorSetLayout();

            // rt descriptor set
            this.RTDescriptorSet();

            // rt pipeline layout
            this.RTPipelineLayout();

            // rt pipeline
            this.RTPipeline();

            // shader binding table
            this.ShaderBindingTable();

            Debug.WriteLine("Initializing Swapchain..");

            uint presentModeCount = 0;
            result = VulkanNative.vkGetPhysicalDeviceSurfacePresentModesKHR(physicalDevice, surface, &presentModeCount, null);
            Helpers.CheckErrors(result);

            VkPresentModeKHR[] presentModes = new VkPresentModeKHR[presentModeCount];
            fixed (VkPresentModeKHR* presentModesPtr = &presentModes[0])
            {
                result = VulkanNative.vkGetPhysicalDeviceSurfacePresentModesKHR(physicalDevice, surface, &presentModeCount, presentModesPtr);
                Helpers.CheckErrors(result);
            }

            bool isMailboxModeSupported = presentModes.Any(m => m == VkPresentModeKHR.VK_PRESENT_MODE_MAILBOX_KHR);

            VkSurfaceCapabilitiesKHR capabilitiesKHR;
            result = VulkanNative.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(physicalDevice, surface, &capabilitiesKHR);
            Helpers.CheckErrors(result);

            var extent = ChooseSwapExtent(capabilitiesKHR);
            VkSwapchainCreateInfoKHR swapchainInfo = new VkSwapchainCreateInfoKHR()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_SWAPCHAIN_CREATE_INFO_KHR,
                pNext = null,
                surface = surface,
                minImageCount = 3,
                imageFormat = desiredSurfaceFormat,
                imageColorSpace = VkColorSpaceKHR.VK_COLOR_SPACE_SRGB_NONLINEAR_KHR,
                imageExtent = extent, //new VkExtent2D(desiredWindowWidth, desiredWindowHeight),
                imageArrayLayers = 1,
                imageUsage = VkImageUsageFlagBits.VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT | VkImageUsageFlagBits.VK_IMAGE_USAGE_TRANSFER_DST_BIT,
                imageSharingMode = VkSharingMode.VK_SHARING_MODE_EXCLUSIVE,
                queueFamilyIndexCount = 0,
                preTransform = VkSurfaceTransformFlagBitsKHR.VK_SURFACE_TRANSFORM_IDENTITY_BIT_KHR,
                compositeAlpha = VkCompositeAlphaFlagBitsKHR.VK_COMPOSITE_ALPHA_OPAQUE_BIT_KHR,
                presentMode = isMailboxModeSupported ? VkPresentModeKHR.VK_PRESENT_MODE_MAILBOX_KHR : VkPresentModeKHR.VK_PRESENT_MODE_FIFO_KHR,
                clipped = true,
                oldSwapchain = default,
            };

            fixed (VkSwapchainKHR* swapchainPtr = &swapchain)
            {
                result = VulkanNative.vkCreateSwapchainKHR(device, &swapchainInfo, null, swapchainPtr);
                Helpers.CheckErrors(result);
            }

            uint amountOfImagesInSwapchain = 0;
            result = VulkanNative.vkGetSwapchainImagesKHR(device, swapchain, &amountOfImagesInSwapchain, null);
            Helpers.CheckErrors(result);

            VkImage* swapchainImages = stackalloc VkImage[] { amountOfImagesInSwapchain };

            result = VulkanNative.vkGetSwapchainImagesKHR(device, swapchain, &amountOfImagesInSwapchain, swapchainImages);
            Helpers.CheckErrors(result);

            VkImageView* imageViews = stackalloc VkImageView[] { amountOfImagesInSwapchain };


            for (uint ii = 0; ii < amountOfImagesInSwapchain; ++ii)
            {
                VkImageViewCreateInfo imageViewInfo = new VkImageViewCreateInfo()
                {
                    sType = VkStructureType.VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO,
                    pNext = null,
                    image = swapchainImages[ii],
                    viewType = VkImageViewType.VK_IMAGE_VIEW_TYPE_2D,
                    format = desiredSurfaceFormat,
                    subresourceRange = new VkImageSubresourceRange()
                    {
                        aspectMask = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
                        baseMipLevel = 0,
                        levelCount = 1,
                        baseArrayLayer = 0,
                        layerCount = 1,
                    },
                };

                result = VulkanNative.vkCreateImageView(device, &imageViewInfo, null, imageViews);
                Helpers.CheckErrors(result);
            };

            Debug.WriteLine("Recording frame commands..");

            VkImageCopy copyRegion = new VkImageCopy()
            {
                srcSubresource = new VkImageSubresourceLayers()
                {
                    aspectMask = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
                    mipLevel = 0,
                    baseArrayLayer = 0,
                    layerCount = 1,
                },
                dstSubresource = new VkImageSubresourceLayers()
                {
                    aspectMask = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
                    mipLevel = 0,
                    baseArrayLayer = 0,
                    layerCount = 1,
                },
                extent = new VkExtent3D() { depth = 1, width = swapchainInfo.imageExtent.width, height = swapchainInfo.imageExtent.height },
            };

            VkImageSubresourceRange subresourceRange = new VkImageSubresourceRange()
            {
                aspectMask = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
                baseMipLevel = 0,
                levelCount = 1,
                baseArrayLayer = 0,
                layerCount = 1,
            };

            VkStridedBufferRegionKHR rayGenSBT = new VkStridedBufferRegionKHR()
            {
                buffer = shaderBindingTable.buffer,
                offset = 0,
                stride = 0,
                size = shaderBindingTableSize,
            };

            VkStridedBufferRegionKHR rayMissSBT = new VkStridedBufferRegionKHR()
            {
                buffer = shaderBindingTable.buffer,
                offset = 2 * rayTracingProperties.shaderGroupHandleSize,
                stride = 0,
                size = shaderBindingTableSize,
            };

            VkStridedBufferRegionKHR rayHitSBT = new VkStridedBufferRegionKHR()
            {
                buffer = shaderBindingTable.buffer,
                offset = 1 * rayTracingProperties.shaderGroupHandleSize,
                stride = 0,
                size = shaderBindingTableSize,
            };

            VkStridedBufferRegionKHR rayCallSBT = new VkStridedBufferRegionKHR()
            {
                buffer = default,
                offset = 0,
                stride = 0,
                size = 0,
            };

            VkCommandBufferAllocateInfo commandBufferAllocateInfo = new VkCommandBufferAllocateInfo()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO,
                pNext = null,
                commandPool = commandPool,
                level = VkCommandBufferLevel.VK_COMMAND_BUFFER_LEVEL_PRIMARY,
                commandBufferCount = amountOfImagesInSwapchain,
            };

            commandBuffers = new VkCommandBuffer[amountOfImagesInSwapchain];
            fixed (VkCommandBuffer* commandBuffersPtr = &commandBuffers[0])
            {
                result = VulkanNative.vkAllocateCommandBuffers(device, &commandBufferAllocateInfo, commandBuffersPtr);
                Helpers.CheckErrors(result);
            }

            VkCommandBufferBeginInfo commandBufferBeginInfo = new VkCommandBufferBeginInfo()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO,
                pNext = null,
                flags = 0,
                pInheritanceInfo = null,
            };

            for (uint ii = 0; ii < amountOfImagesInSwapchain; ++ii)
            {
                VkCommandBuffer commandBuffer = commandBuffers[ii];
                VkImage swapchainImage = swapchainImages[ii];

                result = VulkanNative.vkBeginCommandBuffer(commandBuffer, &commandBufferBeginInfo);
                Helpers.CheckErrors(result);

                // transition offscreen buffer into shader writeable state
                InsertCommandImageBarrier(commandBuffer, offscreenBuffer, 0, VkAccessFlagBits.VK_ACCESS_SHADER_WRITE_BIT, VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED, VkImageLayout.VK_IMAGE_LAYOUT_GENERAL, subresourceRange);

                VulkanNative.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_RAY_TRACING_KHR, pipeline);
                fixed (VkDescriptorSet* descriptorSetPtr = &descriptorSet)
                {
                    VulkanNative.vkCmdBindDescriptorSets(commandBuffer, VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_RAY_TRACING_KHR, pipelineLayout, 0, 1, descriptorSetPtr, 0, (uint*)0);
                }

                VulkanNative.vkCmdTraceRaysKHR(commandBuffer, &rayGenSBT, &rayMissSBT, &rayHitSBT, &rayCallSBT, desiredWindowWidth, desiredWindowHeight, 1);

                // transition swapchain image into copy destination state
                InsertCommandImageBarrier(commandBuffer, swapchainImage, 0, VkAccessFlagBits.VK_ACCESS_TRANSFER_WRITE_BIT, VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, subresourceRange);

                // transition offscreen buffer into copy source state
                InsertCommandImageBarrier(commandBuffer, offscreenBuffer, VkAccessFlagBits.VK_ACCESS_SHADER_WRITE_BIT, VkAccessFlagBits.VK_ACCESS_TRANSFER_READ_BIT, VkImageLayout.VK_IMAGE_LAYOUT_GENERAL, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL, subresourceRange);

                // copy offscreen buffer into swapchain image
                VulkanNative.vkCmdCopyImage(commandBuffer, offscreenBuffer, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL, swapchainImage, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, 1, &copyRegion);

                // transition swapchain image into presentable state
                InsertCommandImageBarrier(commandBuffer, swapchainImage, 0, VkAccessFlagBits.VK_ACCESS_TRANSFER_WRITE_BIT, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, VkImageLayout.VK_IMAGE_LAYOUT_PRESENT_SRC_KHR, subresourceRange);

                result = VulkanNative.vkEndCommandBuffer(commandBuffer);
                Helpers.CheckErrors(result);
            };

            VkSemaphoreCreateInfo semaphoreInfo = new VkSemaphoreCreateInfo()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_SEMAPHORE_CREATE_INFO,
                pNext = null,
            };

            fixed (VkSemaphore* semaphoreImageAvailablePtr = &semaphoreImageAvailable)
            {
                result = VulkanNative.vkCreateSemaphore(device, &semaphoreInfo, null, semaphoreImageAvailablePtr);
                Helpers.CheckErrors(result);
            }

            fixed (VkSemaphore* semaphoreRenderingAvailablePtr = &semaphoreRenderingAvailable)
            {
                result = VulkanNative.vkCreateSemaphore(device, &semaphoreInfo, null, semaphoreRenderingAvailablePtr);
                Helpers.CheckErrors(result);
            }

            Debug.WriteLine("Done!"); ;
            Debug.WriteLine("Drawing..");

            bool isClosing = false;
            window.FormClosing += (s, e) =>
            {
                isClosing = true;
            };

            while (!isClosing)
            {

                // Draw Frame
                uint imageIndex = 0;
                result = VulkanNative.vkAcquireNextImageKHR(device, swapchain, ulong.MaxValue, semaphoreImageAvailable, 0, &imageIndex);
                Helpers.CheckErrors(result);

                VkPipelineStageFlagBits* waitStageMasks = stackalloc VkPipelineStageFlagBits[] { VkPipelineStageFlagBits.VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT };

                VkSubmitInfo submitInfo;
                fixed (VkCommandBuffer* commandBuffersPtr = &commandBuffers[imageIndex])
                fixed (VkSemaphore* semaphoreImageAvailablePtr = &semaphoreImageAvailable)
                fixed (VkSemaphore* semaphoreRenderingAvailablePtr = &semaphoreRenderingAvailable)
                {
                    submitInfo = new VkSubmitInfo()
                    {
                        sType = VkStructureType.VK_STRUCTURE_TYPE_SUBMIT_INFO,
                        pNext = null,
                        waitSemaphoreCount = 1,
                        pWaitSemaphores = semaphoreImageAvailablePtr,
                        pWaitDstStageMask = waitStageMasks,
                        commandBufferCount = 1,
                        pCommandBuffers = commandBuffersPtr,
                        signalSemaphoreCount = 1,
                        pSignalSemaphores = semaphoreRenderingAvailablePtr,
                    };
                }

                result = VulkanNative.vkQueueSubmit(queue, 1, &submitInfo, 0);
                Helpers.CheckErrors(result);

                VkSwapchainKHR* swapChains = stackalloc VkSwapchainKHR[] { swapchain };
                VkPresentInfoKHR presentInfo;
                fixed (VkSemaphore* semaphoreRenderingAvailablePtr = &semaphoreRenderingAvailable)
                {
                    presentInfo = new VkPresentInfoKHR()
                    {
                        sType = VkStructureType.VK_STRUCTURE_TYPE_PRESENT_INFO_KHR,
                        pNext = null,
                        waitSemaphoreCount = 1,
                        pWaitSemaphores = semaphoreRenderingAvailablePtr,
                        swapchainCount = 1,
                        pSwapchains = swapChains,
                        pImageIndices = &imageIndex,
                        pResults = null,
                    };
                }

                result = VulkanNative.vkQueuePresentKHR(queue, &presentInfo);
                Helpers.CheckErrors(result);

                result = VulkanNative.vkQueueWaitIdle(queue);
                Helpers.CheckErrors(result);

                Application.DoEvents();
            }

            return 0;
        }

        private void CreateBottomLevelContainer()
        {
            Debug.WriteLine("Creating Bottom-Level Acceleration Structure..");

            VkAccelerationStructureCreateGeometryTypeInfoKHR accelerationCreateGeometryInfo = default;
            accelerationCreateGeometryInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_CREATE_GEOMETRY_TYPE_INFO_KHR;
            accelerationCreateGeometryInfo.pNext = null;
            accelerationCreateGeometryInfo.geometryType = VkGeometryTypeKHR.VK_GEOMETRY_TYPE_TRIANGLES_KHR;
            accelerationCreateGeometryInfo.maxPrimitiveCount = 128;
            accelerationCreateGeometryInfo.indexType = VkIndexType.VK_INDEX_TYPE_UINT32;
            accelerationCreateGeometryInfo.maxVertexCount = 8;
            accelerationCreateGeometryInfo.vertexFormat = VkFormat.VK_FORMAT_R32G32B32_SFLOAT;
            accelerationCreateGeometryInfo.allowsTransforms = false;

            VkAccelerationStructureCreateInfoKHR accelerationInfo = default;
            accelerationInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_CREATE_INFO_KHR;
            accelerationInfo.pNext = null;
            accelerationInfo.compactedSize = 0;
            accelerationInfo.type = VkAccelerationStructureTypeKHR.VK_ACCELERATION_STRUCTURE_TYPE_BOTTOM_LEVEL_KHR;
            accelerationInfo.flags = VkBuildAccelerationStructureFlagBitsKHR.VK_BUILD_ACCELERATION_STRUCTURE_PREFER_FAST_TRACE_BIT_KHR;
            accelerationInfo.maxGeometryCount = 1;
            accelerationInfo.pGeometryInfos = &accelerationCreateGeometryInfo;
            accelerationInfo.deviceAddress = VK_NULL_HANDLE;

            fixed (VkAccelerationStructureKHR* bottomLevelASPtr = &bottomLevelAS)
            {
                Helpers.CheckErrors(VulkanNative.vkCreateAccelerationStructureKHR(device, &accelerationInfo, null, bottomLevelASPtr));
            }

            AccelerationMemory objectMemory = this.CreateAccelerationScratchBuffer(bottomLevelAS, VkAccelerationStructureMemoryRequirementsTypeKHR.VK_ACCELERATION_STRUCTURE_MEMORY_REQUIREMENTS_TYPE_OBJECT_KHR);

            this.BindAccelerationMemory(bottomLevelAS, objectMemory.memory);

            AccelerationMemory buildScratchMemory = this.CreateAccelerationScratchBuffer(bottomLevelAS, VkAccelerationStructureMemoryRequirementsTypeKHR.VK_ACCELERATION_STRUCTURE_MEMORY_REQUIREMENTS_TYPE_BUILD_SCRATCH_KHR);

            // Get bottom level acceleration structure handle for use in top level instances
            VkAccelerationStructureDeviceAddressInfoKHR devAddrInfo = default;
            devAddrInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_DEVICE_ADDRESS_INFO_KHR;
            devAddrInfo.accelerationStructure = bottomLevelAS;
            bottomLevelASHandle = VulkanNative.vkGetAccelerationStructureDeviceAddressKHR(device, &devAddrInfo);

            // clang-format off
            float[] vertices = {
                +1.0f, +1.0f, +0.0f,
                -1.0f, +1.0f, +0.0f,
                +0.0f, -1.0f, +0.0f
            };
            uint[] indices = { 0, 1, 2 };
            // clang-format on

            AccelerationMemory vertexBuffer = this.CreateMappedBuffer(vertices, (uint)(sizeof(float) * vertices.Length));

            AccelerationMemory indexBuffer = this.CreateMappedBuffer(indices, (uint)(sizeof(uint) * indices.Length));

            VkAccelerationStructureGeometryKHR accelerationGeometry = default;
            accelerationGeometry.sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_KHR;
            accelerationGeometry.pNext = null;
            accelerationGeometry.flags = VkGeometryFlagBitsKHR.VK_GEOMETRY_OPAQUE_BIT_KHR;
            accelerationGeometry.geometryType = VkGeometryTypeKHR.VK_GEOMETRY_TYPE_TRIANGLES_KHR;
            accelerationGeometry.geometry = default;
            accelerationGeometry.geometry.triangles = default;
            accelerationGeometry.geometry.triangles.sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_TRIANGLES_DATA_KHR;
            accelerationGeometry.geometry.triangles.pNext = null;
            accelerationGeometry.geometry.triangles.vertexFormat = VkFormat.VK_FORMAT_R32G32B32_SFLOAT;
            accelerationGeometry.geometry.triangles.vertexData.deviceAddress =
                vertexBuffer.memoryAddress;
            accelerationGeometry.geometry.triangles.vertexStride = 3 * sizeof(float);
            accelerationGeometry.geometry.triangles.indexType = VkIndexType.VK_INDEX_TYPE_UINT32;
            accelerationGeometry.geometry.triangles.indexData.deviceAddress = indexBuffer.memoryAddress;
            accelerationGeometry.geometry.triangles.transformData.deviceAddress = 0;

            VkAccelerationStructureGeometryKHR* ppGeometries = stackalloc VkAccelerationStructureGeometryKHR[] { accelerationGeometry };

            VkAccelerationStructureBuildGeometryInfoKHR accelerationBuildGeometryInfo = default;
            accelerationBuildGeometryInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_BUILD_GEOMETRY_INFO_KHR;
            accelerationBuildGeometryInfo.pNext = null;
            accelerationBuildGeometryInfo.type = VkAccelerationStructureTypeKHR.VK_ACCELERATION_STRUCTURE_TYPE_BOTTOM_LEVEL_KHR;
            accelerationBuildGeometryInfo.flags = VkBuildAccelerationStructureFlagBitsKHR.VK_BUILD_ACCELERATION_STRUCTURE_PREFER_FAST_TRACE_BIT_KHR;
            accelerationBuildGeometryInfo.update = false;
            accelerationBuildGeometryInfo.srcAccelerationStructure = VK_NULL_HANDLE;
            accelerationBuildGeometryInfo.dstAccelerationStructure = bottomLevelAS;
            accelerationBuildGeometryInfo.geometryArrayOfPointers = false;
            accelerationBuildGeometryInfo.geometryCount = 1;
            accelerationBuildGeometryInfo.ppGeometries = &ppGeometries;
            accelerationBuildGeometryInfo.scratchData.deviceAddress = buildScratchMemory.memoryAddress;

            VkAccelerationStructureBuildOffsetInfoKHR accelerationBuildOffsetInfo = default;
            accelerationBuildOffsetInfo.primitiveCount = 1;
            accelerationBuildOffsetInfo.primitiveOffset = 0x0;
            accelerationBuildOffsetInfo.firstVertex = 0;
            accelerationBuildOffsetInfo.transformOffset = 0x0;

            VkAccelerationStructureBuildOffsetInfoKHR*[] accelerationBuildOffsets = { &accelerationBuildOffsetInfo };

            VkCommandBuffer commandBuffer;

            VkCommandBufferAllocateInfo commandBufferAllocateInfo = default;
            commandBufferAllocateInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO;
            commandBufferAllocateInfo.pNext = null;
            commandBufferAllocateInfo.commandPool = commandPool;
            commandBufferAllocateInfo.level = VkCommandBufferLevel.VK_COMMAND_BUFFER_LEVEL_PRIMARY;
            commandBufferAllocateInfo.commandBufferCount = 1;
            Helpers.CheckErrors(VulkanNative.vkAllocateCommandBuffers(device, &commandBufferAllocateInfo, &commandBuffer));

            VkCommandBufferBeginInfo commandBufferBeginInfo = default;
            commandBufferBeginInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO;
            commandBufferBeginInfo.pNext = null;
            commandBufferBeginInfo.flags = VkCommandBufferUsageFlagBits.VK_COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT;
            VulkanNative.vkBeginCommandBuffer(commandBuffer, &commandBufferBeginInfo);

            fixed (VkAccelerationStructureBuildOffsetInfoKHR** accelerationBuildOffsetsPtr = &accelerationBuildOffsets[0])
            {
                VulkanNative.vkCmdBuildAccelerationStructureKHR(commandBuffer, 1, &accelerationBuildGeometryInfo, accelerationBuildOffsetsPtr);
            }

            Helpers.CheckErrors(VulkanNative.vkEndCommandBuffer(commandBuffer));

            VkSubmitInfo submitInfo = default;
            submitInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_SUBMIT_INFO;
            submitInfo.pNext = null;
            submitInfo.commandBufferCount = 1;
            submitInfo.pCommandBuffers = &commandBuffer;

            VkFence fence = VK_NULL_HANDLE;
            VkFenceCreateInfo fenceInfo = default;
            fenceInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_FENCE_CREATE_INFO;
            fenceInfo.pNext = null;

            Helpers.CheckErrors(VulkanNative.vkCreateFence(this.device, &fenceInfo, null, &fence));
            Helpers.CheckErrors(VulkanNative.vkQueueSubmit(this.queue, 1, &submitInfo, fence));
            Helpers.CheckErrors(VulkanNative.vkWaitForFences(device, 1, &fence, true, ulong.MaxValue));

            VulkanNative.vkDestroyFence(device, fence, null);
            VulkanNative.vkFreeCommandBuffers(this.device, this.commandPool, 1, &commandBuffer);

            // make sure bottom AS handle is valid
            if (bottomLevelASHandle == 0)
            {
                Debug.WriteLine("Invalid Handle to BLAS");
                throw new Exception("Invalid Handle to BLAS");
            }
        }
        private void CreateTopLevelContainer()
        {
            Debug.WriteLine("Creating Top-Level Acceleration Structure..");

            VkAccelerationStructureCreateGeometryTypeInfoKHR accelerationCreateGeometryInfo = default;
            accelerationCreateGeometryInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_CREATE_GEOMETRY_TYPE_INFO_KHR;
            accelerationCreateGeometryInfo.pNext = null;
            accelerationCreateGeometryInfo.geometryType = VkGeometryTypeKHR.VK_GEOMETRY_TYPE_INSTANCES_KHR;
            accelerationCreateGeometryInfo.maxPrimitiveCount = 1;

            VkAccelerationStructureCreateInfoKHR accelerationInfo = default;
            accelerationInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_CREATE_INFO_KHR;
            accelerationInfo.pNext = null;
            accelerationInfo.compactedSize = 0;
            accelerationInfo.type = VkAccelerationStructureTypeKHR.VK_ACCELERATION_STRUCTURE_TYPE_TOP_LEVEL_KHR;
            accelerationInfo.flags = VkBuildAccelerationStructureFlagBitsKHR.VK_BUILD_ACCELERATION_STRUCTURE_PREFER_FAST_TRACE_BIT_KHR;
            accelerationInfo.maxGeometryCount = 1;
            accelerationInfo.pGeometryInfos = &accelerationCreateGeometryInfo;
            accelerationInfo.deviceAddress = VK_NULL_HANDLE;

            fixed (VkAccelerationStructureKHR* topLevelASPtr = &topLevelAS)
            {
                Helpers.CheckErrors(VulkanNative.vkCreateAccelerationStructureKHR(device, &accelerationInfo, null, topLevelASPtr));
            }

            AccelerationMemory objectMemory = this.CreateAccelerationScratchBuffer(topLevelAS, VkAccelerationStructureMemoryRequirementsTypeKHR.VK_ACCELERATION_STRUCTURE_MEMORY_REQUIREMENTS_TYPE_OBJECT_KHR);

            this.BindAccelerationMemory(topLevelAS, objectMemory.memory);

            AccelerationMemory buildScratchMemory = this.CreateAccelerationScratchBuffer(topLevelAS, VkAccelerationStructureMemoryRequirementsTypeKHR.VK_ACCELERATION_STRUCTURE_MEMORY_REQUIREMENTS_TYPE_BUILD_SCRATCH_KHR);

            VkAccelerationStructureInstanceKHR[] instances = new VkAccelerationStructureInstanceKHR[]
            {
                new VkAccelerationStructureInstanceKHR()
                {
                    transform = new VkTransformMatrixKHR()
                    {
                        matrix_0 = 1,
                        matrix_1 = 0,
                        matrix_2 = 0,
                        matrix_3 = 0,

                        matrix_4 = 0,
                        matrix_5 = 1,
                        matrix_6 = 0,
                        matrix_7 = 0,

                        matrix_8 = 0,
                        matrix_9 = 0,
                        matrix_10 = 1,
                        matrix_11 = 0,
                    },
                    instanceCustomIndex = 0,
                    mask = 0xff,
                    instanceShaderBindingTableRecordOffset = 0x0,
                    flags = VkGeometryInstanceFlagBitsKHR.VK_GEOMETRY_INSTANCE_TRIANGLE_FACING_CULL_DISABLE_BIT_KHR,
                    accelerationStructureReference = bottomLevelASHandle
                }
            };


            AccelerationMemory instanceBuffer = this.CreateMappedBuffer(instances, (uint)(sizeof(VkAccelerationStructureInstanceKHR) * instances.Length));

            VkAccelerationStructureGeometryKHR accelerationGeometry = default;
            accelerationGeometry.sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_KHR;
            accelerationGeometry.pNext = null;
            accelerationGeometry.flags = VkGeometryFlagBitsKHR.VK_GEOMETRY_OPAQUE_BIT_KHR;
            accelerationGeometry.geometryType = VkGeometryTypeKHR.VK_GEOMETRY_TYPE_INSTANCES_KHR;
            accelerationGeometry.geometry = default;
            accelerationGeometry.geometry.instances = default;
            accelerationGeometry.geometry.instances.sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_INSTANCES_DATA_KHR;
            accelerationGeometry.geometry.instances.pNext = null;
            accelerationGeometry.geometry.instances.arrayOfPointers = false;
            accelerationGeometry.geometry.instances.data.deviceAddress = instanceBuffer.memoryAddress;

            VkAccelerationStructureGeometryKHR* ppGeometries = stackalloc VkAccelerationStructureGeometryKHR[] { accelerationGeometry };

            VkAccelerationStructureBuildGeometryInfoKHR accelerationBuildGeometryInfo = default;
            accelerationBuildGeometryInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_BUILD_GEOMETRY_INFO_KHR;
            accelerationBuildGeometryInfo.pNext = null;
            accelerationBuildGeometryInfo.type = VkAccelerationStructureTypeKHR.VK_ACCELERATION_STRUCTURE_TYPE_TOP_LEVEL_KHR;
            accelerationBuildGeometryInfo.flags = VkBuildAccelerationStructureFlagBitsKHR.VK_BUILD_ACCELERATION_STRUCTURE_PREFER_FAST_TRACE_BIT_KHR;
            accelerationBuildGeometryInfo.update = false;
            accelerationBuildGeometryInfo.srcAccelerationStructure = VK_NULL_HANDLE;
            accelerationBuildGeometryInfo.dstAccelerationStructure = topLevelAS;
            accelerationBuildGeometryInfo.geometryArrayOfPointers = false;
            accelerationBuildGeometryInfo.geometryCount = 1;
            accelerationBuildGeometryInfo.ppGeometries = &ppGeometries;
            accelerationBuildGeometryInfo.scratchData.deviceAddress = buildScratchMemory.memoryAddress;

            VkAccelerationStructureBuildOffsetInfoKHR accelerationBuildOffsetInfo = default;
            accelerationBuildOffsetInfo.primitiveCount = 1;
            accelerationBuildOffsetInfo.primitiveOffset = 0x0;
            accelerationBuildOffsetInfo.firstVertex = 0;
            accelerationBuildOffsetInfo.transformOffset = 0x0;

            VkAccelerationStructureBuildOffsetInfoKHR*[] accelerationBuildOffsets = { &accelerationBuildOffsetInfo };

            VkCommandBuffer commandBuffer;

            VkCommandBufferAllocateInfo commandBufferAllocateInfo = default;
            commandBufferAllocateInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO;
            commandBufferAllocateInfo.pNext = null;
            commandBufferAllocateInfo.commandPool = commandPool;
            commandBufferAllocateInfo.level = VkCommandBufferLevel.VK_COMMAND_BUFFER_LEVEL_PRIMARY;
            commandBufferAllocateInfo.commandBufferCount = 1;
            Helpers.CheckErrors(VulkanNative.vkAllocateCommandBuffers(device, &commandBufferAllocateInfo, &commandBuffer));

            VkCommandBufferBeginInfo commandBufferBeginInfo = default;
            commandBufferBeginInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO;
            commandBufferBeginInfo.pNext = null;
            commandBufferBeginInfo.flags = VkCommandBufferUsageFlagBits.VK_COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT;
            VulkanNative.vkBeginCommandBuffer(commandBuffer, &commandBufferBeginInfo);

            fixed (VkAccelerationStructureBuildOffsetInfoKHR** accelerationBuildOffsetsPtr = &accelerationBuildOffsets[0])
            {
                VulkanNative.vkCmdBuildAccelerationStructureKHR(commandBuffer, 1, &accelerationBuildGeometryInfo, accelerationBuildOffsetsPtr);
            }

            Helpers.CheckErrors(VulkanNative.vkEndCommandBuffer(commandBuffer));

            VkSubmitInfo submitInfo = default;
            submitInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_SUBMIT_INFO;
            submitInfo.pNext = null;
            submitInfo.commandBufferCount = 1;
            submitInfo.pCommandBuffers = &commandBuffer;

            VkFence fence = VK_NULL_HANDLE;
            VkFenceCreateInfo fenceInfo = default;
            fenceInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_FENCE_CREATE_INFO;
            fenceInfo.pNext = null;

            Helpers.CheckErrors(VulkanNative.vkCreateFence(device, &fenceInfo, null, &fence));
            Helpers.CheckErrors(VulkanNative.vkQueueSubmit(queue, 1, &submitInfo, fence));
            Helpers.CheckErrors(VulkanNative.vkWaitForFences(device, 1, &fence, true, ulong.MaxValue));

            VulkanNative.vkDestroyFence(device, fence, null);
            VulkanNative.vkFreeCommandBuffers(device, commandPool, 1, &commandBuffer);
        }
        private void OffscreenBuffer()
        {
            Debug.WriteLine("Creating Offsceen Buffer..");

            VkImageCreateInfo imageInfo = default;
            imageInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_IMAGE_CREATE_INFO;
            imageInfo.pNext = null;
            imageInfo.imageType = VkImageType.VK_IMAGE_TYPE_2D;
            imageInfo.format = desiredSurfaceFormat;
            imageInfo.extent = new VkExtent3D { width = desiredWindowWidth, height = desiredWindowHeight, depth = 1 };
            imageInfo.mipLevels = 1;
            imageInfo.arrayLayers = 1;
            imageInfo.samples = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT;
            imageInfo.tiling = VkImageTiling.VK_IMAGE_TILING_OPTIMAL;
            imageInfo.usage = VkImageUsageFlagBits.VK_IMAGE_USAGE_STORAGE_BIT | VkImageUsageFlagBits.VK_IMAGE_USAGE_TRANSFER_SRC_BIT;
            imageInfo.sharingMode = VkSharingMode.VK_SHARING_MODE_EXCLUSIVE;
            imageInfo.queueFamilyIndexCount = 0;
            imageInfo.pQueueFamilyIndices = null;
            imageInfo.initialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED;

            fixed (VkImage* offscreenBufferPtr = &offscreenBuffer)
            {
                Helpers.CheckErrors(VulkanNative.vkCreateImage(device, &imageInfo, null, offscreenBufferPtr));
            }

            VkMemoryRequirements memoryRequirements = default;
            VulkanNative.vkGetImageMemoryRequirements(device, offscreenBuffer, &memoryRequirements);

            VkMemoryAllocateInfo memoryAllocateInfo = default;
            memoryAllocateInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO;
            memoryAllocateInfo.pNext = null;
            memoryAllocateInfo.allocationSize = memoryRequirements.size;
            memoryAllocateInfo.memoryTypeIndex = this.FindMemoryType(memoryRequirements.memoryTypeBits, VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT);

            fixed (VkDeviceMemory* offscreenBufferMemoryPtr = &this.offscreenBufferMemory)
            {
                Helpers.CheckErrors(VulkanNative.vkAllocateMemory(device, &memoryAllocateInfo, null, offscreenBufferMemoryPtr));
            }

            Helpers.CheckErrors(VulkanNative.vkBindImageMemory(device, offscreenBuffer, offscreenBufferMemory, 0));

            VkImageViewCreateInfo imageViewInfo = default;
            imageViewInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO;
            imageViewInfo.pNext = null;
            imageViewInfo.viewType = VkImageViewType.VK_IMAGE_VIEW_TYPE_2D;
            imageViewInfo.format = desiredSurfaceFormat;
            imageViewInfo.subresourceRange = default;
            imageViewInfo.subresourceRange.aspectMask = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT;
            imageViewInfo.subresourceRange.baseMipLevel = 0;
            imageViewInfo.subresourceRange.levelCount = 1;
            imageViewInfo.subresourceRange.baseArrayLayer = 0;
            imageViewInfo.subresourceRange.layerCount = 1;
            imageViewInfo.image = offscreenBuffer;
            imageViewInfo.flags = 0;
            imageViewInfo.components.r = VkComponentSwizzle.VK_COMPONENT_SWIZZLE_R;
            imageViewInfo.components.g = VkComponentSwizzle.VK_COMPONENT_SWIZZLE_G;
            imageViewInfo.components.b = VkComponentSwizzle.VK_COMPONENT_SWIZZLE_B;
            imageViewInfo.components.a = VkComponentSwizzle.VK_COMPONENT_SWIZZLE_A;

            fixed (VkImageView* offscreenBufferViewPtr = &this.offscreenBufferView)
            {
                Helpers.CheckErrors(VulkanNative.vkCreateImageView(device, &imageViewInfo, null, offscreenBufferViewPtr));
            }
        }
        private void RTDescriptorSetLayout()
        {
            Debug.WriteLine("Creating RT Descriptor Set Layout..");

            VkDescriptorSetLayoutBinding accelerationStructureLayoutBinding = default;
            accelerationStructureLayoutBinding.binding = 0;
            accelerationStructureLayoutBinding.descriptorType = VkDescriptorType.VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR;
            accelerationStructureLayoutBinding.descriptorCount = 1;
            accelerationStructureLayoutBinding.stageFlags = VkShaderStageFlagBits.VK_SHADER_STAGE_RAYGEN_BIT_KHR;
            accelerationStructureLayoutBinding.pImmutableSamplers = null;

            VkDescriptorSetLayoutBinding storageImageLayoutBinding = default;
            storageImageLayoutBinding.binding = 1;
            storageImageLayoutBinding.descriptorType = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_IMAGE;
            storageImageLayoutBinding.descriptorCount = 1;
            storageImageLayoutBinding.stageFlags = VkShaderStageFlagBits.VK_SHADER_STAGE_RAYGEN_BIT_KHR;

            VkDescriptorSetLayoutBinding* bindings = stackalloc VkDescriptorSetLayoutBinding[] { accelerationStructureLayoutBinding, storageImageLayoutBinding };

            VkDescriptorSetLayoutCreateInfo layoutInfo = default;
            layoutInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO;
            layoutInfo.pNext = null;
            layoutInfo.flags = 0;
            layoutInfo.bindingCount = 2;
            layoutInfo.pBindings = bindings;

            fixed (VkDescriptorSetLayout* descriptorSetLayoutPtr = &this.descriptorSetLayout)
            {
                Helpers.CheckErrors(VulkanNative.vkCreateDescriptorSetLayout(device, &layoutInfo, null, descriptorSetLayoutPtr));
            }
        }
        private void RTDescriptorSet()
        {
            Debug.WriteLine("Creating RT Descriptor Set..");

            VkDescriptorPoolSize* poolSizes = stackalloc VkDescriptorPoolSize[]
            {
                new VkDescriptorPoolSize() { type = VkDescriptorType.VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR, descriptorCount = 1 },
                new VkDescriptorPoolSize() { type = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_IMAGE, descriptorCount = 1 }
            };

            VkDescriptorPoolCreateInfo descriptorPoolInfo = default;
            descriptorPoolInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_DESCRIPTOR_POOL_CREATE_INFO;
            descriptorPoolInfo.pNext = null;
            descriptorPoolInfo.flags = 0;
            descriptorPoolInfo.maxSets = 1;
            descriptorPoolInfo.poolSizeCount = 2;
            descriptorPoolInfo.pPoolSizes = poolSizes;

            fixed (VkDescriptorPool* descriptorPoolPtr = &this.descriptorPool)
            {
                Helpers.CheckErrors(VulkanNative.vkCreateDescriptorPool(device, &descriptorPoolInfo, null, descriptorPoolPtr));
            }

            VkDescriptorSetAllocateInfo descriptorSetAllocateInfo = default;
            descriptorSetAllocateInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO;
            descriptorSetAllocateInfo.pNext = null;
            descriptorSetAllocateInfo.descriptorPool = descriptorPool;
            descriptorSetAllocateInfo.descriptorSetCount = 1;
            fixed (VkDescriptorSetLayout* descriptorSetLayoutPtr = &descriptorSetLayout)
            {
                descriptorSetAllocateInfo.pSetLayouts = descriptorSetLayoutPtr;
            }

            fixed (VkDescriptorSet* descriptorSetPtr = &descriptorSet)
            {
                Helpers.CheckErrors(VulkanNative.vkAllocateDescriptorSets(device, &descriptorSetAllocateInfo, descriptorSetPtr));
            }

            VkWriteDescriptorSetAccelerationStructureKHR descriptorAccelerationStructureInfo;
            descriptorAccelerationStructureInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET_ACCELERATION_STRUCTURE_KHR;
            descriptorAccelerationStructureInfo.pNext = null;
            descriptorAccelerationStructureInfo.accelerationStructureCount = 1;
            fixed (VkAccelerationStructureKHR* topLevelASPtr = &topLevelAS)
            {
                descriptorAccelerationStructureInfo.pAccelerationStructures = topLevelASPtr;
            }

            VkWriteDescriptorSet accelerationStructureWrite = default;
            accelerationStructureWrite.sType = VkStructureType.VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET;
            accelerationStructureWrite.pNext = &descriptorAccelerationStructureInfo;
            accelerationStructureWrite.dstSet = descriptorSet;
            accelerationStructureWrite.dstBinding = 0;
            accelerationStructureWrite.descriptorCount = 1;
            accelerationStructureWrite.descriptorType = VkDescriptorType.VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR;

            VkDescriptorImageInfo storageImageInfo = default;
            storageImageInfo.sampler = VK_NULL_HANDLE;
            storageImageInfo.imageView = offscreenBufferView;
            storageImageInfo.imageLayout = VkImageLayout.VK_IMAGE_LAYOUT_GENERAL;

            VkWriteDescriptorSet outputImageWrite = default;
            outputImageWrite.sType = VkStructureType.VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET;
            outputImageWrite.pNext = null;
            outputImageWrite.dstSet = descriptorSet;
            outputImageWrite.dstBinding = 1;
            outputImageWrite.descriptorType = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_IMAGE;
            outputImageWrite.descriptorCount = 1;
            outputImageWrite.pImageInfo = &storageImageInfo;

            VkWriteDescriptorSet* descriptorWrites = stackalloc VkWriteDescriptorSet[] { accelerationStructureWrite, outputImageWrite };

            VulkanNative.vkUpdateDescriptorSets(device, 2, descriptorWrites, 0, null);
        }
        private void RTPipelineLayout()
        {
            Debug.WriteLine("Creating RT Pipeline Layout..");

            VkPipelineLayoutCreateInfo pipelineLayoutInfo = default;
            pipelineLayoutInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO;
            pipelineLayoutInfo.pNext = null;
            pipelineLayoutInfo.flags = 0;
            pipelineLayoutInfo.setLayoutCount = 1;
            fixed (VkDescriptorSetLayout* descriptorSetLayoutPtr = &descriptorSetLayout)
            {
                pipelineLayoutInfo.pSetLayouts = descriptorSetLayoutPtr;
            }
            pipelineLayoutInfo.pushConstantRangeCount = 0;
            pipelineLayoutInfo.pPushConstantRanges = null;

            fixed (VkPipelineLayout* pipelineLayoutPtr = &pipelineLayout)
            {
                Helpers.CheckErrors(VulkanNative.vkCreatePipelineLayout(device, &pipelineLayoutInfo, null, pipelineLayoutPtr));
            }
        }
        private void RTPipeline()
        {
            Debug.WriteLine("Creating RT Pipeline..");

            byte[] rgenShaderSrc = File.ReadAllBytes("Shaders/ray-generation.spv");
            byte[] rchitShaderSrc = File.ReadAllBytes("Shaders/ray-closest-hit.spv");
            byte[] rmissShaderSrc = File.ReadAllBytes("Shaders/ray-miss.spv");

            VkPipelineShaderStageCreateInfo rayGenShaderStageInfo = default;
            rayGenShaderStageInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO;
            rayGenShaderStageInfo.pNext = null;
            rayGenShaderStageInfo.stage = VkShaderStageFlagBits.VK_SHADER_STAGE_RAYGEN_BIT_KHR;
            rayGenShaderStageInfo.module = CreateShaderModule(rgenShaderSrc);
            rayGenShaderStageInfo.pName = "main".ToPointer();

            VkPipelineShaderStageCreateInfo rayChitShaderStageInfo = default;
            rayChitShaderStageInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO;
            rayChitShaderStageInfo.pNext = null;
            rayChitShaderStageInfo.stage = VkShaderStageFlagBits.VK_SHADER_STAGE_CLOSEST_HIT_BIT_KHR;
            rayChitShaderStageInfo.module = CreateShaderModule(rchitShaderSrc);
            rayChitShaderStageInfo.pName = "main".ToPointer();

            VkPipelineShaderStageCreateInfo rayMissShaderStageInfo = default;
            rayMissShaderStageInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO;
            rayMissShaderStageInfo.pNext = null;
            rayMissShaderStageInfo.stage = VkShaderStageFlagBits.VK_SHADER_STAGE_MISS_BIT_KHR;
            rayMissShaderStageInfo.module = CreateShaderModule(rmissShaderSrc);
            rayMissShaderStageInfo.pName = "main".ToPointer();

            VkPipelineShaderStageCreateInfo* shaderStages = stackalloc VkPipelineShaderStageCreateInfo[] { rayGenShaderStageInfo, rayChitShaderStageInfo, rayMissShaderStageInfo };

            VkRayTracingShaderGroupCreateInfoKHR rayGenGroup = default;
            rayGenGroup.sType = VkStructureType.VK_STRUCTURE_TYPE_RAY_TRACING_SHADER_GROUP_CREATE_INFO_KHR;
            rayGenGroup.pNext = null;
            rayGenGroup.type = VkRayTracingShaderGroupTypeKHR.VK_RAY_TRACING_SHADER_GROUP_TYPE_GENERAL_KHR;
            rayGenGroup.generalShader = 0;
            rayGenGroup.closestHitShader = VulkanNative.VK_SHADER_UNUSED_KHR;
            rayGenGroup.anyHitShader = VulkanNative.VK_SHADER_UNUSED_KHR;
            rayGenGroup.intersectionShader = VulkanNative.VK_SHADER_UNUSED_KHR;

            VkRayTracingShaderGroupCreateInfoKHR rayHitGroup = default;
            rayHitGroup.sType = VkStructureType.VK_STRUCTURE_TYPE_RAY_TRACING_SHADER_GROUP_CREATE_INFO_KHR;
            rayHitGroup.pNext = null;
            rayHitGroup.type = VkRayTracingShaderGroupTypeKHR.VK_RAY_TRACING_SHADER_GROUP_TYPE_TRIANGLES_HIT_GROUP_KHR;
            rayHitGroup.generalShader = VulkanNative.VK_SHADER_UNUSED_KHR;
            rayHitGroup.closestHitShader = 1;
            rayHitGroup.anyHitShader = VulkanNative.VK_SHADER_UNUSED_KHR;
            rayHitGroup.intersectionShader = VulkanNative.VK_SHADER_UNUSED_KHR;

            VkRayTracingShaderGroupCreateInfoKHR rayMissGroup = default;
            rayMissGroup.sType = VkStructureType.VK_STRUCTURE_TYPE_RAY_TRACING_SHADER_GROUP_CREATE_INFO_KHR;
            rayMissGroup.pNext = null;
            rayMissGroup.type = VkRayTracingShaderGroupTypeKHR.VK_RAY_TRACING_SHADER_GROUP_TYPE_GENERAL_KHR;
            rayMissGroup.generalShader = 2;
            rayMissGroup.closestHitShader = VulkanNative.VK_SHADER_UNUSED_KHR;
            rayMissGroup.anyHitShader = VulkanNative.VK_SHADER_UNUSED_KHR;
            rayMissGroup.intersectionShader = VulkanNative.VK_SHADER_UNUSED_KHR;

            VkRayTracingShaderGroupCreateInfoKHR* shaderGroups = stackalloc VkRayTracingShaderGroupCreateInfoKHR[] { rayGenGroup, rayHitGroup, rayMissGroup };

            VkRayTracingPipelineCreateInfoKHR pipelineInfo = default;
            pipelineInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_RAY_TRACING_PIPELINE_CREATE_INFO_KHR;
            pipelineInfo.pNext = null;
            pipelineInfo.flags = 0;
            pipelineInfo.stageCount = 3;
            pipelineInfo.pStages = shaderStages;
            pipelineInfo.groupCount = 3;
            pipelineInfo.pGroups = shaderGroups;
            pipelineInfo.maxRecursionDepth = 1;
            pipelineInfo.libraries = default;
            pipelineInfo.libraries.sType = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_LIBRARY_CREATE_INFO_KHR;
            pipelineInfo.libraries.pNext = null;
            pipelineInfo.libraries.libraryCount = 0;
            pipelineInfo.libraries.pLibraries = null;
            pipelineInfo.pLibraryInterface = null;
            pipelineInfo.layout = pipelineLayout;
            pipelineInfo.basePipelineHandle = VK_NULL_HANDLE;
            pipelineInfo.basePipelineIndex = 0;

            fixed (VkPipeline* pipelinePtr = &pipeline)
            {
                Helpers.CheckErrors(VulkanNative.vkCreateRayTracingPipelinesKHR(device, 0, 1, &pipelineInfo, null, pipelinePtr));
            }
        }
        private void ShaderBindingTable()
        {
            Debug.WriteLine("Creating Shader Binding Table..");

            AccelerationMemory newShaderBindingTable = default;
            shaderBindingTableSize = shaderBindingTableGroupCount * rayTracingProperties.shaderGroupHandleSize;

            VkBufferCreateInfo bufferInfo = default;
            bufferInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO;
            bufferInfo.pNext = null;
            bufferInfo.size = shaderBindingTableSize;
            bufferInfo.usage = VkBufferUsageFlagBits.VK_BUFFER_USAGE_TRANSFER_SRC_BIT;
            bufferInfo.sharingMode = VkSharingMode.VK_SHARING_MODE_EXCLUSIVE;
            bufferInfo.queueFamilyIndexCount = 0;
            bufferInfo.pQueueFamilyIndices = null;
            Helpers.CheckErrors(VulkanNative.vkCreateBuffer(device, &bufferInfo, null, &newShaderBindingTable.buffer));

            VkMemoryRequirements memoryRequirements = default;
            VulkanNative.vkGetBufferMemoryRequirements(device, newShaderBindingTable.buffer, &memoryRequirements);

            VkMemoryAllocateInfo memAllocInfo = default;
            memAllocInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO;
            memAllocInfo.pNext = null;
            memAllocInfo.allocationSize = memoryRequirements.size;
            memAllocInfo.memoryTypeIndex = this.FindMemoryType(memoryRequirements.memoryTypeBits, VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT);

            Helpers.CheckErrors(VulkanNative.vkAllocateMemory(device, &memAllocInfo, null, &newShaderBindingTable.memory));

            Helpers.CheckErrors(VulkanNative.vkBindBufferMemory(device, newShaderBindingTable.buffer, newShaderBindingTable.memory, 0));

            IntPtr dstData;
            Helpers.CheckErrors(VulkanNative.vkMapMemory(device, newShaderBindingTable.memory, 0, shaderBindingTableSize, 0, (void**)&dstData));

            VulkanNative.vkGetRayTracingShaderGroupHandlesKHR(device, pipeline, 0, shaderBindingTableGroupCount, (UIntPtr)shaderBindingTableSize, (void*)dstData);
            VulkanNative.vkUnmapMemory(device, newShaderBindingTable.memory);

            this.shaderBindingTable = newShaderBindingTable;
        }

        private VkExtent2D ChooseSwapExtent(VkSurfaceCapabilitiesKHR capabilities)
        {
            if (capabilities.currentExtent.width != uint.MaxValue)
            {
                return capabilities.currentExtent;
            }

            return new VkExtent2D()
            {
                width = Math.Max(capabilities.minImageExtent.width, Math.Min(capabilities.maxImageExtent.width, (uint)this.desiredWindowWidth)),
                height = Math.Max(capabilities.minImageExtent.height, Math.Min(capabilities.maxImageExtent.height, (uint)this.desiredWindowHeight)),
            };
        }

        // Callback Message
        public delegate VkBool32 DebugCallbackDelegate(VkDebugUtilsMessageSeverityFlagBitsEXT messageSeverity, VkDebugUtilsMessageTypeFlagBitsEXT messageType, VkDebugUtilsMessengerCallbackDataEXT pCallbackData, void* pUserData);
        public static DebugCallbackDelegate CallbackDelegate = new DebugCallbackDelegate(DebugCallback);

        private void PopulateDebugMessengerCreateInfo(out VkDebugUtilsMessengerCreateInfoEXT createInfo)
        {
            createInfo = default;
            createInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_DEBUG_UTILS_MESSENGER_CREATE_INFO_EXT;
            createInfo.messageSeverity = VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_INFO_BIT_EXT | 
                                         VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_VERBOSE_BIT_EXT | 
                                         VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_WARNING_BIT_EXT | 
                                         VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT;
            createInfo.messageType = VkDebugUtilsMessageTypeFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_TYPE_GENERAL_BIT_EXT | 
                                     VkDebugUtilsMessageTypeFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_TYPE_PERFORMANCE_BIT_EXT | 
                                     VkDebugUtilsMessageTypeFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_TYPE_VALIDATION_BIT_EXT;
            createInfo.pfnUserCallback = Marshal.GetFunctionPointerForDelegate(CallbackDelegate);
            createInfo.pUserData = null;
        }

        public static VkBool32 DebugCallback(VkDebugUtilsMessageSeverityFlagBitsEXT messageSeverity, VkDebugUtilsMessageTypeFlagBitsEXT messageType, VkDebugUtilsMessengerCallbackDataEXT pCallbackData, void* pUserData)
        {
            Debug.WriteLine($"<<Vulkan Validation Layer>> {Helpers.GetString(pCallbackData.pMessage)}");
            return false;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate VkResult vkCreateDebugUtilsMessengerEXTDelegate(VkInstance instance, VkDebugUtilsMessengerCreateInfoEXT* pCreateInfo, VkAllocationCallbacks* pAllocator, VkDebugUtilsMessengerEXT* pMessenger);
        private static vkCreateDebugUtilsMessengerEXTDelegate vkCreateDebugUtilsMessengerEXT_ptr;
        public static VkResult vkCreateDebugUtilsMessengerEXT(VkInstance instance, VkDebugUtilsMessengerCreateInfoEXT* pCreateInfo, VkAllocationCallbacks* pAllocator, VkDebugUtilsMessengerEXT* pMessenger)
            => vkCreateDebugUtilsMessengerEXT_ptr(instance, pCreateInfo, pAllocator, pMessenger);

        private void SetupDebugMessenger()
        {
#if DEBUG          
            fixed (VkDebugUtilsMessengerEXT* debugMessengerPtr = &debugMessenger)
            {
                var funcPtr = VulkanNative.vkGetInstanceProcAddr(instance, "vkCreateDebugUtilsMessengerEXT".ToPointer());
                if (funcPtr != IntPtr.Zero)
                {
                    vkCreateDebugUtilsMessengerEXT_ptr = Marshal.GetDelegateForFunctionPointer<vkCreateDebugUtilsMessengerEXTDelegate>(funcPtr);

                    VkDebugUtilsMessengerCreateInfoEXT createInfo;
                    this.PopulateDebugMessengerCreateInfo(out createInfo);
                    Helpers.CheckErrors(vkCreateDebugUtilsMessengerEXT(instance, &createInfo, null, debugMessengerPtr));
                }
            }
#endif
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void vkDestroyDebugUtilsMessengerEXTDelegate(VkInstance instance, VkDebugUtilsMessengerEXT messenger, VkAllocationCallbacks* pAllocator);
        private static vkDestroyDebugUtilsMessengerEXTDelegate vkDestroyDebugUtilsMessengerEXT_ptr;
        private VkDebugUtilsMessengerEXT debugMessenger;

        public static void vkDestroyDebugUtilsMessengerEXT(VkInstance instance, VkDebugUtilsMessengerEXT messenger, VkAllocationCallbacks* pAllocator)
            => vkDestroyDebugUtilsMessengerEXT_ptr(instance, messenger, pAllocator);

        private void DestroyDebugMessenger()
        {
#if DEBUG
            var funcPtr = VulkanNative.vkGetInstanceProcAddr(instance, "vkDestroyDebugUtilsMessengerEXT".ToPointer());
            if (funcPtr != IntPtr.Zero)
            {
                vkDestroyDebugUtilsMessengerEXT_ptr = Marshal.GetDelegateForFunctionPointer<vkDestroyDebugUtilsMessengerEXTDelegate>(funcPtr);
                vkDestroyDebugUtilsMessengerEXT(instance, debugMessenger, null);
            }
#endif
        }
    }
}