namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Results;

public static class CriarFatoAgregadoCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarFatoAgregadoCommand command,
        IFatoCandidatoRepository repository,
        IPrecedenciaFaseRepository precedenciaRepository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(precedenciaRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        // Token desconhecido vira o sentinela do enum, que o agregado recusa com o erro do campo.
        _ = ClassificacoesProtecaoDado.TryAnalisar(command.ClassificacaoProtecao, out ClassificacaoProtecaoDado classificacao);
        _ = HipotesesLegaisTratamento.TryAnalisar(command.HipoteseLegal, out HipoteseLegalTratamento hipotese);

        IReadOnlyList<FatoCandidato> fatos = await repository.ListarTodosAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<PrecedenciaFase> precedencias = await precedenciaRepository.ListarVivasAsync(cancellationToken).ConfigureAwait(false);
        Result<FatoCandidato> criar = FatoCandidato.CriarAgregadoDoAdministrador(
            command.Codigo, command.Nome, command.Descricao, command.FatoDeMembro, new CatalogoDeFatos(fatos, precedencias),
            command.PontoResolucao, classificacao, command.FinalidadeTratamento, hipotese);
        if (criar.IsFailure)
        {
            return Result<Guid>.ValidationFailure(criar.Errors);
        }

        return await NovoFato.GravarAsync(criar.Value!, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }
}
