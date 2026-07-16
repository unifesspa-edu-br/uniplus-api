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
/// O checklist em si vive em <c>ProcessoSeletivo.AvaliarConformidade()</c>
/// (Domain) — reusado também pelo gate de <c>Publicar</c> (Story #759 CA-03),
/// para que a leitura pública e o gate de publicação nunca divirjam.
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
            .Select(static item => new ItemConformidadeDto(item.Item, item.Ok))];

        return new ConformidadeProcessoSeletivoDto(processo.Id, itens);
    }
}
