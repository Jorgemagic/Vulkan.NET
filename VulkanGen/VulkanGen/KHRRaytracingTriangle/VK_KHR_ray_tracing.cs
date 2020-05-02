using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using WaveEngine.Bindings.Vulkan;

namespace KHRRaytracingTriangle
{
    public unsafe class VK_KHR_ray_tracing
    {
        const string VK_KHR_SURFACE_EXTENSION_NAME = "VK_KHR_surface";
        const string VK_KHR_WIN32_SURFACE_EXTENSION_NAME = "VK_KHR_win32_surface";
        const string VK_KHR_GET_PHYSICAL_DEVICE_PROPERTIES_2_EXTENSION_NAME = "VK_KHR_get_physical_device_properties2";
        const string VK_KHR_SWAPCHAIN_EXTENSION_NAME = "VK_KHR_swapchain";
        const string VK_KHR_RAY_TRACING_EXTENSION_NAME = "VK_KHR_ray_tracing";
        const string VK_KHR_PIPELINE_LIBRARY_EXTENSION_NAME = "VK_KHR_pipeline_library";
        const string VK_EXT_DESCRIPTOR_INDEXING_EXTENSION_NAME = "VK_EXT_descriptor_indexing";
        const string VK_KHR_BUFFER_DEVICE_ADDRESS_EXTENSION_NAME = "VK_KHR_buffer_device_address";
        const string VK_KHR_DEFERRED_HOST_OPERATIONS_EXTENSION_NAME = "VK_KHR_deferred_host_operations";
        const string VK_KHR_GET_MEMORY_REQUIREMENTS_2_EXTENSION_NAME = "VK_KHR_get_memory_requirements2";
        const int EXIT_FAILURE = 1;
        const int EXIT_SUCCESS = 0;
        const uint VK_SHADER_UNUSED_KHR = (~0U);

        //      #include <Windows.h>

        //#define VK_ENABLE_BETA_EXTENSIONS
        //#define VK_USE_PLATFORM_WIN32_KHR
        //#include <vulkan/vulkan.h>

        //#include <pathcch.h>
        //#include <Shlwapi.h>

        //#include <fstream>
        //#include <iostream>
        //#include <vector>

        //#define VulkanNative.r)                                                                    \
        //    {                                                                                          \
        //        VkResult result = (r);                                                                 \
        //        if (result != VK_SUCCESS) {                                                            \
        //            Debug.WriteLine($"Vulkan Assertion failed in Line " << __LINE__ << " with: " << result \
        //                     );                                                            \
        //        }                                                                                      \
        //    }

        //#define RESOLVE_VK_INSTANCE_PFN(instance, funcName)                                          \
        //    {                                                                                        \
        //        funcName =                                                                           \
        //            reinterpret_cast<PFN_##funcName>(vkGetInstanceProcAddr(instance, "" #funcName)); \
        //        if (funcName == null) {                                                           \
        //            const std::string name = #funcName;                                              \
        //            Debug.WriteLine($"Failed to resolve function " << name);                 \
        //        }                                                                                    \
        //    }

        //#define RESOLVE_VK_DEVICE_PFN(device, funcName)                                                 \
        //    {                                                                                           \
        //        funcName = reinterpret_cast<PFN_##funcName>(vkGetDeviceProcAddr(device, "" #funcName)); \
        //        if (funcName == null) {                                                              \
        //            const std::string name = #funcName;                                                 \
        //            Debug.WriteLine($"Failed to resolve function " << name);                    \
        //        }                                                                                       \
        //    }

        //static std::vector<char> readFile(const std::string& filename)
        //{
        //    printf("Reading %s\n", filename.c_str());
        //    std::ifstream file(filename, std::ios::ate | std::ios::binary);

        //    if (!file.is_open())
        //    {
        //        throw new Exception("Could not open file");
        //    }

        //    size_t fileSize = (size_t)file.tellg();
        //    std::vector<char> buffer(fileSize);

        //    file.seekg(0);
        //    file.read(buffer.data(), fileSize);
        //    file.close();

        //    return buffer;
        //}

        public struct MappedBuffer
        {
            public VkBuffer buffer;
            public VkDeviceMemory memory;
            public ulong memoryAddress;
            public IntPtr mappedPointer;
        };

        //AccelerationMemory MappedBuffer;

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

        MappedBuffer shaderBindingTable = default;
        uint shaderBindingTableSize = 0;
        uint shaderBindingTableGroupCount = 3;

        VkAccelerationStructureKHR bottomLevelAS;
        ulong bottomLevelASHandle = 0;
        VkAccelerationStructureKHR topLevelAS;
        ulong topLevelASHandle = 0;

        uint desiredWindowWidth = 624;
        uint desiredWindowHeight = 441;
        VkFormat desiredSurfaceFormat = VkFormat.VK_FORMAT_B8G8R8A8_UNORM;

        Form window;
        //HINSTANCE windowInstance;

        VkCommandBuffer[] commandBuffers;

        //struct MsgInfo
        //{
        //    HWND hWnd;
        //    UINT uMsg;
        //    WPARAM wParam;
        //    LPARAM lParam;
        //};

        string appName = "VK_KHR_ray_tracing triangle";

        // clang-format off
        string[] instanceExtensions = {
            VK_KHR_SURFACE_EXTENSION_NAME,
            VK_KHR_WIN32_SURFACE_EXTENSION_NAME,
            VK_KHR_GET_PHYSICAL_DEVICE_PROPERTIES_2_EXTENSION_NAME
        };
        string[] validationLayers = {
            "VK_LAYER_KHRONOS_validation"
        };
        string[] deviceExtensions = {
            VK_KHR_SWAPCHAIN_EXTENSION_NAME,
            VK_KHR_RAY_TRACING_EXTENSION_NAME,
            VK_KHR_PIPELINE_LIBRARY_EXTENSION_NAME,
            VK_EXT_DESCRIPTOR_INDEXING_EXTENSION_NAME,
            VK_KHR_BUFFER_DEVICE_ADDRESS_EXTENSION_NAME,
            VK_KHR_DEFERRED_HOST_OPERATIONS_EXTENSION_NAME,
            VK_KHR_GET_MEMORY_REQUIREMENTS_2_EXTENSION_NAME
        };
        // clang-format on

        //std::string GetExecutablePath()
        //{
        //    wchar_t path[MAX_PATH + 1];
        //    DWORD result = GetModuleFileName(NULL, path, sizeof(path) - 1);
        //    if (result == 0 || result == sizeof(path) - 1)
        //        return "";
        //    path[MAX_PATH - 1] = 0;
        //    PathRemoveFileSpecW(path);
        //    std::wstring ws(path);
        //    std::string out(ws.begin(), ws.end());
        //    return out;
        //}

        VkShaderModule CreateShaderModule(byte[] code)
        {
            VkShaderModule shaderModule;
            VkShaderModuleCreateInfo shaderModuleInfo = default;

            fixed (byte* sourcePointer = code)
            {
                shaderModuleInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_SHADER_MODULE_CREATE_INFO;
                shaderModuleInfo.pNext = null;
                shaderModuleInfo.codeSize = (UIntPtr)code.Length;
                shaderModuleInfo.pCode = (uint*)sourcePointer;
                VulkanNative.vkCreateShaderModule(device, &shaderModuleInfo, null, &shaderModule);
            }
            return shaderModule;
        }

        uint FindMemoryType(uint typeFilter, VkMemoryPropertyFlagBits properties)
        {
            VkPhysicalDeviceMemoryProperties memProperties;
            VulkanNative.vkGetPhysicalDeviceMemoryProperties(physicalDevice, &memProperties);
            for (int ii = 0; ii < memProperties.memoryTypeCount; ++ii)
            {
                if (((typeFilter & (1 << ii)) != 0) &&
                    (memProperties.GetMemoryType((uint)ii).propertyFlags & properties) == properties)
                {
                    return (uint)ii;
                }
            };
            throw new Exception("failed to find suitable memory type!");
        }

        ulong GetBufferAddress(VkBuffer buffer)
        {
            //PFN_vkGetBufferDeviceAddressKHR vkGetBufferDeviceAddressKHR = null;
            //RESOLVE_VK_DEVICE_PFN(device, vkGetBufferDeviceAddressKHR);

            VkBufferDeviceAddressInfo bufferAddressInfo = default;
            bufferAddressInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_DEVICE_ADDRESS_INFO;
            bufferAddressInfo.pNext = null;
            bufferAddressInfo.buffer = buffer;

            return VulkanNative.vkGetBufferDeviceAddress(device, &bufferAddressInfo);
        }

