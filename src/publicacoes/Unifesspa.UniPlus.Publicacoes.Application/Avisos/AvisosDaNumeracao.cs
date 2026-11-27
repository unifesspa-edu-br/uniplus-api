namespace Unifesspa.UniPlus.Publicacoes.Application.Avisos;

using System.Globalization;
using System.Linq;

using Unifesspa.UniPlus.Publicacoes.Application.DTOs;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

/// <summary>
/// Os avisos da numeração de um ato — diagnóstico do estado atual, derivado na leitura, nunca
/// persistido. Compartilhado entre o registro e o detalhe; o conjunto <c>excluir</c> tira do
/// diagnóstico os atos que não são colisão: o próprio ato (no detalhe) e o ato que este retifica
/// (uma republicação com o mesmo número é a mesma linhagem, não uma duplicata — ADR-0103).
/// </summary>
internal static class AvisosDaNumeracao
{
    /// <summary>Código do aviso de número duplicado.</summary>
    public const string AvisoNumeroDuplicado = "NumeroDuplicado";

    public static async Task<IReadOnlyList<AvisoNumeracao>> CalcularAsync(
        IAtoNormativoRepository atosRepository,
        AtoNormativo ato,
        IReadOnlyCollection<Guid> excluir,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(atosRepository);
        ArgumentNullException.ThrowIfNull(ato);
        ArgumentNullException.ThrowIfNull(excluir);

        if (ato.Numero is null)
        {
            return [];
        }

        Guid[] conflitantes = await ConflitantesAsync(atosRepository, ato, excluir, cancellationToken)
            .ConfigureAwait(false);

        return conflitantes.Length == 0 ? [] : [NumeroDuplicado(ato, conflitantes)];
    }

    public static AvisoNumeracao NumeroDuplicado(AtoNormativo ato, IReadOnlyList<Guid> conflitantes)
    {
        string mensagem = string.Format(
            CultureInfo.InvariantCulture,
            "Número {0} {1}/{2} do órgão {3} já consta em {4} outro(s) ato(s).",
            ato.Serie,
            ato.Numero,
            ato.Ano,
            ato.Orgao,
            conflitantes.Count);

        return new AvisoNumeracao(AvisoNumeroDuplicado, mensagem, conflitantes);
    }

    /// <summary>
    /// Os atos com a mesma numeração, fora da própria linhagem: republicar na mesma cadeia de
    /// retificação não é colisão.
    /// </summary>
    private static async Task<Guid[]> ConflitantesAsync(
        IAtoNormativoRepository atosRepository,
        AtoNormativo ato,
        IReadOnlyCollection<Guid> excluir,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> comMesmaNumeracao = await atosRepository
            .ListarIdsComMesmaNumeracaoAsync(ato.Numeracao, cancellationToken)
            .ConfigureAwait(false);

        return [.. comMesmaNumeracao.Except(excluir)];
    }
}
