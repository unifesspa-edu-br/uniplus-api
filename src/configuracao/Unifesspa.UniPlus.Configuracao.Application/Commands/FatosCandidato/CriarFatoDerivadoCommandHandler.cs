namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

public static class CriarFatoDerivadoCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarFatoDerivadoCommand command,
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
    private static Result<FatoCandidato> Montar(CriarFatoDerivadoCommand command) =>
        CriacaoDoFato.CriarDerivadoDoAdministrador(
            new DescritivoDoFato(command.Codigo, command.Nome, command.Descricao, TokensDoFato.Componente(command.ComponenteExibicao)),
            new DefinicaoDoFato(
                TokensDoFato.Dominio(command.Dominio),
                Cardinalidade: null,
                FonteValores: null,
                Formato: null,
                command.PontoResolucao,
                TokensDoFato.Escopo(command.Escopo) ?? EscopoFato.Nenhum,
                TokensDoFato.Classificacao(command.ClassificacaoProtecao),
                command.FinalidadeTratamento,
                TokensDoFato.HipoteseLegal(command.HipoteseLegal) ?? HipoteseLegalTratamento.Nenhuma));
}
