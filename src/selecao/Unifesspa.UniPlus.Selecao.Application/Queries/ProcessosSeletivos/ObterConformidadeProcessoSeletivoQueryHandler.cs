namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

/// <summary>
/// Handler da <see cref="ObterConformidadeProcessoSeletivoQuery"/>: leitura
/// pura (sem side effects) que mapeia <see cref="ProcessoSeletivo.AvaliarConformidade"/>
/// para o DTO público — não confundir com a conformidade de
/// <c>ObrigatoriedadeLegal</c> (Stories #852/#853), que avalia regras
/// legais configuráveis aplicáveis ao processo.
/// </summary>
/// <remarks>
/// O checklist em si vive em <c>ProcessoSeletivo.AvaliarConformidade()</c> (Domain) — bicondicional
/// com os SEIS gates estruturais que <c>Publicar</c>/<c>Retificar</c> aplicam (issue #1092), não
/// só o primeiro. Este handler apenas mapeia; a fonte única evita que a leitura pública e os gates
/// de publicação divirjam.
/// </remarks>
public static class ObterConformidadeProcessoSeletivoQueryHandler
{
    public static async Task<ConformidadeProcessoSeletivoDto?> Handle(
        ObterConformidadeProcessoSeletivoQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterComConfiguracaoAsync(query.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return null;
        }

        ItemConformidadeDto[] itens = [.. processo.AvaliarConformidade()
            .Select(static item => new ItemConformidadeDto(item.Codigo, item.Dimensao, item.Mensagem, item.Ok))];

        return new ConformidadeProcessoSeletivoDto(processo.Id, itens);
    }
}