        MappedBuffer CreateMappedBuffer<T>(T[] srcData, uint byteLength)
        {
            MappedBuffer outValue = default;

            VkBufferCreateInfo bufferInfo = default;
            bufferInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO;
            bufferInfo.pNext = null;
            bufferInfo.size = byteLength;
            bufferInfo.usage = VkBufferUsageFlagBits.VK_BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT;
            bufferInfo.sharingMode = VkSharingMode.VK_SHARING_MODE_EXCLUSIVE;
            bufferInfo.queueFamilyIndexCount = 0;
            bufferInfo.pQueueFamilyIndices = null;
            VulkanNative.vkCreateBuffer(device, &bufferInfo, null, &outValue.buffer);

            VkMemoryRequirements memoryRequirements = default;
            VulkanNative.vkGetBufferMemoryRequirements(device, outValue.buffer, &memoryRequirements);

            VkMemoryAllocateFlagsInfo memAllocFlagsInfo = default;
            memAllocFlagsInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_FLAGS_INFO;
            memAllocFlagsInfo.pNext = null;
            memAllocFlagsInfo.flags = VkMemoryAllocateFlagBits.VK_MEMORY_ALLOCATE_DEVICE_ADDRESS_BIT;
            memAllocFlagsInfo.deviceMask = 0;

            VkMemoryAllocateInfo memAllocInfo = default;
            memAllocInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO;
            memAllocInfo.pNext = &memAllocFlagsInfo;
            memAllocInfo.allocationSize = memoryRequirements.size;
            memAllocInfo.memoryTypeIndex =
                FindMemoryType(memoryRequirements.memoryTypeBits,
                               VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT | VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_HOST_COHERENT_BIT);
            VulkanNative.vkAllocateMemory(device, &memAllocInfo, null, &outValue.memory);

            VulkanNative.vkBindBufferMemory(device, outValue.buffer, outValue.memory, 0);

            outValue.memoryAddress = GetBufferAddress(outValue.buffer);

            IntPtr dstData;
            VulkanNative.vkMapMemory(device, outValue.memory, 0, byteLength, 0, (void**)&dstData);
            if (srcData != null)
            {
                GCHandle gcHandle = GCHandle.Alloc(srcData, GCHandleType.Pinned);
                Unsafe.CopyBlock((void*)dstData, (void*)gcHandle.AddrOfPinnedObject(), byteLength);
                gcHandle.Free();
            }
            VulkanNative.vkUnmapMemory(device, outValue.memory);
            outValue.mappedPointer = dstData;

            return outValue;
        }

        VkMemoryRequirements GetAccelerationStructureMemoryRequirements(
            VkAccelerationStructureKHR acceleration,
            VkAccelerationStructureMemoryRequirementsTypeKHR type)
        {
            VkMemoryRequirements2 memoryRequirements2 = default;
            memoryRequirements2.sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_REQUIREMENTS_2;

            //PFN_vkGetAccelerationStructureMemoryRequirementsKHR
            //    vkGetAccelerationStructureMemoryRequirementsKHR = null;
            //RESOLVE_VK_DEVICE_PFN(device, vkGetAccelerationStructureMemoryRequirementsKHR);

            VkAccelerationStructureMemoryRequirementsInfoKHR accelerationMemoryRequirements = default;
            accelerationMemoryRequirements.sType =
                 VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_MEMORY_REQUIREMENTS_INFO_KHR;
            accelerationMemoryRequirements.pNext = null;
            accelerationMemoryRequirements.type = type;
            accelerationMemoryRequirements.buildType = VkAccelerationStructureBuildTypeKHR.VK_ACCELERATION_STRUCTURE_BUILD_TYPE_DEVICE_KHR;
            accelerationMemoryRequirements.accelerationStructure = acceleration;
            VulkanNative.vkGetAccelerationStructureMemoryRequirementsKHR(device, &accelerationMemoryRequirements,
                                                            &memoryRequirements2);

            return memoryRequirements2.memoryRequirements;
        }

        void BindAccelerationMemory(VkAccelerationStructureKHR acceleration, VkDeviceMemory memory)
        {
            //PFN_vkBindAccelerationStructureMemoryKHR vkBindAccelerationStructureMemoryKHR = null;
            //RESOLVE_VK_DEVICE_PFN(device, vkBindAccelerationStructureMemoryKHR);

            VkBindAccelerationStructureMemoryInfoKHR accelerationMemoryBindInfo = default;
            accelerationMemoryBindInfo.sType =
                VkStructureType.VK_STRUCTURE_TYPE_BIND_ACCELERATION_STRUCTURE_MEMORY_INFO_KHR;
            accelerationMemoryBindInfo.pNext = null;
            accelerationMemoryBindInfo.accelerationStructure = acceleration;
            accelerationMemoryBindInfo.memory = memory;
            accelerationMemoryBindInfo.memoryOffset = 0;
            accelerationMemoryBindInfo.deviceIndexCount = 0;
            accelerationMemoryBindInfo.pDeviceIndices = null;

            VulkanNative.vkBindAccelerationStructureMemoryKHR(device, 1, &accelerationMemoryBindInfo);
        }

        MappedBuffer CreateAccelerationScratchBuffer(
            VkAccelerationStructureKHR acceleration,
            VkAccelerationStructureMemoryRequirementsTypeKHR type)
        {
            MappedBuffer outValue = default;

            VkMemoryRequirements asRequirements =
                GetAccelerationStructureMemoryRequirements(acceleration, type);

            VkBufferCreateInfo bufferInfo = default;
            bufferInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO;
            bufferInfo.pNext = null;
            bufferInfo.size = asRequirements.size;
            bufferInfo.usage =
                VkBufferUsageFlagBits.VK_BUFFER_USAGE_RAY_TRACING_BIT_KHR | VkBufferUsageFlagBits.VK_BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT;
            bufferInfo.sharingMode = VkSharingMode.VK_SHARING_MODE_EXCLUSIVE;
            bufferInfo.queueFamilyIndexCount = 0;
            bufferInfo.pQueueFamilyIndices = null;
            VulkanNative.vkCreateBuffer(device, &bufferInfo, null, &outValue.buffer);

            VkMemoryRequirements bufRequirements = default;
            VulkanNative.vkGetBufferMemoryRequirements(device, outValue.buffer, &bufRequirements);

            // buffer requirements can differ to AS requirements, so we max them
            ulong alloctionSize =
                asRequirements.size > bufRequirements.size ? asRequirements.size : bufRequirements.size;
            // combine AS and buf mem types
            uint allocationMemoryBits = bufRequirements.memoryTypeBits | asRequirements.memoryTypeBits;

            VkMemoryAllocateFlagsInfo memAllocFlagsInfo = default;
            memAllocFlagsInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_FLAGS_INFO;
            memAllocFlagsInfo.pNext = null;
            memAllocFlagsInfo.flags = VkMemoryAllocateFlagBits.VK_MEMORY_ALLOCATE_DEVICE_ADDRESS_BIT;
            memAllocFlagsInfo.deviceMask = 0;

            VkMemoryAllocateInfo memAllocInfo = default;
            memAllocInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO;
            memAllocInfo.pNext = &memAllocFlagsInfo;
            memAllocInfo.allocationSize = alloctionSize;
            memAllocInfo.memoryTypeIndex =
                FindMemoryType(allocationMemoryBits, VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT);
            VulkanNative.vkAllocateMemory(device, &memAllocInfo, null, &outValue.memory);

            VulkanNative.vkBindBufferMemory(device, outValue.buffer, outValue.memory, 0);

            outValue.memoryAddress = GetBufferAddress(outValue.buffer);

            return outValue;
        }

        void InsertCommandImageBarrier(VkCommandBuffer commandBuffer,
                                       VkImage image,
                                       VkAccessFlagBits srcAccessMask,
                                       VkAccessFlagBits dstAccessMask,
                                       VkImageLayout oldLayout,
                                       VkImageLayout newLayout,
                                        VkImageSubresourceRange subresourceRange)
        {
            VkImageMemoryBarrier imageMemoryBarrier = default;
            imageMemoryBarrier.sType = VkStructureType.VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER;
            imageMemoryBarrier.pNext = null;
            imageMemoryBarrier.srcAccessMask = srcAccessMask;
            imageMemoryBarrier.dstAccessMask = dstAccessMask;
            imageMemoryBarrier.oldLayout = oldLayout;
            imageMemoryBarrier.newLayout = newLayout;
            imageMemoryBarrier.srcQueueFamilyIndex = VulkanNative.VK_QUEUE_FAMILY_IGNORED;
            imageMemoryBarrier.dstQueueFamilyIndex = VulkanNative.VK_QUEUE_FAMILY_IGNORED;
            imageMemoryBarrier.image = image;
            imageMemoryBarrier.subresourceRange = subresourceRange;

            VulkanNative.vkCmdPipelineBarrier(commandBuffer, VkPipelineStageFlagBits.VK_PIPELINE_STAGE_ALL_COMMANDS_BIT,
                                 VkPipelineStageFlagBits.VK_PIPELINE_STAGE_ALL_COMMANDS_BIT, 0, 0, null, 0, null, 1,
                                 &imageMemoryBarrier);
        }

