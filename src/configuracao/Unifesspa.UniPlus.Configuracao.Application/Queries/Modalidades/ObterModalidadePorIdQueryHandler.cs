namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Modalidades;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ObterModalidadePorIdQueryHandler
{
    public static async Task<ModalidadeDto?> Handle(
        ObterModalidadePorIdQuery query,
        IModalidadeRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        Modalidade? modalidade = await repository
            .ObterPorIdParaLeituraAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        return modalidade?.ToDto();
    }
}
