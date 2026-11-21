namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposEtapa;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;
using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Valida os campos e os conceitos admitidos, acumulando no mesmo lote, antes de buscar o tipo: a violação
/// de campo vence o "não encontrado", e um payload mal formado não consulta o repositório do tipo (ADR-0125,
/// item 5). Por isso a recusa que depende do tipo só aparece quando os campos estão válidos.
/// </summary>
public static class AtualizarTipoEtapaCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarTipoEtapaCommand command,
        ITipoEtapaRepository repository,
        ITipoConceitoRepository conceitos,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(conceitos);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<IReadOnlyList<TipoConceito>> admitidos = await ConferirCampos(command, conceitos, cancellationToken).ConfigureAwait(false);
        return admitidos.IsFailure
            ? Result.ValidationFailure(admitidos.Errors)
            : await AtualizarSeExistir(command, admitidos.Value!, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> AtualizarSeExistir(
        AtualizarTipoEtapaCommand command,
        IReadOnlyList<TipoConceito> admitidos,
        ITipoEtapaRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        TipoEtapa? tipo = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        return tipo is null
            ? Result.Failure(new DomainError(TipoEtapaErrorCodes.NaoEncontrado, "Tipo de etapa não encontrado."))
            : await Aplicar(tipo, command, admitidos, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    // Os conceitos resolvidos, ou todas as violações dos campos e dos conceitos no mesmo lote.
    private static async Task<Result<IReadOnlyList<TipoConceito>>> ConferirCampos(
        AtualizarTipoEtapaCommand command, ITipoConceitoRepository conceitos, CancellationToken cancellationToken)
    {
        AvaliacaoDasEtapasInput avaliacao = command.Avaliacao ?? AvaliacaoDasEtapasInput.Ausente;
        Result<CamposEditaveisDoTipoDeEtapa> campos = TipoEtapa.ValidarCamposEditaveis(
            command.Nome, command.Descricao, avaliacao.AdmitePontuacao, avaliacao.AdmiteEliminacao, avaliacao.ExigeCorrecaoIndependente);
        Result<IReadOnlyList<TipoConceito>> admitidos = await ResolucaoDosConceitosAdmitidos
            .Resolver(avaliacao.ConceitosAdmitidos, conceitos, cancellationToken).ConfigureAwait(false);
        FieldError[] erros = CaminhoDaAvaliacao.Aplicar([.. campos.Errors, .. admitidos.Errors, .. ViolacoesDosConceitos(avaliacao, admitidos)]);
        return erros.Length > 0 ? Result<IReadOnlyList<TipoConceito>>.ValidationFailure(erros) : admitidos;
    }

    private static IReadOnlyList<FieldError> ViolacoesDosConceitos(
        AvaliacaoDasEtapasInput avaliacao, Result<IReadOnlyList<TipoConceito>> admitidos) =>
        admitidos.IsSuccess ? TipoEtapa.ViolacoesDaCorrecaoSemConceito(avaliacao.ExigeCorrecaoIndependente, admitidos.Value!) : [];

    private static async Task<Result> Aplicar(
        TipoEtapa tipo,
        AtualizarTipoEtapaCommand command,
        IReadOnlyList<TipoConceito> admitidos,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        // Com os campos já confirmados, só a regra que depende do próprio tipo pode recusar
        // aqui, e o tipo recusa antes de mutar.
        AvaliacaoDasEtapasInput avaliacao = command.Avaliacao ?? AvaliacaoDasEtapasInput.Ausente;
        Result atualizar = tipo.Atualizar(
            command.Nome, command.Descricao, avaliacao.AdmitePontuacao, avaliacao.AdmiteEliminacao, avaliacao.ExigeCorrecaoIndependente);
        if (atualizar.IsFailure)
        {
            return atualizar;
        }

        tipo.SubstituirConceitosAdmitidos(admitidos);
        return await Salvar(unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    // Endpoint com [RequiresIdempotencyKey]: o conflito do xmin não propaga (ADR-0119). O descarte
    // impede que o SaveChangesAsync do outbox reencontre a substituição da junção depois do retorno.
    private static async Task<Result> Salvar(IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex) || ColisaoDeSubstituicaoConcorrente.Ocorreu(ex))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(new DomainError(
                TipoEtapaErrorCodes.ConflitoDeConcorrencia,
                "Este tipo de etapa foi alterado concorrentemente. Tente novamente."));
        }
    }
}
