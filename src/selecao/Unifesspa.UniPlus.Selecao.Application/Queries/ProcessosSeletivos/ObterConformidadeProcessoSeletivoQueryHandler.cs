namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using DTOs;
using Domain.Entities;
using Domain.Interfaces;

/// <summary>
/// Handler da <see cref="ObterConformidadeProcessoSeletivoQuery"/>: leitura
/// pura (sem side effects) que avalia a presença das dimensões estruturalmente
/// obrigatórias do agregado — não confundir com a conformidade de
/// <c>ObrigatoriedadeLegal</c> (Story #460/#461), que avalia regras
/// legais configuráveis contra o <c>Edital</c> legado.
/// </summary>
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

        ItemConformidadeDto[] itens =
        [
            new ItemConformidadeDto("Etapas", processo.Etapas.Count > 0),
            new ItemConformidadeDto("Atendimento especializado", processo.OfertaAtendimento is not null),
            new ItemConformidadeDto("Distribuição de vagas", processo.DistribuicaoVagas.Count > 0),
        ];

        return new ConformidadeProcessoSeletivoDto(processo.Id, itens);
    }
}
