using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using ClassicVm = WaaS.Space.Classic.ViewModel;

namespace WaaS.WebApi.OpenApi;

public sealed class SharedWebspaceOperationTransformer(JsonSerializerOptions jsonSerializerOptions) : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var hasSharedWebspaceBody = context.Description.ParameterDescriptions
            .Any(p => p.Type == typeof(ClassicVm.SharedWebspace));

        if (hasSharedWebspaceBody && operation.RequestBody?.Content != null)
        {
            if (operation.RequestBody.Content.TryGetValue("application/json", out var mediaType))
            {
                var exampleNode = ClassicWebspaceExamples.ToJsonNode(jsonSerializerOptions);
                mediaType.Example = exampleNode;

                mediaType.Examples = new Dictionary<string, IOpenApiExample>
                {
                    ["Seeded Desired State"] = new OpenApiExample
                    {
                        Summary = "Seeded Classic Webspace (demo / 1234567 / 5001234567)",
                        Description = "The exact desired state seeded in the database for the demo environment.",
                        Value = exampleNode
                    }
                };
            }
        }

        return Task.CompletedTask;
    }
}
