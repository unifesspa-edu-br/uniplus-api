namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// Uma chave do armazenamento a apagar a partir de <see cref="ApagarAPartirDe"/>. Guarda só a chave, que
/// leva o id do arquivo e nenhum dado pessoal.
/// </summary>
public sealed class ArquivoAApagar : EntityBase
{
    public string ObjectKey { get; private set; } = string.Empty;

    public DateTimeOffset ApagarAPartirDe { get; private set; }

    private ArquivoAApagar() { }

    public static ArquivoAApagar Agendar(string objectKey, DateTimeOffset limparApos)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        return new ArquivoAApagar { ObjectKey = objectKey, ApagarAPartirDe = limparApos };
    }
}
