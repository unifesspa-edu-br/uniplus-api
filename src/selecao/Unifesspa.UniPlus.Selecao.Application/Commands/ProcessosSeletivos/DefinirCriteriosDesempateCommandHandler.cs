namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;
using Kernel.Results;

/// <summary>
/// Handler do <see cref="DefinirCriteriosDesempateCommand"/> (Story #774):
/// resolve cada critério no catálogo <c>rol_de_regras</c>
/// (<see cref="IRegraCatalogoReader"/>, Story #772), monta os args tipados
/// conforme o código da regra e congela a referência — a existência do
/// <c>etapa_ref</c> no processo (INV-B6) é garantida pela raiz
/// (<see cref="ProcessoSeletivo.DefinirCriteriosDesempate"/>).
/// </summary>
public static class DefinirCriteriosDesempateCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirCriteriosDesempateCommand command,
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

        List<CriterioDesempate> criterios = [];
        foreach (CriterioDesempateInput input in command.Criterios)
        {
            Result<CriterioDesempate> resultado = await ResolverCriterioAsync(input, regraCatalogoReader, cancellationToken)
                .ConfigureAwait(false);
            if (resultado.IsFailure)
            {
                return Result<MutacaoAceita>.Failure(resultado.Error!);
            }

            criterios.Add(resultado.Value!);
        }

        Result result = processo.DefinirCriteriosDesempate(criterios, command.Precondicao);
        if (result.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(result.Error!);
        }

        // Agregado tracked: persistência por change detection (ValueGeneratedNever
        // nos filhos, ver lição da F0) — não chamar DbSet.Update.
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }

    private static async Task<Result<CriterioDesempate>> ResolverCriterioAsync(
        CriterioDesempateInput input,
        IRegraCatalogoReader regraCatalogoReader,
        CancellationToken cancellationToken)
    {
        RegraCatalogo? regra = await regraCatalogoReader
            .ObterAsync(input.RegraCodigo, input.RegraVersao, cancellationToken)
            .ConfigureAwait(false);
        if (regra is null)
        {
            return Result<CriterioDesempate>.Failure(new DomainError(
                "CriterioDesempate.RegraNaoEncontrada",
                $"Regra de desempate {input.RegraCodigo}/{input.RegraVersao} não encontrada no rol_de_regras."));
        }

        if (regra.Tipo != TipoRegra.CriterioDesempate)
        {
            return Result<CriterioDesempate>.Failure(new DomainError(
                "CriterioDesempate.RegraTipoInvalido",
                $"A regra {input.RegraCodigo}/{input.RegraVersao} não é do tipo criterio_desempate."));
        }

        Result<ArgsCriterioDesempate> argsResult = MontarArgs(input, regra.Codigo);
        if (argsResult.IsFailure)
        {
            return Result<CriterioDesempate>.Failure(argsResult.Error!);
        }

        Result<ReferenciaRegra> referenciaRegraResult = ReferenciaRegra.Criar(regra.Codigo, regra.Versao, regra.Hash);
        if (referenciaRegraResult.IsFailure)
        {
            return Result<CriterioDesempate>.Failure(referenciaRegraResult.Error!);
        }

        return CriterioDesempate.Criar(input.Ordem, referenciaRegraResult.Value!, argsResult.Value!);
    }

    private static Result<ArgsCriterioDesempate> MontarArgs(CriterioDesempateInput input, string regraCodigo) =>
        regraCodigo switch
        {
            CriterioDesempateCodigo.MaiorNotaEtapa => input.EtapaRef is { } etapaRef
                ? Result<ArgsCriterioDesempate>.Success(new ArgsDesempateMaiorNotaEtapa(etapaRef))
                : Result<ArgsCriterioDesempate>.Failure(new DomainError(
                    "CriterioDesempate.EtapaRefObrigatorio",
                    $"O critério na ordem {input.Ordem} exige EtapaRef para a regra {CriterioDesempateCodigo.MaiorNotaEtapa}.")),

            CriterioDesempateCodigo.MaiorIdade =>
                Result<ArgsCriterioDesempate>.Success(new ArgsDesempateMaiorIdade()),

            CriterioDesempateCodigo.Idoso => input.IdadeMinima is { } idadeMinima
                ? Result<ArgsCriterioDesempate>.Success(new ArgsDesempateIdoso(idadeMinima))
                : Result<ArgsCriterioDesempate>.Failure(new DomainError(
                    "CriterioDesempate.IdadeMinimaObrigatoria",
                    $"O critério na ordem {input.Ordem} exige IdadeMinima para a regra {CriterioDesempateCodigo.Idoso}.")),

            CriterioDesempateCodigo.PredicadoFato =>
                !string.IsNullOrWhiteSpace(input.Fato) && !string.IsNullOrWhiteSpace(input.Operador) && !string.IsNullOrWhiteSpace(input.Valor)
                    ? Result<ArgsCriterioDesempate>.Success(new ArgsDesempatePredicadoFato(input.Fato, input.Operador, input.Valor))
                    : Result<ArgsCriterioDesempate>.Failure(new DomainError(
                        "CriterioDesempate.PredicadoFatoIncompleto",
                        $"O critério na ordem {input.Ordem} exige Fato, Operador e Valor para a regra {CriterioDesempateCodigo.PredicadoFato}.")),

            _ => Result<ArgsCriterioDesempate>.Failure(new DomainError(
                "CriterioDesempate.RegraTipoInvalido",
                $"Código de regra de desempate desconhecido: {regraCodigo}.")),
        };
}
