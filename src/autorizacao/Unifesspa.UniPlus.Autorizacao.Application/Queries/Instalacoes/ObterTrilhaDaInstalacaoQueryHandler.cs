namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Instalacoes;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da trilha da instalação: os eventos são montados na leitura, sem tabela de eventos.</summary>
public static class ObterTrilhaDaInstalacaoQueryHandler
{
    public static async Task<Result<IReadOnlyList<EventoDaTrilhaDto>>> Handle(
        ObterTrilhaDaInstalacaoQuery query,
        IEnumerable<IEventosDaOrigem> origens,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(origens);

        IReadOnlyList<EventoDaTrilhaDto>? trilha = await origens.Single(o => o.Tipo == TipoDeOrigem.Instalacao).ObterAsync(query.Id, cancellationToken).ConfigureAwait(false);

        return trilha is null
            ? Result<IReadOnlyList<EventoDaTrilhaDto>>.Failure(InstalacaoHandlerErros.NaoEncontrada())
            : Result<IReadOnlyList<EventoDaTrilhaDto>>.Success(trilha);
    }
}
