using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WaaS.Persistence;

// A generic handler for any type T
public class JsonTypeHandler<T> : SqlMapper.TypeHandler<T>
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = new JsonLowercasePolicy(),
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(new JsonLowercasePolicy()) },
    };

    // Serialize to JSON when saving to DB
    public override void SetValue(IDbDataParameter parameter, T? value)
    {
        parameter.Value = (object)JsonSerializer.Serialize(value, _options) ?? DBNull.Value;
        parameter.DbType = DbType.Object; // Important for Npgsql to treat it as JSONB
        
        // Npgsql specific: Explicitly set NpgsqlDbType to Jsonb
        if (parameter is Npgsql.NpgsqlParameter npgsqlParam)
        {
            npgsqlParam.NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Jsonb;
        }
    }

    // Deserialize from JSON when reading from DB
    public override T? Parse(object value)
    {
        if (value is string json)
        {
            return JsonSerializer.Deserialize<T?>(json, _options);
        }
        return default;
    }
}