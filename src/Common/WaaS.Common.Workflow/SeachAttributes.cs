using Temporalio.Common;

namespace WaaS.Common.Workflow;

public static class SearchAttributes
{
    public static readonly SearchAttributeKey<string> Tenant = SearchAttributeKey.CreateKeyword("Tenant");
    public static readonly SearchAttributeKey<long> SystemInstanceId = SearchAttributeKey.CreateLong("SystemInstanceId");
    public static readonly SearchAttributeKey<long> StackInstanceId = SearchAttributeKey.CreateLong("StackInstanceId");
    public static readonly SearchAttributeKey<string> StateNamespace = SearchAttributeKey.CreateKeyword("StateNamespace");
}