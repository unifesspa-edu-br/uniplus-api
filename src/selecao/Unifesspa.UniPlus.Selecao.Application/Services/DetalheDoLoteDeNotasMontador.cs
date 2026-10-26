namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using DTOs.NotasDoEnem;

/// <summary>Monta o lote com as ocorrências e os ausentes, para a importação e para a consulta.</summary>
public static class DetalheDoLoteDeNotasMontador
{
    /// <summary>
    /// O lote com as ocorrências e os ausentes. Número, nome e CPF mascarado de todos os inscritos citados
    /// saem de uma leitura por inscrição, não de uma por ocorrência.
    /// </summary>
    public static async Task<LoteDeNotasDoEnemDto> MontarAsync(
        ILoteDeNotasDoEnemRepository lotes,
        LoteDeNotasDoEnem lote,
        bool vigente,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lotes);
        ArgumentNullException.ThrowIfNull(lote);

        IReadOnlyDictionary<Guid, IReadOnlyList<string>> ausentes = await lotes
            .ObterAusentesDoLoteAsync(lote.Id, cancellationToken)
            .ConfigureAwait(false);

        Guid[] inscricaoIds =
        [
            .. lote.Ocorrencias.Where(static o => o.InscricaoId is not null).Select(static o => o.InscricaoId!.Value)
                .Concat(ausentes.Keys)
                .Distinct(),
        ];

        IReadOnlyDictionary<Guid, InscritoDoLote> inscritos = await lotes.ObterInscritosAsync(inscricaoIds, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, IReadOnlyList<OpcaoDeCursoDoLoteDto>> opcoes = await NomesDasOfertasDoLote
            .ResolverAsync(lote.ProcessoSeletivoId, inscritos, processoSeletivoRepository, registroCodecs, cancellationToken)
            .ConfigureAwait(false);

        return LoteDeNotasDoEnemDto.De(lote, vigente, new DetalheDoLoteDeNotas(inscritos, ausentes, opcoes), ausentes.Count);
    }
}
