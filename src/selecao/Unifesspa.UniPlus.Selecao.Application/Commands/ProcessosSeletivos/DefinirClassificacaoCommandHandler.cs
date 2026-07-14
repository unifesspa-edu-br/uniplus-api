namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;
using Kernel.Results;

/// <summary>
/// Handler do <see cref="DefinirClassificacaoCommand"/> (Story #775): resolve
/// cada regra no catálogo <c>rol_de_regras</c>
/// (<see cref="IRegraCatalogoReader"/>, Story #772), monta os args tipados de
/// cada regra de eliminação conforme o código e congela as referências — as
/// invariantes que dependem de outras dimensões do agregado (INV-B4, ENEM-only)
/// são garantidas pela raiz (<see cref="ProcessoSeletivo.DefinirClassificacao"/>).
/// </summary>
public static class DefinirClassificacaoCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirClassificacaoCommand command,
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

        Result<ReferenciaRegra> regraCalculoResult = await ResolverRegraAsync(
            command.RegraCalculoCodigo, command.RegraCalculoVersao, TipoRegra.RegraCalculo, regraCatalogoReader, cancellationToken)
            .ConfigureAwait(false);
        if (regraCalculoResult.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(regraCalculoResult.Error!);
        }

        ReferenciaRegra? regraArredondamento = null;
        if (command.RegraArredondamentoCodigo is not null)
        {
            if (command.RegraArredondamentoVersao is null)
            {
                return Result<MutacaoAceita>.Failure(new DomainError(
                    "ConfiguracaoClassificacao.RegraArredondamentoVersaoObrigatoria",
                    "Versão da regra de arredondamento é obrigatória quando o código é informado."));
            }

            Result<ReferenciaRegra> regraArredondamentoResult = await ResolverRegraAsync(
                command.RegraArredondamentoCodigo, command.RegraArredondamentoVersao, TipoRegra.RegraArredondamento, regraCatalogoReader, cancellationToken)
                .ConfigureAwait(false);
            if (regraArredondamentoResult.IsFailure)
            {
                return Result<MutacaoAceita>.Failure(regraArredondamentoResult.Error!);
            }

            regraArredondamento = regraArredondamentoResult.Value!;
        }

        Result<ReferenciaRegra> regraOrdemAlocacaoResult = await ResolverRegraAsync(
            command.RegraOrdemAlocacaoCodigo, command.RegraOrdemAlocacaoVersao, TipoRegra.RegraOrdemAlocacao, regraCatalogoReader, cancellationToken)
            .ConfigureAwait(false);
        if (regraOrdemAlocacaoResult.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(regraOrdemAlocacaoResult.Error!);
        }

        List<RegraEliminacao> regrasEliminacao = [];
        foreach (RegraEliminacaoInput input in command.RegrasEliminacao)
        {
            Result<RegraEliminacao> resultado = await ResolverEliminacaoAsync(input, regraCatalogoReader, cancellationToken)
                .ConfigureAwait(false);
            if (resultado.IsFailure)
            {
                return Result<MutacaoAceita>.Failure(resultado.Error!);
            }

            regrasEliminacao.Add(resultado.Value!);
        }

        Result<ConfiguracaoClassificacao> configuracaoResult = ConfiguracaoClassificacao.Criar(
            regraCalculoResult.Value!,
            regraArredondamento,
            command.CasasArredondamento,
            regraOrdemAlocacaoResult.Value!,
            command.NOpcoesAlocacao,
            regrasEliminacao);
        if (configuracaoResult.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(configuracaoResult.Error!);
        }

        Result result = processo.DefinirClassificacao(configuracaoResult.Value!, command.Precondicao);
        if (result.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(result.Error!);
        }

        // Agregado tracked: persistência por change detection (ValueGeneratedNever
        // nos filhos, ver lição da F0) — não chamar DbSet.Update.
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }

    private static async Task<Result<ReferenciaRegra>> ResolverRegraAsync(
        string codigo,
        string versao,
        TipoRegra tipoEsperado,
        IRegraCatalogoReader regraCatalogoReader,
        CancellationToken cancellationToken)
    {
        RegraCatalogo? regra = await regraCatalogoReader.ObterAsync(codigo, versao, cancellationToken).ConfigureAwait(false);
        if (regra is null)
        {
            return Result<ReferenciaRegra>.Failure(new DomainError(
                "ConfiguracaoClassificacao.RegraNaoEncontrada",
                $"Regra {codigo}/{versao} não encontrada no rol_de_regras."));
        }

        if (regra.Tipo != tipoEsperado)
        {
            return Result<ReferenciaRegra>.Failure(new DomainError(
                "ConfiguracaoClassificacao.RegraTipoInvalido",
                $"A regra {codigo}/{versao} não é do tipo esperado."));
        }

        return ReferenciaRegra.Criar(regra.Codigo, regra.Versao, regra.Hash);
    }

    private static async Task<Result<RegraEliminacao>> ResolverEliminacaoAsync(
        RegraEliminacaoInput input,
        IRegraCatalogoReader regraCatalogoReader,
        CancellationToken cancellationToken)
    {
        RegraCatalogo? regra = await regraCatalogoReader
            .ObterAsync(input.RegraCodigo, input.RegraVersao, cancellationToken)
            .ConfigureAwait(false);
        if (regra is null)
        {
            return Result<RegraEliminacao>.Failure(new DomainError(
                "RegraEliminacao.RegraNaoEncontrada",
                $"Regra de eliminação {input.RegraCodigo}/{input.RegraVersao} não encontrada no rol_de_regras."));
        }

        if (regra.Tipo != TipoRegra.RegraEliminacao)
        {
            return Result<RegraEliminacao>.Failure(new DomainError(
                "RegraEliminacao.RegraTipoInvalido",
                $"A regra {input.RegraCodigo}/{input.RegraVersao} não é do tipo regra_eliminacao."));
        }

        Result<ArgsRegraEliminacao> argsResult = MontarArgs(input);
        if (argsResult.IsFailure)
        {
            return Result<RegraEliminacao>.Failure(argsResult.Error!);
        }

        Result<ReferenciaRegra> referenciaRegraResult = ReferenciaRegra.Criar(regra.Codigo, regra.Versao, regra.Hash);
        if (referenciaRegraResult.IsFailure)
        {
            return Result<RegraEliminacao>.Failure(referenciaRegraResult.Error!);
        }

        return RegraEliminacao.Criar(referenciaRegraResult.Value!, argsResult.Value!);
    }

    private static Result<ArgsRegraEliminacao> MontarArgs(RegraEliminacaoInput input) =>
        input.RegraCodigo switch
        {
            RegraEliminacaoCodigo.ElimNotaMinimaEtapa => input.EtapaRef is { } etapaRef && input.NotaMinima is { } notaMinima && input.Minimo is null
                ? Result<ArgsRegraEliminacao>.Success(new ArgsElimNotaMinimaEtapa(etapaRef, notaMinima))
                : Result<ArgsRegraEliminacao>.Failure(new DomainError(
                    "RegraEliminacao.EtapaRefENotaMinimaObrigatorios",
                    $"EtapaRef e NotaMinima são obrigatórios (e Minimo não se aplica) para a regra {RegraEliminacaoCodigo.ElimNotaMinimaEtapa}.")),

            RegraEliminacaoCodigo.ElimCorteRedacao => input.Minimo is { } minimo && input.EtapaRef is null && input.NotaMinima is null
                ? Result<ArgsRegraEliminacao>.Success(new ArgsElimCorteRedacao(minimo))
                : Result<ArgsRegraEliminacao>.Failure(new DomainError(
                    "RegraEliminacao.MinimoObrigatorio",
                    $"Minimo é obrigatório (e EtapaRef/NotaMinima não se aplicam) para a regra {RegraEliminacaoCodigo.ElimCorteRedacao}.")),

            RegraEliminacaoCodigo.ElimZeroEmArea => input.EtapaRef is null && input.NotaMinima is null && input.Minimo is null
                ? Result<ArgsRegraEliminacao>.Success(new ArgsElimZeroEmArea())
                : Result<ArgsRegraEliminacao>.Failure(new DomainError(
                    "RegraEliminacao.ArgsIncompativeisComRegra",
                    $"A regra {RegraEliminacaoCodigo.ElimZeroEmArea} não aceita args (EtapaRef/NotaMinima/Minimo).")),

            _ => Result<ArgsRegraEliminacao>.Failure(new DomainError(
                "RegraEliminacao.RegraTipoInvalido",
                $"Código de regra de eliminação desconhecido: {input.RegraCodigo}.")),
        };
}
