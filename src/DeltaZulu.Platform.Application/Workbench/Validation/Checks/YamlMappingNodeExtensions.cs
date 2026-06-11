using YamlDotNet.RepresentationModel;

namespace DeltaZulu.Platform.Application.Workbench.Validation.Checks;

internal static class YamlMappingNodeExtensions
{
    internal static bool ContainsScalarKey(this YamlMappingNode mapping, string key)
        => mapping.Children.Keys
            .OfType<YamlScalarNode>()
            .Any(node => string.Equals(node.Value, key, StringComparison.OrdinalIgnoreCase));

    internal static bool TryGetChild(this YamlMappingNode mapping, string key, out YamlNode node)
    {
        foreach (var child in mapping.Children)
        {
            if (child.Key is YamlScalarNode { Value: { } value } &&
                string.Equals(value, key, StringComparison.OrdinalIgnoreCase))
            {
                node = child.Value;
                return true;
            }
        }

        node = new YamlScalarNode(string.Empty);
        return false;
    }

    internal static bool TryGetScalar(this YamlMappingNode mapping, string key, out string value)
    {
        if (mapping.TryGetChild(key, out var node) && node is YamlScalarNode { Value: { } scalarValue })
        {
            value = scalarValue;
            return true;
        }

        value = string.Empty;
        return false;
    }
}