namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// Handler do <see cref="DefinirBonusRegionalCommand"/> (RN05, Story #774):
/// <c>RegraCodigo</c> nulo remove o bônus (toggle por ausência, INV-B5); caso
/// contrário resolve a regra <c>BONUS-MULTIPLICATIVO</c> no
/// <c>rol_de_regras</c> (<see cref="IRegraCatalogoReader"/>, Story #772).
/// </summary>
public static class DefinirBonusRegionalCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirBonusRegionalCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegraCatalogoReader regraCatalogoReader,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(regraCatalogoReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        // A precondição é conferida AQUI, logo depois do 404 e antes das regras de negócio
        // que este handler avalia (existência de cadastros, coerência de referências): ela
        // as precede na ordem da ADR-0110 D9. Um cliente com If-Match defasado tem de saber
        // disso antes de sair caçando um cadastro que ele não errou.
        //
        // O que ela NÃO precede é a validação de SCHEMA do payload: o FluentValidation roda
        // como middleware do Wolverine, antes deste handler, e um command malformado morre
        // ali com 422 sem que o guard chegue a rodar. É desvio consciente da D9 — corrigi-lo
        // exigiria carregar o agregado no middleware, o que é pior. O custo é uma rodada
        // extra para quem erra as DUAS coisas ao mesmo tempo; nenhum estado é corrompido.
        //
        // O mesmo guard continua dentro do Definir* do domínio: esta antecipação dá a ordem,
        // não a garantia.
        if (processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio)
        {
            return Result<MutacaoAceita>.Failure(bloqueio);
        }

        if (command.RegraCodigo is null)
        {
            Result removerResult = processo.DefinirBonusRegional(null, command.Precondicao);
            if (removerResult.IsFailure)
            {
                return Result<MutacaoAceita>.Failure(removerResult.Error!);
            }

            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
        }

        if (command.RegraVersao is null || command.Fator is null)
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
                "ConfiguracaoBonusRegional.CamposObrigatorios",
                "RegraVersao e Fator são obrigatórios quando RegraCodigo é informado."));
        }

        RegraCatalogo? regra = await regraCatalogoReader
            .ObterAsync(command.RegraCodigo, command.RegraVersao, cancellationToken)
            .ConfigureAwait(false);
        if (regra is null)
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
                "ConfiguracaoBonusRegional.RegraNaoEncontrada",
                $"Regra de bônus {command.RegraCodigo}/{command.RegraVersao} não encontrada no rol_de_regras."));
        }

        if (regra.Tipo != TipoRegra.RegraBonus)
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
                "ConfiguracaoBonusRegional.RegraTipoInvalido",
                $"A regra {command.RegraCodigo}/{command.RegraVersao} não é do tipo regra_bonus."));
        }

        Result<ReferenciaRegra> referenciaRegraResult = ReferenciaRegra.Criar(regra.Codigo, regra.Versao, regra.Hash);
        if (referenciaRegraResult.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(referenciaRegraResult.Error!);
        }

        Result<ConfiguracaoBonusRegional> bonusResult = ConfiguracaoBonusRegional.Criar(
            referenciaRegraResult.Value!, command.Fator.Value, command.Teto, command.MunicipioConvenio, command.BaseLegal);
        if (bonusResult.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(bonusResult.Error!);
        }

        Result definirResult = processo.DefinirBonusRegional(bonusResult.Value!, command.Precondicao);
        if (definirResult.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(definirResult.Error!);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }
}
