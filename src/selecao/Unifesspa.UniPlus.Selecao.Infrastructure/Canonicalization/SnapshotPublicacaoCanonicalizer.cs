namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Globalization;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Implementação do serializador canônico dos 17 blocos do snapshot de
/// publicação (ADR-0100). Projeta a configuração viva do agregado num
/// payload canônico — 11 blocos reais + 6 stubs
/// <c>{"status":"nao_construido"}</c> para dimensões que a Feature #40 ainda
/// não implementou (ADR-0100 item 10) — e devolve os bytes via
/// <see cref="HashCanonicalComputer.ComputeSnapshotBytes"/>.
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
/// </remarks>
public sealed class SnapshotPublicacaoCanonicalizer : ISnapshotPublicacaoCanonicalizer
{
    private const string SchemaVersionAtual = "1.0";
    private const string AlgoritmoHashAtual = "canonical-json/sha256@v1";
    private const int EscalaPadrao = 4;
    private const int EscalaPercentual = 2;

    private static readonly JsonObject NaoConstruido = new() { ["status"] = "nao_construido" };

    public SnapshotCanonico Canonicalizar(ProcessoSeletivo processo, DadosEdital dados, string hashEdital)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(dados);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashEdital);

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
            ["hashesEdital"] = SerializarHashesEdital(dados, hashEdital),
            ["documentosExigidos"] = NaoConstruido.DeepClone(),
            ["formulario"] = NaoConstruido.DeepClone(),
            ["cascataRemanejamento"] = NaoConstruido.DeepClone(),
            ["divulgacao"] = NaoConstruido.DeepClone(),
            ["cronogramaFases"] = NaoConstruido.DeepClone(),
            ["identidadesUnidade"] = NaoConstruido.DeepClone(),
        };

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
        foreach (EtapaProcesso etapa in processo.Etapas)
        {
            array.Add(new JsonObject
            {
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
        foreach (ConfiguracaoDistribuicaoVagas configuracao in processo.DistribuicaoVagas)
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
        foreach (ConfiguracaoDistribuicaoVagas configuracao in processo.DistribuicaoVagas)
        {
            foreach (ModalidadeSelecionada modalidade in configuracao.Modalidades)
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
        if (processo.OfertaAtendimento is not { } oferta)
        {
            return NaoConstruido.DeepClone().AsObject();
        }

        return new JsonObject
        {
            ["condicoes"] = new JsonArray([.. oferta.Condicoes.Select(static c => (JsonNode)new JsonObject
            {
                ["condicaoOrigemId"] = c.CondicaoOrigemId,
                ["condicaoCodigo"] = HashCanonicalComputer.NormalizeNfc(c.CondicaoCodigo),
                ["condicaoNome"] = HashCanonicalComputer.NormalizeNfc(c.CondicaoNome),
            })]),
            ["recursos"] = new JsonArray([.. oferta.Recursos.Select(static r => (JsonNode)new JsonObject
            {
                ["recursoOrigemId"] = r.RecursoOrigemId,
                ["recursoNome"] = HashCanonicalComputer.NormalizeNfc(r.RecursoNome),
            })]),
            ["tiposDeficiencia"] = new JsonArray([.. oferta.TiposDeficiencia.Select(static t => (JsonNode)new JsonObject
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
        foreach (CriterioDesempate criterio in processo.CriteriosDesempate)
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
            ["fato"] = HashCanonicalComputer.NormalizeNfc(predicadoFato.Fato),
            ["operador"] = HashCanonicalComputer.NormalizeNfc(predicadoFato.Operador),
            ["valor"] = HashCanonicalComputer.NormalizeNfc(predicadoFato.Valor),
        },
        _ => throw new InvalidOperationException($"Variante de {nameof(ArgsCriterioDesempate)} não reconhecida: {args.GetType()}."),
    };

    private static JsonObject SerializarClassificacao(ProcessoSeletivo processo)
    {
        if (processo.Classificacao is not { } classificacao)
        {
            return NaoConstruido.DeepClone().AsObject();
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
            ["regrasEliminacao"] = new JsonArray([.. classificacao.RegrasEliminacao.Select(static r => (JsonNode)new JsonObject
            {
                ["regra"] = SerializarReferenciaRegra(r.Regra),
                ["args"] = SerializarArgsRegraEliminacao(r.Args),
            })]),
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

    private static JsonObject SerializarReferenciaRegra(ReferenciaRegra regra) => new()
    {
        ["codigo"] = regra.Codigo,
        ["versao"] = regra.Versao,
        ["hash"] = regra.Hash,
    };
}
