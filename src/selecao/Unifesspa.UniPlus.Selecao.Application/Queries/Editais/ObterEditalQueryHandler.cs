namespace Unifesspa.UniPlus.Selecao.Application.Queries.Editais;

using DTOs;
using Domain.Entities;
using Domain.Interfaces;

/// <summary>
/// Handler convention-based do <see cref="ObterEditalQuery"/>: leitura simples
/// pelo repositório, projetada em <see cref="EditalDto"/>. Retorna
/// <c>null</c> quando o edital não existe — o controller mapeia para 404.
/// </summary>
public static class ObterEditalQueryHandler
{
    public static async Task<EditalDto?> Handle(
        ObterEditalQuery query,
        IEditalRepository editalRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(editalRepository);

        Edital? edital = await editalRepository.ObterPorIdAsync(query.Id, cancellationToken).ConfigureAwait(false);
        if (edital is null)
        {
            return null;
        }

        return new EditalDto(
            edital.Id,
            edital.NumeroEdital.ToString(),
            edital.Titulo,
            edital.TipoProcesso.ToString(),
            edital.Status.ToString(),
            edital.MaximoOpcoesCurso,
            edital.BonusRegionalHabilitado,
            edital.CreatedAt);
    }
}