        //LRESULT CALLBACK WndProc(HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam)
        //{
        //    MsgInfo info{ hWnd, uMsg, wParam, lParam};
        //    switch (info.uMsg)
        //    {
        //        case WM_CLOSE:
        //            DestroyWindow(info.hWnd);
        //            PostQuitMessage(0);
        //            break;
        //    }
        //    return (DefWindowProc(hWnd, uMsg, wParam, lParam));
        //}

        bool IsValidationLayerAvailable(string layerName)
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
            // clang-format off
            //PFN_vkGetPhysicalDeviceSurfaceSupportKHR vkGetPhysicalDeviceSurfaceSupportKHR = null;

            //PFN_vkCreateSwapchainKHR vkCreateSwapchainKHR = null;
            //PFN_vkGetSwapchainImagesKHR vkGetSwapchainImagesKHR = null;

            //PFN_vkCreateAccelerationStructureKHR vkCreateAccelerationStructureKHR = null;
            //PFN_vkCreateRayTracingPipelinesKHR vkCreateRayTracingPipelinesKHR = null;
            //PFN_vkCmdBuildAccelerationStructureKHR vkCmdBuildAccelerationStructureKHR = null;
            //PFN_vkDestroyAccelerationStructureKHR vkDestroyAccelerationStructureKHR = null;
            //PFN_vkGetRayTracingShaderGroupHandlesKHR vkGetRayTracingShaderGroupHandlesKHR = null;
            //PFN_vkCmdTraceRaysKHR vkCmdTraceRaysKHR = null;

            //PFN_vkGetAccelerationStructureDeviceAddressKHR vkGetAccelerationStructureDeviceAddressKHR = null;
            // clang-format on

            //TCHAR dest[MAX_PATH];
            //const DWORD length = GetModuleFileName(null, dest, MAX_PATH);
            //PathCchRemoveFileSpec(dest, MAX_PATH);

            //std::wstring basePath = std::wstring(dest);

            //windowInstance = GetModuleHandle(0);

            //WNDCLASSEX wndClass;
            //wndClass.cbSize = sizeof(WNDCLASSEX);
            //wndClass.style = CS_HREDRAW | CS_VREDRAW;
            //wndClass.lpfnWndProc = WndProc;
            //wndClass.cbClsExtra = 0;
            //wndClass.cbWndExtra = 0;
            //wndClass.hInstance = windowInstance;
            //wndClass.hIcon = LoadIcon(null, IDI_APPLICATION);
            //wndClass.hCursor = LoadCursor(null, IDC_ARROW);
            //wndClass.hbrBackground = (HBRUSH)GetStockObject(BLACK_BRUSH);
            //wndClass.lpszMenuName = null;
            //wndClass.lpszClassName = appName.c_str();
            //wndClass.hIconSm = LoadIcon(null, IDI_WINLOGO);

            //if (!RegisterClassEx(&wndClass))
            //{
            //    Debug.WriteLine($"Failed to create window");
            //    return EXIT_FAILURE;
            //}

            //const DWORD exStyle = WS_EX_APPWINDOW | WS_EX_WINDOWEDGE;
            //const DWORD style = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_CLIPSIBLINGS | WS_CLIPCHILDREN;

            //RECT windowRect;
            //windowRect.left = 0;
            //windowRect.top = 0;
            //windowRect.right = desiredWindowWidth;
            //windowRect.bottom = desiredWindowHeight;
            //AdjustWindowRectEx(&windowRect, style, FALSE, exStyle);

            //window = CreateWindowEx(0, appName.c_str(), appName.c_str(),
            //                        style | WS_CLIPSIBLINGS | WS_CLIPCHILDREN, 0, 0,
            //                        windowRect.right - windowRect.left, windowRect.bottom - windowRect.top,
            //                        null, null, windowInstance, null);

            //if (!window)
            //{
            //    Debug.WriteLine($"Failed to create window");
            //    return EXIT_FAILURE;
            //}

            //const uint x = ((uint)GetSystemMetrics(SM_CXSCREEN) - windowRect.right) / 2;
            //const uint y = ((uint)GetSystemMetrics(SM_CYSCREEN) - windowRect.bottom) / 2;
            //SetWindowPos(window, 0, x, y, 0, 0, SWP_NOZORDER | SWP_NOSIZE);

            //ShowWindow(window, SW_SHOW);
            //SetForegroundWindow(window);
            //SetFocus(window);

            window = new Form();
            window.Text = appName;
            window.Size = new System.Drawing.Size((int)640, (int)480);
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

            VkApplicationInfo appInfo = default;
            appInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_APPLICATION_INFO;
            appInfo.pNext = null;
            appInfo.pApplicationName = "Hello Triangle".ToPointer();
            appInfo.applicationVersion = Helpers.Version(1, 0, 0);
            appInfo.pEngineName = "No Engine".ToPointer();
            appInfo.engineVersion = Helpers.Version(1, 0, 0);
            appInfo.apiVersion = Helpers.Version(1, 2, 0);

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

            VkInstanceCreateInfo createInfo = default;
            createInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO;
            createInfo.pNext = null;
            createInfo.pApplicationInfo = &appInfo;
            createInfo.enabledExtensionCount = (uint)instanceExtensions.Length;
            createInfo.ppEnabledExtensionNames = (byte**)extensionsToEnableArray;
            createInfo.enabledLayerCount = (uint)availableValidationLayers.Count;
            createInfo.ppEnabledLayerNames = (byte**)layersToEnableArray;

            fixed (VkInstance* instancePtr = &instance)
            {
                VulkanNative.vkCreateInstance(&createInfo, null, instancePtr);
            }

            uint deviceCount = 0;
            VulkanNative.vkEnumeratePhysicalDevices(instance, &deviceCount, null);
            if (deviceCount <= 0)
            {
                Debug.WriteLine($"No physical devices available");
                return EXIT_FAILURE;
            }
            VkPhysicalDevice[] devices = new VkPhysicalDevice[deviceCount];
            fixed (VkPhysicalDevice* devicesPtr = devices)
            {
                VulkanNative.vkEnumeratePhysicalDevices(instance, &deviceCount, devicesPtr);
            }

            // find RT compatible device
            for (uint ii = 0; ii < devices.Length; ++ii)
            {
                // acquire RT features
                VkPhysicalDeviceRayTracingFeaturesKHR rayTracingFeatures = default;
                rayTracingFeatures.sType = VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_RAY_TRACING_FEATURES_KHR;
                rayTracingFeatures.pNext = null;

                VkPhysicalDeviceFeatures2 deviceFeatures2 = default;
                deviceFeatures2.sType = VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_FEATURES_2;
                deviceFeatures2.pNext = &rayTracingFeatures;
                VulkanNative.vkGetPhysicalDeviceFeatures2(devices[ii], &deviceFeatures2);

                if (rayTracingFeatures.rayTracing == true)
                {
                    physicalDevice = devices[ii];
                    break;
                }
            };

            if (physicalDevice == default)
            {
                Debug.WriteLine($"'No ray tracing compatible GPU found");
            }

            VkPhysicalDeviceProperties deviceProperties = default;
            VulkanNative.vkGetPhysicalDeviceProperties(physicalDevice, &deviceProperties);
            Debug.WriteLine($"GPU: {Helpers.GetString(deviceProperties.deviceName)}");

            float queuePriority = 0.0f;

            VkDeviceQueueCreateInfo deviceQueueInfo = default;
            deviceQueueInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO;
            deviceQueueInfo.pNext = null;
            deviceQueueInfo.queueFamilyIndex = 0;
            deviceQueueInfo.queueCount = 1;
            deviceQueueInfo.pQueuePriorities = &queuePriority;

            VkPhysicalDeviceRayTracingFeaturesKHR deviceRayTracingFeatures = default;
            deviceRayTracingFeatures.sType = VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_RAY_TRACING_FEATURES_KHR;
            deviceRayTracingFeatures.pNext = null;
            deviceRayTracingFeatures.rayTracing = true;

            VkPhysicalDeviceVulkan12Features deviceVulkan12Features = default;
            deviceVulkan12Features.sType = VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_2_FEATURES;
            deviceVulkan12Features.pNext = &deviceRayTracingFeatures;
            deviceVulkan12Features.bufferDeviceAddress = true;

            int deviceExtensionsCount = deviceExtensions.Length;
            IntPtr* deviceExtensionsArray = stackalloc IntPtr[deviceExtensionsCount];
            for (int i = 0; i < deviceExtensionsCount; i++)
            {
                string extension = deviceExtensions[i];
                deviceExtensionsArray[i] = Marshal.StringToHGlobalAnsi(extension);
            }

