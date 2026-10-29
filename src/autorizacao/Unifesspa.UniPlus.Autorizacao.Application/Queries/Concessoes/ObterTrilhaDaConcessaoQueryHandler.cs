namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Concessoes;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler da trilha: monta, na leitura, os eventos da concessão e os que a origem dela
/// acrescenta pela porta <see cref="IEventosDaOrigem"/>.
/// </summary>
public static class ObterTrilhaDaConcessaoQueryHandler
{
    public static async Task<Result<IReadOnlyList<EventoDaTrilhaDto>>> Handle(
        ObterTrilhaDaConcessaoQuery query,
        IConcessaoRepository repository,
        IEnumerable<IEventosDaOrigem> origens,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
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

        IReadOnlyList<EventoDaTrilhaDto> ordenados = [.. eventos.OrderBy(e => e.Instante)];

        return Result<IReadOnlyList<EventoDaTrilhaDto>>.Success(ordenados);
    }
}
