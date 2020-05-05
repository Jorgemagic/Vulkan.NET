using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Vortice.Mathematics;
using Vortice.Vulkan;

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

        //#define Vulkan.r)                                                                    \
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
            public IntPtr memoryAddress;
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
        IntPtr bottomLevelASHandle = IntPtr.Zero;
        VkAccelerationStructureKHR topLevelAS;
        IntPtr topLevelASHandle = IntPtr.Zero;

        uint desiredWindowWidth = 624;
        uint desiredWindowHeight = 441;
        VkFormat desiredSurfaceFormat = VkFormat.B8G8R8A8UNorm;

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
                shaderModuleInfo.sType = VkStructureType.ShaderModuleCreateInfo;
                shaderModuleInfo.pNext = null;
                shaderModuleInfo.codeSize = (UIntPtr)code.Length;
                shaderModuleInfo.pCode = (uint*)sourcePointer;
                Vulkan.vkCreateShaderModule(device, &shaderModuleInfo, null, out shaderModule);
            }
            return shaderModule;
        }

        uint FindMemoryType(uint typeFilter, VkMemoryPropertyFlags properties)
        {
            VkPhysicalDeviceMemoryProperties memProperties;
            Vulkan.vkGetPhysicalDeviceMemoryProperties(physicalDevice, out memProperties);
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

        IntPtr GetBufferAddress(VkBuffer buffer)
        {
            //PFN_vkGetBufferDeviceAddressKHR vkGetBufferDeviceAddressKHR = null;
            //RESOLVE_VK_DEVICE_PFN(device, vkGetBufferDeviceAddressKHR);

            VkBufferDeviceAddressInfo bufferAddressInfo = default;
            bufferAddressInfo.sType = VkStructureType.BufferDeviceAddressInfo;
            bufferAddressInfo.pNext = null;
            bufferAddressInfo.buffer = buffer;

            return Vulkan.vkGetBufferDeviceAddress(device, &bufferAddressInfo);
        }

        MappedBuffer CreateMappedBuffer<T>(T[] srcData, uint byteLength)
        {
            MappedBuffer outValue = default;

            VkBufferCreateInfo bufferInfo = default;
            bufferInfo.sType = VkStructureType.BufferCreateInfo;
            bufferInfo.pNext = null;
            bufferInfo.size = byteLength;
            bufferInfo.usage = VkBufferUsageFlags.ShaderDeviceAddressKHR;
            bufferInfo.sharingMode = VkSharingMode.Exclusive;
            bufferInfo.queueFamilyIndexCount = 0;
            bufferInfo.pQueueFamilyIndices = null;
            Vulkan.vkCreateBuffer(device, &bufferInfo, null, out outValue.buffer);

            VkMemoryRequirements memoryRequirements = default;
            Vulkan.vkGetBufferMemoryRequirements(device, outValue.buffer, out memoryRequirements);

            VkMemoryAllocateFlagsInfo memAllocFlagsInfo = default;
            memAllocFlagsInfo.sType = VkStructureType.MemoryAllocateFlagsInfo;
            memAllocFlagsInfo.pNext = null;
            memAllocFlagsInfo.flags = VkMemoryAllocateFlags.DeviceAddressKHR;
            memAllocFlagsInfo.deviceMask = 0;

            VkMemoryAllocateInfo memAllocInfo = default;
            memAllocInfo.sType = VkStructureType.MemoryAllocateInfo;
            memAllocInfo.pNext = &memAllocFlagsInfo;
            memAllocInfo.allocationSize = memoryRequirements.size;
            memAllocInfo.memoryTypeIndex =
                FindMemoryType(memoryRequirements.memoryTypeBits,
                               VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent);
            Vulkan.vkAllocateMemory(device, &memAllocInfo, null, &outValue.memory);

            Vulkan.vkBindBufferMemory(device, outValue.buffer, outValue.memory, 0);

            outValue.memoryAddress = GetBufferAddress(outValue.buffer);

            IntPtr dstData;
            Vulkan.vkMapMemory(device, outValue.memory, 0, byteLength, 0, (void**)&dstData);
            if (srcData != null)
            {
                GCHandle gcHandle = GCHandle.Alloc(srcData, GCHandleType.Pinned);
                Unsafe.CopyBlock((void*)dstData, (void*)gcHandle.AddrOfPinnedObject(), byteLength);
                gcHandle.Free();
            }
            Vulkan.vkUnmapMemory(device, outValue.memory);
            outValue.mappedPointer = dstData;

            return outValue;
        }

        VkMemoryRequirements GetAccelerationStructureMemoryRequirements(
            VkAccelerationStructureKHR acceleration,
            VkAccelerationStructureMemoryRequirementsTypeKHR type)
        {
            VkMemoryRequirements2 memoryRequirements2 = default;
            memoryRequirements2.sType = VkStructureType.MemoryRequirements2;

            //PFN_vkGetAccelerationStructureMemoryRequirementsKHR
            //    vkGetAccelerationStructureMemoryRequirementsKHR = null;
            //RESOLVE_VK_DEVICE_PFN(device, vkGetAccelerationStructureMemoryRequirementsKHR);

            VkAccelerationStructureMemoryRequirementsInfoKHR accelerationMemoryRequirements = default;
            accelerationMemoryRequirements.sType =
                 VkStructureType.AccelerationStructureMemoryRequirementsInfoKHR;
            accelerationMemoryRequirements.pNext = null;
            accelerationMemoryRequirements.type = type;
            accelerationMemoryRequirements.buildType = VkAccelerationStructureBuildTypeKHR.DeviceKHR;
            accelerationMemoryRequirements.accelerationStructure = acceleration;
            Vulkan.vkGetAccelerationStructureMemoryRequirementsKHR(device, &accelerationMemoryRequirements,
                                                            &memoryRequirements2);

            return memoryRequirements2.memoryRequirements;
        }

        void BindAccelerationMemory(VkAccelerationStructureKHR acceleration, VkDeviceMemory memory)
        {
            //PFN_vkBindAccelerationStructureMemoryKHR vkBindAccelerationStructureMemoryKHR = null;
            //RESOLVE_VK_DEVICE_PFN(device, vkBindAccelerationStructureMemoryKHR);

            VkBindAccelerationStructureMemoryInfoKHR accelerationMemoryBindInfo = default;
            accelerationMemoryBindInfo.sType =
                VkStructureType.BindAccelerationStructureMemoryInfoKHR;
            accelerationMemoryBindInfo.pNext = null;
            accelerationMemoryBindInfo.accelerationStructure = acceleration;
            accelerationMemoryBindInfo.memory = memory;
            accelerationMemoryBindInfo.memoryOffset = 0;
            accelerationMemoryBindInfo.deviceIndexCount = 0;
            accelerationMemoryBindInfo.pDeviceIndices = null;

            Vulkan.vkBindAccelerationStructureMemoryKHR(device, 1, &accelerationMemoryBindInfo);
        }

        MappedBuffer CreateAccelerationScratchBuffer(
            VkAccelerationStructureKHR acceleration,
            VkAccelerationStructureMemoryRequirementsTypeKHR type)
        {
            MappedBuffer outValue = default;

            VkMemoryRequirements asRequirements =
                GetAccelerationStructureMemoryRequirements(acceleration, type);

            VkBufferCreateInfo bufferInfo = default;
            bufferInfo.sType = VkStructureType.BufferCreateInfo;
            bufferInfo.pNext = null;
            bufferInfo.size = asRequirements.size;
            bufferInfo.usage =
                VkBufferUsageFlags.RayTracingKHR | VkBufferUsageFlags.ShaderDeviceAddressKHR;
            bufferInfo.sharingMode = VkSharingMode.Exclusive;
            bufferInfo.queueFamilyIndexCount = 0;
            bufferInfo.pQueueFamilyIndices = null;
            Vulkan.vkCreateBuffer(device, &bufferInfo, null, out outValue.buffer);

            VkMemoryRequirements bufRequirements = default;
            Vulkan.vkGetBufferMemoryRequirements(device, outValue.buffer, out bufRequirements);

            // buffer requirements can differ to AS requirements, so we max them
            ulong alloctionSize =
                asRequirements.size > bufRequirements.size ? asRequirements.size : bufRequirements.size;
            // combine AS and buf mem types
            uint allocationMemoryBits = bufRequirements.memoryTypeBits | asRequirements.memoryTypeBits;

            VkMemoryAllocateFlagsInfo memAllocFlagsInfo = default;
            memAllocFlagsInfo.sType = VkStructureType.MemoryAllocateFlagsInfo;
            memAllocFlagsInfo.pNext = null;
            memAllocFlagsInfo.flags = VkMemoryAllocateFlags.DeviceAddressKHR;
            memAllocFlagsInfo.deviceMask = 0;

            VkMemoryAllocateInfo memAllocInfo = default;
            memAllocInfo.sType = VkStructureType.MemoryAllocateInfo;
            memAllocInfo.pNext = &memAllocFlagsInfo;
            memAllocInfo.allocationSize = alloctionSize;
            memAllocInfo.memoryTypeIndex =
                FindMemoryType(allocationMemoryBits, VkMemoryPropertyFlags.DeviceLocal);
            Vulkan.vkAllocateMemory(device, &memAllocInfo, null, &outValue.memory);

            Vulkan.vkBindBufferMemory(device, outValue.buffer, outValue.memory, 0);

            outValue.memoryAddress = GetBufferAddress(outValue.buffer);

            return outValue;
        }

        void InsertCommandImageBarrier(VkCommandBuffer commandBuffer,
                                       VkImage image,
                                       VkAccessFlags srcAccessMask,
                                       VkAccessFlags dstAccessMask,
                                       VkImageLayout oldLayout,
                                       VkImageLayout newLayout,
                                        VkImageSubresourceRange subresourceRange)
        {
            VkImageMemoryBarrier imageMemoryBarrier = default;
            imageMemoryBarrier.sType = VkStructureType.ImageMemoryBarrier;
            imageMemoryBarrier.pNext = null;
            imageMemoryBarrier.srcAccessMask = srcAccessMask;
            imageMemoryBarrier.dstAccessMask = dstAccessMask;
            imageMemoryBarrier.oldLayout = oldLayout;
            imageMemoryBarrier.newLayout = newLayout;
            imageMemoryBarrier.srcQueueFamilyIndex = Vulkan.QueueFamilyIgnored;
            imageMemoryBarrier.dstQueueFamilyIndex = Vulkan.QueueFamilyIgnored;
            imageMemoryBarrier.image = image;
            imageMemoryBarrier.subresourceRange = subresourceRange;

            Vulkan.vkCmdPipelineBarrier(commandBuffer, VkPipelineStageFlags.AllCommands,
                                 VkPipelineStageFlags.AllCommands, 0, 0, null, 0, null, 1,
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
            /*uint propertyCount = 0;
            Vulkan.vkEnumerateInstanceLayerProperties(&propertyCount, null);
            VkLayerProperties* properties = stackalloc VkLayerProperties[(int)propertyCount];
            var result = Vulkan.vkEnumerateInstanceLayerProperties(&propertyCount, properties);
            Helpers.CheckErrors(result);

            // loop through all toggled layers and check if we can enable each
            for (uint ii = 0; ii < propertyCount; ++ii)
            {
                string pLayerName = Helpers.GetString(properties[ii].layerName);
                if (layerName.Equals(pLayerName))
                    return true;
            }

            return false;*/
            return true;
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

            Vulkan.vkInitialize();

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
            appInfo.sType = VkStructureType.ApplicationInfo;
            appInfo.pNext = null;
            appInfo.pApplicationName = "Hello Triangle".ToPointer();
            appInfo.applicationVersion = new VkVersion(1, 0, 0);
            appInfo.pEngineName = "No Engine".ToPointer();
            appInfo.engineVersion = new VkVersion(1, 0, 0);
            appInfo.apiVersion = new VkVersion(1, 2, 0);

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
            createInfo.sType = VkStructureType.InstanceCreateInfo;
            createInfo.pNext = null;
            createInfo.pApplicationInfo = &appInfo;
            createInfo.enabledExtensionCount = (uint)instanceExtensions.Length;
            createInfo.ppEnabledExtensionNames = (byte**)extensionsToEnableArray;
            createInfo.enabledLayerCount = (uint)availableValidationLayers.Count;
            createInfo.ppEnabledLayerNames = (byte**)layersToEnableArray;
            
            Helpers.CheckErrors(Vulkan.vkCreateInstance(&createInfo, null, out instance));
            Vulkan.vkLoadInstance(instance);

            uint deviceCount = 0;
            Vulkan.vkEnumeratePhysicalDevices(instance, &deviceCount, null);
            if (deviceCount <= 0)
            {
                Debug.WriteLine($"No physical devices available");
                return EXIT_FAILURE;
            }
            VkPhysicalDevice[] devices = new VkPhysicalDevice[deviceCount];
            fixed (VkPhysicalDevice* devicesPtr = devices)
            {
                Vulkan.vkEnumeratePhysicalDevices(instance, &deviceCount, devicesPtr);
            }

            // find RT compatible device
            for (uint ii = 0; ii < devices.Length; ++ii)
            {
                // acquire RT features
                VkPhysicalDeviceRayTracingFeaturesKHR rayTracingFeatures = default;
                rayTracingFeatures.sType = VkStructureType.PhysicalDeviceRayTracingFeaturesKHR;
                rayTracingFeatures.pNext = null;

                VkPhysicalDeviceFeatures2 deviceFeatures2 = default;
                deviceFeatures2.sType = VkStructureType.PhysicalDeviceFeatures2;
                deviceFeatures2.pNext = &rayTracingFeatures;
                Vulkan.vkGetPhysicalDeviceFeatures2(devices[ii], out deviceFeatures2);

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
            Vulkan.vkGetPhysicalDeviceProperties(physicalDevice, out deviceProperties);
            Debug.WriteLine($"GPU: {Helpers.GetString(deviceProperties.deviceName)}");

            float queuePriority = 0.0f;

            VkDeviceQueueCreateInfo deviceQueueInfo = default;
            deviceQueueInfo.sType = VkStructureType.DeviceQueueCreateInfo;
            deviceQueueInfo.pNext = null;
            deviceQueueInfo.queueFamilyIndex = 0;
            deviceQueueInfo.queueCount = 1;
            deviceQueueInfo.pQueuePriorities = &queuePriority;

            VkPhysicalDeviceRayTracingFeaturesKHR deviceRayTracingFeatures = default;
            deviceRayTracingFeatures.sType = VkStructureType.PhysicalDeviceRayTracingFeaturesKHR;
            deviceRayTracingFeatures.pNext = null;
            deviceRayTracingFeatures.rayTracing = true;

            VkPhysicalDeviceVulkan12Features deviceVulkan12Features = default;
            deviceVulkan12Features.sType = VkStructureType.PhysicalDeviceVulkan12Features;
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
            deviceInfo.sType = VkStructureType.DeviceCreateInfo;
            deviceInfo.pNext = &deviceVulkan12Features;
            deviceInfo.queueCreateInfoCount = 1;
            deviceInfo.pQueueCreateInfos = &deviceQueueInfo;
            deviceInfo.enabledExtensionCount = (uint)deviceExtensions.Length;
            deviceInfo.ppEnabledExtensionNames = (byte**)deviceExtensionsArray;
            deviceInfo.pEnabledFeatures = null;


            Vulkan.vkCreateDevice(physicalDevice, &deviceInfo, null, out device);

            Vulkan.vkGetDeviceQueue(device, 0, 0, out queue);

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
            surfaceCreateInfo.sType = VkStructureType.Win32SurfaceCreateInfoKHR;
            surfaceCreateInfo.pNext = null;
            surfaceCreateInfo.flags = 0;
            surfaceCreateInfo.hinstance = Process.GetCurrentProcess().Handle;
            surfaceCreateInfo.hwnd = window.Handle;


            Vulkan.vkCreateWin32SurfaceKHR(instance, &surfaceCreateInfo, null, out surface);

            VkBool32 surfaceSupport = false;
            Vulkan.vkGetPhysicalDeviceSurfaceSupportKHR(physicalDevice, 0, surface, out surfaceSupport);
            if (!surfaceSupport)
            {
                Debug.WriteLine($"No surface rendering support");
                return EXIT_FAILURE;
            }

            VkCommandPoolCreateInfo cmdPoolInfo = default;
            cmdPoolInfo.sType = VkStructureType.CommandPoolCreateInfo;
            cmdPoolInfo.pNext = null;
            cmdPoolInfo.flags = 0;
            cmdPoolInfo.queueFamilyIndex = 0;

            Vulkan.vkCreateCommandPool(device, &cmdPoolInfo, null, out commandPool);

            // acquire RT properties
            VkPhysicalDeviceRayTracingPropertiesKHR rayTracingProperties = default;
            rayTracingProperties.sType = VkStructureType.PhysicalDeviceRayTracingPropertiesKHR;
            rayTracingProperties.pNext = null;

            VkPhysicalDeviceProperties2 deviceProperties2 = default;
            deviceProperties2.sType = VkStructureType.PhysicalDeviceProperties2;
            deviceProperties2.pNext = &rayTracingProperties;
            Vulkan.vkGetPhysicalDeviceProperties2(physicalDevice, out deviceProperties2);

            // create bottom-level container
            {
                Debug.WriteLine($"Creating Bottom-Level Acceleration Structure..");

                VkAccelerationStructureCreateGeometryTypeInfoKHR accelerationCreateGeometryInfo = default;
                accelerationCreateGeometryInfo.sType =
                    VkStructureType.AccelerationStructureCreateGeometryTypeInfoKHR;
                accelerationCreateGeometryInfo.pNext = null;
                accelerationCreateGeometryInfo.geometryType = VkGeometryTypeKHR.TrianglesKHR;
                accelerationCreateGeometryInfo.maxPrimitiveCount = 128;
                accelerationCreateGeometryInfo.indexType = VkIndexType.Uint32;
                accelerationCreateGeometryInfo.maxVertexCount = 8;
                accelerationCreateGeometryInfo.vertexFormat = VkFormat.R32G32B32SFloat;
                accelerationCreateGeometryInfo.allowsTransforms = false;

                VkAccelerationStructureCreateInfoKHR accelerationInfo = default;
                accelerationInfo.sType = VkStructureType.AccelerationStructureCreateInfoKHR;
                accelerationInfo.pNext = null;
                accelerationInfo.compactedSize = 0;
                accelerationInfo.type = VkAccelerationStructureTypeKHR.BottomLevelKHR;
                accelerationInfo.flags = VkBuildAccelerationStructureFlagsKHR.PreferFastTraceKHR;
                accelerationInfo.maxGeometryCount = 1;
                accelerationInfo.pGeometryInfos = &accelerationCreateGeometryInfo;
                accelerationInfo.deviceAddress = IntPtr.Zero;

                fixed (VkAccelerationStructureKHR* bottomLevelASPtr = &bottomLevelAS)
                {
                    Vulkan.vkCreateAccelerationStructureKHR(device, &accelerationInfo, null, bottomLevelASPtr);
                }

                MappedBuffer objectMemory = CreateAccelerationScratchBuffer(
                    bottomLevelAS, VkAccelerationStructureMemoryRequirementsTypeKHR.ObjectKHR);

                BindAccelerationMemory(bottomLevelAS, objectMemory.memory);

                MappedBuffer buildScratchMemory = CreateAccelerationScratchBuffer(
                    bottomLevelAS, VkAccelerationStructureMemoryRequirementsTypeKHR.BuildScratchKHR);

                // Get bottom level acceleration structure handle for use in top level instances
                VkAccelerationStructureDeviceAddressInfoKHR devAddrInfo = default;
                devAddrInfo.sType = VkStructureType.AccelerationStructureDeviceAddressInfoKHR;
                devAddrInfo.accelerationStructure = bottomLevelAS;
                bottomLevelASHandle = Vulkan.vkGetAccelerationStructureDeviceAddressKHR(device, &devAddrInfo);

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
                accelerationGeometry.sType = VkStructureType.AccelerationStructureGeometryKHR;
                accelerationGeometry.pNext = null;
                accelerationGeometry.flags = VkGeometryFlagsKHR.OpaqueKHR;
                accelerationGeometry.geometryType = VkGeometryTypeKHR.TrianglesKHR;
                accelerationGeometry.geometry = default;
                accelerationGeometry.geometry.triangles = default;
                accelerationGeometry.geometry.triangles.sType =
                     VkStructureType.AccelerationStructureGeometryTrianglesDataKHR;
                accelerationGeometry.geometry.triangles.pNext = null;
                accelerationGeometry.geometry.triangles.vertexFormat = VkFormat.R32G32B32SFloat;
                accelerationGeometry.geometry.triangles.vertexData.deviceAddress =
                    vertexBuffer.memoryAddress;
                accelerationGeometry.geometry.triangles.vertexStride = 3 * sizeof(float);
                accelerationGeometry.geometry.triangles.indexType = VkIndexType.Uint32;
                accelerationGeometry.geometry.triangles.indexData.deviceAddress = indexBuffer.memoryAddress;
                accelerationGeometry.geometry.triangles.transformData.deviceAddress = IntPtr.Zero;

                VkAccelerationStructureGeometryKHR[] accelerationGeometries = new VkAccelerationStructureGeometryKHR[] { accelerationGeometry };
                VkAccelerationStructureBuildGeometryInfoKHR accelerationBuildGeometryInfo = default;
                fixed (VkAccelerationStructureGeometryKHR* ppGeometries = &accelerationGeometries[0])
                {
                    accelerationBuildGeometryInfo.sType =
                        VkStructureType.AccelerationStructureBuildGeometryInfoKHR;
                    accelerationBuildGeometryInfo.pNext = null;
                    accelerationBuildGeometryInfo.type = VkAccelerationStructureTypeKHR.BottomLevelKHR;
                    accelerationBuildGeometryInfo.flags =
                        VkBuildAccelerationStructureFlagsKHR.PreferFastTraceKHR;
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
                commandBufferAllocateInfo1.sType = VkStructureType.CommandBufferAllocateInfo;
                commandBufferAllocateInfo1.pNext = null;
                commandBufferAllocateInfo1.commandPool = commandPool;
                commandBufferAllocateInfo1.level = VkCommandBufferLevel.Primary;
                commandBufferAllocateInfo1.commandBufferCount = 1;
                Vulkan.
                    vkAllocateCommandBuffers(device, &commandBufferAllocateInfo1, &commandBuffer);

                VkCommandBufferBeginInfo commandBufferBeginInfo1 = default;
                commandBufferBeginInfo1.sType = VkStructureType.CommandBufferBeginInfo;
                commandBufferBeginInfo1.pNext = null;
                commandBufferBeginInfo1.flags = VkCommandBufferUsageFlags.OneTimeSubmit;
                Vulkan.vkBeginCommandBuffer(commandBuffer, &commandBufferBeginInfo1);

                fixed (VkAccelerationStructureBuildOffsetInfoKHR** accelerationBuildOffsetsPtr = &accelerationBuildOffsets[0])
                {
                    Vulkan.vkCmdBuildAccelerationStructureKHR(commandBuffer, 1, &accelerationBuildGeometryInfo,
                                                       accelerationBuildOffsetsPtr);
                }

                Vulkan.vkEndCommandBuffer(commandBuffer);

                VkSubmitInfo submitInfo = default;
                submitInfo.sType = VkStructureType.SubmitInfo;
                submitInfo.pNext = null;
                submitInfo.commandBufferCount = 1;
                submitInfo.pCommandBuffers = &commandBuffer;

                VkFence fence = 0;
                VkFenceCreateInfo fenceInfo = default;
                fenceInfo.sType = VkStructureType.FenceCreateInfo;
                fenceInfo.pNext = null;

                Vulkan.vkCreateFence(device, &fenceInfo, null, out fence);
                Vulkan.vkQueueSubmit(queue, 1, &submitInfo, fence);
                Vulkan.vkWaitForFences(device, 1, &fence, true, ulong.MaxValue);

                Vulkan.vkDestroyFence(device, fence, null);
                Vulkan.vkFreeCommandBuffers(device, commandPool, 1, &commandBuffer);

                // make sure bottom AS handle is valid
                if (bottomLevelASHandle == IntPtr.Zero)
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
                    VkStructureType.AccelerationStructureCreateGeometryTypeInfoKHR;
                accelerationCreateGeometryInfo.pNext = null;
                accelerationCreateGeometryInfo.geometryType = VkGeometryTypeKHR.InstancesKHR;
                accelerationCreateGeometryInfo.maxPrimitiveCount = 1;

                VkAccelerationStructureCreateInfoKHR accelerationInfo = default;
                accelerationInfo.sType = VkStructureType.AccelerationStructureCreateInfoKHR;
                accelerationInfo.pNext = null;
                accelerationInfo.compactedSize = 0;
                accelerationInfo.type = VkAccelerationStructureTypeKHR.TopLevelKHR;
                accelerationInfo.flags = VkBuildAccelerationStructureFlagsKHR.PreferFastTraceKHR;
                accelerationInfo.maxGeometryCount = 1;
                accelerationInfo.pGeometryInfos = &accelerationCreateGeometryInfo;
                accelerationInfo.deviceAddress = IntPtr.Zero;

                fixed (VkAccelerationStructureKHR* topLevelASPtr = &topLevelAS)
                {
                    Vulkan.
                        vkCreateAccelerationStructureKHR(device, &accelerationInfo, null, topLevelASPtr);
                }

                MappedBuffer objectMemory = CreateAccelerationScratchBuffer(
                    topLevelAS, VkAccelerationStructureMemoryRequirementsTypeKHR.ObjectKHR);

                BindAccelerationMemory(topLevelAS, objectMemory.memory);

                MappedBuffer buildScratchMemory = CreateAccelerationScratchBuffer(
                    topLevelAS, VkAccelerationStructureMemoryRequirementsTypeKHR.BuildScratchKHR);

                VkAccelerationStructureInstanceKHR[] instances = new VkAccelerationStructureInstanceKHR[]
                {
                    new VkAccelerationStructureInstanceKHR()
                    {
                        transform = new VkTransformMatrixKHR()
                        {
                            matrix_0 = 1.0f,
                            matrix_1 = 0.0f,
                            matrix_2 = 0.0f,

                            //matrix_3 = 0.0f,

                            //matrix_4 = 0.0f,
                            //matrix_5 = 1.0f,
                            //matrix_6 = 0.0f,
                            //matrix_7 = 0.0f,

                            //matrix_8 = 0.0f,
                            //matrix_9 = 0.0f,
                            //matrix_10 = 1.0f,
                            //matrix_11 = 0.0f,
                        },
                        instanceCustomIndex = 0,
                        mask = 0xff,
                        instanceShaderBindingTableRecordOffset = 0x0,
                        flags = VkGeometryInstanceFlagsKHR.TriangleFacingCullDisableKHR,
                        accelerationStructureReference = (ulong)bottomLevelASHandle,
                    }
                };

                MappedBuffer instanceBuffer = CreateMappedBuffer(
                    instances, (uint)(sizeof(VkAccelerationStructureInstanceKHR) * instances.Length));

                VkAccelerationStructureGeometryKHR accelerationGeometry = default;
                accelerationGeometry.sType = VkStructureType.AccelerationStructureGeometryKHR;
                accelerationGeometry.pNext = null;
                accelerationGeometry.flags = VkGeometryFlagsKHR.OpaqueKHR;
                accelerationGeometry.geometryType = VkGeometryTypeKHR.InstancesKHR;
                accelerationGeometry.geometry = default;
                accelerationGeometry.geometry.instances = default;
                accelerationGeometry.geometry.instances.sType =
                    VkStructureType.AccelerationStructureGeometryInstancesDataKHR;
                accelerationGeometry.geometry.instances.pNext = null;
                accelerationGeometry.geometry.instances.arrayOfPointers = false;
                accelerationGeometry.geometry.instances.data.deviceAddress = instanceBuffer.memoryAddress;

                VkAccelerationStructureGeometryKHR[] accelerationGeometries = new VkAccelerationStructureGeometryKHR[]
                        { accelerationGeometry};

                VkAccelerationStructureBuildGeometryInfoKHR accelerationBuildGeometryInfo = default;
                fixed (VkAccelerationStructureGeometryKHR* ppGeometries = &accelerationGeometries[0])
                {

                    accelerationBuildGeometryInfo.sType =
                        VkStructureType.AccelerationStructureBuildGeometryInfoKHR;
                    accelerationBuildGeometryInfo.pNext = null;
                    accelerationBuildGeometryInfo.type = VkAccelerationStructureTypeKHR.TopLevelKHR;
                    accelerationBuildGeometryInfo.flags =
                        VkBuildAccelerationStructureFlagsKHR.PreferFastTraceKHR;
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
                commandBufferAllocateInfo2.sType = VkStructureType.CommandBufferAllocateInfo;
                commandBufferAllocateInfo2.pNext = null;
                commandBufferAllocateInfo2.commandPool = commandPool;
                commandBufferAllocateInfo2.level = VkCommandBufferLevel.Primary;
                commandBufferAllocateInfo2.commandBufferCount = 1;
                Vulkan.
                    vkAllocateCommandBuffers(device, &commandBufferAllocateInfo2, &commandBuffer);

                VkCommandBufferBeginInfo commandBufferBeginInfo2 = default;
                commandBufferBeginInfo2.sType = VkStructureType.CommandBufferBeginInfo;
                commandBufferBeginInfo2.pNext = null;
                commandBufferBeginInfo2.flags = VkCommandBufferUsageFlags.OneTimeSubmit;
                Vulkan.vkBeginCommandBuffer(commandBuffer, &commandBufferBeginInfo2);

                fixed (VkAccelerationStructureBuildOffsetInfoKHR** accelerationBuildOffsetsPtr = &accelerationBuildOffsets[0])
                {
                    Vulkan.vkCmdBuildAccelerationStructureKHR(commandBuffer, 1, &accelerationBuildGeometryInfo,
                                                       accelerationBuildOffsetsPtr);
                }

                Vulkan.vkEndCommandBuffer(commandBuffer);

                VkSubmitInfo submitInfo = default;
                submitInfo.sType = VkStructureType.SubmitInfo;
                submitInfo.pNext = null;
                submitInfo.commandBufferCount = 1;
                submitInfo.pCommandBuffers = &commandBuffer;

                VkFence fence = 0;
                VkFenceCreateInfo fenceInfo = default;
                fenceInfo.sType = VkStructureType.FenceCreateInfo;
                fenceInfo.pNext = null;

                Vulkan.vkCreateFence(device, &fenceInfo, null, out fence);
                Vulkan.vkQueueSubmit(queue, 1, &submitInfo, fence);
                Vulkan.vkWaitForFences(device, 1, &fence, true, ulong.MaxValue);

                Vulkan.vkDestroyFence(device, fence, null);
                Vulkan.vkFreeCommandBuffers(device, commandPool, 1, &commandBuffer);
            }

            // offscreen buffer
            {
                Debug.WriteLine($"Creating Offsceen Buffer..");

                VkImageCreateInfo imageInfo = default;
                imageInfo.sType = VkStructureType.ImageCreateInfo;
                imageInfo.pNext = null;
                imageInfo.imageType = VkImageType.Image2D;
                imageInfo.format = desiredSurfaceFormat;
                imageInfo.extent = new Vortice.Mathematics.Size3() { Width = (int)desiredWindowWidth, Height = (int)desiredWindowHeight, Depth = 1 };
                imageInfo.mipLevels = 1;
                imageInfo.arrayLayers = 1;
                imageInfo.samples = VkSampleCountFlags.Count1;
                imageInfo.tiling = VkImageTiling.Optimal;
                imageInfo.usage = VkImageUsageFlags.Storage | VkImageUsageFlags.TransferSrc;
                imageInfo.sharingMode = VkSharingMode.Exclusive;
                imageInfo.queueFamilyIndexCount = 0;
                imageInfo.pQueueFamilyIndices = null;
                imageInfo.initialLayout = VkImageLayout.Undefined;

                Vulkan.vkCreateImage(device, &imageInfo, null, out offscreenBuffer);

                VkMemoryRequirements memoryRequirements = default;
                Vulkan.vkGetImageMemoryRequirements(device, offscreenBuffer, out memoryRequirements);

                VkMemoryAllocateInfo memoryAllocateInfo = default;
                memoryAllocateInfo.sType = VkStructureType.MemoryAllocateInfo;
                memoryAllocateInfo.pNext = null;
                memoryAllocateInfo.allocationSize = memoryRequirements.size;
                memoryAllocateInfo.memoryTypeIndex =
                    FindMemoryType(memoryRequirements.memoryTypeBits, VkMemoryPropertyFlags.DeviceLocal);

                fixed (VkDeviceMemory* offscreenBufferMemoryPtr = &offscreenBufferMemory)
                {
                    Vulkan.
                        vkAllocateMemory(device, &memoryAllocateInfo, null, offscreenBufferMemoryPtr);
                }

                Vulkan.vkBindImageMemory(device, offscreenBuffer, offscreenBufferMemory, 0);

                VkImageViewCreateInfo imageViewInfo = default;
                imageViewInfo.sType = VkStructureType.ImageViewCreateInfo;
                imageViewInfo.pNext = null;
                imageViewInfo.viewType = VkImageViewType.Image2D;
                imageViewInfo.format = desiredSurfaceFormat;
                imageViewInfo.subresourceRange = default;
                imageViewInfo.subresourceRange.aspectMask = VkImageAspectFlags.Color;
                imageViewInfo.subresourceRange.baseMipLevel = 0;
                imageViewInfo.subresourceRange.levelCount = 1;
                imageViewInfo.subresourceRange.baseArrayLayer = 0;
                imageViewInfo.subresourceRange.layerCount = 1;
                imageViewInfo.image = offscreenBuffer;
                imageViewInfo.flags = 0;
                imageViewInfo.components.r = VkComponentSwizzle.R;
                imageViewInfo.components.g = VkComponentSwizzle.G;
                imageViewInfo.components.b = VkComponentSwizzle.B;
                imageViewInfo.components.a = VkComponentSwizzle.A;

                Vulkan.vkCreateImageView(device, &imageViewInfo, null, out offscreenBufferView);
            }

            // rt descriptor set layout
            {
                Debug.WriteLine($"Creating RT Descriptor Set Layout..");

                VkDescriptorSetLayoutBinding accelerationStructureLayoutBinding = default;
                accelerationStructureLayoutBinding.binding = 0;
                accelerationStructureLayoutBinding.descriptorType =
                    VkDescriptorType.AccelerationStructureKHR;
                accelerationStructureLayoutBinding.descriptorCount = 1;
                accelerationStructureLayoutBinding.stageFlags = VkShaderStageFlags.RaygenKHR;
                accelerationStructureLayoutBinding.pImmutableSamplers = null;

                VkDescriptorSetLayoutBinding storageImageLayoutBinding = default;
                storageImageLayoutBinding.binding = 1;
                storageImageLayoutBinding.descriptorType = VkDescriptorType.StorageImage;
                storageImageLayoutBinding.descriptorCount = 1;
                storageImageLayoutBinding.stageFlags = VkShaderStageFlags.RaygenKHR;

                VkDescriptorSetLayoutBinding* bindings = stackalloc VkDescriptorSetLayoutBinding[]
                        { accelerationStructureLayoutBinding, storageImageLayoutBinding };

                VkDescriptorSetLayoutCreateInfo layoutInfo = default;
                layoutInfo.sType = VkStructureType.DescriptorSetLayoutCreateInfo;
                layoutInfo.pNext = null;
                layoutInfo.flags = 0;
                layoutInfo.bindingCount = 2;
                layoutInfo.pBindings = bindings;

                Vulkan.
                    vkCreateDescriptorSetLayout(device, &layoutInfo, null, out descriptorSetLayout);
            }

            // rt descriptor set
            {
                Debug.WriteLine($"Creating RT Descriptor Set..");

                VkDescriptorPoolSize* poolSizes = stackalloc VkDescriptorPoolSize[] {
                    new VkDescriptorPoolSize()
                    {
                        type = VkDescriptorType.AccelerationStructureKHR,
                        descriptorCount = 1,
                    },
                    new VkDescriptorPoolSize()
                    {
                        type = VkDescriptorType.StorageImage,
                        descriptorCount = 1,
                    }
                };

                VkDescriptorPoolCreateInfo descriptorPoolInfo = default;
                descriptorPoolInfo.sType = VkStructureType.DescriptorPoolCreateInfo;
                descriptorPoolInfo.pNext = null;
                descriptorPoolInfo.flags = 0;
                descriptorPoolInfo.maxSets = 1;
                descriptorPoolInfo.poolSizeCount = 2;
                descriptorPoolInfo.pPoolSizes = poolSizes;

                Vulkan.
                    vkCreateDescriptorPool(device, &descriptorPoolInfo, null, out descriptorPool);

                VkDescriptorSetAllocateInfo descriptorSetAllocateInfo = default;
                descriptorSetAllocateInfo.sType = VkStructureType.DescriptorSetAllocateInfo;
                descriptorSetAllocateInfo.pNext = null;
                descriptorSetAllocateInfo.descriptorPool = descriptorPool;
                descriptorSetAllocateInfo.descriptorSetCount = 1;
                fixed (VkDescriptorSetLayout* descriptorSetLayoutPtr = &descriptorSetLayout)
                {
                    descriptorSetAllocateInfo.pSetLayouts = descriptorSetLayoutPtr;
                }

                fixed (VkDescriptorSet* descriptorSetPtr = &descriptorSet)
                {
                    Vulkan.
                    vkAllocateDescriptorSets(device, &descriptorSetAllocateInfo, descriptorSetPtr);
                }

                VkWriteDescriptorSetAccelerationStructureKHR descriptorAccelerationStructureInfo;
                descriptorAccelerationStructureInfo.sType =
                    VkStructureType.WriteDescriptorSetAccelerationStructureKHR;
                descriptorAccelerationStructureInfo.pNext = null;
                descriptorAccelerationStructureInfo.accelerationStructureCount = 1;
                fixed (VkAccelerationStructureKHR* topLevelASPtr = &topLevelAS)
                {
                    descriptorAccelerationStructureInfo.pAccelerationStructures = topLevelASPtr;
                }

                VkWriteDescriptorSet accelerationStructureWrite = default;
                accelerationStructureWrite.sType = VkStructureType.WriteDescriptorSet;
                accelerationStructureWrite.pNext = &descriptorAccelerationStructureInfo;
                accelerationStructureWrite.dstSet = descriptorSet;
                accelerationStructureWrite.dstBinding = 0;
                accelerationStructureWrite.descriptorCount = 1;
                accelerationStructureWrite.descriptorType = VkDescriptorType.AccelerationStructureKHR;

                VkDescriptorImageInfo storageImageInfo = default;
                storageImageInfo.sampler = 0;
                storageImageInfo.imageView = offscreenBufferView;
                storageImageInfo.imageLayout = VkImageLayout.General;

                VkWriteDescriptorSet outputImageWrite = default;
                outputImageWrite.sType = VkStructureType.WriteDescriptorSet;
                outputImageWrite.pNext = null;
                outputImageWrite.dstSet = descriptorSet;
                outputImageWrite.dstBinding = 1;
                outputImageWrite.descriptorType = VkDescriptorType.StorageImage;
                outputImageWrite.descriptorCount = 1;
                outputImageWrite.pImageInfo = &storageImageInfo;

                VkWriteDescriptorSet* descriptorWrites = stackalloc VkWriteDescriptorSet[]
                        { accelerationStructureWrite, outputImageWrite };

                Vulkan.vkUpdateDescriptorSets(device, 2, descriptorWrites, 0,
                                       null);
            }

            // rt pipeline layout
            {
                Debug.WriteLine($"Creating RT Pipeline Layout..");

                VkPipelineLayoutCreateInfo pipelineLayoutInfo = default;
                pipelineLayoutInfo.sType = VkStructureType.PipelineLayoutCreateInfo;
                pipelineLayoutInfo.pNext = null;
                pipelineLayoutInfo.flags = 0;
                pipelineLayoutInfo.setLayoutCount = 1;
                fixed (VkDescriptorSetLayout* descriptorSetLayoutPtr = &descriptorSetLayout)
                {
                    pipelineLayoutInfo.pSetLayouts = descriptorSetLayoutPtr;
                }
                pipelineLayoutInfo.pushConstantRangeCount = 0;
                pipelineLayoutInfo.pPushConstantRanges = null;

                Vulkan.
                    vkCreatePipelineLayout(device, &pipelineLayoutInfo, null, out pipelineLayout);
            }

            // rt pipeline
            {
                Debug.WriteLine($"Creating RT Pipeline..");

                //std::string basePath = GetExecutablePath() + "/../../shaders";

                byte[] rgenShaderSrc = File.ReadAllBytes("Shaders/ray-generation.spv");
                byte[] rchitShaderSrc = File.ReadAllBytes("Shaders/ray-closest-hit.spv");
                byte[] rmissShaderSrc = File.ReadAllBytes("Shaders/ray-miss.spv");

                VkPipelineShaderStageCreateInfo rayGenShaderStageInfo = default;
                rayGenShaderStageInfo.sType = VkStructureType.PipelineShaderStageCreateInfo;
                rayGenShaderStageInfo.pNext = null;
                rayGenShaderStageInfo.stage = VkShaderStageFlags.RaygenKHR;
                rayGenShaderStageInfo.module = CreateShaderModule(rgenShaderSrc);
                rayGenShaderStageInfo.pName = "main".ToPointer();

                VkPipelineShaderStageCreateInfo rayChitShaderStageInfo = default;
                rayChitShaderStageInfo.sType = VkStructureType.PipelineShaderStageCreateInfo;
                rayChitShaderStageInfo.pNext = null;
                rayChitShaderStageInfo.stage = VkShaderStageFlags.ClosestHitKHR;
                rayChitShaderStageInfo.module = CreateShaderModule(rchitShaderSrc);
                rayChitShaderStageInfo.pName = "main".ToPointer();

                VkPipelineShaderStageCreateInfo rayMissShaderStageInfo = default;
                rayMissShaderStageInfo.sType = VkStructureType.PipelineShaderStageCreateInfo;
                rayMissShaderStageInfo.pNext = null;
                rayMissShaderStageInfo.stage = VkShaderStageFlags.MissKHR;
                rayMissShaderStageInfo.module = CreateShaderModule(rmissShaderSrc);
                rayMissShaderStageInfo.pName = "main".ToPointer();

                VkPipelineShaderStageCreateInfo* shaderStages = stackalloc VkPipelineShaderStageCreateInfo[] {
                    rayGenShaderStageInfo,
                    rayChitShaderStageInfo,
                    rayMissShaderStageInfo
                };

                VkRayTracingShaderGroupCreateInfoKHR rayGenGroup = default;
                rayGenGroup.sType = VkStructureType.RayTracingShaderGroupCreateInfoKHR;
                rayGenGroup.pNext = null;
                rayGenGroup.type = VkRayTracingShaderGroupTypeKHR.GeneralKHR;
                rayGenGroup.generalShader = 0;
                rayGenGroup.closestHitShader = VK_SHADER_UNUSED_KHR;
                rayGenGroup.anyHitShader = VK_SHADER_UNUSED_KHR;
                rayGenGroup.intersectionShader = VK_SHADER_UNUSED_KHR;

                VkRayTracingShaderGroupCreateInfoKHR rayHitGroup = default;
                rayHitGroup.sType = VkStructureType.RayTracingShaderGroupCreateInfoKHR;
                rayHitGroup.pNext = null;
                rayHitGroup.type = VkRayTracingShaderGroupTypeKHR.TrianglesHitGroupKHR;
                rayHitGroup.generalShader = VK_SHADER_UNUSED_KHR;
                rayHitGroup.closestHitShader = 1;
                rayHitGroup.anyHitShader = VK_SHADER_UNUSED_KHR;
                rayHitGroup.intersectionShader = VK_SHADER_UNUSED_KHR;

                VkRayTracingShaderGroupCreateInfoKHR rayMissGroup = default;
                rayMissGroup.sType = VkStructureType.RayTracingShaderGroupCreateInfoKHR;
                rayMissGroup.pNext = null;
                rayMissGroup.type = VkRayTracingShaderGroupTypeKHR.GeneralKHR;
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
                pipelineInfo.sType = VkStructureType.RayTracingPipelineCreateInfoKHR;
                pipelineInfo.pNext = null;
                pipelineInfo.flags = 0;
                pipelineInfo.stageCount = 3;
                pipelineInfo.pStages = shaderStages;
                pipelineInfo.groupCount = 3;
                pipelineInfo.pGroups = shaderGroups;
                pipelineInfo.maxRecursionDepth = 1;
                pipelineInfo.libraries = default;
                pipelineInfo.libraries.sType = VkStructureType.PipelineLibraryCreateInfoKHR;
                pipelineInfo.libraries.pNext = null;
                pipelineInfo.libraries.libraryCount = 0;
                pipelineInfo.libraries.pLibraries = null;
                pipelineInfo.pLibraryInterface = null;
                pipelineInfo.layout = pipelineLayout;
                pipelineInfo.basePipelineHandle = 0;
                pipelineInfo.basePipelineIndex = 0;

                fixed (VkPipeline* pipelinePtr = &pipeline)
                {
                    Vulkan.
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
                bufferInfo.sType = VkStructureType.BufferCreateInfo;
                bufferInfo.pNext = null;
                bufferInfo.size = shaderBindingTableSize;
                bufferInfo.usage = VkBufferUsageFlags.TransferSrc;
                bufferInfo.sharingMode = VkSharingMode.Exclusive;
                bufferInfo.queueFamilyIndexCount = 0;
                bufferInfo.pQueueFamilyIndices = null;

                Vulkan.vkCreateBuffer(device, &bufferInfo, null, out shaderBindingTable.buffer);

                VkMemoryRequirements memoryRequirements = default;
                Vulkan.vkGetBufferMemoryRequirements(device, shaderBindingTable.buffer, out memoryRequirements);

                VkMemoryAllocateInfo memAllocInfo = default;
                memAllocInfo.sType = VkStructureType.MemoryAllocateInfo;
                memAllocInfo.pNext = null;
                memAllocInfo.allocationSize = memoryRequirements.size;
                memAllocInfo.memoryTypeIndex =
                    FindMemoryType(memoryRequirements.memoryTypeBits, VkMemoryPropertyFlags.HostVisible);

                VkDeviceMemory newMemory;
                Vulkan.
                    vkAllocateMemory(device, &memAllocInfo, null, &newMemory);
                shaderBindingTable.memory = newMemory;

                Vulkan.
                    vkBindBufferMemory(device, shaderBindingTable.buffer, shaderBindingTable.memory, 0);

                void* dstData;
                Vulkan.
                    vkMapMemory(device, shaderBindingTable.memory, 0, shaderBindingTableSize, 0, &dstData);

                Vulkan.vkGetRayTracingShaderGroupHandlesKHR(device, pipeline, 0, shaderBindingTableGroupCount,
                                                     new UIntPtr(shaderBindingTableSize), dstData); // TODO UIntPtr
                Vulkan.vkUnmapMemory(device, shaderBindingTable.memory);
            }

            Debug.WriteLine($"Initializing Swapchain..");

            uint presentModeCount = 0;
            Vulkan.vkGetPhysicalDeviceSurfacePresentModesKHR(physicalDevice, surface,
                                                                       &presentModeCount, null);

            VkPresentModeKHR[] presentModes = new VkPresentModeKHR[presentModeCount];
            fixed (VkPresentModeKHR* presentModesPtr = presentModes)
            {
                Vulkan.vkGetPhysicalDeviceSurfacePresentModesKHR(physicalDevice, surface, &presentModeCount,
                                                          presentModesPtr);
            }

            bool isMailboxModeSupported = presentModes.Any(m => m == VkPresentModeKHR.MailboxKHR);

            VkSurfaceCapabilitiesKHR capabilitiesKHR;
            var result = Vulkan.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(physicalDevice, surface, out capabilitiesKHR);
            Helpers.CheckErrors(result);

            var extent = ChooseSwapExtent(capabilitiesKHR);
            VkSwapchainCreateInfoKHR swapchainInfo = default;
            swapchainInfo.sType = VkStructureType.SwapchainCreateInfoKHR;
            swapchainInfo.pNext = null;
            swapchainInfo.surface = surface;
            swapchainInfo.minImageCount = 3;
            swapchainInfo.imageFormat = desiredSurfaceFormat;
            swapchainInfo.imageColorSpace = VkColorSpaceKHR.SrgbNonlinearKHR;
            swapchainInfo.imageExtent.Width = extent.Width; //desiredWindowWidth;
            swapchainInfo.imageExtent.Height = extent.Height; //desiredWindowHeight;
            swapchainInfo.imageArrayLayers = 1;
            swapchainInfo.imageUsage =
                 VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.TransferDst;
            swapchainInfo.imageSharingMode = VkSharingMode.Exclusive;
            swapchainInfo.queueFamilyIndexCount = 0;
            swapchainInfo.preTransform = VkSurfaceTransformFlagsKHR.IdentityKHR;
            swapchainInfo.compositeAlpha = VkCompositeAlphaFlagsKHR.OpaqueKHR;
            swapchainInfo.presentMode =
                isMailboxModeSupported ? VkPresentModeKHR.MailboxKHR : VkPresentModeKHR.FifoKHR;
            swapchainInfo.clipped = true;
            swapchainInfo.oldSwapchain = 0;

            Vulkan.vkCreateSwapchainKHR(device, &swapchainInfo, null, out swapchain);

            uint amountOfImagesInSwapchain = 0;
            Vulkan.vkGetSwapchainImagesKHR(device, swapchain, &amountOfImagesInSwapchain, null);
            VkImage[] swapchainImages = new VkImage[amountOfImagesInSwapchain];

            fixed (VkImage* swapchainImagesPtr = &swapchainImages[0])
            {
                Vulkan.vkGetSwapchainImagesKHR(device, swapchain, &amountOfImagesInSwapchain,
                                                         swapchainImagesPtr);
            }

            VkImageView[] imageViews = new VkImageView[amountOfImagesInSwapchain];

            for (uint ii = 0; ii < amountOfImagesInSwapchain; ++ii)
            {
                VkImageViewCreateInfo imageViewInfo = default;
                imageViewInfo.sType = VkStructureType.ImageViewCreateInfo;
                imageViewInfo.pNext = null;
                imageViewInfo.image = swapchainImages[ii];
                imageViewInfo.viewType = VkImageViewType.Image2D;
                imageViewInfo.format = desiredSurfaceFormat;
                imageViewInfo.subresourceRange.aspectMask = VkImageAspectFlags.Color;
                imageViewInfo.subresourceRange.baseMipLevel = 0;
                imageViewInfo.subresourceRange.levelCount = 1;
                imageViewInfo.subresourceRange.baseArrayLayer = 0;
                imageViewInfo.subresourceRange.layerCount = 1;

                Vulkan.vkCreateImageView(device, &imageViewInfo, null, out imageViews[ii]);
            };

            Debug.WriteLine($"Recording frame commands..");

            VkImageCopy copyRegion = default;
            copyRegion.srcSubresource = default;
            copyRegion.srcSubresource.aspectMask = VkImageAspectFlags.Color;
            copyRegion.srcSubresource.mipLevel = 0;
            copyRegion.srcSubresource.baseArrayLayer = 0;
            copyRegion.srcSubresource.layerCount = 1;
            copyRegion.dstSubresource.aspectMask = VkImageAspectFlags.Color;
            copyRegion.dstSubresource.mipLevel = 0;
            copyRegion.dstSubresource.baseArrayLayer = 0;
            copyRegion.dstSubresource.layerCount = 1;
            copyRegion.extent = default;
            copyRegion.extent.Depth = 1;
            copyRegion.extent.Width = (int)desiredWindowWidth;
            copyRegion.extent.Height = (int)desiredWindowHeight;

            VkImageSubresourceRange subresourceRange = default;
            subresourceRange.aspectMask = VkImageAspectFlags.Color;
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
            commandBufferAllocateInfo.sType = VkStructureType.CommandBufferAllocateInfo;
            commandBufferAllocateInfo.pNext = null;
            commandBufferAllocateInfo.commandPool = commandPool;
            commandBufferAllocateInfo.level = VkCommandBufferLevel.Primary;
            commandBufferAllocateInfo.commandBufferCount = amountOfImagesInSwapchain;

            commandBuffers = new VkCommandBuffer[amountOfImagesInSwapchain];

            fixed (VkCommandBuffer* commandBuffersPtr = &commandBuffers[0])
            {
                Vulkan.
                    vkAllocateCommandBuffers(device, &commandBufferAllocateInfo, commandBuffersPtr);
            }

            VkCommandBufferBeginInfo commandBufferBeginInfo = default;
            commandBufferBeginInfo.sType = VkStructureType.CommandBufferBeginInfo;
            commandBufferBeginInfo.pNext = null;
            commandBufferBeginInfo.flags = 0;
            commandBufferBeginInfo.pInheritanceInfo = null;

            for (uint ii = 0; ii < amountOfImagesInSwapchain; ++ii)
            {
                VkCommandBuffer commandBuffer = commandBuffers[ii];
                VkImage swapchainImage = swapchainImages[ii];

                Vulkan.vkBeginCommandBuffer(commandBuffer, &commandBufferBeginInfo);

                // transition offscreen buffer into shader writeable state
                InsertCommandImageBarrier(commandBuffer, offscreenBuffer, 0, VkAccessFlags.ShaderWrite,
                                          VkImageLayout.Undefined, VkImageLayout.General,
                                          subresourceRange);

                Vulkan.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.RayTracingKHR, pipeline);
                fixed (VkDescriptorSet* descriptorSetPtr = &descriptorSet)
                {
                    Vulkan.vkCmdBindDescriptorSets(commandBuffer, VkPipelineBindPoint.RayTracingKHR,
                                        pipelineLayout, 0, 1, descriptorSetPtr, 0, (uint*)0);
                }
                Vulkan.vkCmdTraceRaysKHR(commandBuffer, &rayGenSBT, &rayMissSBT, &rayHitSBT, &rayCallSBT,
                                  desiredWindowWidth, desiredWindowHeight, 1);

                // transition swapchain image into copy destination state
                InsertCommandImageBarrier(commandBuffer, swapchainImage, 0, VkAccessFlags.TransferWrite,
                                          VkImageLayout.Undefined,
                                          VkImageLayout.TransferDstOptimal, subresourceRange);

                // transition offscreen buffer into copy source state
                InsertCommandImageBarrier(commandBuffer, offscreenBuffer, VkAccessFlags.ShaderWrite,
                                          VkAccessFlags.TransferRead, VkImageLayout.General,
                                          VkImageLayout.TransferSrcOptimal, subresourceRange);

                // copy offscreen buffer into swapchain image
                Vulkan.vkCmdCopyImage(commandBuffer, offscreenBuffer, VkImageLayout.TransferSrcOptimal,
                               swapchainImage, VkImageLayout.TransferDstOptimal, 1, &copyRegion);

                // transition swapchain image into presentable state
                InsertCommandImageBarrier(commandBuffer, swapchainImage, 0, VkAccessFlags.TransferWrite,
                                          VkImageLayout.TransferDstOptimal,
                                          VkImageLayout.PresentSrcKHR, subresourceRange);

                Vulkan.vkEndCommandBuffer(commandBuffer);
            }

            VkSemaphoreCreateInfo semaphoreInfo = default;
            semaphoreInfo.sType = VkStructureType.SemaphoreCreateInfo;
            semaphoreInfo.pNext = null;

            Vulkan.vkCreateSemaphore(device, &semaphoreInfo, null, out semaphoreImageAvailable);

            Vulkan.
                vkCreateSemaphore(device, &semaphoreInfo, null, out semaphoreRenderingAvailable);

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
                Vulkan.vkAcquireNextImageKHR(device, swapchain, ulong.MaxValue,
                                                       semaphoreImageAvailable, 0, out imageIndex);

                VkPipelineStageFlags* waitStageMasks = stackalloc VkPipelineStageFlags[] { VkPipelineStageFlags.ColorAttachmentOutput };

                VkSubmitInfo submitInfo = default;
                submitInfo.sType = VkStructureType.SubmitInfo;
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

                Vulkan.vkQueueSubmit(queue, 1, &submitInfo, 0);

                VkPresentInfoKHR presentInfo = default;
                presentInfo.sType = VkStructureType.PresentInfoKHR;
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

                Vulkan.vkQueuePresentKHR(queue, &presentInfo);

                Vulkan.vkQueueWaitIdle(queue);

                Application.DoEvents();
            }

            return EXIT_SUCCESS;
        }

        private Size ChooseSwapExtent(VkSurfaceCapabilitiesKHR capabilities)
        {
            if (capabilities.currentExtent.Width != int.MaxValue)
            {
                return capabilities.currentExtent;
            }

            return new Size()
            {
                Width = (int)Math.Max(capabilities.minImageExtent.Width, Math.Min(capabilities.maxImageExtent.Width, (uint)this.desiredWindowWidth)),
                Height = (int)Math.Max(capabilities.minImageExtent.Height, Math.Min(capabilities.maxImageExtent.Height, (uint)this.desiredWindowHeight)),
            };
        }
    }
}
