namespace Unifesspa.UniPlus.Configuracao.Application.Queries.CondicoesAtendimento;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class ObterCondicaoAtendimentoPorIdQueryHandler
{
    public static async Task<Result<CondicaoAtendimentoDto>> Handle(
        ObterCondicaoAtendimentoPorIdQuery query,
        ICondicaoAtendimentoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        CondicaoAtendimentoEspecializado? condicao = await repository
            .ObterPorIdParaLeituraAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        return condicao is null
            ? Result<CondicaoAtendimentoDto>.Failure(new DomainError(CondicaoAtendimentoErrorCodes.NaoEncontrada, "Condição de atendimento não encontrada."))
            : Result<CondicaoAtendimentoDto>.Success(condicao.ToDto());
    }
}
