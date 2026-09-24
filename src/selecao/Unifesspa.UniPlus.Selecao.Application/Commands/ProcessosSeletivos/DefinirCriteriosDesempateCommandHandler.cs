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
/// Handler do <see cref="DefinirCriteriosDesempateCommand"/> (Story #774). Lê o catálogo de
/// critérios do <c>rol_de_regras</c> (<see cref="IRegraCatalogoReader"/>, Story #772) e, quando
/// preciso, o vocabulário de fatos antes de travar o processo; depois do 404, do If-Match e do
/// teto, resolve cada critério no catálogo já lido, monta os args tipados conforme o código da
/// regra e congela a referência — a
/// existência do <c>etapa_ref</c> no processo (INV-B6) é garantida pela raiz
/// (<see cref="ProcessoSeletivo.DefinirCriteriosDesempate"/>). Quando algum critério referencia
/// <c>DESEMPATE-PREDICADO-FATO</c>, resolve também o vocabulário fechado de fatos do candidato
/// (<see cref="IFatoCandidatoReader"/>, #846, ADR-0111) para que
/// <see cref="CriterioDesempate.Criar"/> valide a condição contra ele (fecha o INV-B6 do
/// <c>Fato</c>). As recusas de todos os critérios se acumulam, cada uma no campo do item,
/// granularidade nunca coberta pelo FluentValidation.
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

        // Acima do teto a lista não é lida item a item: o validator não confere os itens dela,
        // e a recusa do agregado é a única resposta.
        List<FieldError> quantidadeErros = ProcessoSeletivo.ValidarQuantidadeDeCriteriosDesempate(command.Criterios.Count);
        bool resolverCriterios = quantidadeErros.Count == 0 && command.Criterios.Count > 0;

        // As leituras que não dependem do processo rodam antes de travá-lo para a mutação. O
        // vocabulário só é resolvido (I/O cross-módulo) quando algum critério referencia
        // DESEMPATE-PREDICADO-FATO, e o catálogo de critérios é lido uma vez, para cada critério
        // se resolver em memória.
        IReadOnlyDictionary<string, DescritorFatoCandidato>? vocabularioFatos =
            resolverCriterios && command.Criterios.Any(static c => c.RegraCodigo == CriterioDesempateCodigo.PredicadoFato)
                ? await ResolverVocabularioFatosAsync(fatoCandidatoReader, cancellationToken).ConfigureAwait(false)
                : null;
        IReadOnlyList<RegraCatalogo> regrasDesempate = resolverCriterios
            ? await regraCatalogoReader
                .ListarPorTipoAsync(TipoRegra.CriterioDesempate, cancellationToken)
                .ConfigureAwait(false)
            : [];

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

        if (quantidadeErros.Count > 0)
        {
            return Result<MutacaoAceita>.ValidationFailure(quantidadeErros);
        }

        Dictionary<(string Codigo, string Versao), RegraCatalogo> catalogo = regrasDesempate
            .ToDictionary(static r => (r.Codigo, r.Versao));

        // Acumula (ADR-0125) as recusas de todos os critérios. O que não chega a ser criado
        // também segue para a conferência contra o processo.
        List<FieldError> erros = [];
        List<CriterioDesempateInformado> informados = [];
        List<CriterioDesempate> criterios = [];
        for (int indice = 0; indice < command.Criterios.Count; indice++)
        {
            CriterioDesempateInput input = command.Criterios[indice];
            (Result<CriterioDesempate> resultado, ArgsCriterioDesempate? args) = await ResolverCriterioAsync(
                    input, catalogo, regraCatalogoReader, vocabularioFatos, cancellationToken)
                .ConfigureAwait(false);
            informados.Add(new CriterioDesempateInformado(input.Ordem, args));
            if (resultado.IsFailure)
            {
                erros.AddRange(resultado.Errors.Select(erro => erro with
                {
                    Field = ProcessoSeletivo.CampoDoCriterioDesempate(indice, erro.Field),
                }));
                continue;
            }

            criterios.Add(resultado.Value!);
        }

        if (erros.Count > 0)
        {
            erros.AddRange(processo.ValidarCriteriosDesempate(informados));
            return Result<MutacaoAceita>.ValidationFailure(processo.AnexarAreasAceitasDoDesempate(erros));
        }

        Result result = processo.DefinirCriteriosDesempate(criterios, command.Precondicao);
        if (result.IsFailure)
        {
            return Result<MutacaoAceita>.ValidationFailure(result.Errors);
        }

        // Agregado tracked: persistência por change detection (ValueGeneratedNever
        // nos filhos) — não chamar DbSet.Update.
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

    /// <summary>
    /// Resolve a regra do critério no catálogo já carregado, monta os args e cria o critério.
    /// Toda recusa sai com o campo relativo ao item (<c>ordem</c>, <c>regraCodigo</c>,
    /// <c>etapaRef</c>, <c>idadeMinima</c>, <c>fato</c>, <c>operador</c>, <c>valor</c>). Os args
    /// voltam mesmo quando a criação recusa, para o processo conferi-los contra si mesmo.
    /// </summary>
    private static async Task<(Result<CriterioDesempate> Resultado, ArgsCriterioDesempate? Args)> ResolverCriterioAsync(
        CriterioDesempateInput input,
        Dictionary<(string Codigo, string Versao), RegraCatalogo> catalogo,
        IRegraCatalogoReader regraCatalogoReader,
        IReadOnlyDictionary<string, DescritorFatoCandidato>? vocabularioFatos,
        CancellationToken cancellationToken)
    {
        // A recusa que sai antes de CriterioDesempate.Criar acumula a da ordem, que Criar
        // confere e que não depende da regra.
        Result<CriterioDesempate> Recusado(IEnumerable<FieldError> erros) =>
            Result<CriterioDesempate>.ValidationFailure([.. CriterioDesempate.ValidarOrdem(input.Ordem), .. erros]);

        if (!catalogo.TryGetValue((input.RegraCodigo, input.RegraVersao), out RegraCatalogo? regra))
        {
            // Fora do catálogo de critérios de desempate: consulta a regra pelo código, só para
            // distinguir a que não existe da que existe com outro tipo.
            regra = await regraCatalogoReader
                .ObterAsync(input.RegraCodigo, input.RegraVersao, cancellationToken)
                .ConfigureAwait(false);
            if (regra is null)
            {
                return (Recusado([new("regraCodigo", new DomainError(
                    "CriterioDesempate.RegraNaoEncontrada",
                    $"Regra de desempate {input.RegraCodigo}/{input.RegraVersao} não encontrada no rol_de_regras."))]), null);
            }

            if (regra.Tipo != TipoRegra.CriterioDesempate)
            {
                return (Recusado([new("regraCodigo", new DomainError(
                    "CriterioDesempate.RegraTipoInvalido",
                    $"A regra {input.RegraCodigo}/{input.RegraVersao} não é do tipo criterio_desempate."))]), null);
            }
        }

        Result<ArgsCriterioDesempate> argsResult = MontarArgs(input, regra.Codigo);
        if (argsResult.IsFailure)
        {
            return (Recusado(argsResult.Errors), null);
        }

        Result<ReferenciaRegra> referenciaRegraResult = ReferenciaRegra.Criar(regra.Codigo, regra.Versao, regra.Hash);
        if (referenciaRegraResult.IsFailure)
        {
            return (Recusado(referenciaRegraResult.Errors.Select(static erro => erro with { Field = "regraCodigo" })), argsResult.Value);
        }

        return (CriterioDesempate.Criar(input.Ordem, referenciaRegraResult.Value!, argsResult.Value!, vocabularioFatos), argsResult.Value);
    }

    private static Result<ArgsCriterioDesempate> Recusa(string campo, DomainError erro) =>
        Result<ArgsCriterioDesempate>.ValidationFailure([new(campo, erro)]);

    private static Result<ArgsCriterioDesempate> MontarArgs(CriterioDesempateInput input, string regraCodigo) =>
        regraCodigo switch
        {
            CriterioDesempateCodigo.MaiorNotaEtapa => input.EtapaRef is { } etapaRef
                ? Result<ArgsCriterioDesempate>.Success(new ArgsDesempateMaiorNotaEtapa(etapaRef))
                : Recusa("etapaRef", new DomainError(
                    "CriterioDesempate.EtapaRefObrigatorio",
                    $"O critério na ordem {input.Ordem} exige EtapaRef para a regra {CriterioDesempateCodigo.MaiorNotaEtapa}.")),

            CriterioDesempateCodigo.MaiorIdade =>
                Result<ArgsCriterioDesempate>.Success(new ArgsDesempateMaiorIdade()),

            CriterioDesempateCodigo.Idoso => input.IdadeMinima is { } idadeMinima
                ? Result<ArgsCriterioDesempate>.Success(new ArgsDesempateIdoso(idadeMinima))
                : Recusa("idadeMinima", new DomainError(
                    "CriterioDesempate.IdadeMinimaObrigatoria",
                    $"O critério na ordem {input.Ordem} exige IdadeMinima para a regra {CriterioDesempateCodigo.Idoso}.")),

            CriterioDesempateCodigo.PredicadoFato => MontarArgsPredicadoFato(input),

            // A ordem ausente vira lista vazia, e item nulo vira texto vazio: quem recusa, com
            // o campo de cada item, é CriterioDesempate.Criar, junto com as demais violações.
            // Um item além do teto basta para a recusa da lista inteira, e o resto nem é lido.
            CriterioDesempateCodigo.MaiorNotaAreaEnem =>
                Result<ArgsCriterioDesempate>.Success(new ArgsDesempateMaiorNotaAreaEnem(
                    [.. (input.Areas ?? []).Take(CriterioDesempate.AreasMaximo + 1).Select(static area => area?.Trim() ?? string.Empty)])),

            _ => Recusa("regraCodigo", new DomainError(
                "CriterioDesempate.RegraTipoInvalido",
                $"Código de regra de desempate desconhecido: {regraCodigo}.")),
        };

    private static Result<ArgsCriterioDesempate> MontarArgsPredicadoFato(CriterioDesempateInput input)
    {
        (string Campo, string? Texto)[] informados = [("fato", input.Fato), ("operador", input.Operador), ("valor", input.Valor)];
        List<FieldError> ausentes = [.. informados
            .Where(static campo => string.IsNullOrWhiteSpace(campo.Texto))
            .Select(campo => new FieldError(campo.Campo, new DomainError(
                "CriterioDesempate.PredicadoFatoIncompleto",
                $"O critério na ordem {input.Ordem} exige Fato, Operador e Valor para a regra {CriterioDesempateCodigo.PredicadoFato}.")))];
        if (ausentes.Count > 0)
        {
            return Result<ArgsCriterioDesempate>.ValidationFailure(ausentes);
        }

        Operador operador = OperadorCodigo.FromCodigo(input.Operador!);
        JsonElement valor = InterpretarValor(input.Valor!);

        Result<CondicaoDnf> condicaoResult = CondicaoDnf.Criar(input.Fato!, operador, valor);
        if (condicaoResult.IsFailure)
        {
            // Fato e operador presentes: a recusa é do operador desconhecido ou do valor
            // incoerente com ele.
            return Recusa(operador == Operador.Nenhuma ? "operador" : "valor", condicaoResult.Error!);
        }

        return Result<ArgsCriterioDesempate>.Success(new ArgsDesempatePredicadoFato(condicaoResult.Value!));
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