            VkDeviceCreateInfo deviceInfo = default;
            deviceInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO;
            deviceInfo.pNext = &deviceVulkan12Features;
            deviceInfo.queueCreateInfoCount = 1;
            deviceInfo.pQueueCreateInfos = &deviceQueueInfo;
            deviceInfo.enabledExtensionCount = (uint)deviceExtensions.Length;
            deviceInfo.ppEnabledExtensionNames = (byte**)deviceExtensionsArray;
            deviceInfo.pEnabledFeatures = null;

            fixed (VkDevice* devicePtr = &device)
            {
                VulkanNative.vkCreateDevice(physicalDevice, &deviceInfo, null, devicePtr);
            }

            fixed (VkQueue* queuePtr = &queue)
            {
                VulkanNative.vkGetDeviceQueue(device, 0, 0, queuePtr);
            }

            // clang-format off
            //RESOLVE_VK_INSTANCE_PFN(instance, vkGetPhysicalDeviceSurfaceSupportKHR);

            //RESOLVE_VK_DEVICE_PFN(device, vkCreateSwapchainKHR);
            //RESOLVE_VK_DEVICE_PFN(device, vkGetSwapchainImagesKHR);

            //RESOLVE_VK_DEVICE_PFN(device, vkCreateAccelerationStructureKHR);
            //RESOLVE_VK_DEVICE_PFN(device, vkCreateRayTracingPipelinesKHR);
            //RESOLVE_VK_DEVICE_PFN(device, vkCmdBuildAccelerationStructureKHR);
            //RESOLVE_VK_DEVICE_PFN(device, vkDestroyAccelerationStructureKHR);
            //RESOLVE_VK_DEVICE_PFN(device, vkGetRayTracingShaderGroupHandlesKHR);
            //RESOLVE_VK_DEVICE_PFN(device, vkCmdTraceRaysKHR);
            //RESOLVE_VK_DEVICE_PFN(device, vkGetAccelerationStructureDeviceAddressKHR);
            // clang-format on

            VkWin32SurfaceCreateInfoKHR surfaceCreateInfo;
            surfaceCreateInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_WIN32_SURFACE_CREATE_INFO_KHR;
            surfaceCreateInfo.pNext = null;
            surfaceCreateInfo.flags = 0;
            surfaceCreateInfo.hinstance = Process.GetCurrentProcess().Handle;
            surfaceCreateInfo.hwnd = window.Handle;

            fixed (VkSurfaceKHR* surfacePtr = &surface)
            {
                VulkanNative.vkCreateWin32SurfaceKHR(instance, &surfaceCreateInfo, null, surfacePtr);
            }

            VkBool32 surfaceSupport = false;
            VulkanNative.vkGetPhysicalDeviceSurfaceSupportKHR(physicalDevice, 0, surface, &surfaceSupport);
            if (!surfaceSupport)
            {
                Debug.WriteLine($"No surface rendering support");
                return EXIT_FAILURE;
            }

            VkCommandPoolCreateInfo cmdPoolInfo = default;
            cmdPoolInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO;
            cmdPoolInfo.pNext = null;
            cmdPoolInfo.flags = 0;
            cmdPoolInfo.queueFamilyIndex = 0;

            fixed (VkCommandPool* commandPoolPtr = &commandPool)
            {
                VulkanNative.vkCreateCommandPool(device, &cmdPoolInfo, null, commandPoolPtr);
            }

            // acquire RT properties
            VkPhysicalDeviceRayTracingPropertiesKHR rayTracingProperties = default;
            rayTracingProperties.sType = VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_RAY_TRACING_PROPERTIES_KHR;
            rayTracingProperties.pNext = null;

            VkPhysicalDeviceProperties2 deviceProperties2 = default;
            deviceProperties2.sType = VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_PROPERTIES_2;
            deviceProperties2.pNext = &rayTracingProperties;
            VulkanNative.vkGetPhysicalDeviceProperties2(physicalDevice, &deviceProperties2);

            // create bottom-level container
            {
                Debug.WriteLine($"Creating Bottom-Level Acceleration Structure..");

                VkAccelerationStructureCreateGeometryTypeInfoKHR accelerationCreateGeometryInfo = default;
                accelerationCreateGeometryInfo.sType =
                    VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_CREATE_GEOMETRY_TYPE_INFO_KHR;
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
                accelerationInfo.deviceAddress = 0;

                fixed (VkAccelerationStructureKHR* bottomLevelASPtr = &bottomLevelAS)
                {
                    VulkanNative.vkCreateAccelerationStructureKHR(device, &accelerationInfo, null, bottomLevelASPtr);
                }

                MappedBuffer objectMemory = CreateAccelerationScratchBuffer(
                    bottomLevelAS, VkAccelerationStructureMemoryRequirementsTypeKHR.VK_ACCELERATION_STRUCTURE_MEMORY_REQUIREMENTS_TYPE_OBJECT_KHR);

                BindAccelerationMemory(bottomLevelAS, objectMemory.memory);

                MappedBuffer buildScratchMemory = CreateAccelerationScratchBuffer(
                    bottomLevelAS, VkAccelerationStructureMemoryRequirementsTypeKHR.VK_ACCELERATION_STRUCTURE_MEMORY_REQUIREMENTS_TYPE_BUILD_SCRATCH_KHR);

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

                MappedBuffer vertexBuffer =
                    CreateMappedBuffer(vertices, (uint)(sizeof(float) * vertices.Length));

                MappedBuffer indexBuffer =
                    CreateMappedBuffer(indices, (uint)(sizeof(uint) * indices.Length));

                VkAccelerationStructureGeometryKHR accelerationGeometry = default;
                accelerationGeometry.sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_KHR;
                accelerationGeometry.pNext = null;
                accelerationGeometry.flags = VkGeometryFlagBitsKHR.VK_GEOMETRY_OPAQUE_BIT_KHR;
                accelerationGeometry.geometryType = VkGeometryTypeKHR.VK_GEOMETRY_TYPE_TRIANGLES_KHR;
                accelerationGeometry.geometry = default;
                accelerationGeometry.geometry.triangles = default;
                accelerationGeometry.geometry.triangles.sType =
                     VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_TRIANGLES_DATA_KHR;
                accelerationGeometry.geometry.triangles.pNext = null;
                accelerationGeometry.geometry.triangles.vertexFormat = VkFormat.VK_FORMAT_R32G32B32_SFLOAT;
                accelerationGeometry.geometry.triangles.vertexData.deviceAddress =
                    vertexBuffer.memoryAddress;
                accelerationGeometry.geometry.triangles.vertexStride = 3 * sizeof(float);
                accelerationGeometry.geometry.triangles.indexType = VkIndexType.VK_INDEX_TYPE_UINT32;
                accelerationGeometry.geometry.triangles.indexData.deviceAddress = indexBuffer.memoryAddress;
                accelerationGeometry.geometry.triangles.transformData.deviceAddress = 0;

                VkAccelerationStructureGeometryKHR[] accelerationGeometries = new VkAccelerationStructureGeometryKHR[] { accelerationGeometry };
                VkAccelerationStructureBuildGeometryInfoKHR accelerationBuildGeometryInfo = default;
                fixed (VkAccelerationStructureGeometryKHR* ppGeometries = &accelerationGeometries[0])
                {
                    accelerationBuildGeometryInfo.sType =
                        VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_BUILD_GEOMETRY_INFO_KHR;
                    accelerationBuildGeometryInfo.pNext = null;
                    accelerationBuildGeometryInfo.type = VkAccelerationStructureTypeKHR.VK_ACCELERATION_STRUCTURE_TYPE_BOTTOM_LEVEL_KHR;
                    accelerationBuildGeometryInfo.flags =
                        VkBuildAccelerationStructureFlagBitsKHR.VK_BUILD_ACCELERATION_STRUCTURE_PREFER_FAST_TRACE_BIT_KHR;
                    accelerationBuildGeometryInfo.update = false;
                    accelerationBuildGeometryInfo.srcAccelerationStructure = 0;
                    accelerationBuildGeometryInfo.dstAccelerationStructure = bottomLevelAS;
                    accelerationBuildGeometryInfo.geometryArrayOfPointers = false;
                    accelerationBuildGeometryInfo.geometryCount = 1;
                    accelerationBuildGeometryInfo.ppGeometries = &ppGeometries;
                    accelerationBuildGeometryInfo.scratchData.deviceAddress = buildScratchMemory.memoryAddress;
                }
                VkAccelerationStructureBuildOffsetInfoKHR accelerationBuildOffsetInfo = default;
                accelerationBuildOffsetInfo.primitiveCount = 1;
                accelerationBuildOffsetInfo.primitiveOffset = 0x0;
                accelerationBuildOffsetInfo.firstVertex = 0;
                accelerationBuildOffsetInfo.transformOffset = 0x0;

                VkAccelerationStructureBuildOffsetInfoKHR*[] accelerationBuildOffsets = {
            &accelerationBuildOffsetInfo};

                VkCommandBuffer commandBuffer;

                VkCommandBufferAllocateInfo commandBufferAllocateInfo1 = default;
                commandBufferAllocateInfo1.sType = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO;
                commandBufferAllocateInfo1.pNext = null;
                commandBufferAllocateInfo1.commandPool = commandPool;
                commandBufferAllocateInfo1.level = VkCommandBufferLevel.VK_COMMAND_BUFFER_LEVEL_PRIMARY;
                commandBufferAllocateInfo1.commandBufferCount = 1;
                VulkanNative.
                    vkAllocateCommandBuffers(device, &commandBufferAllocateInfo1, &commandBuffer);

                VkCommandBufferBeginInfo commandBufferBeginInfo1 = default;
                commandBufferBeginInfo1.sType = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO;
                commandBufferBeginInfo1.pNext = null;
                commandBufferBeginInfo1.flags = VkCommandBufferUsageFlagBits.VK_COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT;
                VulkanNative.vkBeginCommandBuffer(commandBuffer, &commandBufferBeginInfo1);

                fixed (VkAccelerationStructureBuildOffsetInfoKHR** accelerationBuildOffsetsPtr = &accelerationBuildOffsets[0])
                {
                    VulkanNative.vkCmdBuildAccelerationStructureKHR(commandBuffer, 1, &accelerationBuildGeometryInfo,
                                                       accelerationBuildOffsetsPtr);
                }

                VulkanNative.vkEndCommandBuffer(commandBuffer);

                VkSubmitInfo submitInfo = default;
                submitInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_SUBMIT_INFO;
                submitInfo.pNext = null;
                submitInfo.commandBufferCount = 1;
                submitInfo.pCommandBuffers = &commandBuffer;

                VkFence fence = 0;
                VkFenceCreateInfo fenceInfo = default;
                fenceInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_FENCE_CREATE_INFO;
                fenceInfo.pNext = null;

                VulkanNative.vkCreateFence(device, &fenceInfo, null, &fence);
                VulkanNative.vkQueueSubmit(queue, 1, &submitInfo, fence);
                VulkanNative.vkWaitForFences(device, 1, &fence, true, ulong.MaxValue);

                VulkanNative.vkDestroyFence(device, fence, null);
                VulkanNative.vkFreeCommandBuffers(device, commandPool, 1, &commandBuffer);

                // make sure bottom AS handle is valid
                if (bottomLevelASHandle == 0)
                {
                    Debug.WriteLine($"Invalid Handle to BLAS");
                    return EXIT_FAILURE;
                }
            }

