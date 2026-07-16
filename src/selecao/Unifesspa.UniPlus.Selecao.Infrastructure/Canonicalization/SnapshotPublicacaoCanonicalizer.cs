namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Globalization;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Implementação da projeção canônica do envelope de congelamento (ADR-0100,
/// ADR-0109). Projeta a configuração viva do agregado num payload de 17 chaves
/// — <b>12 blocos reais + 5 stubs</b> <c>{"status":"nao_construido"}</c> para as
/// dimensões que a Feature #40 ainda não implementou (ADR-0100 item 10) — e
/// devolve os bytes via <see cref="HashCanonicalComputer.ComputeSnapshotBytes"/>.
/// <c>documentosExigidos</c> (Story #853) é um dos 12: já carrega
/// <c>obrigatoriedades[]</c> real, com <c>exigencias[]</c> (#554) ainda
/// aninhada como stub — o bloco nasce parcialmente real.
/// </summary>
/// <remarks>
/// <para>
/// Todo campo string de negócio passa por <see cref="HashCanonicalComputer.NormalizeNfc"/>;
/// todo decimal por <see cref="HashCanonicalComputer.SerializeDecimalCanonical"/>
/// com a escala declarada do campo; instantes por
/// <see cref="HashCanonicalComputer.SerializeInstantCanonical"/>. A
/// reordenação de chaves e a serialização byte-estável final acontecem em
/// <see cref="HashCanonicalComputer.ComputeSnapshotBytes"/> — este tipo só
/// monta o <see cref="JsonObject"/> com os valores já normalizados.
/// </para>
/// <para>
/// <strong>Leitura do bloco "vagas" vs. "distribuição" (ADR-0100 item 10):</strong>
/// o modelo atual não tem um motor de cálculo de vagas — <c>ConfiguracaoDistribuicaoVagas</c>
/// já é o conjunto de INPUTS (voBase, PR, regra, modalidades); o
/// <c>QuadroDeVagas</c> (quantidade por modalidade) é output derivado de um
/// motor futuro. Por isso "vagas" serializa como stub
/// <c>nao_construido</c> e "distribuição" carrega os inputs reais.
/// </para>
/// <para>
/// <strong>Ordenação determinística das coleções (ADR-0109 D9):</strong>
/// <c>IProcessoSeletivoRepository.ObterComConfiguracaoAsync</c> não aplica
/// <c>ORDER BY</c> aos <c>Include</c> das coleções filhas — a ordem física
/// devolvida pelo Postgres para a MESMA linha pode variar entre leituras
/// (plano de execução, VACUUM, etc.). Como a ordem de um array entra no
/// hash, todo bloco baseado em coleção é ordenado por uma chave estável
/// antes de serializar: campo <c>Ordem</c> quando ele é semântico (etapas,
/// critérios de desempate), identidade de negócio única quando existe
/// (oferta/modalidade/condição/recurso por seus <c>*OrigemId</c>), e — onde
/// não há chave natural — pela <b>chave de conteúdo</b>: os bytes canônicos
/// do próprio item. Ordenar por <c>EntityBase.Id</c> era determinístico entre
/// leituras da MESMA linha, mas não entre <b>configurações equivalentes</b>:
/// duas regras de eliminação idênticas inseridas em ordem inversa recebem
/// Guids v7 distintos e produziriam bytes distintos para a mesma configuração.
/// A chave de conteúdo faz o envelope depender só do que ele diz.
/// </para>
/// </remarks>
public sealed class SnapshotPublicacaoCanonicalizer : ISnapshotPublicacaoCanonicalizer
{
    /// <summary>
    /// Versão da <b>forma</b> do envelope (ADR-0109 D1). Sobe a cada mudança de
    /// forma — chave nova, ou um stub virando conteúdo real. Não há CHECK de
    /// <c>schema_version</c> no banco: o bump é livre e não pede migration. Toda
    /// versão aqui declarada tem de ter a sua golden fixture correspondente —
    /// um teste de política falha o build se não tiver.
    /// </summary>
    internal const string SchemaVersionAtual = "1.1";

    private const string AlgoritmoHashAtual = "canonical-json/sha256@v1";
    private const int EscalaPadrao = 4;
    private const int EscalaPercentual = 2;

    /// <summary>
    /// Os 5 blocos que ainda não têm dono (ADR-0109 D8): <c>vagas</c>,
    /// <c>formulario</c>, <c>cascataRemanejamento</c>, <c>divulgacao</c>,
    /// <c>identidadesUnidade</c> — mais a sub-chave <c>exigencias</c> dentro de
    /// <c>documentosExigidos</c> (#554). Um bloco de topo <b>real</b> nunca emite
    /// este literal na raiz: se a dimensão é obrigatória, a ausência é pendência
    /// de conformidade e o gate recusa antes de canonicalizar.
    /// </summary>
    private static readonly JsonObject NaoConstruido = new() { ["status"] = "nao_construido" };

    public SnapshotCanonico Canonicalizar(EntradaCanonicalizacao entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);

        ProcessoSeletivo processo = entrada.Processo;
        DadosEdital dados = entrada.Dados;
        RetificacaoInfo? retificacao = entrada.Retificacao;

        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(dados);
        ArgumentException.ThrowIfNullOrWhiteSpace(entrada.HashDocumento);

        JsonObject payload = new()
        {
            ["periodo"] = SerializarPeriodo(dados),
            ["etapas"] = SerializarEtapas(processo),
            ["vagas"] = NaoConstruido.DeepClone(),
            ["distribuicao"] = SerializarDistribuicao(processo),
            ["modalidades"] = SerializarModalidades(processo),
            ["ofertas"] = SerializarOfertas(processo),
            ["atendimento"] = SerializarAtendimento(processo),
            ["bonusRegional"] = SerializarBonusRegional(processo),
            ["criteriosDesempate"] = SerializarCriteriosDesempate(processo),
            ["classificacao"] = SerializarClassificacao(processo),
            ["hashesEdital"] = SerializarHashesEdital(dados, entrada.HashDocumento),
            ["documentosExigidos"] = SerializarDocumentosExigidos(entrada.Conformidade),
            ["formulario"] = NaoConstruido.DeepClone(),
            ["cascataRemanejamento"] = NaoConstruido.DeepClone(),
            ["divulgacao"] = NaoConstruido.DeepClone(),
            ["cronogramaFases"] = SerializarCronogramaFases(processo),
            ["identidadesUnidade"] = NaoConstruido.DeepClone(),
        };

        // ADR-0101: a retificação ACRESCENTA um 18º bloco preservando os 17
        // anteriores. A abertura não escreve esta chave — seu payload é
        // byte-a-byte o mesmo do T4 (a reordenação de chaves em
        // ComputeSnapshotBytes independe da ordem de inserção aqui).
        if (retificacao is not null)
        {
            payload["retificacao"] = new JsonObject
            {
                ["editalRetificadoId"] = retificacao.EditalRetificadoId,
                ["motivo"] = HashCanonicalComputer.NormalizeNfc(retificacao.Motivo),
            };
        }

        byte[] bytes = HashCanonicalComputer.ComputeSnapshotBytes(payload);
        return new SnapshotCanonico(bytes, SchemaVersionAtual, AlgoritmoHashAtual);
    }

    private static JsonObject SerializarPeriodo(DadosEdital dados) => new()
    {
        ["numero"] = dados.Numero is { } numero ? HashCanonicalComputer.NormalizeNfc(numero) : null,
        ["inicio"] = dados.PeriodoInscricaoInicio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["fim"] = dados.PeriodoInscricaoFim.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    };

    private static JsonArray SerializarEtapas(ProcessoSeletivo processo)
    {
        JsonArray array = [];
        IOrderedEnumerable<EtapaProcesso> ordenadas = processo.Etapas
            .OrderBy(static e => e.Ordem ?? int.MaxValue)
            .ThenBy(static e => e.Id);
        foreach (EtapaProcesso etapa in ordenadas)
        {
            array.Add(new JsonObject
            {
                // Id incluído (achado Codex, revisão do PR #791): os blocos
                // "criteriosDesempate" (DESEMPATE-MAIOR-NOTA-ETAPA) e
                // "classificacao" (ELIM-NOTA-MINIMA-ETAPA) congelam um
                // etapaRef apontando para este Id — sem ele aqui, o snapshot
                // teria uma referência não resolvível dentro do próprio JSON
                // congelado, obrigando a consultar a tabela viva (mutável)
                // para interpretar um documento que deveria ser autocontido.
                ["id"] = etapa.Id,
                ["nome"] = HashCanonicalComputer.NormalizeNfc(etapa.Nome),
                ["carater"] = etapa.Carater.ToString(),
                ["peso"] = etapa.Peso is { } peso ? HashCanonicalComputer.SerializeDecimalCanonical(peso, EscalaPadrao) : null,
                ["notaMinima"] = etapa.NotaMinima is { } notaMinima ? HashCanonicalComputer.SerializeDecimalCanonical(notaMinima, EscalaPadrao) : null,
                ["ordem"] = etapa.Ordem,
            });
        }

        return array;
    }

    private static JsonArray SerializarDistribuicao(ProcessoSeletivo processo)
    {
        JsonArray array = [];
        foreach (ConfiguracaoDistribuicaoVagas configuracao in OrdenarPorOfertaCursoOrigemId(processo.DistribuicaoVagas))
        {
            array.Add(new JsonObject
            {
                ["ofertaCursoOrigemId"] = configuracao.OfertaCursoOrigemId,
                ["voBase"] = configuracao.VoBase,
                ["pr"] = HashCanonicalComputer.SerializeDecimalCanonical(configuracao.Pr, EscalaPadrao),
                ["regraDistribuicao"] = SerializarReferenciaRegra(configuracao.RegraDistribuicao),
                ["referenciaDemografica"] = configuracao.ReferenciaDemografica is { } referencia
                    ? SerializarReferenciaDemografica(referencia)
                    : null,
            });
        }

        return array;
    }

    private static JsonObject SerializarReferenciaDemografica(ReferenciaReservaDemograficaSnapshot referencia) => new()
    {
        ["origemId"] = referencia.OrigemId,
        ["censoReferencia"] = HashCanonicalComputer.NormalizeNfc(referencia.CensoReferencia),
        ["ppiPercentual"] = HashCanonicalComputer.SerializeDecimalCanonical(referencia.PpiPercentual, EscalaPercentual),
        ["quilombolaPercentual"] = HashCanonicalComputer.SerializeDecimalCanonical(referencia.QuilombolaPercentual, EscalaPercentual),
        ["pcdPercentual"] = HashCanonicalComputer.SerializeDecimalCanonical(referencia.PcdPercentual, EscalaPercentual),
        ["baseLegal"] = HashCanonicalComputer.NormalizeNfc(referencia.BaseLegal),
    };

    private static JsonArray SerializarModalidades(ProcessoSeletivo processo)
    {
        JsonArray array = [];
        foreach (ConfiguracaoDistribuicaoVagas configuracao in OrdenarPorOfertaCursoOrigemId(processo.DistribuicaoVagas))
        {
            IOrderedEnumerable<ModalidadeSelecionada> modalidadesOrdenadas = configuracao.Modalidades
                .OrderBy(static m => m.Codigo, StringComparer.Ordinal);
            foreach (ModalidadeSelecionada modalidade in modalidadesOrdenadas)
            {
                array.Add(new JsonObject
                {
                    ["ofertaCursoOrigemId"] = configuracao.OfertaCursoOrigemId,
                    ["modalidadeOrigemId"] = modalidade.ModalidadeOrigemId,
                    ["codigo"] = HashCanonicalComputer.NormalizeNfc(modalidade.Codigo),
                    ["descricao"] = modalidade.Descricao is { } descricao ? HashCanonicalComputer.NormalizeNfc(descricao) : null,
                    ["naturezaLegal"] = modalidade.NaturezaLegal.ToString(),
                    ["composicaoVagas"] = modalidade.ComposicaoVagas.ToString(),
                    ["composicaoOrigemCodigo"] = modalidade.ComposicaoOrigemCodigo,
                    ["regraRemanejamento"] = modalidade.RegraRemanejamento.ToString(),
                    ["remanejamentoDestino"] = modalidade.RemanejamentoDestino,
                    ["remanejamentoPar"] = modalidade.RemanejamentoPar,
                    ["remanejamentoFallback"] = modalidade.RemanejamentoFallback,
                    ["criteriosCumulativos"] = new JsonArray([.. modalidade.CriteriosCumulativos.Select(static c => (JsonNode?)JsonValue.Create(c))]),
                    ["acaoQuandoIndeferido"] = modalidade.AcaoQuandoIndeferido,
                    ["baseLegal"] = HashCanonicalComputer.NormalizeNfc(modalidade.BaseLegal),
                });
            }
        }

        return array;
    }

    // OfertaCursoOrigemId é único por processo (ConfiguracaoDistribuicaoVagas
    // — "cada oferta de curso só pode ter uma distribuição de vagas no
    // processo", validado em ProcessoSeletivo.DefinirDistribuicaoVagas) —
    // chave de negócio estável, sem empate possível.
    private static IOrderedEnumerable<ConfiguracaoDistribuicaoVagas> OrdenarPorOfertaCursoOrigemId(
        IEnumerable<ConfiguracaoDistribuicaoVagas> distribuicoes) =>
        distribuicoes.OrderBy(static d => d.OfertaCursoOrigemId);

    private static JsonArray SerializarOfertas(ProcessoSeletivo processo)
    {
        IEnumerable<string> ofertaIds = processo.DistribuicaoVagas
            .Select(static d => d.OfertaCursoOrigemId.ToString())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static id => id, StringComparer.Ordinal);

        return new JsonArray([.. ofertaIds.Select(static id => (JsonNode?)JsonValue.Create(id))]);
    }

    private static JsonObject SerializarAtendimento(ProcessoSeletivo processo)
    {
        // D8 — um bloco REAL nunca emite `nao_construido`. O atendimento é
        // dimensão obrigatória: a sua ausência é pendência de conformidade, e
        // o gate (ProcessoSeletivo.PendenciaDeConformidade) recusa a transição
        // antes de a canonicalização acontecer. Chegar aqui sem oferta é
        // invariante quebrada — falha alto, não congela um stub em silêncio.
        if (processo.OfertaAtendimento is not { } oferta)
        {
            throw new InvalidOperationException(
                "Canonicalização de processo sem oferta de atendimento especializado — o gate de conformidade deveria ter recusado a transição antes deste ponto.");
        }

        return new JsonObject
        {
            ["condicoes"] = new JsonArray([.. oferta.Condicoes
                .OrderBy(static c => c.CondicaoOrigemId)
                .Select(static c => (JsonNode)new JsonObject
                {
                    ["condicaoOrigemId"] = c.CondicaoOrigemId,
                    ["condicaoCodigo"] = HashCanonicalComputer.NormalizeNfc(c.CondicaoCodigo),
                    ["condicaoNome"] = HashCanonicalComputer.NormalizeNfc(c.CondicaoNome),
                })]),
            ["recursos"] = new JsonArray([.. oferta.Recursos
                .OrderBy(static r => r.RecursoOrigemId)
                .Select(static r => (JsonNode)new JsonObject
                {
                    ["recursoOrigemId"] = r.RecursoOrigemId,
                    ["recursoNome"] = HashCanonicalComputer.NormalizeNfc(r.RecursoNome),
                })]),
            ["tiposDeficiencia"] = new JsonArray([.. oferta.TiposDeficiencia
                .OrderBy(static t => t.TipoDeficienciaOrigemId)
                .Select(static t => (JsonNode)new JsonObject
                {
                    ["tipoDeficienciaOrigemId"] = t.TipoDeficienciaOrigemId,
                    ["tipoDeficienciaNome"] = HashCanonicalComputer.NormalizeNfc(t.TipoDeficienciaNome),
                })]),
        };
    }

    private static JsonObject SerializarBonusRegional(ProcessoSeletivo processo)
    {
        if (processo.BonusRegional is not { } bonus)
        {
            return new JsonObject { ["presente"] = false };
        }

        return new JsonObject
        {
            ["presente"] = true,
            ["regra"] = SerializarReferenciaRegra(bonus.Regra),
            ["fator"] = HashCanonicalComputer.SerializeDecimalCanonical(bonus.Fator, EscalaPadrao),
            ["teto"] = bonus.Teto is { } teto ? HashCanonicalComputer.SerializeDecimalCanonical(teto, EscalaPadrao) : null,
            ["municipioConvenio"] = bonus.MunicipioConvenio is { } municipio ? HashCanonicalComputer.NormalizeNfc(municipio) : null,
            ["baseLegal"] = bonus.BaseLegal is { } baseLegal ? HashCanonicalComputer.NormalizeNfc(baseLegal) : null,
        };
    }

    private static JsonArray SerializarCriteriosDesempate(ProcessoSeletivo processo)
    {
        JsonArray array = [];
        foreach (CriterioDesempate criterio in processo.CriteriosDesempate.OrderBy(static c => c.Ordem))
        {
            array.Add(new JsonObject
            {
                ["ordem"] = criterio.Ordem,
                ["regra"] = SerializarReferenciaRegra(criterio.Regra),
                ["args"] = SerializarArgsCriterioDesempate(criterio.Args),
            });
        }

        return array;
    }

    private static JsonObject SerializarArgsCriterioDesempate(ArgsCriterioDesempate args) => args switch
    {
        ArgsDesempateMaiorNotaEtapa maiorNotaEtapa => new JsonObject { ["etapaRef"] = maiorNotaEtapa.EtapaRef },
        ArgsDesempateMaiorIdade => [],
        ArgsDesempateIdoso idoso => new JsonObject { ["idadeMinima"] = idoso.IdadeMinima },
        ArgsDesempatePredicadoFato predicadoFato => new JsonObject
        {
            ["fato"] = HashCanonicalComputer.NormalizeNfc(predicadoFato.Condicao.Fato),
            ["operador"] = predicadoFato.Condicao.Operador.ToCodigo(),
            ["valor"] = JsonNode.Parse(predicadoFato.Condicao.Valor.GetRawText()),
        },
        _ => throw new InvalidOperationException($"Variante de {nameof(ArgsCriterioDesempate)} não reconhecida: {args.GetType()}."),
    };

    private static JsonObject SerializarClassificacao(ProcessoSeletivo processo)
    {
        // D8 — ver a nota em SerializarAtendimento. A classificação é o bloco que
        // determina o resultado do certame; congelá-la como `nao_construido` num
        // documento juridicamente vinculante é o pior modo de falha do envelope.
        if (processo.Classificacao is not { } classificacao)
        {
            throw new InvalidOperationException(
                "Canonicalização de processo sem configuração de classificação — o gate de conformidade deveria ter recusado a transição antes deste ponto.");
        }

        return new JsonObject
        {
            ["regraCalculo"] = SerializarReferenciaRegra(classificacao.RegraCalculo),
            ["regraArredondamento"] = classificacao.RegraArredondamento is { } arredondamento
                ? SerializarReferenciaRegra(arredondamento)
                : null,
            ["casasArredondamento"] = classificacao.CasasArredondamento,
            ["regraOrdemAlocacao"] = SerializarReferenciaRegra(classificacao.RegraOrdemAlocacao),
            ["nOpcoesAlocacao"] = classificacao.NOpcoesAlocacao,
            // D9 — RegrasEliminacao não tem chave de negócio única (cardinalidade
            // múltipla: duas ELIM-NOTA-MINIMA-ETAPA distintas são válidas, ex. PS
            // Convênios). Ordenar por `Id` era determinístico entre leituras da
            // mesma linha, mas NÃO entre configurações equivalentes — dois processos
            // com as mesmas regras inseridas em ordem inversa recebem Guids v7
            // distintos e produziriam envelopes distintos para a mesma configuração.
            // A ordenação é pela CHAVE DE CONTEÚDO: os bytes canônicos do próprio
            // item. O envelope passa a depender só do que ele diz.
            ["regrasEliminacao"] = OrdenarPorConteudo(classificacao.RegrasEliminacao
                .Select(static r => new JsonObject
                {
                    ["regra"] = SerializarReferenciaRegra(r.Regra),
                    ["args"] = SerializarArgsRegraEliminacao(r.Args),
                })),
        };
    }

    private static JsonObject SerializarArgsRegraEliminacao(ArgsRegraEliminacao args) => args switch
    {
        ArgsElimNotaMinimaEtapa notaMinima => new JsonObject
        {
            ["etapaRef"] = notaMinima.EtapaRef,
            ["notaMinima"] = HashCanonicalComputer.SerializeDecimalCanonical(notaMinima.NotaMinima, EscalaPadrao),
        },
        ArgsElimCorteRedacao corteRedacao => new JsonObject
        {
            ["minimo"] = HashCanonicalComputer.SerializeDecimalCanonical(corteRedacao.Minimo, EscalaPadrao),
        },
        ArgsElimZeroEmArea => [],
        _ => throw new InvalidOperationException($"Variante de {nameof(ArgsRegraEliminacao)} não reconhecida: {args.GetType()}."),
    };

    private static JsonObject SerializarHashesEdital(DadosEdital dados, string hashEdital) => new()
    {
        ["documentoEditalId"] = dados.DocumentoEditalId,
        ["hashSha256"] = hashEdital,
    };

    /// <summary>
    /// Story #853 §3.4: o bloco ganha DUAS chaves — <c>exigencias</c> (documentos exigidos
    /// por fase, #554, ainda stub) e <c>obrigatoriedades</c> (regras legais avaliadas, esta
    /// story). O bloco deixa de ser <c>nao_construido</c> na raiz: ele já tem conteúdo real
    /// (as obrigatoriedades), mesmo com uma das duas dimensões ainda pendente — D8 fala do
    /// bloco INTEIRO nunca fingir conteúdo, não impede que ele nasça parcialmente real.
    /// </summary>
    private static JsonObject SerializarDocumentosExigidos(ResultadoConformidade? conformidade) => new()
    {
        ["exigencias"] = NaoConstruido.DeepClone(),
        ["obrigatoriedades"] = SerializarObrigatoriedades(conformidade),
    };

    /// <summary>
    /// Ordenação determinística por <c>RegraId</c> (Guid v7, cronológico) — mesma convenção
    /// de chave estável já usada para coleções sem ordem semântica própria. Só regras
    /// aprovadas chegam aqui: o gate já recusou a transição antes de canonicalizar se
    /// qualquer uma reprovasse (§3.4) — o campo <c>aprovada</c> é mantido por paridade
    /// estrutural, não porque possa vir falso num snapshot real.
    /// </summary>
    private static JsonArray SerializarObrigatoriedades(ResultadoConformidade? conformidade)
    {
        JsonArray array = [];
        if (conformidade is null)
        {
            return array;
        }

        foreach (RegraAvaliada regra in conformidade.Regras.OrderBy(static r => r.RegraId))
        {
            array.Add(new JsonObject
            {
                ["regraId"] = regra.RegraId,
                ["regraCodigo"] = HashCanonicalComputer.NormalizeNfc(regra.RegraCodigo),
                ["categoria"] = regra.Categoria.ToString(),
                ["tipoProcessoCodigoAvaliado"] = HashCanonicalComputer.NormalizeNfc(regra.TipoProcessoCodigoAvaliado),
                ["predicado"] = SerializarPredicadoObrigatoriedade(regra.Predicado),
                ["aprovada"] = regra.Aprovada,
                ["baseLegal"] = HashCanonicalComputer.NormalizeNfc(regra.BaseLegal),
                ["atoNormativoUrl"] = regra.AtoNormativoUrl is { } url ? HashCanonicalComputer.NormalizeNfc(url) : null,
                ["portariaInterna"] = regra.PortariaInterna is { } portaria ? HashCanonicalComputer.NormalizeNfc(portaria) : null,
                ["descricaoHumana"] = HashCanonicalComputer.NormalizeNfc(regra.DescricaoHumana),
                ["vigenciaInicio"] = regra.VigenciaInicio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["vigenciaFim"] = regra.VigenciaFim?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["hash"] = regra.Hash,
            });
        }

        return array;
    }

    /// <summary>
    /// A variante em <c>args</c> é decidida pelo TIPO da regra ($tipo), mesma técnica de
    /// <see cref="SerializarArgsCriterioDesempate"/> e <see cref="SerializarArgsRegraEliminacao"/>
    /// — nunca um discriminador JSON solto.
    /// </summary>
    private static JsonObject SerializarPredicadoObrigatoriedade(PredicadoObrigatoriedade predicado)
    {
        (string tipo, JsonObject args) = predicado switch
        {
            EtapaObrigatoria p => ("etapaObrigatoria", new JsonObject
            {
                ["tipoEtapaCodigo"] = HashCanonicalComputer.NormalizeNfc(p.TipoEtapaCodigo),
            }),
            ModalidadesMinimas p => ("modalidadesMinimas", new JsonObject
            {
                ["codigos"] = new JsonArray([.. p.Codigos.Select(static c => JsonValue.Create(HashCanonicalComputer.NormalizeNfc(c)))]),
            }),
            DesempateDeveIncluir p => ("desempateDeveIncluir", new JsonObject
            {
                ["criterio"] = HashCanonicalComputer.NormalizeNfc(p.Criterio),
            }),
            DocumentoObrigatorioParaModalidade p => ("documentoObrigatorioParaModalidade", new JsonObject
            {
                ["modalidade"] = HashCanonicalComputer.NormalizeNfc(p.Modalidade),
                ["tipoDocumento"] = HashCanonicalComputer.NormalizeNfc(p.TipoDocumento),
            }),
            AtendimentoDisponivel p => ("atendimentoDisponivel", new JsonObject
            {
                ["necessidades"] = new JsonArray([.. p.Necessidades.Select(static n => JsonValue.Create(HashCanonicalComputer.NormalizeNfc(n)))]),
            }),
            ConcorrenciaDuplaObrigatoria => ("concorrenciaDuplaObrigatoria", []),
            Customizado p => ("customizado", new JsonObject
            {
                ["parametros"] = JsonNode.Parse(p.Parametros.GetRawText()),
            }),
            _ => throw new InvalidOperationException(
                $"Variante de {nameof(PredicadoObrigatoriedade)} não reconhecida: {predicado.GetType()}."),
        };

        return new JsonObject
        {
            ["tipo"] = tipo,
            ["args"] = args,
        };
    }

    /// <summary>
    /// O cronograma de fases (Story #851): <c>origemCandidatos</c> — atributo de raiz do
    /// agregado, chave-irmã dentro deste bloco (o envelope não tem bloco genérico de
    /// metadados do processo, ADR-0100 item 10) — e o array <c>fases</c>, ordenado
    /// deterministicamente por <c>OrderBy(Ordem).ThenBy(Id)</c> (a ordem entra no hash e o
    /// repositório não aplica <c>ORDER BY</c> aos <c>Include</c>).
    /// </summary>
    private static JsonObject SerializarCronogramaFases(ProcessoSeletivo processo) => new()
    {
        ["origemCandidatos"] = processo.OrigemCandidatos.ToString(),
        ["fases"] = SerializarFasesCronograma(processo),
    };

    private static JsonArray SerializarFasesCronograma(ProcessoSeletivo processo)
    {
        JsonArray array = [];
        IOrderedEnumerable<FaseCronograma> ordenadas = processo.CronogramaFases
            .OrderBy(static f => f.Ordem)
            .ThenBy(static f => f.Id);
        foreach (FaseCronograma fase in ordenadas)
        {
            array.Add(new JsonObject
            {
                ["ordem"] = fase.Ordem,
                ["faseCanonicaOrigemId"] = fase.FaseCanonicaOrigemId,
                ["codigo"] = HashCanonicalComputer.NormalizeNfc(fase.Codigo),
                ["donoInstitucional"] = HashCanonicalComputer.NormalizeNfc(fase.DonoInstitucional),
                ["origemData"] = fase.OrigemData.ToString(),
                ["agrupaEtapas"] = fase.AgrupaEtapas,
                ["permiteComplementacao"] = fase.PermiteComplementacao,
                ["produzResultado"] = fase.ProduzResultado,
                ["resultadoDefinitivo"] = fase.ResultadoDefinitivo,
                ["coletaInscricao"] = fase.ColetaInscricao,
                ["inicio"] = fase.Inicio is { } inicio ? HashCanonicalComputer.SerializeInstantCanonical(inicio) : null,
                ["fim"] = fase.Fim is { } fim ? HashCanonicalComputer.SerializeInstantCanonical(fim) : null,
                ["atoProduzidoCodigo"] = fase.AtoProduzidoCodigo is { } atoCodigo ? HashCanonicalComputer.NormalizeNfc(atoCodigo) : null,
                ["atoProduzidoEfeitoIrreversivel"] = fase.AtoProduzidoEfeitoIrreversivel,
                ["bancasRequeridas"] = SerializarBancasRequeridas(fase),
                ["regraRecurso"] = fase.RegraRecurso is { } regraRecurso ? SerializarRegraRecursoFase(regraRecurso) : null,
            });
        }

        return array;
    }

    private static JsonArray SerializarBancasRequeridas(FaseCronograma fase)
    {
        IOrderedEnumerable<BancaRequerida> ordenadas = fase.BancasRequeridas
            .OrderBy(static b => b.TipoBancaOrigemId)
            .ThenBy(static b => b.Codigo, StringComparer.Ordinal);

        return new JsonArray([.. ordenadas.Select(static b => (JsonNode)new JsonObject
        {
            ["tipoBancaOrigemId"] = b.TipoBancaOrigemId,
            ["codigo"] = HashCanonicalComputer.NormalizeNfc(b.Codigo),
        })]);
    }

    private static JsonObject SerializarRegraRecursoFase(RegraRecursoFase regraRecurso) => new()
    {
        ["regra"] = SerializarReferenciaRegra(regraRecurso.Regra),
        ["args"] = SerializarArgsRegraPrazoRecurso(regraRecurso.Args),
    };

    private static JsonObject SerializarArgsRegraPrazoRecurso(ArgsRegraPrazoRecurso args) => new()
    {
        ["prazoValor"] = HashCanonicalComputer.SerializeDecimalCanonical(args.PrazoValor, EscalaPadrao),
        ["prazoUnidade"] = args.PrazoUnidade.ToString(),
        ["atoAncoraCodigo"] = HashCanonicalComputer.NormalizeNfc(args.AtoAncoraCodigo),
        ["suspensividadePrimeiraInstanciaValor"] = args.SuspensividadePrimeiraInstanciaValor is { } v1
            ? HashCanonicalComputer.SerializeDecimalCanonical(v1, EscalaPadrao)
            : null,
        ["suspensividadePrimeiraInstanciaUnidade"] = args.SuspensividadePrimeiraInstanciaUnidade?.ToString(),
        ["suspensividadeSegundaInstanciaValor"] = args.SuspensividadeSegundaInstanciaValor is { } v2
            ? HashCanonicalComputer.SerializeDecimalCanonical(v2, EscalaPadrao)
            : null,
        ["suspensividadeSegundaInstanciaUnidade"] = args.SuspensividadeSegundaInstanciaUnidade?.ToString(),
    };

    /// <summary>
    /// Ordena um array pela <b>chave de conteúdo</b> de cada item — os seus
    /// próprios bytes canônicos (ADR-0109 D9). Usado onde a coleção não tem
    /// chave de negócio natural: sem isso, a identidade técnica da linha (um
    /// Guid) vazaria para dentro do hash e duas configurações equivalentes
    /// produziriam envelopes distintos.
    /// </summary>
    private static JsonArray OrdenarPorConteudo(IEnumerable<JsonObject> itens)
    {
        IOrderedEnumerable<JsonObject> ordenados = itens.OrderBy(
            static item => System.Text.Encoding.UTF8.GetString(HashCanonicalComputer.ComputeSnapshotBytes(item)),
            StringComparer.Ordinal);

        return new JsonArray([.. ordenados.Select(static item => (JsonNode)item)]);
    }

    private static JsonObject SerializarReferenciaRegra(ReferenciaRegra regra) => new()
    {
        ["codigo"] = regra.Codigo,
        ["versao"] = regra.Versao,
        ["hash"] = regra.Hash,
    };
}
