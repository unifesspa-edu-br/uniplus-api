// O namespace não é PascalCase porque deve casar com o `namespace` do esquema Avro
// (`Contracts/Schemas/LoteDoRegistroDeAcesso.avsc`, em Authorization): o Apache.Avro instancia
// o tipo por reflexão, a partir de <namespace>.<nome>.
namespace unifesspa.uniplus.autorizacao.registrodeacesso;

using global::Avro;
using global::Avro.Specific;

using Unifesspa.UniPlus.Infrastructure.Core.Authorization.Avro;

/// <summary>
/// Valor da mensagem do registro de acesso: o lote de entradas de uma gravação. Escrita à mão, e não gerada, como os
/// demais eventos Avro (ADR-0051); nome e namespace são os do esquema.
/// </summary>
public sealed class LoteDoRegistroDeAcesso : ISpecificRecord
{
    /// <summary>Entradas do lote, na ordem em que foram decididas.</summary>
    public IList<EntradaDoLote> Registros { get; set; } = [];

    /// <inheritdoc />
    public Schema Schema => EsquemaDoRegistroDeAcesso.Lote;

    /// <inheritdoc />
    public object Get(int fieldPos) => fieldPos == 0
        ? Registros
        : throw new AvroRuntimeException($"Posição de campo inválida em LoteDoRegistroDeAcesso.Get: {fieldPos}");

    /// <inheritdoc />
    public void Put(int fieldPos, object fieldValue)
    {
        if (fieldPos != 0)
        {
            throw new AvroRuntimeException($"Posição de campo inválida em LoteDoRegistroDeAcesso.Put: {fieldPos}");
        }

        Registros = (IList<EntradaDoLote>)fieldValue;
    }
}
