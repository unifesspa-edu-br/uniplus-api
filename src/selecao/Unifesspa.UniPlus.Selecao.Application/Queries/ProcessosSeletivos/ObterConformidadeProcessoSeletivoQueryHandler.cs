namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

/// <summary>
/// Handler da <see cref="ObterConformidadeProcessoSeletivoQuery"/>: leitura pura (sem side effects) que carrega o
/// processo e delega a avaliação do checklist a <see cref="AvaliacaoDaConformidade"/> — não confundir com a
/// conformidade de <c>ObrigatoriedadeLegal</c> (Stories #852/#853), que avalia regras legais configuráveis
/// aplicáveis ao processo.
/// </summary>
/// <remarks>
/// <b>Uma leitura por handler, não por requisição.</b> Este handler lê o calendário uma vez. Numa recusa de
/// publicação, o controller o consulta de novo para enriquecer o 422 com o checklist (issue #1096) — são dois
/// handlers, cada um com a sua leitura, e isso é correto: a invariante de leitura única é do handler, não do
/// endpoint.
/// </remarks>
public static class ObterConformidadeProcessoSeletivoQueryHandler
{
    public static async Task<ConformidadeProcessoSeletivoDto?> Handle(
        ObterConformidadeProcessoSeletivoQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        AvaliacaoDaConformidade avaliacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(avaliacao);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterComConfiguracaoAsync(query.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        return processo is null
            ? null
            : await avaliacao.AvaliarAsync(processo, cancellationToken).ConfigureAwait(false);
    }
}
