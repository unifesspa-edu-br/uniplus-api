namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class CriarFatoDerivadoCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarFatoDerivadoCommand command,
        IFatoCandidatoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        // Ausente é "não informado" e token desconhecido é "fora do vocabulário": o agregado recusa cada um no campo.
        Result<FatoCandidato> criar = FatoCandidato.CriarDerivadoDoAdministrador(
            command.Codigo, command.Nome, command.Descricao,
            TokensDoFato.Dominio(command.Dominio) ?? DominioFato.Nenhum,
            command.PontoResolucao,
            TokensDoFato.Escopo(command.Escopo) ?? EscopoFato.Nenhum,
            TokensDoFato.Classificacao(command.ClassificacaoProtecao) ?? ClassificacaoProtecaoDado.Nenhuma,
            command.FinalidadeTratamento,
            TokensDoFato.HipoteseLegal(command.HipoteseLegal) ?? HipoteseLegalTratamento.Nenhuma);
        if (criar.IsFailure)
        {
            return Result<Guid>.ValidationFailure(criar.Errors);
        }

        return await NovoFato.GravarAsync(criar.Value!, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }
}