            // create top-level container
            {
                Debug.WriteLine($"Creating Top-Level Acceleration Structure..");

                VkAccelerationStructureCreateGeometryTypeInfoKHR accelerationCreateGeometryInfo = default;
                accelerationCreateGeometryInfo.sType =
                    VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_CREATE_GEOMETRY_TYPE_INFO_KHR;
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
                accelerationInfo.deviceAddress = 0;

                fixed (VkAccelerationStructureKHR* topLevelASPtr = &topLevelAS)
                {
                    VulkanNative.
                        vkCreateAccelerationStructureKHR(device, &accelerationInfo, null, topLevelASPtr);
                }

                MappedBuffer objectMemory = CreateAccelerationScratchBuffer(
                    topLevelAS, VkAccelerationStructureMemoryRequirementsTypeKHR.VK_ACCELERATION_STRUCTURE_MEMORY_REQUIREMENTS_TYPE_OBJECT_KHR);

                BindAccelerationMemory(topLevelAS, objectMemory.memory);

                MappedBuffer buildScratchMemory = CreateAccelerationScratchBuffer(
                    topLevelAS, VkAccelerationStructureMemoryRequirementsTypeKHR.VK_ACCELERATION_STRUCTURE_MEMORY_REQUIREMENTS_TYPE_BUILD_SCRATCH_KHR);

                VkAccelerationStructureInstanceKHR[] instances = new VkAccelerationStructureInstanceKHR[]
                {
                    new VkAccelerationStructureInstanceKHR()
                    {
                        transform = new VkTransformMatrixKHR()
                        {
                            matrix_0 = 1.0f,
                            matrix_1 = 0.0f,
                            matrix_2 = 0.0f,
                            matrix_3 = 0.0f,

                            matrix_4 = 0.0f,
                            matrix_5 = 1.0f,
                            matrix_6 = 0.0f,
                            matrix_7 = 0.0f,

                            matrix_8 = 0.0f,
                            matrix_9 = 0.0f,
                            matrix_10 = 1.0f,
                            matrix_11 = 0.0f,
                        },
                        instanceCustomIndex = 0,
                        mask = 0xff,
                        instanceShaderBindingTableRecordOffset = 0x0,
                        flags = VkGeometryInstanceFlagBitsKHR.VK_GEOMETRY_INSTANCE_TRIANGLE_FACING_CULL_DISABLE_BIT_KHR,
                        accelerationStructureReference = bottomLevelASHandle,
                    }
                };

                MappedBuffer instanceBuffer = CreateMappedBuffer(
                    instances, (uint)(sizeof(VkAccelerationStructureInstanceKHR) * instances.Length));

                VkAccelerationStructureGeometryKHR accelerationGeometry = default;
                accelerationGeometry.sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_KHR;
                accelerationGeometry.pNext = null;
                accelerationGeometry.flags = VkGeometryFlagBitsKHR.VK_GEOMETRY_OPAQUE_BIT_KHR;
                accelerationGeometry.geometryType = VkGeometryTypeKHR.VK_GEOMETRY_TYPE_INSTANCES_KHR;
                accelerationGeometry.geometry = default;
                accelerationGeometry.geometry.instances = default;
                accelerationGeometry.geometry.instances.sType =
                    VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_INSTANCES_DATA_KHR;
                accelerationGeometry.geometry.instances.pNext = null;
                accelerationGeometry.geometry.instances.arrayOfPointers = false;
                accelerationGeometry.geometry.instances.data.deviceAddress = instanceBuffer.memoryAddress;

                VkAccelerationStructureGeometryKHR[] accelerationGeometries = new VkAccelerationStructureGeometryKHR[]
                        { accelerationGeometry};

                VkAccelerationStructureBuildGeometryInfoKHR accelerationBuildGeometryInfo = default;
                fixed (VkAccelerationStructureGeometryKHR* ppGeometries = &accelerationGeometries[0])
                {

                    accelerationBuildGeometryInfo.sType =
                        VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_BUILD_GEOMETRY_INFO_KHR;
                    accelerationBuildGeometryInfo.pNext = null;
                    accelerationBuildGeometryInfo.type = VkAccelerationStructureTypeKHR.VK_ACCELERATION_STRUCTURE_TYPE_TOP_LEVEL_KHR;
                    accelerationBuildGeometryInfo.flags =
                        VkBuildAccelerationStructureFlagBitsKHR.VK_BUILD_ACCELERATION_STRUCTURE_PREFER_FAST_TRACE_BIT_KHR;
                    accelerationBuildGeometryInfo.update = false;
                    accelerationBuildGeometryInfo.srcAccelerationStructure = 0;
                    accelerationBuildGeometryInfo.dstAccelerationStructure = topLevelAS;
                    accelerationBuildGeometryInfo.geometryArrayOfPointers = false;
                    accelerationBuildGeometryInfo.geometryCount = 1;
                    accelerationBuildGeometryInfo.ppGeometries = &ppGeometries;
                    accelerationBuildGeometryInfo.scratchData.deviceAddress = buildScratchMemory.memoryAddress;
                }

                VkAccelerationStructureBuildOffsetInfoKHR accelerationBuildOffsetInfo = default;
                accelerationBuildOffsetInfo.primitiveCount = 1;
                accelerationBuildOffsetInfo.primitiveOffset = 0x0;
                accelerationBuildOffsetInfo.firstVertex = 0;
                accelerationBuildOffsetInfo.transformOffset = 0x0;

                VkAccelerationStructureBuildOffsetInfoKHR*[] accelerationBuildOffsets = {
            &accelerationBuildOffsetInfo};

                VkCommandBuffer commandBuffer;

                VkCommandBufferAllocateInfo commandBufferAllocateInfo2 = default;
                commandBufferAllocateInfo2.sType = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO;
                commandBufferAllocateInfo2.pNext = null;
                commandBufferAllocateInfo2.commandPool = commandPool;
                commandBufferAllocateInfo2.level = VkCommandBufferLevel.VK_COMMAND_BUFFER_LEVEL_PRIMARY;
                commandBufferAllocateInfo2.commandBufferCount = 1;
                VulkanNative.
                    vkAllocateCommandBuffers(device, &commandBufferAllocateInfo2, &commandBuffer);

                VkCommandBufferBeginInfo commandBufferBeginInfo2 = default;
                commandBufferBeginInfo2.sType = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO;
                commandBufferBeginInfo2.pNext = null;
                commandBufferBeginInfo2.flags = VkCommandBufferUsageFlagBits.VK_COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT;
                VulkanNative.vkBeginCommandBuffer(commandBuffer, &commandBufferBeginInfo2);

                fixed (VkAccelerationStructureBuildOffsetInfoKHR** accelerationBuildOffsetsPtr = &accelerationBuildOffsets[0])
                {
                    VulkanNative.vkCmdBuildAccelerationStructureKHR(commandBuffer, 1, &accelerationBuildGeometryInfo,
                                                       accelerationBuildOffsetsPtr);
                }

                VulkanNative.vkEndCommandBuffer(commandBuffer);

                VkSubmitInfo submitInfo = default;
                submitInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_SUBMIT_INFO;
                submitInfo.pNext = null;
                submitInfo.commandBufferCount = 1;
                submitInfo.pCommandBuffers = &commandBuffer;

                VkFence fence = 0;
                VkFenceCreateInfo fenceInfo = default;
                fenceInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_FENCE_CREATE_INFO;
                fenceInfo.pNext = null;

                VulkanNative.vkCreateFence(device, &fenceInfo, null, &fence);
                VulkanNative.vkQueueSubmit(queue, 1, &submitInfo, fence);
                VulkanNative.vkWaitForFences(device, 1, &fence, true, ulong.MaxValue);

                VulkanNative.vkDestroyFence(device, fence, null);
                VulkanNative.vkFreeCommandBuffers(device, commandPool, 1, &commandBuffer);
            }

