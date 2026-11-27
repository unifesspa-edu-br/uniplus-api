namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Modalidades;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class ObterModalidadePorIdQueryHandler
{
    public static async Task<Result<ModalidadeDto>> Handle(
        ObterModalidadePorIdQuery query,
        IModalidadeRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        Modalidade? modalidade = await repository
            .ObterPorIdParaLeituraAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        return modalidade is null
            ? Result<ModalidadeDto>.Failure(new DomainError(ModalidadeErrorCodes.NaoEncontrada, "Modalidade não encontrada."))
            : Result<ModalidadeDto>.Success(modalidade.ToDto());
    }
}
