namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
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
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<FatoCandidato> criar = Montar(command);
        return criar.IsFailure
            ? Result<Guid>.ValidationFailure(criar.Errors)
            : await NovoFato.GravarAsync(criar.Value!, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    // Ausente é "não informado" e token desconhecido é "fora do vocabulário": o agregado recusa cada um no campo.
    private static Result<FatoCandidato> Montar(CriarFatoCandidatoCommand command) =>
        CriacaoDoFato.CriarDoAdministrador(
            new DescritivoDoFato(command.Codigo, command.Nome, command.Descricao, TokensDoFato.Componente(command.ComponenteExibicao)),
            new DefinicaoDoFato(
                TokensDoFato.Dominio(command.Dominio),
                TokensDoFato.Cardinalidade(command.Cardinalidade),
                TokensDoFato.Fonte(command.FonteValores),
                TokensDoFato.Formato(command.Formato),
                command.PontoResolucao,
                TokensDoFato.Escopo(command.Escopo),
                TokensDoFato.Classificacao(command.ClassificacaoProtecao),
                command.FinalidadeTratamento,
                TokensDoFato.HipoteseLegal(command.HipoteseLegal)));
}