            // offscreen buffer
            {
                Debug.WriteLine($"Creating Offsceen Buffer..");

                VkImageCreateInfo imageInfo = default;
                imageInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_IMAGE_CREATE_INFO;
                imageInfo.pNext = null;
                imageInfo.imageType = VkImageType.VK_IMAGE_TYPE_2D;
                imageInfo.format = desiredSurfaceFormat;
                imageInfo.extent = new VkExtent3D() { width = desiredWindowWidth, height = desiredWindowHeight, depth = 1 };
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
                    VulkanNative.vkCreateImage(device, &imageInfo, null, offscreenBufferPtr);
                }

                VkMemoryRequirements memoryRequirements = default;
                VulkanNative.vkGetImageMemoryRequirements(device, offscreenBuffer, &memoryRequirements);

                VkMemoryAllocateInfo memoryAllocateInfo = default;
                memoryAllocateInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO;
                memoryAllocateInfo.pNext = null;
                memoryAllocateInfo.allocationSize = memoryRequirements.size;
                memoryAllocateInfo.memoryTypeIndex =
                    FindMemoryType(memoryRequirements.memoryTypeBits, VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT);

                fixed (VkDeviceMemory* offscreenBufferMemoryPtr = &offscreenBufferMemory)
                {
                    VulkanNative.
                        vkAllocateMemory(device, &memoryAllocateInfo, null, offscreenBufferMemoryPtr);
                }

                VulkanNative.vkBindImageMemory(device, offscreenBuffer, offscreenBufferMemory, 0);

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

                fixed (VkImageView* offscreenBufferViewPtr = &offscreenBufferView)
                {
                    VulkanNative.vkCreateImageView(device, &imageViewInfo, null, offscreenBufferViewPtr);
                }
            }

            // rt descriptor set layout
            {
                Debug.WriteLine($"Creating RT Descriptor Set Layout..");

                VkDescriptorSetLayoutBinding accelerationStructureLayoutBinding = default;
                accelerationStructureLayoutBinding.binding = 0;
                accelerationStructureLayoutBinding.descriptorType =
                    VkDescriptorType.VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR;
                accelerationStructureLayoutBinding.descriptorCount = 1;
                accelerationStructureLayoutBinding.stageFlags = VkShaderStageFlagBits.VK_SHADER_STAGE_RAYGEN_BIT_KHR;
                accelerationStructureLayoutBinding.pImmutableSamplers = null;

                VkDescriptorSetLayoutBinding storageImageLayoutBinding = default;
                storageImageLayoutBinding.binding = 1;
                storageImageLayoutBinding.descriptorType = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_IMAGE;
                storageImageLayoutBinding.descriptorCount = 1;
                storageImageLayoutBinding.stageFlags = VkShaderStageFlagBits.VK_SHADER_STAGE_RAYGEN_BIT_KHR;

                VkDescriptorSetLayoutBinding* bindings = stackalloc VkDescriptorSetLayoutBinding[]
                        { accelerationStructureLayoutBinding, storageImageLayoutBinding };

                VkDescriptorSetLayoutCreateInfo layoutInfo = default;
                layoutInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO;
                layoutInfo.pNext = null;
                layoutInfo.flags = 0;
                layoutInfo.bindingCount = 2;
                layoutInfo.pBindings = bindings;

                fixed (VkDescriptorSetLayout* descriptorSetLayoutPtr = &descriptorSetLayout)
                {
                    VulkanNative.
                        vkCreateDescriptorSetLayout(device, &layoutInfo, null, descriptorSetLayoutPtr);
                }
            }

            // rt descriptor set
            {
                Debug.WriteLine($"Creating RT Descriptor Set..");

                VkDescriptorPoolSize* poolSizes = stackalloc VkDescriptorPoolSize[] {
                    new VkDescriptorPoolSize()
                    {
                        type = VkDescriptorType.VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR,
                        descriptorCount = 1,
                    },
                    new VkDescriptorPoolSize()
                    {
                        type = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_IMAGE,
                        descriptorCount = 1,
                    }
                };

                VkDescriptorPoolCreateInfo descriptorPoolInfo = default;
                descriptorPoolInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_DESCRIPTOR_POOL_CREATE_INFO;
                descriptorPoolInfo.pNext = null;
                descriptorPoolInfo.flags = 0;
                descriptorPoolInfo.maxSets = 1;
                descriptorPoolInfo.poolSizeCount = 2;
                descriptorPoolInfo.pPoolSizes = poolSizes;

                fixed (VkDescriptorPool* descriptorPoolPtr = &descriptorPool)
                {
                    VulkanNative.
                        vkCreateDescriptorPool(device, &descriptorPoolInfo, null, descriptorPoolPtr);
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
                    VulkanNative.
                    vkAllocateDescriptorSets(device, &descriptorSetAllocateInfo, descriptorSetPtr);
                }

                VkWriteDescriptorSetAccelerationStructureKHR descriptorAccelerationStructureInfo;
                descriptorAccelerationStructureInfo.sType =
                    VkStructureType.VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET_ACCELERATION_STRUCTURE_KHR;
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
                storageImageInfo.sampler = 0;
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

                VkWriteDescriptorSet* descriptorWrites = stackalloc VkWriteDescriptorSet[]
                        { accelerationStructureWrite, outputImageWrite };

                VulkanNative.vkUpdateDescriptorSets(device, 2, descriptorWrites, 0,
                                       null);
            }

