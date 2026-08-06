using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WaaS.Persistence;

public class JsonTypeHandler<T> : SqlMapper.TypeHandler<T>
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = new JsonLowercasePolicy(),
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(new JsonLowercasePolicy()) },
    };

    public override void SetValue(IDbDataParameter parameter, T? value)
    {
        parameter.Value = (object)JsonSerializer.Serialize(value, _options) ?? DBNull.Value;
        parameter.DbType = DbType.Object;
        
        if (parameter is NpgsqlParameter npgsqlParam)
        {
            npgsqlParam.NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Jsonb;
        }
    }

    public override T? Parse(object value)
    {
        if (value is string json)
        {
            return JsonSerializer.Deserialize<T?>(json, _options);
        }
        return default;
    }
}