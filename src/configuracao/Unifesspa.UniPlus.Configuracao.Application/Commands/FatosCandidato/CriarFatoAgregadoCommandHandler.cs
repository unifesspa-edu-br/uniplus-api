namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

public static class CriarFatoAgregadoCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarFatoAgregadoCommand command,
        IFatoCandidatoRepository repository,
        IPrecedenciaFaseRepository precedenciaRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(precedenciaRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        IReadOnlyList<FatoCandidato> fatos = await repository.ListarTodosAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<PrecedenciaFase> precedencias = await precedenciaRepository.ListarVivasAsync(cancellationToken).ConfigureAwait(false);
        Result<FatoCandidato> criar = Montar(command, new CatalogoDeFatos(fatos, precedencias));
        if (criar.IsFailure)
        {
            return Result<Guid>.ValidationFailure(criar.Errors);
        }

        return await NovoFato.GravarAsync(criar.Value!, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    // O agregado infere o domínio, a cardinalidade, a fonte e o escopo do fato de membro: a definição traz só o resto.
    private static Result<FatoCandidato> Montar(CriarFatoAgregadoCommand command, CatalogoDeFatos catalogo) =>
        CriacaoDoFato.CriarAgregadoDoAdministrador(
            new DescritivoDoFato(command.Codigo, command.Nome, command.Descricao, TokensDoFato.Componente(command.ComponenteExibicao)),
            command.FatoDeMembro,
            catalogo,
            new DefinicaoDoFato(
                Dominio: null,
                Cardinalidade: null,
                FonteValores: null,
                Formato: null,
                command.PontoResolucao,
                Escopo: null,
                TokensDoFato.Classificacao(command.ClassificacaoProtecao),
                command.FinalidadeTratamento,
                TokensDoFato.HipoteseLegal(command.HipoteseLegal) ?? HipoteseLegalTratamento.Nenhuma));
}