            // rt pipeline layout
            {
                Debug.WriteLine($"Creating RT Pipeline Layout..");

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
                    VulkanNative.
                        vkCreatePipelineLayout(device, &pipelineLayoutInfo, null, pipelineLayoutPtr);
                }
            }

            // rt pipeline
            {
                Debug.WriteLine($"Creating RT Pipeline..");

                //std::string basePath = GetExecutablePath() + "/../../shaders";

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

                VkPipelineShaderStageCreateInfo* shaderStages = stackalloc VkPipelineShaderStageCreateInfo[] {
                    rayGenShaderStageInfo,
                    rayChitShaderStageInfo,
                    rayMissShaderStageInfo
                };

                VkRayTracingShaderGroupCreateInfoKHR rayGenGroup = default;
                rayGenGroup.sType = VkStructureType.VK_STRUCTURE_TYPE_RAY_TRACING_SHADER_GROUP_CREATE_INFO_KHR;
                rayGenGroup.pNext = null;
                rayGenGroup.type = VkRayTracingShaderGroupTypeKHR.VK_RAY_TRACING_SHADER_GROUP_TYPE_GENERAL_KHR;
                rayGenGroup.generalShader = 0;
                rayGenGroup.closestHitShader = VK_SHADER_UNUSED_KHR;
                rayGenGroup.anyHitShader = VK_SHADER_UNUSED_KHR;
                rayGenGroup.intersectionShader = VK_SHADER_UNUSED_KHR;

                VkRayTracingShaderGroupCreateInfoKHR rayHitGroup = default;
                rayHitGroup.sType = VkStructureType.VK_STRUCTURE_TYPE_RAY_TRACING_SHADER_GROUP_CREATE_INFO_KHR;
                rayHitGroup.pNext = null;
                rayHitGroup.type = VkRayTracingShaderGroupTypeKHR.VK_RAY_TRACING_SHADER_GROUP_TYPE_TRIANGLES_HIT_GROUP_KHR;
                rayHitGroup.generalShader = VK_SHADER_UNUSED_KHR;
                rayHitGroup.closestHitShader = 1;
                rayHitGroup.anyHitShader = VK_SHADER_UNUSED_KHR;
                rayHitGroup.intersectionShader = VK_SHADER_UNUSED_KHR;

                VkRayTracingShaderGroupCreateInfoKHR rayMissGroup = default;
                rayMissGroup.sType = VkStructureType.VK_STRUCTURE_TYPE_RAY_TRACING_SHADER_GROUP_CREATE_INFO_KHR;
                rayMissGroup.pNext = null;
                rayMissGroup.type = VkRayTracingShaderGroupTypeKHR.VK_RAY_TRACING_SHADER_GROUP_TYPE_GENERAL_KHR;
                rayMissGroup.generalShader = 2;
                rayMissGroup.closestHitShader = VK_SHADER_UNUSED_KHR;
                rayMissGroup.anyHitShader = VK_SHADER_UNUSED_KHR;
                rayMissGroup.intersectionShader = VK_SHADER_UNUSED_KHR;

                VkRayTracingShaderGroupCreateInfoKHR* shaderGroups = stackalloc VkRayTracingShaderGroupCreateInfoKHR[] {
                    rayGenGroup,
                    rayHitGroup,
                    rayMissGroup
                };

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
                pipelineInfo.basePipelineHandle = 0;
                pipelineInfo.basePipelineIndex = 0;

                fixed (VkPipeline* pipelinePtr = &pipeline)
                {
                    VulkanNative.
                        vkCreateRayTracingPipelinesKHR(device, 0, 1, &pipelineInfo, null, pipelinePtr);
                }
            }

            // shader binding table
            {
                Debug.WriteLine($"Creating Shader Binding Table..");

                shaderBindingTable = default;
                shaderBindingTableSize =
                    shaderBindingTableGroupCount * rayTracingProperties.shaderGroupHandleSize;

                VkBufferCreateInfo bufferInfo = default;
                bufferInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO;
                bufferInfo.pNext = null;
                bufferInfo.size = shaderBindingTableSize;
                bufferInfo.usage = VkBufferUsageFlagBits.VK_BUFFER_USAGE_TRANSFER_SRC_BIT;
                bufferInfo.sharingMode = VkSharingMode.VK_SHARING_MODE_EXCLUSIVE;
                bufferInfo.queueFamilyIndexCount = 0;
                bufferInfo.pQueueFamilyIndices = null;
                VkBuffer newBuffer;
                VulkanNative.vkCreateBuffer(device, &bufferInfo, null, &newBuffer);
                shaderBindingTable.buffer = newBuffer;

                VkMemoryRequirements memoryRequirements = default;
                VulkanNative.vkGetBufferMemoryRequirements(device, shaderBindingTable.buffer, &memoryRequirements);

                VkMemoryAllocateInfo memAllocInfo = default;
                memAllocInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO;
                memAllocInfo.pNext = null;
                memAllocInfo.allocationSize = memoryRequirements.size;
                memAllocInfo.memoryTypeIndex =
                    FindMemoryType(memoryRequirements.memoryTypeBits, VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT);

                VkDeviceMemory newMemory;
                VulkanNative.
                    vkAllocateMemory(device, &memAllocInfo, null, &newMemory);
                shaderBindingTable.memory = newMemory;

                VulkanNative.
                    vkBindBufferMemory(device, shaderBindingTable.buffer, shaderBindingTable.memory, 0);

                void* dstData;
                VulkanNative.
                    vkMapMemory(device, shaderBindingTable.memory, 0, shaderBindingTableSize, 0, &dstData);

                VulkanNative.vkGetRayTracingShaderGroupHandlesKHR(device, pipeline, 0, shaderBindingTableGroupCount,
                                                     new UIntPtr(shaderBindingTableSize), dstData); // TODO UIntPtr
                VulkanNative.vkUnmapMemory(device, shaderBindingTable.memory);
            }

            Debug.WriteLine($"Initializing Swapchain..");

            uint presentModeCount = 0;
            VulkanNative.vkGetPhysicalDeviceSurfacePresentModesKHR(physicalDevice, surface,
                                                                       &presentModeCount, null);

            VkPresentModeKHR[] presentModes = new VkPresentModeKHR[presentModeCount];
            fixed (VkPresentModeKHR* presentModesPtr = presentModes)
            {
                VulkanNative.vkGetPhysicalDeviceSurfacePresentModesKHR(physicalDevice, surface, &presentModeCount,
                                                          presentModesPtr);
            }

            bool isMailboxModeSupported = presentModes.Any(m => m == VkPresentModeKHR.VK_PRESENT_MODE_MAILBOX_KHR);

            VkSurfaceCapabilitiesKHR capabilitiesKHR;
            var result = VulkanNative.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(physicalDevice, surface, &capabilitiesKHR);
            Helpers.CheckErrors(result);

            var extent = ChooseSwapExtent(capabilitiesKHR);
            VkSwapchainCreateInfoKHR swapchainInfo = default;
            swapchainInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_SWAPCHAIN_CREATE_INFO_KHR;
            swapchainInfo.pNext = null;
            swapchainInfo.surface = surface;
            swapchainInfo.minImageCount = 3;
            swapchainInfo.imageFormat = desiredSurfaceFormat;
            swapchainInfo.imageColorSpace = VkColorSpaceKHR.VK_COLOR_SPACE_SRGB_NONLINEAR_KHR;
            swapchainInfo.imageExtent.width = extent.width; //desiredWindowWidth;
            swapchainInfo.imageExtent.height = extent.height; //desiredWindowHeight;
            swapchainInfo.imageArrayLayers = 1;
            swapchainInfo.imageUsage =
                VkImageUsageFlagBits.VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT | VkImageUsageFlagBits.VK_IMAGE_USAGE_TRANSFER_DST_BIT;
            swapchainInfo.imageSharingMode = VkSharingMode.VK_SHARING_MODE_EXCLUSIVE;
            swapchainInfo.queueFamilyIndexCount = 0;
            swapchainInfo.preTransform = VkSurfaceTransformFlagBitsKHR.VK_SURFACE_TRANSFORM_IDENTITY_BIT_KHR;
            swapchainInfo.compositeAlpha = VkCompositeAlphaFlagBitsKHR.VK_COMPOSITE_ALPHA_OPAQUE_BIT_KHR;
            swapchainInfo.presentMode =
                isMailboxModeSupported ? VkPresentModeKHR.VK_PRESENT_MODE_MAILBOX_KHR : VkPresentModeKHR.VK_PRESENT_MODE_FIFO_KHR;
            swapchainInfo.clipped = true;
            swapchainInfo.oldSwapchain = 0;

            fixed (VkSwapchainKHR* swapchainPtr = &swapchain)
            {
                VulkanNative.vkCreateSwapchainKHR(device, &swapchainInfo, null, swapchainPtr);
            }

            uint amountOfImagesInSwapchain = 0;
            VulkanNative.vkGetSwapchainImagesKHR(device, swapchain, &amountOfImagesInSwapchain, null);
            VkImage[] swapchainImages = new VkImage[amountOfImagesInSwapchain];

            fixed (VkImage* swapchainImagesPtr = &swapchainImages[0])
            {
                VulkanNative.vkGetSwapchainImagesKHR(device, swapchain, &amountOfImagesInSwapchain,
                                                         swapchainImagesPtr);
            }

            VkImageView[] imageViews = new VkImageView[amountOfImagesInSwapchain];

            for (uint ii = 0; ii < amountOfImagesInSwapchain; ++ii)
            {
                VkImageViewCreateInfo imageViewInfo = default;
                imageViewInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO;
                imageViewInfo.pNext = null;
                imageViewInfo.image = swapchainImages[ii];
                imageViewInfo.viewType = VkImageViewType.VK_IMAGE_VIEW_TYPE_2D;
                imageViewInfo.format = desiredSurfaceFormat;
                imageViewInfo.subresourceRange.aspectMask = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT;
                imageViewInfo.subresourceRange.baseMipLevel = 0;
                imageViewInfo.subresourceRange.levelCount = 1;
                imageViewInfo.subresourceRange.baseArrayLayer = 0;
                imageViewInfo.subresourceRange.layerCount = 1;

                fixed (VkImageView* imagesViewPtr = &imageViews[ii])
                {
                    VulkanNative.vkCreateImageView(device, &imageViewInfo, null, imagesViewPtr);
                }
            };

            Debug.WriteLine($"Recording frame commands..");

            VkImageCopy copyRegion = default;
            copyRegion.srcSubresource = default;
            copyRegion.srcSubresource.aspectMask = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT;
            copyRegion.srcSubresource.mipLevel = 0;
            copyRegion.srcSubresource.baseArrayLayer = 0;
            copyRegion.srcSubresource.layerCount = 1;
            copyRegion.dstSubresource.aspectMask = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT;
            copyRegion.dstSubresource.mipLevel = 0;
            copyRegion.dstSubresource.baseArrayLayer = 0;
            copyRegion.dstSubresource.layerCount = 1;
            copyRegion.extent = default;
            copyRegion.extent.depth = 1;
            copyRegion.extent.width = desiredWindowWidth;
            copyRegion.extent.height = desiredWindowHeight;

            VkImageSubresourceRange subresourceRange = default;
            subresourceRange.aspectMask = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT;
            subresourceRange.baseMipLevel = 0;
            subresourceRange.levelCount = 1;
            subresourceRange.baseArrayLayer = 0;
            subresourceRange.layerCount = 1;

            // clang-format off
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
            // clang-format on

            VkCommandBufferAllocateInfo commandBufferAllocateInfo = default;
            commandBufferAllocateInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO;
            commandBufferAllocateInfo.pNext = null;
            commandBufferAllocateInfo.commandPool = commandPool;
            commandBufferAllocateInfo.level = VkCommandBufferLevel.VK_COMMAND_BUFFER_LEVEL_PRIMARY;
            commandBufferAllocateInfo.commandBufferCount = amountOfImagesInSwapchain;

            commandBuffers = new VkCommandBuffer[amountOfImagesInSwapchain];

            fixed (VkCommandBuffer* commandBuffersPtr = &commandBuffers[0])
            {
                VulkanNative.
                    vkAllocateCommandBuffers(device, &commandBufferAllocateInfo, commandBuffersPtr);
            }

            VkCommandBufferBeginInfo commandBufferBeginInfo = default;
            commandBufferBeginInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO;
            commandBufferBeginInfo.pNext = null;
            commandBufferBeginInfo.flags = 0;
            commandBufferBeginInfo.pInheritanceInfo = null;

            for (uint ii = 0; ii < amountOfImagesInSwapchain; ++ii)
            {
                VkCommandBuffer commandBuffer = commandBuffers[ii];
                VkImage swapchainImage = swapchainImages[ii];

                VulkanNative.vkBeginCommandBuffer(commandBuffer, &commandBufferBeginInfo);

                // transition offscreen buffer into shader writeable state
                InsertCommandImageBarrier(commandBuffer, offscreenBuffer, 0, VkAccessFlagBits.VK_ACCESS_SHADER_WRITE_BIT,
                                          VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED, VkImageLayout.VK_IMAGE_LAYOUT_GENERAL,
                                          subresourceRange);

                VulkanNative.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_RAY_TRACING_KHR, pipeline);
                fixed (VkDescriptorSet* descriptorSetPtr = &descriptorSet)
                {
                    VulkanNative.vkCmdBindDescriptorSets(commandBuffer, VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_RAY_TRACING_KHR,
                                        pipelineLayout, 0, 1, descriptorSetPtr, 0, (uint*)0);
                }
                VulkanNative.vkCmdTraceRaysKHR(commandBuffer, &rayGenSBT, &rayMissSBT, &rayHitSBT, &rayCallSBT,
                                  desiredWindowWidth, desiredWindowHeight, 1);

                // transition swapchain image into copy destination state
                InsertCommandImageBarrier(commandBuffer, swapchainImage, 0, VkAccessFlagBits.VK_ACCESS_TRANSFER_WRITE_BIT,
                                          VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                                          VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, subresourceRange);

                // transition offscreen buffer into copy source state
                InsertCommandImageBarrier(commandBuffer, offscreenBuffer, VkAccessFlagBits.VK_ACCESS_SHADER_WRITE_BIT,
                                          VkAccessFlagBits.VK_ACCESS_TRANSFER_READ_BIT, VkImageLayout.VK_IMAGE_LAYOUT_GENERAL,
                                          VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL, subresourceRange);

                // copy offscreen buffer into swapchain image
                VulkanNative.vkCmdCopyImage(commandBuffer, offscreenBuffer, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                               swapchainImage, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, 1, &copyRegion);

                // transition swapchain image into presentable state
                InsertCommandImageBarrier(commandBuffer, swapchainImage, 0, VkAccessFlagBits.VK_ACCESS_TRANSFER_WRITE_BIT,
                                          VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                                          VkImageLayout.VK_IMAGE_LAYOUT_PRESENT_SRC_KHR, subresourceRange);

                VulkanNative.vkEndCommandBuffer(commandBuffer);
            }

            VkSemaphoreCreateInfo semaphoreInfo = default;
            semaphoreInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_SEMAPHORE_CREATE_INFO;
            semaphoreInfo.pNext = null;

            fixed (VkSemaphore* semaphoreImageAvailablePtr = &semaphoreImageAvailable)
            {
                VulkanNative.vkCreateSemaphore(device, &semaphoreInfo, null, semaphoreImageAvailablePtr);
            }

            fixed (VkSemaphore* semaphoreRenderingAvailablePtr = &semaphoreRenderingAvailable)
            {
                VulkanNative.
                    vkCreateSemaphore(device, &semaphoreInfo, null, semaphoreRenderingAvailablePtr);
            }

            Debug.WriteLine($"Done!");
            Debug.WriteLine($"Drawing..");

            //MSG msg;
            //bool quitMessageReceived = false;
            //while (!quitMessageReceived)
            //{
            //    while (PeekMessage(&msg, null, 0, 0, PM_REMOVE))
            //    {
            //        TranslateMessage(&msg);
            //        DispatchMessage(&msg);
            //        if (msg.message == WM_QUIT)
            //        {
            //            quitMessageReceived = true;
            //            break;
            //        }
            //    }
            //    if (!quitMessageReceived)
            //    {
            bool isClosing = false;
            window.FormClosing += (s, e) =>
            {
                isClosing = true;
            };

            while (!isClosing)
            {
                uint imageIndex = 0;
                VulkanNative.vkAcquireNextImageKHR(device, swapchain, ulong.MaxValue,
                                                       semaphoreImageAvailable, 0, &imageIndex);

                VkPipelineStageFlagBits* waitStageMasks = stackalloc VkPipelineStageFlagBits[] { VkPipelineStageFlagBits.VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT };

                VkSubmitInfo submitInfo = default;
                submitInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_SUBMIT_INFO;
                submitInfo.pNext = null;
                submitInfo.waitSemaphoreCount = 1;
                fixed (VkSemaphore* semaphoreImageAvailablePtr = &semaphoreImageAvailable)
                {
                    submitInfo.pWaitSemaphores = semaphoreImageAvailablePtr;
                }
                submitInfo.pWaitDstStageMask = waitStageMasks;
                submitInfo.commandBufferCount = 1;
                fixed (VkCommandBuffer* commandBuffersPtr = &commandBuffers[imageIndex])
                {
                    submitInfo.pCommandBuffers = commandBuffersPtr;
                }
                submitInfo.signalSemaphoreCount = 1;
                fixed (VkSemaphore* semaphoreRenderingAvailablePtr = &semaphoreRenderingAvailable)
                {
                    submitInfo.pSignalSemaphores = semaphoreRenderingAvailablePtr;
                }

                VulkanNative.vkQueueSubmit(queue, 1, &submitInfo, 0);

                VkPresentInfoKHR presentInfo = default;
                presentInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_PRESENT_INFO_KHR;
                presentInfo.pNext = null;
                presentInfo.waitSemaphoreCount = 1;
                fixed (VkSemaphore* semaphoreRenderingAvailablePtr = &semaphoreRenderingAvailable)
                {
                    presentInfo.pWaitSemaphores = semaphoreRenderingAvailablePtr;
                }
                presentInfo.swapchainCount = 1;
                fixed (VkSwapchainKHR* swapchainPtr = &swapchain)
                {
                    presentInfo.pSwapchains = swapchainPtr;
                }
                presentInfo.pImageIndices = &imageIndex;
                presentInfo.pResults = null;

                VulkanNative.vkQueuePresentKHR(queue, &presentInfo);

                VulkanNative.vkQueueWaitIdle(queue);

                Application.DoEvents();
            }

            return EXIT_SUCCESS;
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
    }
}
