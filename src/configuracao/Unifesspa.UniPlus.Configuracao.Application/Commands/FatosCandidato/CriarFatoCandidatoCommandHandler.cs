namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Valida o fato por inteiro primeiro (sem I/O), com as violações acumuladas (ADR-0125), e só então
/// o grava (<see cref="NovoFato"/>).
/// </summary>
public static class CriarFatoCandidatoCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarFatoCandidatoCommand command,
        IFatoCandidatoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        // Ausente é "não informado" e token desconhecido é "fora do vocabulário": o agregado recusa cada um no campo.
        Result<FatoCandidato> criar = FatoCandidato.CriarDoAdministrador(
            command.Codigo, command.Nome, command.Descricao,
            TokensDoFato.Dominio(command.Dominio) ?? DominioFato.Nenhum,
            TokensDoFato.Cardinalidade(command.Cardinalidade) ?? CardinalidadeFato.Nenhuma,
            TokensDoFato.Fonte(command.FonteValores),
            TokensDoFato.Formato(command.Formato),
            command.PontoResolucao,
            TokensDoFato.Escopo(command.Escopo) ?? EscopoFato.Nenhum,
            TokensDoFato.Classificacao(command.ClassificacaoProtecao) ?? ClassificacaoDeProtecao.Nenhuma,
            command.FinalidadeTratamento,
            TokensDoFato.HipoteseLegal(command.HipoteseLegal) ?? HipoteseLegalTratamento.Nenhuma);
        if (criar.IsFailure)
        {
            return Result<Guid>.ValidationFailure(criar.Errors);
        }

        return await NovoFato.GravarAsync(criar.Value!, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }
}
