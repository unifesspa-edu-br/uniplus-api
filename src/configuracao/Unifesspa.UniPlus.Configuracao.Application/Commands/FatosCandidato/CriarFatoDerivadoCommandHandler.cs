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

        // Token desconhecido vira o sentinela do enum, que o agregado recusa com o erro do campo.
        _ = DominiosFato.TryAnalisar(command.Dominio, out DominioFato dominio);
        _ = EscoposFato.TryAnalisar(command.Escopo, out EscopoFato escopo);
        _ = ClassificacoesProtecaoDado.TryAnalisar(command.ClassificacaoProtecao, out ClassificacaoProtecaoDado classificacao);
        _ = HipotesesLegaisTratamento.TryAnalisar(command.HipoteseLegal, out HipoteseLegalTratamento hipotese);

        Result<FatoCandidato> criar = FatoCandidato.CriarDerivadoDoAdministrador(
            command.Codigo, command.Nome, command.Descricao, dominio, command.PontoResolucao, escopo, classificacao,
            command.FinalidadeTratamento, hipotese);
        if (criar.IsFailure)
        {
            return Result<Guid>.ValidationFailure(criar.Errors);
        }

        return await NovoFato.GravarAsync(criar.Value!, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }
}
