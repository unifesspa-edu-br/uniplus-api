namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Referencias;

/// <summary>
/// Handler do <see cref="DefinirCriteriosDesempateCommand"/>. Lê o catálogo de
/// critérios do <c>regras_do_catalogo</c> (<see cref="IRegraCatalogoReader"/>) e, quando
/// preciso, o vocabulário de fatos antes de bloquear o processo; depois do 404 e do
/// teto, resolve cada critério no catálogo já lido, monta os parâmetros tipados conforme o código da
/// regra e congela a referência — a
/// existência do <c>etapa_ref</c> no processo é garantida pela raiz
/// (<see cref="ProcessoSeletivo.DefinirCriteriosDesempate"/>). Quando algum critério referencia
/// <c>DESEMPATE-PREDICADO-FATO</c>, resolve também o vocabulário fechado de fatos do candidato
/// (<see cref="IFatoCandidatoReader"/>, ADR-0111) para que
/// <see cref="CriterioDesempate.Criar"/> valide a condição contra ele (fecha a integridade da referência à etapa do
/// <c>Fato</c>). As recusas de todos os critérios se acumulam, cada uma no campo do item,
/// granularidade nunca coberta pelo FluentValidation.
/// </summary>
public static class DefinirCriteriosDesempateCommandHandler
{
    public static async Task<Result> Handle(
        DefinirCriteriosDesempateCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegraCatalogoReader regraCatalogoReader,
        IFatoCandidatoReader fatoCandidatoReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(regraCatalogoReader);
        ArgumentNullException.ThrowIfNull(fatoCandidatoReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        // Acima do teto a lista não é lida item a item: o validator não confere os itens dela,
        // e a recusa do agregado é a única resposta.
        List<FieldError> quantidadeErros = RegrasDoDesempate.ConferirQuantidadeDeCriteriosDesempate(command.Criterios.Count);
        bool resolverCriterios = quantidadeErros.Count == 0 && command.Criterios.Count > 0;

        // As leituras que não dependem do processo rodam antes de travá-lo para a mutação. O
        // vocabulário só é resolvido (I/O cross-módulo) quando algum critério referencia
        // DESEMPATE-PREDICADO-FATO, e o catálogo de critérios é lido uma vez, para cada critério
        // se resolver em memória.
        (IReadOnlyDictionary<string, DescritorFatoCandidato> Vocabulario, IReadOnlyDictionary<string, FatoCandidatoView> Catalogo)? fatos =
            resolverCriterios && command.Criterios.Any(static c => c.RegraCodigo == CriterioDesempateCodigo.PredicadoFato)
                ? await ResolverVocabularioFatosAsync(fatoCandidatoReader, cancellationToken).ConfigureAwait(false)
                : null;
        IReadOnlyDictionary<string, DescritorFatoCandidato>? vocabularioFatos = fatos?.Vocabulario;
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
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        // Fora do rascunho a recusa vem antes de consultar cadastros.
        if (processo.MutacaoBloqueada() is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        if (quantidadeErros.Count > 0)
        {
            return Result.ValidationFailure(quantidadeErros);
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
            (Result<CriterioDesempate> resultado, ParametrosCriterioDesempate? parametros) = await ResolverCriterioAsync(
                    input, catalogo, regraCatalogoReader, vocabularioFatos, cancellationToken)
                .ConfigureAwait(false);
            informados.Add(new CriterioDesempateInformado(input.Ordem, parametros));
            if (resultado.IsFailure)
            {
                erros.AddRange(resultado.Errors.Select(erro => erro with
                {
                    Field = RegrasDoDesempate.CampoDoCriterioDesempate(indice, erro.Field),
                }));
                continue;
            }

            criterios.Add(resultado.Value!);
        }

        if (erros.Count > 0)
        {
            erros.AddRange(RegrasDoDesempate.ConferirCriteriosDesempate(processo.Declaracao(), informados));
            return Result.ValidationFailure(RegrasDoDesempate.AnexarAreasAceitasDoDesempate(processo.Declaracao(), erros));
        }

        if (fatos is { } catalogoFatos)
        {
            Result vinculoNovo = ConferenciaDeVinculoNovo.Conferir(
                catalogoFatos.Catalogo,
                ReferenciasVivas.Vinculos(processo.Declaracao()),
                VinculosDeFatos.De(
                    [],
                    criterios.Select(static c => c.Parametros).OfType<ParametrosDesempatePredicadoFato>()
                        .Select(static a => (a.Condicao.Fato, a.Condicao.Valor))));
            if (vinculoNovo.IsFailure)
            {
                return Result.Failure(vinculoNovo.Error!);
            }
        }

        Result result = processo.DefinirCriteriosDesempate(criterios);
        if (result.IsFailure)
        {
            return Result.ValidationFailure(result.Errors);
        }

        // Agregado tracked: persistência por change detection (ValueGeneratedNever
        // nos filhos) — não chamar DbSet.Update.
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>
    /// Mapeia <see cref="FatoCandidatoView"/> (o DTO cross-módulo real do catálogo)
    /// para o <see cref="DescritorFatoCandidato"/> próprio do Domain de
    /// Selecao. Um fato categórico de <b>escopo-processo</b> (<c>Dominio ==
    /// "CATEGORICO"</c> com <c>ValoresDominio</c> nulo — ex.: <c>MODALIDADE</c>,
    /// <c>CONDICAO_ATENDIMENTO</c>) não é representável nesta Story (domínio
    /// dinâmico, fora de escopo — ADR-0111) e fica de fora do vocabulário
    /// fechado: um predicado que o cite reprova como fato desconhecido.
    /// </summary>
    private static async Task<(IReadOnlyDictionary<string, DescritorFatoCandidato> Vocabulario, IReadOnlyDictionary<string, FatoCandidatoView> Catalogo)> ResolverVocabularioFatosAsync(
        IFatoCandidatoReader fatoCandidatoReader, CancellationToken cancellationToken)
    {
        IReadOnlyList<FatoCandidatoView> fatos = await fatoCandidatoReader
            .ListarAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, DescritorFatoCandidato> vocabulario = [];
        foreach (FatoCandidatoView fato in fatos)
        {
            // O desempate só cita fato de domínio conhecido no catálogo: o categórico cujos valores vêm
            // do processo fica fora, como sempre ficou.
            TipoDominioFato? tipoDominio = VocabularioDeFatos.Classificar(fato) is { } classificado
                && classificado != TipoDominioFato.CategoricoDinamico
                    ? classificado
                    : null;

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

        return (vocabulario, fatos.ToDictionary(static f => f.Codigo, StringComparer.Ordinal));
    }

    /// <summary>
    /// Resolve a regra do critério no catálogo já carregado, monta os parâmetros e cria o critério.
    /// Toda recusa sai com o campo relativo ao item (<c>ordem</c>, <c>regraCodigo</c>,
    /// <c>etapaRef</c>, <c>idadeMinima</c>, <c>fato</c>, <c>operador</c>, <c>valor</c>). Os parâmetros
    /// voltam mesmo quando a criação recusa, para o processo conferi-los contra si mesmo.
    /// </summary>
    private static async Task<(Result<CriterioDesempate> Resultado, ParametrosCriterioDesempate? Parametros)> ResolverCriterioAsync(
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
                    $"Regra de desempate {input.RegraCodigo}/{input.RegraVersao} não encontrada no catálogo de regras."))]), null);
            }

            if (regra.Tipo != TipoRegra.CriterioDesempate)
            {
                return (Recusado([new("regraCodigo", new DomainError(
                    "CriterioDesempate.RegraTipoInvalido",
                    $"A regra {input.RegraCodigo}/{input.RegraVersao} não é do tipo criterio_desempate."))]), null);
            }
        }

        Result<ParametrosCriterioDesempate> parametrosResult = MontarParametros(input, regra.Codigo);
        if (parametrosResult.IsFailure)
        {
            return (Recusado(parametrosResult.Errors), null);
        }

        return (CriterioDesempate.Criar(input.Ordem, regra.Referencia(), parametrosResult.Value!, vocabularioFatos), parametrosResult.Value);
    }

    private static Result<ParametrosCriterioDesempate> Recusa(string campo, DomainError erro) =>
        Result<ParametrosCriterioDesempate>.ValidationFailure([new(campo, erro)]);

    private static Result<ParametrosCriterioDesempate> MontarParametros(CriterioDesempateInput input, string regraCodigo) =>
        regraCodigo switch
        {
            CriterioDesempateCodigo.MaiorNotaEtapa => ParametrosDaMaiorNotaNaEtapa(input),
            CriterioDesempateCodigo.MaiorIdade => Result<ParametrosCriterioDesempate>.Success(new ParametrosDesempateMaiorIdade()),
            CriterioDesempateCodigo.Idoso => ParametrosDoIdoso(input),
            CriterioDesempateCodigo.PredicadoFato => MontarParametrosPredicadoFato(input),
            CriterioDesempateCodigo.MaiorNotaAreaEnem => ParametrosDaMaiorNotaPorAreaDoEnem(input),
            _ => Recusa("regraCodigo", new DomainError(
                "CriterioDesempate.RegraTipoInvalido",
                $"Código de regra de desempate desconhecido: {regraCodigo}.")),
        };

    private static Result<ParametrosCriterioDesempate> ParametrosDaMaiorNotaNaEtapa(CriterioDesempateInput input) =>
        input.EtapaRef is { } etapaRef
            ? Result<ParametrosCriterioDesempate>.Success(new ParametrosDesempateMaiorNotaEtapa(etapaRef))
            : Recusa("etapaRef", new DomainError(
                "CriterioDesempate.EtapaRefObrigatorio",
                $"O critério na ordem {input.Ordem} exige EtapaRef para a regra {CriterioDesempateCodigo.MaiorNotaEtapa}."));

    private static Result<ParametrosCriterioDesempate> ParametrosDoIdoso(CriterioDesempateInput input) =>
        input.IdadeMinima is { } idadeMinima
            ? Result<ParametrosCriterioDesempate>.Success(new ParametrosDesempateIdoso(idadeMinima))
            : Recusa("idadeMinima", new DomainError(
                "CriterioDesempate.IdadeMinimaObrigatoria",
                $"O critério na ordem {input.Ordem} exige IdadeMinima para a regra {CriterioDesempateCodigo.Idoso}."));

    /// <summary>
    /// A ordem ausente vira lista vazia, e item nulo vira texto vazio: quem recusa, com o campo de cada item, é
    /// <c>CriterioDesempate.Criar</c>, junto com as demais violações. Um item além do teto basta para a recusa da lista
    /// inteira, e o resto nem é lido.
    /// </summary>
    private static Result<ParametrosCriterioDesempate> ParametrosDaMaiorNotaPorAreaDoEnem(CriterioDesempateInput input) =>
        Result<ParametrosCriterioDesempate>.Success(new ParametrosDesempateMaiorNotaAreaEnem(
            [.. (input.Areas ?? []).Take(CriterioDesempate.AreasMaximo + 1).Select(static area => area?.Trim() ?? string.Empty)]));

    private static Result<ParametrosCriterioDesempate> MontarParametrosPredicadoFato(CriterioDesempateInput input)
    {
        List<FieldError> ausentes = CamposAusentesDoPredicado(input);
        return ausentes.Count > 0
            ? Result<ParametrosCriterioDesempate>.ValidationFailure(ausentes)
            : MontarCondicaoDoPredicado(input);
    }

    private static List<FieldError> CamposAusentesDoPredicado(CriterioDesempateInput input)
    {
        (string Campo, string? Texto)[] informados = [("fato", input.Fato), ("operador", input.Operador), ("valor", input.Valor)];
        return [.. informados
            .Where(static campo => string.IsNullOrWhiteSpace(campo.Texto))
            .Select(campo => new FieldError(campo.Campo, new DomainError(
                "CriterioDesempate.PredicadoFatoIncompleto",
                $"O critério na ordem {input.Ordem} exige Fato, Operador e Valor para a regra {CriterioDesempateCodigo.PredicadoFato}.")))];
    }

    /// <summary>
    /// Com fato, operador e valor presentes, a recusa é do operador desconhecido ou do valor incoerente com ele.
    /// </summary>
    private static Result<ParametrosCriterioDesempate> MontarCondicaoDoPredicado(CriterioDesempateInput input)
    {
        Operador operador = OperadorCodigo.FromCodigo(input.Operador!);
        Result<CondicaoDnf> condicao = CondicaoDnf.Criar(input.Fato!, operador, InterpretarValor(input.Valor!));
        if (condicao.IsFailure)
        {
            return Recusa(operador == Operador.Nenhuma ? "operador" : "valor", condicao.Error!);
        }

        return Result<ParametrosCriterioDesempate>.Success(new ParametrosDesempatePredicadoFato(condicao.Value!));
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
