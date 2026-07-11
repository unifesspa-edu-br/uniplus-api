namespace Unifesspa.UniPlus.Publicacoes.Application.Avisos;

using System.Globalization;
using System.Linq;

using Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;
using Unifesspa.UniPlus.Publicacoes.Application.DTOs;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

/// <summary>
/// Computa o aviso de número duplicado (AC4) de um ato — diagnóstico do estado
/// atual, derivado na leitura, nunca persistido. Compartilhado entre o registro e
/// o detalhe; o conjunto <c>excluir</c> tira do diagnóstico os atos que não são
/// colisão: o próprio ato (no detalhe) e o ato que este retifica (uma
/// republicação com o mesmo número é a mesma linhagem, não uma duplicata — ADR-0103).
/// </summary>
internal static class AvisoNumeracaoCalculator
{
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

        IReadOnlyList<Guid> conflitantes = await atosRepository
            .ListarIdsComMesmaNumeracaoAsync(
                ato.Orgao, ato.Serie, ato.Ano, ato.Numero, excluirId: null, cancellationToken)
            .ConfigureAwait(false);

        if (excluir.Count > 0)
        {
            HashSet<Guid> excluirSet = [.. excluir];
            conflitantes = [.. conflitantes.Where(id => !excluirSet.Contains(id))];
        }

        if (conflitantes.Count == 0)
        {
            return [];
        }

        string mensagem = string.Format(
            CultureInfo.InvariantCulture,
            "Número {0} {1}/{2} do órgão {3} já consta em {4} outro(s) ato(s).",
            ato.Serie,
            ato.Numero,
            ato.Ano,
            ato.Orgao,
            conflitantes.Count);

        return [new AvisoNumeracao(AtoNormativoRegras.AvisoNumeroDuplicado, mensagem, conflitantes)];
    }
}
