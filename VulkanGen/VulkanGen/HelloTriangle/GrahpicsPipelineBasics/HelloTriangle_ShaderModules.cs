
using System;
using System.IO;
using WaveEngine.Bindings.Vulkan;

namespace HelloTriangle
{
    public unsafe partial class HelloTriangle
    {
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

        private void CreateGraphicsPipeline()
        {
            byte[] vertShaderCode = File.ReadAllBytes("Shaders/vert.spv");
            byte[] fragShaderCode = File.ReadAllBytes("Shaders/frag.spv");

            VkShaderModule vertShaderModule = this.CreateShaderModule(vertShaderCode);
            VkShaderModule fragShaderModule = this.CreateShaderModule(fragShaderCode);

            VkPipelineShaderStageCreateInfo vertShaderStageInfo = new VkPipelineShaderStageCreateInfo()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO,
                stage = VkShaderStageFlagBits.VK_SHADER_STAGE_VERTEX_BIT,
                module = vertShaderModule,
                pName = "main".ToPointer(),
            };

            VkPipelineShaderStageCreateInfo fragShaderStageInfo = new VkPipelineShaderStageCreateInfo()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO,
                stage = VkShaderStageFlagBits.VK_SHADER_STAGE_FRAGMENT_BIT,
                module = fragShaderModule,
                pName = "main".ToPointer(),
            };

            VkPipelineShaderStageCreateInfo* shaderStages = stackalloc VkPipelineShaderStageCreateInfo[] { vertShaderStageInfo, fragShaderStageInfo };

            VulkanNative.vkDestroyShaderModule(device, fragShaderModule, null);
            VulkanNative.vkDestroyShaderModule(device, vertShaderModule, null);
        }
    }
}
