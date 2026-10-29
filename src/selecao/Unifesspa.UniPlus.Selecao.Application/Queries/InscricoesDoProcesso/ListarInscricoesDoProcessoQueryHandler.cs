namespace Unifesspa.UniPlus.Selecao.Application.Queries.InscricoesDoProcesso;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A consulta de quem opera o processo vê o CPF só mascarado. A permissão de consultar as inscrições já foi decidida na
/// borda, na unidade dona do processo; este handler não decide acesso e lê só o nome, o CPF e o instante do envio.
/// </summary>
public static class ListarInscricoesDoProcessoQueryHandler
{
    public static async Task<ListarInscricoesDoProcessoResult> Handle(
        ListarInscricoesDoProcessoQuery query,
        IInscricaoRepository inscricaoRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);

        (IReadOnlyList<ResumoDaInscricaoDoProcesso> inscricoes, Guid? anteriorAfterId, Guid? proximoAfterId) = await inscricaoRepository
            .ListarEnviadasDoProcessoAsync(query.ProcessoSeletivoId, query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        InscricaoDoProcessoDto[] items = [.. inscricoes.Select(static i => new InscricaoDoProcessoDto(
            i.Id, i.NumeroInscricao, i.Situacao, i.Nome, i.Cpf.Mascarado, i.SubmetidaEm))];

        return new ListarInscricoesDoProcessoResult(items, anteriorAfterId, proximoAfterId);
    }
}
