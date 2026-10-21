namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// Uma chave do armazenamento a apagar a partir de <see cref="LimparApos"/>. Guarda só a chave, que
/// leva o id do arquivo e nenhum dado pessoal.
/// </summary>
public sealed class ChaveALimpar : EntityBase
{
    public string ObjectKey { get; private set; } = string.Empty;

    public DateTimeOffset LimparApos { get; private set; }

    private ChaveALimpar() { }

    public static ChaveALimpar Agendar(string objectKey, DateTimeOffset limparApos)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        return new ChaveALimpar { ObjectKey = objectKey, LimparApos = limparApos };
    }
}
