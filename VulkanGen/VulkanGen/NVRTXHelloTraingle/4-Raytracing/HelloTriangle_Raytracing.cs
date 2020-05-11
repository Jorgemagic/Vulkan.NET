using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using WaveEngine.Bindings.Vulkan;

namespace NVRTXHelloTriangle
{
    public unsafe partial class NVRTXHelloTriangle
    {
        // Ray tracing acceleration structure
        public struct AccelerationStructure
        {
            public VkDeviceMemory memory;
            public VkAccelerationStructureKHR accelerationStructure;
            public ulong handle;
        }

        // Ray tracing geometry instance
        public struct GeometryInstance
        {
            /// Transform matrix, containing only the top 3 rows
            public Matrix3x4 transform;
            /// Instance index
            public uint instanceId;
            /// Visibility mask
            public uint mask;
            /// Index of the hit group which will be invoked when a ray hits the instance
            public uint instanceOffset;
            /// Instance flags, such as culling
            public uint flags;
            /// Opaque handle of the bottom-level acceleration structure
            public ulong accelerationStructureHandle;
        }

        private const int INDEX_RAYGEN = 0;
        private const int INDEX_MISS = 1;
        private const int INDEX_CLOSEST_HIT = 2;

        private const int NUM_SHADER_GROUPS = 3;

        private VkPhysicalDeviceRayTracingPropertiesNV rayTracingProperties = default;

        private AccelerationStructure bottomLevelAS = default;
        private AccelerationStructure topLevelAS = default;

        private VulkanBuffer vertexBuffer;
        private VulkanBuffer indexBuffer;
        private uint indexCount;
        private VulkanBuffer shaderBindingTable;

        public struct StorageImage
        {
            public VkDeviceMemory memory;
            public VkImage image;
            public VkImageView view;
            public VkFormat format;
        };

        private StorageImage storageImage;

        public struct UniformData
        {
            public Matrix4x4 viewInverse;
            public Matrix4x4 projInverse;
        };

        private UniformData uniformData;

        private VulkanBuffer ubo;

        private VkPipeline pipeline;
        private VkPipelineLayout pipelineLayout;
        private VkDescriptorSet descriptorSet;
        private VkDescriptorSetLayout descriptorSetLayout;
        private VkDescriptorPool descriptorPool;

