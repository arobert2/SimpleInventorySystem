using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Text.Json;

public class JsonTypeHandler<T> : SqlMapper.TypeHandler<T>
{
    public override T? Parse(object value)
    {
        var json = value?.ToString();
        return json == null ? default : JsonSerializer.Deserialize<T>(json);
    }

    public override void SetValue(IDbDataParameter parameter, T? value)
    {
        parameter.Value = JsonSerializer.Serialize(value);
    }
}
