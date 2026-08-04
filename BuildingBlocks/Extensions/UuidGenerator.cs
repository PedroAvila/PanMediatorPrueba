namespace BuildingBlocks.Extensions;

public static class UuidGenerator
{
    /// <summary>
    /// Genera directamente un UUID en formato binario para MySQL BINARY(16)
    /// </summary>
    public static byte[] GenerateBinaryUuid()
    {
        return Guid.NewGuid().ToByteArray();
    }

    /// <summary>
    /// Convierte un Guid a formato binario para MySQL
    /// </summary>
    public static byte[] ToDatabaseUuid(this Guid guid)
    {
        return guid.ToByteArray();
    }

    /// <summary>
    /// Convierte bytes de MySQL a Guid
    /// </summary>
    public static Guid FromDatabaseUuid(byte[] bytes)
    {
        return new Guid(bytes);
    }
}