        public void Prepare()
        {
            // Query the ray tracing properties of the current implementation, we will need them later on
            this.rayTracingProperties.sType = VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_RAY_TRACING_PROPERTIES_NV;
            VkPhysicalDeviceProperties2 deviceProps2 = default;
            deviceProps2.sType = VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_PROPERTIES_2;
            fixed (VkPhysicalDeviceRayTracingPropertiesNV* rayTracingPropertiesPtr = &rayTracingProperties)
            {
                deviceProps2.pNext = rayTracingPropertiesPtr;
            }
            VulkanNative.vkGetPhysicalDeviceProperties2(physicalDevice, &deviceProps2);

            // CommandBuffers
            this.commandBuffers = new VkCommandBuffer[this.swapChainFramebuffers.Length];
            
            VkCommandBufferAllocateInfo cmdBufAllocateInfo = default;
            cmdBufAllocateInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO;
            cmdBufAllocateInfo.commandPool = this.commandPool;
            cmdBufAllocateInfo.level =  VkCommandBufferLevel.VK_COMMAND_BUFFER_LEVEL_PRIMARY;
            cmdBufAllocateInfo.commandBufferCount = (uint)this.commandBuffers.Length;

            fixed (VkCommandBuffer* commanBuffersPtr = &this.commandBuffers[0])
            {
                Helpers.CheckErrors(VulkanNative.vkAllocateCommandBuffers(device, &cmdBufAllocateInfo, commanBuffersPtr));
            }
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

        private void CreateStorageImage()
        {
            VkImageCreateInfo image = new VkImageCreateInfo();
            image.imageType = VkImageType.VK_IMAGE_TYPE_2D;
            image.format = this.swapChainImageFormat;
            image.extent.width = this.swapChainExtent.width;
            image.extent.height = this.swapChainExtent.height;
            image.extent.depth = 1;
            image.mipLevels = 1;
            image.arrayLayers = 1;
            image.samples = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT;
            image.tiling = VkImageTiling.VK_IMAGE_TILING_OPTIMAL;
            image.usage = VkImageUsageFlagBits.VK_IMAGE_USAGE_TRANSFER_SRC_BIT | VkImageUsageFlagBits.VK_IMAGE_USAGE_STORAGE_BIT;
            image.initialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED;
            VkImage newStorageImage;
            Helpers.CheckErrors(VulkanNative.vkCreateImage(device, &image, null, &newStorageImage));
            this.storageImage.image = newStorageImage;

            VkMemoryRequirements memReqs;
            VulkanNative.vkGetImageMemoryRequirements(device, storageImage.image, &memReqs);
            VkMemoryAllocateInfo memoryAllocateInfo = new VkMemoryAllocateInfo();
            memoryAllocateInfo.allocationSize = memReqs.size;
            memoryAllocateInfo.memoryTypeIndex = this.FindMemoryType(memReqs.memoryTypeBits, VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT);

            VkDeviceMemory newStorageMemory;
            Helpers.CheckErrors(VulkanNative.vkAllocateMemory(device, &memoryAllocateInfo, null, &newStorageMemory));
            storageImage.memory = newStorageMemory;
            Helpers.CheckErrors(VulkanNative.vkBindImageMemory(device, storageImage.image, storageImage.memory, 0));

            VkImageViewCreateInfo colorImageView = new VkImageViewCreateInfo();
            colorImageView.viewType = VkImageViewType.VK_IMAGE_VIEW_TYPE_2D;
            colorImageView.format = this.swapChainImageFormat;
            colorImageView.subresourceRange = new VkImageSubresourceRange();
            colorImageView.subresourceRange.aspectMask = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT;
            colorImageView.subresourceRange.baseMipLevel = 0;
            colorImageView.subresourceRange.levelCount = 1;
            colorImageView.subresourceRange.baseArrayLayer = 0;
            colorImageView.subresourceRange.layerCount = 1;
            colorImageView.image = storageImage.image;
            VkImageView newStorageImageView;
            Helpers.CheckErrors(VulkanNative.vkCreateImageView(device, &colorImageView, null, &newStorageImageView));
            storageImage.view = newStorageImageView;

            VkCommandBuffer cmdBuffer = this.CreateCommandBuffer(VkCommandBufferLevel.VK_COMMAND_BUFFER_LEVEL_PRIMARY, this.commandPool, true);

            this.SetImageLayout(cmdBuffer, storageImage.image,
                VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                VkImageLayout.VK_IMAGE_LAYOUT_GENERAL,
                new VkImageSubresourceRange()
                {
                    aspectMask = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
                    baseMipLevel = 0,
                    levelCount = 1,
                    baseArrayLayer = 0,
                    layerCount = 1,
                });

            Helpers.CheckErrors(VulkanNative.vkEndCommandBuffer(cmdBuffer));

            VkSubmitInfo submitInfo = default;
            submitInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_SUBMIT_INFO;
            submitInfo.commandBufferCount = 1;
            submitInfo.pCommandBuffers = &cmdBuffer;
            //// Create fence to ensure that the command buffer has finished executing
            //VkFenceCreateInfo fenceInfo = vks::initializers::fenceCreateInfo(VK_FLAGS_NONE);
            //VkFence fence;
            //VK_CHECK_RESULT(vkCreateFence(logicalDevice, &fenceInfo, nullptr, &fence));
            // Submit to the queue
            Helpers.CheckErrors(VulkanNative.vkQueueSubmit(this.graphicsQueue, 1, &submitInfo, 0));
            // Wait for the fence to signal that command buffer has finished executing
            //VK_CHECK_RESULT(vkWaitForFences(logicalDevice, 1, &fence, VK_TRUE, DEFAULT_FENCE_TIMEOUT));
            //vkDestroyFence(logicalDevice, fence, nullptr);
            //if (free)
            //{
            //    vkFreeCommandBuffers(logicalDevice, pool, 1, &commandBuffer);
            //}
        }

        private VkCommandBuffer CreateCommandBuffer(VkCommandBufferLevel level, VkCommandPool pool, bool begin = false)
        {
            VkCommandBufferAllocateInfo cmdBufAllocateInfo = default;
            cmdBufAllocateInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO;
            cmdBufAllocateInfo.commandPool = pool;
            cmdBufAllocateInfo.level = level;
            cmdBufAllocateInfo.commandBufferCount = 1;

            VkCommandBuffer cmdBuffer;
            Helpers.CheckErrors(VulkanNative.vkAllocateCommandBuffers(this.device, &cmdBufAllocateInfo, &cmdBuffer));

            // If requested, also start recording for the new command buffer
            if (begin)
            {
                VkCommandBufferBeginInfo cmdBufInfo = default;
                cmdBufInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO;

                Helpers.CheckErrors(VulkanNative.vkBeginCommandBuffer(cmdBuffer, &cmdBufInfo));
            }

            return cmdBuffer;
        }

        private void CreateBottomLevelAccelerationStructure(VkGeometryNV* geometries)
        {
            VkAccelerationStructureInfoNV accelerationStructureInfo = default;
            accelerationStructureInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_INFO_NV;
            accelerationStructureInfo.type = VkAccelerationStructureTypeKHR.VK_ACCELERATION_STRUCTURE_TYPE_BOTTOM_LEVEL_KHR;
            accelerationStructureInfo.instanceCount = 0;
            accelerationStructureInfo.geometryCount = 1;
            accelerationStructureInfo.pGeometries = geometries;

            VkAccelerationStructureCreateInfoNV accelerationStructureCI = default;
            accelerationStructureCI.sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_CREATE_INFO_NV;
            accelerationStructureCI.info = accelerationStructureInfo;

            fixed (VkAccelerationStructureKHR* bottomLevelASPtr = &bottomLevelAS.accelerationStructure)
            {
                Helpers.CheckErrors(VulkanNative.vkCreateAccelerationStructureNV(device, &accelerationStructureCI, null, bottomLevelASPtr));
            }

            VkAccelerationStructureMemoryRequirementsInfoNV memoryRequirementsInfo = default;
            memoryRequirementsInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_MEMORY_REQUIREMENTS_INFO_NV;
            memoryRequirementsInfo.type = VkAccelerationStructureMemoryRequirementsTypeKHR.VK_ACCELERATION_STRUCTURE_MEMORY_REQUIREMENTS_TYPE_OBJECT_KHR;
            memoryRequirementsInfo.accelerationStructure = bottomLevelAS.accelerationStructure;

            VkMemoryRequirements2 memoryRequirements2 = default;
            VulkanNative.vkGetAccelerationStructureMemoryRequirementsNV(device, &memoryRequirementsInfo, &memoryRequirements2);

            VkMemoryAllocateInfo memoryAllocateInfo = default;
            memoryAllocateInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO;
            memoryAllocateInfo.allocationSize = memoryRequirements2.memoryRequirements.size;
            memoryAllocateInfo.memoryTypeIndex = this.FindMemoryType(memoryRequirements2.memoryRequirements.memoryTypeBits, VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT);

            fixed (VkDeviceMemory* bottomlLevelASMemory = &bottomLevelAS.memory)
            {
                Helpers.CheckErrors(VulkanNative.vkAllocateMemory(device, &memoryAllocateInfo, null, bottomlLevelASMemory));
            }

            VkBindAccelerationStructureMemoryInfoKHR accelerationStructureMemoryInfo = default;
            accelerationStructureMemoryInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_BIND_ACCELERATION_STRUCTURE_MEMORY_INFO_KHR;
            accelerationStructureMemoryInfo.accelerationStructure = bottomLevelAS.accelerationStructure;
            accelerationStructureMemoryInfo.memory = bottomLevelAS.memory;

            Helpers.CheckErrors(VulkanNative.vkBindAccelerationStructureMemoryKHR(device, 1, &accelerationStructureMemoryInfo));

            IntPtr bottomLevelASHandle = IntPtr.Zero;
            Helpers.CheckErrors(VulkanNative.vkGetAccelerationStructureHandleNV(device, bottomLevelAS.accelerationStructure, (UIntPtr)sizeof(ulong), (void*)bottomLevelASHandle));
            bottomLevelAS.handle = (ulong)bottomLevelASHandle;
        }

        private void CreateTopLevelAccelerationStructure()
        {            
            VkAccelerationStructureInfoNV accelerationStructureInfo = default;
            accelerationStructureInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_INFO_NV;
            accelerationStructureInfo.type = VkAccelerationStructureTypeKHR.VK_ACCELERATION_STRUCTURE_TYPE_TOP_LEVEL_KHR;
            accelerationStructureInfo.instanceCount = 1;
            accelerationStructureInfo.geometryCount = 0;

            VkAccelerationStructureCreateInfoNV accelerationStructureCI = default;
            accelerationStructureCI.sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_CREATE_INFO_NV;
            accelerationStructureCI.info = accelerationStructureInfo;
            fixed (VkAccelerationStructureKHR* topLevelASPtr = &topLevelAS.accelerationStructure)
            {
                Helpers.CheckErrors(VulkanNative.vkCreateAccelerationStructureNV(device, &accelerationStructureCI, null, topLevelASPtr));
            }

            VkAccelerationStructureMemoryRequirementsInfoNV memoryRequirementsInfo = default;
            memoryRequirementsInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_MEMORY_REQUIREMENTS_INFO_NV;
            memoryRequirementsInfo.type = VkAccelerationStructureMemoryRequirementsTypeKHR.VK_ACCELERATION_STRUCTURE_MEMORY_REQUIREMENTS_TYPE_OBJECT_KHR;
            memoryRequirementsInfo.accelerationStructure = topLevelAS.accelerationStructure;

            VkMemoryRequirements2 memoryRequirements2 = default;
            VulkanNative.vkGetAccelerationStructureMemoryRequirementsNV(device, &memoryRequirementsInfo, &memoryRequirements2);

            VkMemoryAllocateInfo memoryAllocateInfo = default;
            memoryAllocateInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO;
            memoryAllocateInfo.allocationSize = memoryRequirements2.memoryRequirements.size;
            memoryAllocateInfo.memoryTypeIndex = this.FindMemoryType(memoryRequirements2.memoryRequirements.memoryTypeBits, VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT);
            fixed (VkDeviceMemory* topLevelASMemory = &topLevelAS.memory)
            {
                Helpers.CheckErrors(VulkanNative.vkAllocateMemory(device, &memoryAllocateInfo, null, topLevelASMemory));
            }

            VkBindAccelerationStructureMemoryInfoKHR accelerationStructureMemoryInfo = default;
            accelerationStructureMemoryInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_BIND_ACCELERATION_STRUCTURE_MEMORY_INFO_KHR;
            accelerationStructureMemoryInfo.accelerationStructure = topLevelAS.accelerationStructure;
            accelerationStructureMemoryInfo.memory = topLevelAS.memory;
            Helpers.CheckErrors(VulkanNative.vkBindAccelerationStructureMemoryKHR(device, 1, &accelerationStructureMemoryInfo));

            IntPtr topLevelASHandle = IntPtr.Zero;
            Helpers.CheckErrors(VulkanNative.vkGetAccelerationStructureHandleNV(device, topLevelAS.accelerationStructure, (UIntPtr)sizeof(ulong), (void*)topLevelASHandle));
            topLevelAS.handle = (ulong)topLevelASHandle;
        }

        struct Vertex
        {
            public Vector3 pos;

            public Vertex(Vector3 position)
            {
                this.pos = position;
            }
        };

        VkResult CreateBuffer<T>(VkBufferUsageFlagBits usageFlags, VkMemoryPropertyFlagBits memoryPropertyFlags, ref VulkanBuffer buffer, ulong size, T[] data)
        {
            GCHandle gcHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
            IntPtr srcDataPtr = gcHandle.AddrOfPinnedObject();
            var result = this.CreateBuffer(usageFlags, memoryPropertyFlags, ref buffer, size, (void*)srcDataPtr);
            gcHandle.Free();

            return result;
        }

        VkResult CreateBuffer<T>(VkBufferUsageFlagBits usageFlags, VkMemoryPropertyFlagBits memoryPropertyFlags, ref VulkanBuffer buffer, ulong size, T data)
        {
            IntPtr srcDataPtr = (IntPtr)Unsafe.AsPointer(ref data);
            var result = this.CreateBuffer(usageFlags, memoryPropertyFlags, ref buffer, size, (void*)srcDataPtr);
            return result;
        }

        VkResult CreateBuffer(VkBufferUsageFlagBits usageFlags, VkMemoryPropertyFlagBits memoryPropertyFlags, ref VulkanBuffer buffer, ulong size, void* data = null)
        {
            buffer.device = this.device;

            // Create the buffer handle
            VkBufferCreateInfo bufferCreateInfo = default;
            bufferCreateInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO;
            bufferCreateInfo.usage = usageFlags;
            bufferCreateInfo.size = size;

            fixed (VkBuffer* bufferPtr = &buffer.buffer)
            {
                Helpers.CheckErrors(VulkanNative.vkCreateBuffer(this.device, &bufferCreateInfo, null, bufferPtr));
            }

            // Create the memory backing up the buffer handle
            VkMemoryRequirements memReqs;
            VkMemoryAllocateInfo memAlloc = default;
            memAlloc.sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO;

            VulkanNative.vkGetBufferMemoryRequirements(this.device, buffer.buffer, &memReqs);
            memAlloc.allocationSize = memReqs.size;
            // Find a memory type index that fits the properties of the buffer
            memAlloc.memoryTypeIndex = this.FindMemoryType(memReqs.memoryTypeBits, memoryPropertyFlags);

            fixed (VkDeviceMemory* bufferMemoryPtr = &buffer.memory)
            {
                Helpers.CheckErrors(VulkanNative.vkAllocateMemory(this.device, &memAlloc, null, bufferMemoryPtr));
            }

            buffer.alignment = memReqs.alignment;
            buffer.size = size;
            buffer.usageFlags = usageFlags;
            buffer.memoryPropertyFlags = memoryPropertyFlags;

            // If a pointer to the buffer data has been passed, map the buffer and copy over the data
            if (data != null)
            {               
                Helpers.CheckErrors(buffer.Map());
                Unsafe.CopyBlock((void*)buffer.mapped, data, (uint)size);
                if ((memoryPropertyFlags & VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_HOST_COHERENT_BIT) == 0)
                    buffer.Flush();

                buffer.Unmap();              
            }


            // Initialize a default descriptor that covers the whole buffer size
            buffer.SetupDescriptor();

            // Attach the memory to the buffer object
            return buffer.Bind();
        }

        private void CreateScene()
        {

            // Setup vertices for a single triangle        
            Vertex[] vertices =
            {
                new Vertex(new Vector3(1.0f,  1.0f, 0.0f)),
                new Vertex(new Vector3(-1.0f,  1.0f, 0.0f)),
                new Vertex(new Vector3(0.0f, -1.0f, 0.0f))
            };

            // Setup indices
            uint[] indices = { 0, 1, 2 };
            indexCount = (uint)indices.Length;

            // Create buffers
            // For the sake of simplicity we won't stage the vertex data to the gpu memory
            // Vertex buffer
            Helpers.CheckErrors(this.CreateBuffer(
                VkBufferUsageFlagBits.VK_BUFFER_USAGE_VERTEX_BUFFER_BIT,
                VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT | VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_HOST_COHERENT_BIT,
                ref vertexBuffer,
                (ulong)(vertices.Length * sizeof(Vertex)),
                vertices));
            // Index buffer
            Helpers.CheckErrors(this.CreateBuffer(
                VkBufferUsageFlagBits.VK_BUFFER_USAGE_INDEX_BUFFER_BIT,
                VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT | VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_HOST_COHERENT_BIT,
                ref indexBuffer,
                (ulong)(indices.Length * sizeof(uint)),
                indices));

            /*
                Create the bottom level acceleration structure containing the actual scene geometry
            */
            VkGeometryNV geometry = default;
            geometry.sType = VkStructureType.VK_STRUCTURE_TYPE_GEOMETRY_NV;
            geometry.geometryType = VkGeometryTypeKHR.VK_GEOMETRY_TYPE_TRIANGLES_KHR;
            geometry.geometry.triangles.sType = VkStructureType.VK_STRUCTURE_TYPE_GEOMETRY_TRIANGLES_NV;
            geometry.geometry.triangles.vertexData = vertexBuffer.buffer;
            geometry.geometry.triangles.vertexOffset = 0;
            geometry.geometry.triangles.vertexCount = (uint)vertices.Length;
            geometry.geometry.triangles.vertexStride = (ulong)sizeof(Vertex);
            geometry.geometry.triangles.vertexFormat = VkFormat.VK_FORMAT_R32G32B32_SFLOAT;
            geometry.geometry.triangles.indexData = indexBuffer.buffer;
            geometry.geometry.triangles.indexOffset = 0;
            geometry.geometry.triangles.indexCount = indexCount;
            geometry.geometry.triangles.indexType = VkIndexType.VK_INDEX_TYPE_UINT32;
            geometry.geometry.triangles.transformData = 0;
            geometry.geometry.triangles.transformOffset = 0;
            geometry.geometry.aabbs = default;
            geometry.geometry.aabbs.sType = VkStructureType.VK_STRUCTURE_TYPE_GEOMETRY_AABB_NV;
            geometry.flags = VkGeometryFlagBitsKHR.VK_GEOMETRY_OPAQUE_BIT_KHR;

            this.CreateBottomLevelAccelerationStructure(&geometry);

            /*
                Create the top-level acceleration structure that contains geometry instance information
            */

            // Single instance with a 3x4 transform matrix for the ray traced triangle
            VulkanBuffer instanceBuffer = default;

            Matrix3x4 transform = Matrix4x4.Identity.ToMatrix3x4();

            GeometryInstance geometryInstance = default;
            geometryInstance.transform = transform;
            geometryInstance.instanceId = 0;
            geometryInstance.mask = 0xff;
            geometryInstance.instanceOffset = 0;
            geometryInstance.flags = 1;
            geometryInstance.accelerationStructureHandle = bottomLevelAS.handle;
            GeometryInstance[] geometries = new GeometryInstance[] { geometryInstance };

            Helpers.CheckErrors(this.CreateBuffer(
                VkBufferUsageFlagBits.VK_BUFFER_USAGE_RAY_TRACING_BIT_KHR,
                VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT | VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_HOST_COHERENT_BIT,
                ref instanceBuffer,
                (ulong)sizeof(GeometryInstance),
                geometries));

            this.CreateTopLevelAccelerationStructure();

            /*
                Build the acceleration structure
            */

            // Acceleration structure build requires some scratch space to store temporary information
            VkAccelerationStructureMemoryRequirementsInfoNV memoryRequirementsInfo = default;
            memoryRequirementsInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_MEMORY_REQUIREMENTS_INFO_NV;
            memoryRequirementsInfo.type = VkAccelerationStructureMemoryRequirementsTypeKHR.VK_ACCELERATION_STRUCTURE_MEMORY_REQUIREMENTS_TYPE_BUILD_SCRATCH_KHR;

            VkMemoryRequirements2 memReqBottomLevelAS;
            memoryRequirementsInfo.accelerationStructure = bottomLevelAS.accelerationStructure;
            VulkanNative.vkGetAccelerationStructureMemoryRequirementsNV(device, &memoryRequirementsInfo, &memReqBottomLevelAS);

            VkMemoryRequirements2 memReqTopLevelAS;
            memoryRequirementsInfo.accelerationStructure = topLevelAS.accelerationStructure;
            VulkanNative.vkGetAccelerationStructureMemoryRequirementsNV(device, &memoryRequirementsInfo, &memReqTopLevelAS);

            ulong scratchBufferSize = Math.Max(memReqBottomLevelAS.memoryRequirements.size, memReqTopLevelAS.memoryRequirements.size);

            VulkanBuffer scratchBuffer = default;
            Helpers.CheckErrors(this.CreateBuffer(
                VkBufferUsageFlagBits.VK_BUFFER_USAGE_RAY_TRACING_BIT_KHR,
                VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT,
                ref scratchBuffer,
                (ulong)scratchBufferSize));

            VkCommandBuffer cmdBuffer = this.CreateCommandBuffer(VkCommandBufferLevel.VK_COMMAND_BUFFER_LEVEL_PRIMARY, this.commandPool, true);

            /*
                Build bottom level acceleration structure
            */
            VkAccelerationStructureInfoNV buildInfo = default;
            buildInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_INFO_NV;
            buildInfo.type = VkAccelerationStructureTypeKHR.VK_ACCELERATION_STRUCTURE_TYPE_BOTTOM_LEVEL_KHR;
            buildInfo.geometryCount = 1;
            buildInfo.pGeometries = &geometry;

            VulkanNative.vkCmdBuildAccelerationStructureNV(
                cmdBuffer,
                &buildInfo,
                0,
                0,
                false,
                bottomLevelAS.accelerationStructure,
                0,
                scratchBuffer.buffer,
                0);

            VkMemoryBarrier memoryBarrier = default;
            memoryBarrier.sType = VkStructureType.VK_STRUCTURE_TYPE_MEMORY_BARRIER;
            memoryBarrier.srcAccessMask = VkAccessFlagBits.VK_ACCESS_ACCELERATION_STRUCTURE_WRITE_BIT_KHR | VkAccessFlagBits.VK_ACCESS_ACCELERATION_STRUCTURE_READ_BIT_KHR;
            memoryBarrier.dstAccessMask = VkAccessFlagBits.VK_ACCESS_ACCELERATION_STRUCTURE_WRITE_BIT_KHR | VkAccessFlagBits.VK_ACCESS_ACCELERATION_STRUCTURE_READ_BIT_KHR;
            VulkanNative.vkCmdPipelineBarrier(cmdBuffer, VkPipelineStageFlagBits.VK_PIPELINE_STAGE_ACCELERATION_STRUCTURE_BUILD_BIT_KHR, VkPipelineStageFlagBits.VK_PIPELINE_STAGE_ACCELERATION_STRUCTURE_BUILD_BIT_KHR, 0, 1, &memoryBarrier, 0, null, 0, null);

            /*
                Build top-level acceleration structure
            */
            buildInfo.type = VkAccelerationStructureTypeKHR.VK_ACCELERATION_STRUCTURE_TYPE_TOP_LEVEL_KHR;
            buildInfo.pGeometries = null;
            buildInfo.geometryCount = 0;
            buildInfo.instanceCount = 1;

            VulkanNative.vkCmdBuildAccelerationStructureNV(
                cmdBuffer,
                &buildInfo,
                instanceBuffer.buffer,
                0,
                false,
                topLevelAS.accelerationStructure,
                0,
                scratchBuffer.buffer,
                0);

            VulkanNative.vkCmdPipelineBarrier(cmdBuffer, VkPipelineStageFlagBits.VK_PIPELINE_STAGE_ACCELERATION_STRUCTURE_BUILD_BIT_KHR, VkPipelineStageFlagBits.VK_PIPELINE_STAGE_ACCELERATION_STRUCTURE_BUILD_BIT_KHR, 0, 1, &memoryBarrier, 0, null, 0, null);

            Helpers.CheckErrors(VulkanNative.vkEndCommandBuffer(cmdBuffer));

            VkSubmitInfo submitInfo = default;
            submitInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_SUBMIT_INFO;
            submitInfo.commandBufferCount = 1;
            submitInfo.pCommandBuffers = &cmdBuffer;
            //// Create fence to ensure that the command buffer has finished executing
            //VkFenceCreateInfo fenceInfo = vks::initializers::fenceCreateInfo(VK_FLAGS_NONE);
            //VkFence fence;
            //VK_CHECK_RESULT(vkCreateFence(logicalDevice, &fenceInfo, nullptr, &fence));
            // Submit to the queue
            Helpers.CheckErrors(VulkanNative.vkQueueSubmit(this.graphicsQueue, 1, &submitInfo, 0));
            // Wait for the fence to signal that command buffer has finished executing
            //VK_CHECK_RESULT(vkWaitForFences(logicalDevice, 1, &fence, VK_TRUE, DEFAULT_FENCE_TIMEOUT));
            //vkDestroyFence(logicalDevice, fence, nullptr);
            //if (free)
            //{
            //    vkFreeCommandBuffers(logicalDevice, pool, 1, &commandBuffer);
            //}

            scratchBuffer.Destroy();
            instanceBuffer.Destroy();
        }

        private int CopyShaderIdentifier(IntPtr data, ushort* shaderHandleStorage, uint groupIndex)
        {
            uint shaderGroupHandleSize = rayTracingProperties.shaderGroupHandleSize;
            Unsafe.CopyBlock((void*)data, shaderHandleStorage + groupIndex * shaderGroupHandleSize, shaderGroupHandleSize);
            return (int)shaderGroupHandleSize;
        }

        private void CreateShaderBindingTable()
        {
            // Create buffer for the shader binding table
            uint sbtSize = rayTracingProperties.shaderGroupHandleSize * NUM_SHADER_GROUPS;
            Helpers.CheckErrors(this.CreateBuffer(
                VkBufferUsageFlagBits.VK_BUFFER_USAGE_RAY_TRACING_BIT_KHR,
                VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT,
                ref shaderBindingTable,
                (ulong)sbtSize));
            shaderBindingTable.Map();

            ushort* shaderHandleStorage = stackalloc ushort[(int)sbtSize];
            // Get shader identifiers
            Helpers.CheckErrors(VulkanNative.vkGetRayTracingShaderGroupHandlesKHR(device, pipeline, 0, NUM_SHADER_GROUPS, (UIntPtr)sbtSize, shaderHandleStorage));
            IntPtr data = shaderBindingTable.mapped;
            // Copy the shader identifiers to the shader binding table
            data += this.CopyShaderIdentifier(data, shaderHandleStorage, (uint)INDEX_RAYGEN);
            data += this.CopyShaderIdentifier(data, shaderHandleStorage, (uint)INDEX_MISS);
            data += this.CopyShaderIdentifier(data, shaderHandleStorage, (uint)INDEX_CLOSEST_HIT);
            shaderBindingTable.Unmap();
        }

        private void CreateDescriptorSets()
        {
            VkDescriptorPoolSize* poolSizes = stackalloc VkDescriptorPoolSize[] {
                new VkDescriptorPoolSize() { type = VkDescriptorType.VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR, descriptorCount = 1 },
                new VkDescriptorPoolSize() { type = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_IMAGE, descriptorCount = 1 },
                new VkDescriptorPoolSize() { type = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER, descriptorCount = 1 }
            };

            VkDescriptorPoolCreateInfo descriptorPoolCreateInfo = default;
            descriptorPoolCreateInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_DESCRIPTOR_POOL_CREATE_INFO;
            descriptorPoolCreateInfo.poolSizeCount = 3;
            descriptorPoolCreateInfo.pPoolSizes = poolSizes;
            descriptorPoolCreateInfo.maxSets = 1;
            fixed (VkDescriptorPool* descriptorPoolPtr = &this.descriptorPool)
            {
                Helpers.CheckErrors(VulkanNative.vkCreateDescriptorPool(device, &descriptorPoolCreateInfo, null, descriptorPoolPtr));
            }

            VkDescriptorSetAllocateInfo descriptorSetAllocateInfo = default;
            descriptorSetAllocateInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO;
            descriptorSetAllocateInfo.descriptorPool = descriptorPool;
            fixed (VkDescriptorSetLayout* descriptorSetLayoutPtr = &this.descriptorSetLayout)
            {
                descriptorSetAllocateInfo.pSetLayouts = descriptorSetLayoutPtr;
            }
            descriptorSetAllocateInfo.descriptorSetCount = 1;

            fixed (VkDescriptorSet* descriptorSetPtr = &this.descriptorSet)
            {
                Helpers.CheckErrors(VulkanNative.vkAllocateDescriptorSets(device, &descriptorSetAllocateInfo, descriptorSetPtr));
            }

            VkWriteDescriptorSetAccelerationStructureKHR descriptorAccelerationStructureInfo = default;
            descriptorAccelerationStructureInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET_ACCELERATION_STRUCTURE_KHR;
            descriptorAccelerationStructureInfo.accelerationStructureCount = 1;
            fixed (VkAccelerationStructureKHR* topLevelASPtr = &topLevelAS.accelerationStructure)
            {
                descriptorAccelerationStructureInfo.pAccelerationStructures = topLevelASPtr;
            }

            VkWriteDescriptorSet accelerationStructureWrite = default;
            accelerationStructureWrite.sType = VkStructureType.VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET;
            // The specialized acceleration structure descriptor has to be chained
            accelerationStructureWrite.pNext = &descriptorAccelerationStructureInfo;
            accelerationStructureWrite.dstSet = descriptorSet;
            accelerationStructureWrite.dstBinding = 0;
            accelerationStructureWrite.descriptorCount = 1;
            accelerationStructureWrite.descriptorType = VkDescriptorType.VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR;

            VkDescriptorImageInfo storageImageDescriptor = default;
            storageImageDescriptor.imageView = storageImage.view;
            storageImageDescriptor.imageLayout = VkImageLayout.VK_IMAGE_LAYOUT_GENERAL;

            VkWriteDescriptorSet resultImageWrite = default;
            resultImageWrite.sType = VkStructureType.VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET;
            resultImageWrite.dstSet = this.descriptorSet;
            resultImageWrite.descriptorType = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_IMAGE;
            resultImageWrite.dstBinding = 1;
            resultImageWrite.pImageInfo = &storageImageDescriptor;
            resultImageWrite.descriptorCount = 1;

            VkWriteDescriptorSet uniformBufferWrite = default;
            uniformBufferWrite.sType = VkStructureType.VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET;
            uniformBufferWrite.dstSet = this.descriptorSet;
            uniformBufferWrite.descriptorType = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER;
            uniformBufferWrite.dstBinding = 2;
            fixed (VkDescriptorBufferInfo* uboDescriptor = &ubo.descriptor)
            {
                uniformBufferWrite.pBufferInfo = uboDescriptor;
            }
            uniformBufferWrite.descriptorCount = 1;

            VkWriteDescriptorSet* writeDescriptorSets = stackalloc VkWriteDescriptorSet[] {
                accelerationStructureWrite,
                resultImageWrite,
                uniformBufferWrite
            };
            VulkanNative.vkUpdateDescriptorSets(device, (uint)3, writeDescriptorSets, 0, null);
        }

        VkShaderModule CreateShaderModule(byte[] code)
        {
            VkShaderModuleCreateInfo createInfo = new VkShaderModuleCreateInfo();
            createInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_SHADER_MODULE_CREATE_INFO;
            createInfo.codeSize = (UIntPtr)code.Length;

            fixed (byte* sourcePointer = code)
            {
                createInfo.pCode = (uint*)sourcePointer;
            }

            VkShaderModule shaderModule;
            Helpers.CheckErrors(VulkanNative.vkCreateShaderModule(device, &createInfo, null, &shaderModule));

            return shaderModule;
        }

        private VkPipelineShaderStageCreateInfo LoadShader(string fileName, VkShaderStageFlagBits stage)
        {
            byte[] spirv = File.ReadAllBytes(fileName);

            VkPipelineShaderStageCreateInfo shaderStage = default;
            shaderStage.sType = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO;
            shaderStage.stage = stage;
            shaderStage.module = this.CreateShaderModule(spirv);
            shaderStage.pName = "main".ToPointer();

            return shaderStage;
        }

        private void CreateRayTracingPipeline()
        {
            VkDescriptorSetLayoutBinding accelerationStructureLayoutBinding = default;
            accelerationStructureLayoutBinding.binding = 0;
            accelerationStructureLayoutBinding.descriptorType = VkDescriptorType.VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR;
            accelerationStructureLayoutBinding.descriptorCount = 1;
            accelerationStructureLayoutBinding.stageFlags = VkShaderStageFlagBits.VK_SHADER_STAGE_RAYGEN_BIT_KHR;

            VkDescriptorSetLayoutBinding resultImageLayoutBinding = default;
            resultImageLayoutBinding.binding = 1;
            resultImageLayoutBinding.descriptorType = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_IMAGE;
            resultImageLayoutBinding.descriptorCount = 1;
            resultImageLayoutBinding.stageFlags = VkShaderStageFlagBits.VK_SHADER_STAGE_RAYGEN_BIT_KHR;

            VkDescriptorSetLayoutBinding uniformBufferBinding = default;
            uniformBufferBinding.binding = 2;
            uniformBufferBinding.descriptorType = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER;
            uniformBufferBinding.descriptorCount = 1;
            uniformBufferBinding.stageFlags = VkShaderStageFlagBits.VK_SHADER_STAGE_RAYGEN_BIT_KHR;

            VkDescriptorSetLayoutBinding* bindings = stackalloc VkDescriptorSetLayoutBinding[] {
                accelerationStructureLayoutBinding,
                resultImageLayoutBinding,
                uniformBufferBinding
            };

            VkDescriptorSetLayoutCreateInfo layoutInfo = default;
            layoutInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO;
            layoutInfo.bindingCount = 3;
            layoutInfo.pBindings = bindings;
            fixed (VkDescriptorSetLayout* descriptorSetLayoutPtr = &descriptorSetLayout)
            {
                Helpers.CheckErrors(VulkanNative.vkCreateDescriptorSetLayout(device, &layoutInfo, null, descriptorSetLayoutPtr));

                VkPipelineLayoutCreateInfo pipelineLayoutCreateInfo = default;
                pipelineLayoutCreateInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO;
                pipelineLayoutCreateInfo.setLayoutCount = 1;
                pipelineLayoutCreateInfo.pSetLayouts = descriptorSetLayoutPtr;

                VkPipelineLayout newPipelineLayout;
                Helpers.CheckErrors(VulkanNative.vkCreatePipelineLayout(device, &pipelineLayoutCreateInfo, null, &newPipelineLayout));
                this.pipelineLayout = newPipelineLayout;
            }

            const uint shaderIndexRaygen = 0;
            const uint shaderIndexMiss = 1;
            const uint shaderIndexClosestHit = 2;

            VkPipelineShaderStageCreateInfo* shaderStages = stackalloc VkPipelineShaderStageCreateInfo[3];
            shaderStages[shaderIndexRaygen] = this.LoadShader("Shaders/raygen.rgen.spv", VkShaderStageFlagBits.VK_SHADER_STAGE_RAYGEN_BIT_KHR);
            shaderStages[shaderIndexMiss] = this.LoadShader("Shaders/miss.rmiss.spv", VkShaderStageFlagBits.VK_SHADER_STAGE_MISS_BIT_KHR);
            shaderStages[shaderIndexClosestHit] = this.LoadShader("Shaders/closesthit.rchit.spv", VkShaderStageFlagBits.VK_SHADER_STAGE_CLOSEST_HIT_BIT_KHR);

            /*
                Setup ray tracing shader groups
            */
            VkRayTracingShaderGroupCreateInfoNV* groups = stackalloc VkRayTracingShaderGroupCreateInfoNV[NUM_SHADER_GROUPS];
            for (int i = 0; i < NUM_SHADER_GROUPS; i++)
            {
                var group = groups[i];

                // Init all groups with some default values
                group.sType = VkStructureType.VK_STRUCTURE_TYPE_RAY_TRACING_SHADER_GROUP_CREATE_INFO_NV;
                group.generalShader = VulkanNative.VK_SHADER_UNUSED_NV;
                group.closestHitShader = VulkanNative.VK_SHADER_UNUSED_NV;
                group.anyHitShader = VulkanNative.VK_SHADER_UNUSED_NV;
                group.intersectionShader = VulkanNative.VK_SHADER_UNUSED_NV;
            }

            // Links shaders and types to ray tracing shader groups
            groups[INDEX_RAYGEN].type = VkRayTracingShaderGroupTypeKHR.VK_RAY_TRACING_SHADER_GROUP_TYPE_GENERAL_KHR;
            groups[INDEX_RAYGEN].generalShader = shaderIndexRaygen;
            groups[INDEX_MISS].type = VkRayTracingShaderGroupTypeKHR.VK_RAY_TRACING_SHADER_GROUP_TYPE_GENERAL_KHR;
            groups[INDEX_MISS].generalShader = shaderIndexMiss;
            groups[INDEX_CLOSEST_HIT].type = VkRayTracingShaderGroupTypeKHR.VK_RAY_TRACING_SHADER_GROUP_TYPE_TRIANGLES_HIT_GROUP_KHR;
            groups[INDEX_CLOSEST_HIT].generalShader = VulkanNative.VK_SHADER_UNUSED_NV;
            groups[INDEX_CLOSEST_HIT].closestHitShader = shaderIndexClosestHit;

            VkRayTracingPipelineCreateInfoNV rayPipelineInfo = default;
            rayPipelineInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_RAY_TRACING_PIPELINE_CREATE_INFO_NV;
            rayPipelineInfo.stageCount = 3;
            rayPipelineInfo.pStages = shaderStages;
            rayPipelineInfo.groupCount = 3;
            rayPipelineInfo.pGroups = groups;
            rayPipelineInfo.maxRecursionDepth = 1;
            rayPipelineInfo.layout = this.pipelineLayout;

            VkPipeline newPipeline;
            Helpers.CheckErrors(VulkanNative.vkCreateRayTracingPipelinesNV(device, 0, 1, &rayPipelineInfo, null, &newPipeline));
            this.pipeline = newPipeline;
        }

        void CreateUniformBuffer()
        {
            Helpers.CheckErrors(this.CreateBuffer(
                VkBufferUsageFlagBits.VK_BUFFER_USAGE_UNIFORM_BUFFER_BIT,
                VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT | VkMemoryPropertyFlagBits.VK_MEMORY_PROPERTY_HOST_COHERENT_BIT,
                ref ubo,
                (ulong)sizeof(UniformData),
                uniformData));
            Helpers.CheckErrors(ubo.Map());

            this.UpdateUniformBuffers();
        }

        private void BuildCommandBuffers()
        {
            VkCommandBufferBeginInfo cmdBufInfo = default;
            cmdBufInfo.sType = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO;

            VkImageSubresourceRange subresourceRange = new VkImageSubresourceRange()
            {
                aspectMask = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
                baseMipLevel = 0,
                levelCount = 1,
                baseArrayLayer = 0,
                layerCount = 1,
            };

            for (int i = 0; i < this.commandBuffers.Length; ++i)
            {
                Helpers.CheckErrors(VulkanNative.vkBeginCommandBuffer(this.commandBuffers[i], &cmdBufInfo));

                /*
                    Dispatch the ray tracing commands
                */
                VulkanNative.vkCmdBindPipeline(this.commandBuffers[i], VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_RAY_TRACING_KHR, pipeline);
                fixed (VkDescriptorSet* descriptorSetPtr = &this.descriptorSet)
                {
                    VulkanNative.vkCmdBindDescriptorSets(this.commandBuffers[i], VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_RAY_TRACING_KHR, this.pipelineLayout, 0, 1, descriptorSetPtr, 0, null);
                }
                // Calculate shader binding offsets, which is pretty straight forward in our example 
                ulong bindingOffsetRayGenShader = rayTracingProperties.shaderGroupHandleSize * INDEX_RAYGEN;
                ulong bindingOffsetMissShader = rayTracingProperties.shaderGroupHandleSize * INDEX_MISS;
                ulong bindingOffsetHitShader = rayTracingProperties.shaderGroupHandleSize * INDEX_CLOSEST_HIT;
                ulong bindingStride = rayTracingProperties.shaderGroupHandleSize;

                VulkanNative.vkCmdTraceRaysNV(this.commandBuffers[i],
                    shaderBindingTable.buffer, bindingOffsetRayGenShader,
                    shaderBindingTable.buffer, bindingOffsetMissShader, bindingStride,
                    shaderBindingTable.buffer, bindingOffsetHitShader, bindingStride,
                    0, 0, 0,
                    WIDTH, HEIGHT, 1);

                /*
                    Copy raytracing output to swap chain image
                */

                // Prepare current swapchain image as transfer destination
                this.SetImageLayout(
                    this.commandBuffers[i],
                    this.swapChainImages[i],
                     VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                    VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                    subresourceRange);

                // Prepare ray tracing output image as transfer source
                this.SetImageLayout(
                    this.commandBuffers[i],
                    storageImage.image,
                    VkImageLayout.VK_IMAGE_LAYOUT_GENERAL,
                    VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                    subresourceRange);

                VkImageCopy copyRegion = default;
                copyRegion.srcSubresource = new VkImageSubresourceLayers()
                {
                    aspectMask = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
                    mipLevel = 0,
                    baseArrayLayer = 0,
                    layerCount = 1
                };
                copyRegion.srcOffset = new VkOffset3D();
                copyRegion.dstSubresource = new VkImageSubresourceLayers()
                {
                    aspectMask = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
                    mipLevel = 0,
                    baseArrayLayer = 0,
                    layerCount = 1
                };
                copyRegion.dstOffset = new VkOffset3D();
                copyRegion.extent = new VkExtent3D() { width = this.swapChainExtent.width, height = this.swapChainExtent.height, depth = 1 };
                VulkanNative.vkCmdCopyImage(this.commandBuffers[i], storageImage.image, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL, this.swapChainImages[i], VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, 1, &copyRegion);

                // Transition swap chain image back for presentation
                this.SetImageLayout(
                    this.commandBuffers[i],
                    this.swapChainImages[i],
                    VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                    VkImageLayout.VK_IMAGE_LAYOUT_PRESENT_SRC_KHR,
                    subresourceRange);

                // Transition ray tracing output image back to general layout
                this.SetImageLayout(
                    this.commandBuffers[i],
                    storageImage.image,
                    VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                    VkImageLayout.VK_IMAGE_LAYOUT_GENERAL,
                    subresourceRange);

                //@todo: Default render pass setup willl overwrite contents
                //vkCmdBeginRenderPass(drawCmdBuffers[i], &renderPassBeginInfo, VK_SUBPASS_CONTENTS_INLINE);
                //drawUI(drawCmdBuffers[i]);
                //vkCmdEndRenderPass(drawCmdBuffers[i]);

                Helpers.CheckErrors(VulkanNative.vkEndCommandBuffer(this.commandBuffers[i]));
            }
        }

        public float ConvertToRadians(float angle)
        {
            return (float)((Math.PI / 180) * angle);
        }

        private void UpdateUniformBuffers()
        {
            var perpective = Matrix4x4.CreatePerspectiveFieldOfView(this.ConvertToRadians(60.0f), (float)WIDTH / (float)HEIGHT, 0.1f, 512.0f);
            var view = Matrix4x4.CreateTranslation(0, 0, -2.5f);
            Matrix4x4.Invert(perpective, out uniformData.projInverse);
            Matrix4x4.Invert(view, out uniformData.viewInverse);

            GCHandle gcHandle = GCHandle.Alloc(uniformData, GCHandleType.Pinned);
            IntPtr srcDataPtr = gcHandle.AddrOfPinnedObject();            
            Unsafe.CopyBlock((void*)ubo.mapped, (void*)srcDataPtr, (uint)Unsafe.SizeOf<UniformData>());
            gcHandle.Free();
        }

        private void SetImageLayout(
            VkCommandBuffer cmdbuffer,
            VkImage image,
            VkImageLayout oldImageLayout,
            VkImageLayout newImageLayout,
            VkImageSubresourceRange subresourceRange,
            VkPipelineStageFlagBits srcStageMask = VkPipelineStageFlagBits.VK_PIPELINE_STAGE_ALL_COMMANDS_BIT,
            VkPipelineStageFlagBits dstStageMask = VkPipelineStageFlagBits.VK_PIPELINE_STAGE_ALL_COMMANDS_BIT)
        {
            // Create an image barrier object
            VkImageMemoryBarrier imageMemoryBarrier = default;
            imageMemoryBarrier.sType = VkStructureType.VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER;
            imageMemoryBarrier.srcQueueFamilyIndex = VulkanNative.VK_QUEUE_FAMILY_IGNORED;
            imageMemoryBarrier.dstQueueFamilyIndex = VulkanNative.VK_QUEUE_FAMILY_IGNORED;
            imageMemoryBarrier.oldLayout = oldImageLayout;
            imageMemoryBarrier.newLayout = newImageLayout;
            imageMemoryBarrier.image = image;
            imageMemoryBarrier.subresourceRange = subresourceRange;

            // Source layouts (old)
            // Source access mask controls actions that have to be finished on the old layout
            // before it will be transitioned to the new layout
            switch (oldImageLayout)
            {
                case VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED:
                    // Image layout is undefined (or does not matter)
                    // Only valid as initial layout
                    // No flags required, listed only for completeness
                    imageMemoryBarrier.srcAccessMask = 0;
                    break;

                case VkImageLayout.VK_IMAGE_LAYOUT_PREINITIALIZED:
                    // Image is preinitialized
                    // Only valid as initial layout for linear images, preserves memory contents
                    // Make sure host writes have been finished
                    imageMemoryBarrier.srcAccessMask = VkAccessFlagBits.VK_ACCESS_HOST_WRITE_BIT;
                    break;

                case VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL:
                    // Image is a color attachment
                    // Make sure any writes to the color buffer have been finished
                    imageMemoryBarrier.srcAccessMask = VkAccessFlagBits.VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT;
                    break;

                case VkImageLayout.VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL:
                    // Image is a depth/stencil attachment
                    // Make sure any writes to the depth/stencil buffer have been finished
                    imageMemoryBarrier.srcAccessMask = VkAccessFlagBits.VK_ACCESS_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT;
                    break;

                case VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL:
                    // Image is a transfer source 
                    // Make sure any reads from the image have been finished
                    imageMemoryBarrier.srcAccessMask = VkAccessFlagBits.VK_ACCESS_TRANSFER_READ_BIT;
                    break;

                case VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL:
                    // Image is a transfer destination
                    // Make sure any writes to the image have been finished
                    imageMemoryBarrier.srcAccessMask = VkAccessFlagBits.VK_ACCESS_TRANSFER_WRITE_BIT;
                    break;

                case VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL:
                    // Image is read by a shader
                    // Make sure any shader reads from the image have been finished
                    imageMemoryBarrier.srcAccessMask = VkAccessFlagBits.VK_ACCESS_SHADER_READ_BIT;
                    break;
                default:
                    // Other source layouts aren't handled (yet)
                    break;
            }

            // Target layouts (new)
            // Destination access mask controls the dependency for the new image layout
            switch (newImageLayout)
            {
                case VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL:
                    // Image will be used as a transfer destination
                    // Make sure any writes to the image have been finished
                    imageMemoryBarrier.dstAccessMask = VkAccessFlagBits.VK_ACCESS_TRANSFER_WRITE_BIT;
                    break;

                case VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL:
                    // Image will be used as a transfer source
                    // Make sure any reads from the image have been finished
                    imageMemoryBarrier.dstAccessMask = VkAccessFlagBits.VK_ACCESS_TRANSFER_READ_BIT;
                    break;

                case VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL:
                    // Image will be used as a color attachment
                    // Make sure any writes to the color buffer have been finished
                    imageMemoryBarrier.dstAccessMask = VkAccessFlagBits.VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT;
                    break;

                case VkImageLayout.VK_IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL:
                    // Image layout will be used as a depth/stencil attachment
                    // Make sure any writes to depth/stencil buffer have been finished
                    imageMemoryBarrier.dstAccessMask = imageMemoryBarrier.dstAccessMask | VkAccessFlagBits.VK_ACCESS_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT;
                    break;

                case VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL:
                    // Image will be read in a shader (sampler, input attachment)
                    // Make sure any writes to the image have been finished
                    if (imageMemoryBarrier.srcAccessMask == 0)
                    {
                        imageMemoryBarrier.srcAccessMask = VkAccessFlagBits.VK_ACCESS_HOST_WRITE_BIT | VkAccessFlagBits.VK_ACCESS_TRANSFER_WRITE_BIT;
                    }
                    imageMemoryBarrier.dstAccessMask = VkAccessFlagBits.VK_ACCESS_SHADER_READ_BIT;
                    break;
                default:
                    // Other source layouts aren't handled (yet)
                    break;
            }

            // Put barrier inside setup command buffer
            VulkanNative.vkCmdPipelineBarrier(
                cmdbuffer,
                srcStageMask,
                dstStageMask,
                0,
                0, null,
                0, null,
                1, &imageMemoryBarrier);
        }
    }
}
