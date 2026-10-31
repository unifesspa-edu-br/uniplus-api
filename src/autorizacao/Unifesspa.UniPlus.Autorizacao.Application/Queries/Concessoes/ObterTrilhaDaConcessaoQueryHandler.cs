namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Concessoes;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler da trilha: monta, na leitura, os eventos da concessão, os que a origem dela
/// acrescenta pela porta <see cref="IEventosDaOrigem"/> e as decisões das revisões periódicas.
/// </summary>
public static class ObterTrilhaDaConcessaoQueryHandler
{
    public static async Task<Result<IReadOnlyList<EventoDaTrilhaDto>>> Handle(
        ObterTrilhaDaConcessaoQuery query,
        IConcessaoRepository repository,
        IRevisaoDeAcessosRepository revisoes,
        IEnumerable<IEventosDaOrigem> origens,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(revisoes);
        ArgumentNullException.ThrowIfNull(origens);

        ConcessaoComPerfil? item = await repository.ObterComPerfilAsync(query.Id, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return Result<IReadOnlyList<EventoDaTrilhaDto>>.Failure(ConcessaoHandlerErros.NaoEncontrada(query.Id));
        }

        Concessao concessao = item.Concessao;
        List<EventoDaTrilhaDto> eventos = [.. ConcessaoMapping.EventosDaConcessao(concessao)];

        IEventosDaOrigem? origem = origens.FirstOrDefault(o => o.Tipo == concessao.OrigemTipo);
        if (origem is not null)
        {
            eventos.AddRange(await origem.ObterAsync(concessao.OrigemId, cancellationToken).ConfigureAwait(false) ?? []);
        }

        // A decisão da revisão é lida dos itens, e não de uma trilha à parte.
        eventos.AddRange((await revisoes.ListarDecisoesDaConcessaoAsync(concessao.Id, cancellationToken).ConfigureAwait(false))
            .Select(RevisaoMapping.ToEvento));

        IReadOnlyList<EventoDaTrilhaDto> ordenados = [.. eventos.OrderBy(e => e.Instante)];

        return Result<IReadOnlyList<EventoDaTrilhaDto>>.Success(ordenados);
    }
}
