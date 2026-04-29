using System.Data.Common;

namespace DocumentManagement.Infrastructure.Data;

public static class DbCommandExtensions
{
    public static DbParameter AddParameter(
        this DbCommand command,
        string name,
        object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name.StartsWith('@') ? name : "@" + name;
        parameter.Value = value ?? DBNull.Value;

        command.Parameters.Add(parameter);

        return parameter;
    }
}
