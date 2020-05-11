using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using WaveEngine.Bindings.Vulkan;

namespace NVRTXHelloTriangle
{
	public unsafe struct VulkanBuffer
	{
		public VkDevice device;
		public VkBuffer buffer;
		public VkDeviceMemory memory;
		public VkDescriptorBufferInfo descriptor;
		public ulong size;
		public ulong alignment;
		public IntPtr mapped;

		/** @brief Usage flags to be filled by external source at buffer creation (to query at some later point) */
		public VkBufferUsageFlagBits usageFlags;
		/** @brief Memory propertys flags to be filled by external source at buffer creation (to query at some later point) */
		public VkMemoryPropertyFlagBits memoryPropertyFlags;

		/** 
		* Map a memory range of this buffer. If successful, mapped points to the specified buffer range.
		* 
		* @param size (Optional) Size of the memory range to map. Pass VK_WHOLE_SIZE to map the complete buffer range.
		* @param offset (Optional) Byte offset from beginning
		* 
		* @return VkResult of the buffer mapping call
		*/
		public VkResult Map(ulong size = VulkanNative.VK_WHOLE_SIZE, ulong offset = 0)
		{
			IntPtr mappedPtr;
			var result = VulkanNative.vkMapMemory(device, memory, offset, size, 0, (void**)&mappedPtr);
			this.mapped = mappedPtr;
			return result;
		}

		/**
		* Unmap a mapped memory range
		*
		* @note Does not return a result as vkUnmapMemory can't fail
		*/
		public void Unmap()
		{
			if (this.mapped != IntPtr.Zero)
			{
				VulkanNative.vkUnmapMemory(device, memory);
				this.mapped = IntPtr.Zero;
			}
		}

		/** 
		* Attach the allocated memory block to the buffer
		* 
		* @param offset (Optional) Byte offset (from the beginning) for the memory region to bind
		* 
		* @return VkResult of the bindBufferMemory call
		*/
		public VkResult Bind(ulong offset = 0)
		{
			return VulkanNative.vkBindBufferMemory(device, buffer, memory, offset);
		}

		/**
		* Setup the default descriptor for this buffer
		*
		* @param size (Optional) Size of the memory range of the descriptor
		* @param offset (Optional) Byte offset from beginning
		*
		*/
		public void SetupDescriptor(ulong size = VulkanNative.VK_WHOLE_SIZE, ulong offset = 0)
		{
			descriptor.offset = offset;
			descriptor.buffer = buffer;
			descriptor.range = size;
		}

		/**
		* Copies the specified data to the mapped buffer
		* 
		* @param data Pointer to the data to copy
		* @param size Size of the data to copy in machine units
		*
		*/
		public void CopyTo(void* data, ulong size)
		{
			Debug.Assert(mapped != IntPtr.Zero);
			Unsafe.CopyBlock((void*)this.mapped, data, (uint)size);
		}

		/** 
		* Flush a memory range of the buffer to make it visible to the device
		*
		* @note Only required for non-coherent memory
		*
		* @param size (Optional) Size of the memory range to flush. Pass VK_WHOLE_SIZE to flush the complete buffer range.
		* @param offset (Optional) Byte offset from beginning
		*
		* @return VkResult of the flush call
		*/
		public VkResult Flush(ulong size = VulkanNative.VK_WHOLE_SIZE, ulong offset = 0)
		{
			VkMappedMemoryRange mappedRange = default;
			mappedRange.sType = VkStructureType.VK_STRUCTURE_TYPE_MAPPED_MEMORY_RANGE;
			mappedRange.memory = memory;
			mappedRange.offset = offset;
			mappedRange.size = size;
			return VulkanNative.vkFlushMappedMemoryRanges(device, 1, &mappedRange);
		}

		/**
		* Invalidate a memory range of the buffer to make it visible to the host
		*
		* @note Only required for non-coherent memory
		*
		* @param size (Optional) Size of the memory range to invalidate. Pass VK_WHOLE_SIZE to invalidate the complete buffer range.
		* @param offset (Optional) Byte offset from beginning
		*
		* @return VkResult of the invalidate call
		*/
		public VkResult Invalidate(ulong size = VulkanNative.VK_WHOLE_SIZE, ulong offset = 0)
		{
			VkMappedMemoryRange mappedRange = default;
			mappedRange.sType = VkStructureType.VK_STRUCTURE_TYPE_MAPPED_MEMORY_RANGE;
			mappedRange.memory = memory;
			mappedRange.offset = offset;
			mappedRange.size = size;
			return VulkanNative.vkInvalidateMappedMemoryRanges(device, 1, &mappedRange);
		}

		/** 
		* Release all Vulkan resources held by this buffer
		*/
		public void Destroy()
		{
			if (this.buffer != default)
			{
				VulkanNative.vkDestroyBuffer(device, buffer, null);
			}
			if (this.memory != default)
			{
				VulkanNative.vkFreeMemory(device, memory, null);
			}
		}

	};
}