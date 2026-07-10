namespace Unifesspa.UniPlus.Publicacoes.Application.Avisos;

using System.Globalization;

using Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;
using Unifesspa.UniPlus.Publicacoes.Application.DTOs;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

/// <summary>
/// Computa o aviso de número duplicado (AC4) de um ato — diagnóstico do estado
/// atual, derivado na leitura, nunca persistido. Compartilhado entre o registro
/// (exclui nada: o próprio ato ainda não existe) e o detalhe (exclui o próprio
/// ato do conjunto de conflitantes).
/// </summary>
internal static class AvisoNumeracaoCalculator
{
    public static async Task<IReadOnlyList<AvisoNumeracao>> CalcularAsync(
        IAtoNormativoRepository atosRepository,
        AtoNormativo ato,
        Guid? excluirId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(atosRepository);
        ArgumentNullException.ThrowIfNull(ato);

        if (ato.Numero is null)
        {
            return [];
        }

        IReadOnlyList<Guid> conflitantes = await atosRepository
            .ListarIdsComMesmaNumeracaoAsync(
                ato.Orgao, ato.Serie, ato.Ano, ato.Numero, excluirId, cancellationToken)
            .ConfigureAwait(false);

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
