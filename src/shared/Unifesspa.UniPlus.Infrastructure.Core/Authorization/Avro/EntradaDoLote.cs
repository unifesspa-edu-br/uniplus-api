namespace unifesspa.uniplus.autorizacao.registrodeacesso;

using global::Avro;
using global::Avro.Specific;

using Unifesspa.UniPlus.Infrastructure.Core.Authorization.Avro;

/// <summary>Uma entrada do lote: o registro de acesso e a chave pela qual o consumidor deduplica.</summary>
public sealed class EntradaDoLote : ISpecificRecord
{
    /// <summary>Resumo SHA-256 do acesso, em hexadecimal minúsculo.</summary>
    public string Chave { get; set; } = string.Empty;

    /// <summary>O registro do acesso.</summary>
    public RegistroDeAcesso Registro { get; set; } = new();

    /// <inheritdoc />
    public Schema Schema => EsquemaDoRegistroDeAcesso.Entrada;

    /// <inheritdoc />
    public object Get(int fieldPos) => fieldPos switch
    {
        0 => Chave,
        1 => Registro,
        _ => throw new AvroRuntimeException($"Posição de campo inválida em EntradaDoLote.Get: {fieldPos}"),
    };

    /// <inheritdoc />
    public void Put(int fieldPos, object fieldValue)
    {
        switch (fieldPos)
        {
            case 0:
                Chave = (string)fieldValue;
                break;
            case 1:
                Registro = (RegistroDeAcesso)fieldValue;
                break;
            default:
                throw new AvroRuntimeException($"Posição de campo inválida em EntradaDoLote.Put: {fieldPos}");
        }
    }
}
