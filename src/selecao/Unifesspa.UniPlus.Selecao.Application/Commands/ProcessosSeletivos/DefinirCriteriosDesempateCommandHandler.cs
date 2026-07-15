namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json;

using Abstractions;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;
using Kernel.Results;
using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Handler do <see cref="DefinirCriteriosDesempateCommand"/> (Story #774):
/// resolve cada critério no catálogo <c>rol_de_regras</c>
/// (<see cref="IRegraCatalogoReader"/>, Story #772), monta os args tipados
/// conforme o código da regra e congela a referência — a existência do
/// <c>etapa_ref</c> no processo (INV-B6) é garantida pela raiz
/// (<see cref="ProcessoSeletivo.DefinirCriteriosDesempate"/>). Quando algum
/// critério referencia <c>DESEMPATE-PREDICADO-FATO</c>, resolve também o
/// vocabulário fechado de fatos do candidato (<see cref="IFatoCandidatoReader"/>,
/// #846, ADR-0111) para que <see cref="CriterioDesempate.Criar"/> valide a
/// condição contra ele (fecha o INV-B6 do <c>Fato</c>).
/// </summary>
public static class DefinirCriteriosDesempateCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirCriteriosDesempateCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegraCatalogoReader regraCatalogoReader,
        IFatoCandidatoReader fatoCandidatoReader,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(regraCatalogoReader);
        ArgumentNullException.ThrowIfNull(fatoCandidatoReader);
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

        // O vocabulário só é resolvido (I/O cross-módulo) quando algum critério de fato
        // referencia DESEMPATE-PREDICADO-FATO — o caso comum (demais 3 regras) não paga
        // esse custo.
        IReadOnlyDictionary<string, DescritorFatoCandidato>? vocabularioFatos = command.Criterios
            .Any(static c => c.RegraCodigo == CriterioDesempateCodigo.PredicadoFato)
                ? await ResolverVocabularioFatosAsync(fatoCandidatoReader, cancellationToken).ConfigureAwait(false)
                : null;

        List<CriterioDesempate> criterios = [];
        foreach (CriterioDesempateInput input in command.Criterios)
        {
            Result<CriterioDesempate> resultado = await ResolverCriterioAsync(input, regraCatalogoReader, vocabularioFatos, cancellationToken)
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

    /// <summary>
    /// Mapeia <see cref="FatoCandidatoView"/> (o DTO cross-módulo real do #846)
    /// para o <see cref="DescritorFatoCandidato"/> próprio do Domain de
    /// Selecao. Um fato categórico de <b>escopo-processo</b> (<c>Dominio ==
    /// "CATEGORICO"</c> com <c>ValoresDominio</c> nulo — ex.: <c>MODALIDADE</c>,
    /// <c>CONDICAO_ATENDIMENTO</c>) não é representável nesta Story (domínio
    /// dinâmico, fora de escopo — ADR-0111) e fica de fora do vocabulário
    /// fechado: um predicado que o cite reprova como fato desconhecido.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, DescritorFatoCandidato>> ResolverVocabularioFatosAsync(
        IFatoCandidatoReader fatoCandidatoReader, CancellationToken cancellationToken)
    {
        IReadOnlyList<FatoCandidatoView> fatos = await fatoCandidatoReader
            .ListarAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, DescritorFatoCandidato> vocabulario = [];
        foreach (FatoCandidatoView fato in fatos)
        {
            TipoDominioFato? tipoDominio = fato switch
            {
                { Dominio: "BOOLEANO" } => TipoDominioFato.Booleano,
                { Dominio: "NUMERICO" } => TipoDominioFato.Numerico,
                { Dominio: "CATEGORICO", ValoresDominio.Count: > 0 } => TipoDominioFato.CategoricoEstatico,
                _ => null,
            };

            if (tipoDominio is not { } tipo)
            {
                continue;
            }

            Result<DescritorFatoCandidato> descritorResult = DescritorFatoCandidato.Criar(fato.Codigo, tipo, fato.ValoresDominio);
            if (descritorResult.IsSuccess)
            {
                vocabulario[fato.Codigo] = descritorResult.Value!;
            }
        }

        return vocabulario;
    }

    private static async Task<Result<CriterioDesempate>> ResolverCriterioAsync(
        CriterioDesempateInput input,
        IRegraCatalogoReader regraCatalogoReader,
        IReadOnlyDictionary<string, DescritorFatoCandidato>? vocabularioFatos,
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

        return CriterioDesempate.Criar(input.Ordem, referenciaRegraResult.Value!, argsResult.Value!, vocabularioFatos);
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

            CriterioDesempateCodigo.PredicadoFato => MontarArgsPredicadoFato(input),

            _ => Result<ArgsCriterioDesempate>.Failure(new DomainError(
                "CriterioDesempate.RegraTipoInvalido",
                $"Código de regra de desempate desconhecido: {regraCodigo}.")),
        };

    private static Result<ArgsCriterioDesempate> MontarArgsPredicadoFato(CriterioDesempateInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Fato) || string.IsNullOrWhiteSpace(input.Operador) || string.IsNullOrWhiteSpace(input.Valor))
        {
            return Result<ArgsCriterioDesempate>.Failure(new DomainError(
                "CriterioDesempate.PredicadoFatoIncompleto",
                $"O critério na ordem {input.Ordem} exige Fato, Operador e Valor para a regra {CriterioDesempateCodigo.PredicadoFato}."));
        }

        Operador operador = OperadorCodigo.FromCodigo(input.Operador);
        JsonElement valor = InterpretarValor(input.Valor);

        Result<CondicaoDnf> condicaoResult = CondicaoDnf.Criar(input.Fato, operador, valor);
        return condicaoResult.IsFailure
            ? Result<ArgsCriterioDesempate>.Failure(condicaoResult.Error!)
            : Result<ArgsCriterioDesempate>.Success(new ArgsDesempatePredicadoFato(condicaoResult.Value!));
    }

    /// <summary>
    /// <see cref="CriterioDesempateInput.Valor"/> permanece texto plano (mesma forma
    /// flat do wire de comando) — mas quando o texto já É um JSON válido (booleano,
    /// número, string entre aspas, ou array para o operador EM), ele é interpretado
    /// como tal, para que a matriz operador × domínio (ADR-0111) tenha o tipo correto
    /// a validar. Texto que não é JSON válido (ex.: um código categórico sem aspas)
    /// é tratado como o escalar de string que representa.
    /// </summary>
    private static JsonElement InterpretarValor(string valor)
    {
        try
        {
            using JsonDocument documento = JsonDocument.Parse(valor);
            return documento.RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(valor);
        }
    }
}
