using System.Collections.Generic;
using UnityEditor.ShaderFoundry;

namespace UnityEditor.ShaderFoundry.UnitTests
{
    internal class PropertyAttributeData
    {
        public string UniformName;
        public string DisplayName;
        public string DefaultValue;
        public bool? Exposed;
        public UniformDataSource? DataSource;
    }

    class BlockBuilderUtilities
    {
        internal static void MarkAsProperty(ShaderContainer container, StructField.Builder fieldBuilder, PropertyAttributeData propertyAttribute)
        {
            var propAttributeBuilder = new ShaderAttribute.Builder(container, "Property");
            if (!string.IsNullOrEmpty(propertyAttribute.UniformName))
                propAttributeBuilder.Param("uniformName", propertyAttribute.UniformName);
            if (!string.IsNullOrEmpty(propertyAttribute.DisplayName))
                propAttributeBuilder.Param("displayName", propertyAttribute.DisplayName);
            if (!string.IsNullOrEmpty(propertyAttribute.DefaultValue))
                propAttributeBuilder.Param("defaultValue", propertyAttribute.DefaultValue);
            if (propertyAttribute.DataSource is UniformDataSource dataSource)
                propAttributeBuilder.Param("dataSource", dataSource.ToString());
            if (propertyAttribute.Exposed is bool exposed)
                propAttributeBuilder.Param("exposed", exposed.ToString());
            fieldBuilder.AddAttribute(propAttributeBuilder.Build());
        }

        internal class FieldData
        {
            internal PropertyAttributeData PropertyAttribute;
            internal List<ShaderAttribute> ExtraAttributes;
            internal ShaderType Type;
            internal string Name;
        }

        internal class PropertyDeclarationData
        {
            internal List<FieldData> Fields;
            internal string OutputInstanceName = "outputs";
            internal string InputInstanceName = "inputs";

            internal PropertyAttributeData PropertyAttribute;
            internal List<ShaderAttribute> ExtraAttributes;
            internal ShaderType FieldType;
            internal string FieldName;
            internal delegate void OutputsAssignmentDelegate(ShaderFunction.Builder builder, PropertyDeclarationData propData);
            internal OutputsAssignmentDelegate OutputsAssignmentCallback = null;

            // Allows creating extra data in the block, such as helper functions.
            internal delegate void ExtraBlockGenerationDelegate(Block.Builder blockBuilder, PropertyDeclarationData propData);
            internal ExtraBlockGenerationDelegate ExtraBlockGenerationCallback = null;
        }

        internal static Block.Builder CreateSimplePropertyBlockBuilder(ShaderContainer container, string blockName, PropertyDeclarationData propertyData)
        {
            var blockBuilder = new Block.Builder(container, blockName);

            // Build the input type
            var inputFields = new List<FieldData>();
            // TODO @ SHADERS: This should get cleaned up eventually to not have the primary field, but all of the tests need to get updated.
            // Build the primary field into our list of fields.
            if (propertyData.FieldName != null)
            {
                var primaryField = new FieldData
                {
                    Name = propertyData.FieldName,
                    Type = propertyData.FieldType,
                    ExtraAttributes = propertyData.ExtraAttributes,
                    PropertyAttribute = propertyData.PropertyAttribute,
                };
                inputFields.Add(primaryField);
            }
            if (propertyData.Fields != null)
                inputFields.AddRange(propertyData.Fields);

            var inputTypeBuilder = new ShaderType.StructBuilder(blockBuilder, "Input");
            foreach (var fieldData in inputFields)
            {
                var fieldBuilder = new StructField.Builder(container, fieldData.Name, fieldData.Type);
                if (fieldData.PropertyAttribute != null)
                    MarkAsProperty(container, fieldBuilder, fieldData.PropertyAttribute);

                if (fieldData.ExtraAttributes != null)
                {
                    foreach (var attribute in fieldData.ExtraAttributes)
                        fieldBuilder.AddAttribute(attribute);
                }
                inputTypeBuilder.AddField(fieldBuilder.Build());
            }

            var inputAlphaBuilder = new StructField.Builder(container, "Alpha", container._float);
            inputTypeBuilder.AddField(inputAlphaBuilder.Build());
            var inputType = inputTypeBuilder.Build();

            // Build the output type
            var outputTypeBuilder = new ShaderType.StructBuilder(blockBuilder, "Output");
            var outputBaseColorBuilder = new StructField.Builder(container, "BaseColor", container._float3);
            outputTypeBuilder.AddField(outputBaseColorBuilder.Build());
            var outputAlphaBuilder = new StructField.Builder(container, "Alpha", container._float);
            outputTypeBuilder.AddField(outputAlphaBuilder.Build());
            var outputType = outputTypeBuilder.Build();

            if (propertyData.ExtraBlockGenerationCallback != null)
                propertyData.ExtraBlockGenerationCallback(blockBuilder, propertyData);

            // Build the entry point
            var entryPointFnBuilder = new ShaderFunction.Builder(blockBuilder, "Apply", outputType);
            entryPointFnBuilder.AddInput(inputType, propertyData.InputInstanceName);
            entryPointFnBuilder.AddLine($"{outputType.Name} {propertyData.OutputInstanceName};");
            entryPointFnBuilder.AddLine($"{propertyData.OutputInstanceName}.Alpha = {propertyData.InputInstanceName}.Alpha;");
            if (propertyData.OutputsAssignmentCallback != null)
                propertyData.OutputsAssignmentCallback(entryPointFnBuilder, propertyData);
            entryPointFnBuilder.AddLine($"return {propertyData.OutputInstanceName};");
            var entryPointFn = entryPointFnBuilder.Build();

            // Setup the block
            blockBuilder.AddType(inputType);
            blockBuilder.AddType(outputType);
            blockBuilder.SetEntryPointFunction(entryPointFn);
            return blockBuilder;
        }

        internal static Block CreateSimplePropertyBlock(ShaderContainer container, string blockName, PropertyDeclarationData propertyData)
        {
            return CreateSimplePropertyBlockBuilder(container, blockName, propertyData).Build();
        }
    }
}
