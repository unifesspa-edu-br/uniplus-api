namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Implementação da projeção canônica do envelope de congelamento (ADR-0100,
/// ADR-0109). Projeta a configuração viva do agregado num payload de 23 chaves
/// — <b>19 blocos reais + 4 stubs</b> <c>{"status":"nao_construido"}</c> para as
/// dimensões que a Feature #40 ainda não implementou (ADR-0100 item 10) — e
/// devolve os bytes via <see cref="PerfilCanonicoV1"/>.
/// <c>documentosExigidos</c> (Story #853) é um dos 14: já carrega
/// <c>obrigatoriedades[]</c> real, com <c>exigencias[]</c> (#554) ainda
/// aninhada como stub — o bloco nasce parcialmente real. <c>vagas</c> (issue
/// #848/ADR-0115) é outro: o quadro por oferta, calculado no ramo federal ou
/// fixado no institucional, sempre materializado junto da configuração.
/// <c>arvoreSatisfacao</c> (Story #923) é o mais novo: a topologia da árvore de
/// satisfação de <c>documentosExigidos</c>, sempre materializada (0..* raízes).
/// </summary>
/// <remarks>
/// <para>
/// Todo campo string de negócio passa por <see cref="HashCanonicalComputer.NormalizeNfc"/>;
/// todo decimal por <see cref="HashCanonicalComputer.SerializeDecimalCanonical"/>
/// com a escala declarada do campo; instantes por
/// <see cref="HashCanonicalComputer.SerializeInstantCanonical"/>. A
/// reordenação de chaves e a serialização byte-estável final acontecem no
/// <see cref="IPerfilCanonico"/> — este tipo só monta o <see cref="JsonObject"/>
/// com os valores já normalizados. A normalização NFC é responsabilidade daqui,
/// não do perfil: o perfil serializa o nó que recebe, e uma sequência combinante
/// que chegasse até ele produziria bytes distintos dos da forma pré-composta.
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
/// do próprio item, comparados como bytes
/// (<see cref="ComparadorLexicograficoDeBytes"/>), não como texto decodificado.
/// Ordenar por <c>EntityBase.Id</c> era determinístico entre
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
    /// <remarks>
    /// Story #919 (RN08): <c>documentosExigidos</c> ganha a sub-chave
    /// <c>metadadosFatos</c> (<see cref="SerializarMetadadosFatos"/>). O bump para
    /// <c>1.3</c> também é o primeiro <c>schema_version</c> a refletir corretamente
    /// <c>formatosPermitidos</c> (Story #918, PR anterior) — que ficou sem bump próprio;
    /// não se retrabalha aquela PR, só se reconhece que "1.3" é a forma real de hoje.
    /// Story #923 (snapshot conjunto final, PR 4/4 da change
    /// <c>documentos-exigidos-composicao</c>): o bump para <c>1.4</c> acrescenta o 14º
    /// bloco de topo, <c>arvoreSatisfacao</c> — a TOPOLOGIA da árvore de satisfação
    /// (<see cref="Domain.Entities.NoExigencia"/>, Stories #920/#921/#922), até aqui
    /// fail-closed na publicação (<see cref="Domain.Entities.ProcessoSeletivo.PendenciaDaArvoreDeSatisfacaoAindaNaoPublicavel"/>,
    /// removido nesta Story) por não ter onde congelar. <c>documentosExigidos.exigencias</c>
    /// continua sendo a config POR-exigência (folha); <c>arvoreSatisfacao</c> é a
    /// TOPOLOGIA (grupos E/OU, cardinalidade de grupo, repetição por entidade) — cada
    /// folha da árvore referencia sua exigência pelo mesmo <c>exigenciaId</c> já congelado
    /// em <c>documentosExigidos.exigencias[].exigenciaId</c>, sem duplicar conteúdo.
    /// </remarks>
    internal const string SchemaVersionAtual = "0.0.2";

    /// <summary>
    /// Perfil de bytes sob o qual a emissão de hoje congela — as regras de ordenação, escape e
    /// digest, identificadas à parte da <see cref="SchemaVersionAtual"/>. O rótulo gravado em
    /// <c>versao_configuracao.algoritmo_hash</c> vem daqui, do mesmo objeto que produz os
    /// bytes: literal e código andando juntos por construção, não por disciplina.
    /// </summary>
    private static readonly PerfilCanonicoV1 PerfilAtual = PerfilCanonicoV1.Instancia;

    private const int EscalaPadrao = 4;
    private const int EscalaPercentual = 2;

    /// <summary>
    /// Os 4 blocos que ainda não têm dono (ADR-0109 D8): <c>formulario</c>,
    /// <c>cascataRemanejamento</c>, <c>divulgacao</c>, <c>identidadesUnidade</c>
    /// — mais a sub-chave <c>exigencias</c> dentro de <c>documentosExigidos</c>
    /// (#554). <c>vagas</c> saiu daqui na issue #848 — virou bloco real, sempre
    /// materializado junto da configuração (ADR-0115). Um bloco de topo
    /// <b>real</b> nunca emite este literal na raiz: se a dimensão é
    /// obrigatória, a ausência é pendência de conformidade e o gate recusa
    /// antes de canonicalizar.
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
            ["vagas"] = SerializarVagas(processo),
            ["distribuicao"] = SerializarDistribuicao(processo),
            ["modalidades"] = SerializarModalidades(processo),
            ["ofertas"] = SerializarOfertas(processo),
            ["atendimento"] = SerializarAtendimento(processo),
            ["bonusRegional"] = SerializarBonusRegional(processo),
            ["criteriosDesempate"] = SerializarCriteriosDesempate(processo),
            ["classificacao"] = SerializarClassificacao(processo),
            ["hashesEdital"] = SerializarHashesEdital(dados, entrada.HashDocumento),
            ["documentosExigidos"] = SerializarDocumentosExigidos(processo, entrada.Conformidade, entrada.MetadadosFatosCongelados),
            ["arvoreSatisfacao"] = SerializarArvoreSatisfacao(processo),
            ["formulario"] = NaoConstruido.DeepClone(),
            ["cascataRemanejamento"] = NaoConstruido.DeepClone(),
            ["divulgacao"] = NaoConstruido.DeepClone(),
            ["cronogramaFases"] = SerializarCronogramaFases(processo),
            ["identidadesUnidade"] = NaoConstruido.DeepClone(),
            ["fatosColetados"] = SerializarFatosColetados(processo.FatosColetados),
            ["regrasDerivacao"] = SerializarRegrasDerivacao(processo.RegrasDerivacao),
            ["grafoDependencia"] = SerializarGrafoDependencia(processo),
            ["versaoInterpretador"] = MotorDerivacao.VersaoSemantica,
            ["modalidadesOfertadas"] = SerializarModalidadesOfertadas(processo.DistribuicaoVagas),
        };

        // ADR-0101: a retificação ACRESCENTA um 24º bloco preservando os 23
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

        byte[] bytes = PerfilAtual.Serializar(payload);
        return new SnapshotCanonico(bytes, SchemaVersionAtual, PerfilAtual.Algoritmo);
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
                ["regraAjuste"] = configuracao.RegraAjuste is { } regraAjuste ? SerializarReferenciaRegra(regraAjuste) : null,
                ["referenciaDemografica"] = configuracao.ReferenciaDemografica is { } referencia
                    ? SerializarReferenciaDemografica(referencia)
                    : null,
            });
        }

        return array;
    }

    /// <summary>
    /// O quadro de vagas (issue #848/ADR-0115) — output derivado, congelado
    /// separadamente dos insumos (<see cref="SerializarDistribuicao"/>) para
    /// que a prova de reprodutibilidade não seja tautológica: recomputa-se o
    /// quadro a partir dos insumos congelados e compara-se a este bloco.
    /// </summary>
    private static JsonArray SerializarVagas(ProcessoSeletivo processo)
    {
        JsonArray array = [];
        foreach (ConfiguracaoDistribuicaoVagas configuracao in OrdenarPorOfertaCursoOrigemId(processo.DistribuicaoVagas))
        {
            JsonArray quadro = [];
            foreach (VagaOfertada vaga in configuracao.VagasOfertadas.OrderBy(static v => v.ModalidadeCodigo, StringComparer.Ordinal))
            {
                quadro.Add(new JsonObject
                {
                    ["modalidadeCodigo"] = HashCanonicalComputer.NormalizeNfc(vaga.ModalidadeCodigo),
                    ["quantidade"] = vaga.Quantidade,
                });
            }

            array.Add(new JsonObject
            {
                ["ofertaCursoOrigemId"] = configuracao.OfertaCursoOrigemId,
                ["quadro"] = quadro,
                ["vrNominal"] = configuracao.VrNominal,
                ["vrFinal"] = configuracao.VrFinal,
                ["estouro"] = configuracao.Estouro,
                ["capadoEmVo"] = configuracao.CapadoEmVo,
                ["totalPublicado"] = configuracao.TotalPublicado,
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
                    ["quantidadeDeclarada"] = modalidade.QuantidadeDeclarada,
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
        // Um bloco REAL nunca emite `nao_construido` (ADR-0100 item 10, decisão D8).
        // O atendimento é dimensão obrigatória: a sua ausência é pendência de
        // conformidade, e o gate (ProcessoSeletivo.PendenciaDeConformidade) recusa a
        // transição antes de a canonicalização acontecer. Chegar aqui sem oferta é
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
        // Mesma regra de SerializarAtendimento acima (nenhum bloco real vira stub). A
        // classificação é o bloco que determina o resultado do certame; congelá-la como
        // `nao_construido` num documento juridicamente vinculante é o pior modo de falha
        // do envelope.
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
            // RegrasEliminacao não tem chave de negócio única (cardinalidade
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
    /// Story #554 (PR #903, bump 1.2): <c>exigencias</c> deixa de ser stub — cada
    /// <see cref="DocumentoExigido"/> viva do processo vira um item rico (CA-09: identidade
    /// estável por <c>exigenciaId</c>). Duas chaves-irmãs novas (B-03):
    /// <c>referenciaTemporalFatos</c> — a POLÍTICA crua (<see cref="ValueObjects.ReferenciaTemporalFatos"/>,
    /// o INSUMO) — e <c>dataReferenciaFatos</c> — a <see cref="DateOnly"/> já resolvida a
    /// partir dela (o OUTPUT). Mesmo padrão de <c>distribuicao</c>/<c>vagas</c>: congelar o
    /// insumo ao lado do output derivado é o que torna a prova de reprodutibilidade
    /// NÃO-tautológica — reidratar recompõe a política, <see cref="Entities.ProcessoSeletivo.ResolverDataReferenciaFatos"/>
    /// recalcula o output a partir dela, e o round-trip compara os bytes.
    /// </summary>
    private static JsonObject SerializarDocumentosExigidos(
        ProcessoSeletivo processo,
        ResultadoConformidade? conformidade,
        IReadOnlyDictionary<string, MetadadoFatoCongelado>? metadadosFatosCongelados) => new()
        {
            ["exigencias"] = SerializarExigencias(processo.DocumentosExigidos),
            ["obrigatoriedades"] = SerializarObrigatoriedades(conformidade),
            ["referenciaTemporalFatos"] = processo.ReferenciaTemporalFatos is { } referencia ? new JsonObject
            {
                ["tipo"] = referencia.Tipo.ToCodigo(),
                ["data"] = referencia.Data?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["faseId"] = referencia.FaseId,
            }
            : null,
            ["dataReferenciaFatos"] = processo.ResolverDataReferenciaFatos() is { } data
            ? data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null,
            ["metadadosFatos"] = SerializarMetadadosFatos(metadadosFatosCongelados),
        };

    /// <summary>
    /// Story #919 (RN08): congela o metadado — domínio, origem, cardinalidade, ponto de
    /// resolução, binding e o(s) conjunto(s) de valores — de cada fato do candidato citado
    /// em alguma <see cref="CondicaoGatilho"/> de alguma <see cref="DocumentoExigido"/> do
    /// processo. Bloco IRMÃO de <c>exigencias</c>/<c>obrigatoriedades</c>/
    /// <c>referenciaTemporalFatos</c> dentro de <c>documentosExigidos</c> — array SEMPRE
    /// presente (nunca <c>nao_construido</c>, D9), vazio quando nenhuma condição existe.
    /// </summary>
    /// <remarks>
    /// <para>
    /// O canonicalizador não deriva este dicionário de <c>processo.DocumentosExigidos</c> —
    /// ele apenas SERIALIZA o que o handler já resolveu via <c>IFatoCandidatoReader</c>
    /// (ADR-0042, mesmo tratamento que <see cref="SerializarObrigatoriedades"/> recebe para
    /// <c>Conformidade</c>). A garantia de que todo fato referenciado tem metadado
    /// resolvido — "sem faltante" — é do HANDLER, antes de canonicalizar: a projeção pura
    /// não tem I/O para revalidar isso contra o catálogo vivo.
    /// </para>
    /// <para>
    /// A chave do fato é <c>fatoCodigo</c> — não <c>codigo</c> — e a do valor declarado é
    /// <c>valorCodigo</c>: <c>EnvelopeCanonicoGoldenTests.Envelope_ReferenciasDeRegraSaoTripla</c>
    /// trata qualquer objeto com a chave BARE <c>codigo</c> (sem <c>naturezaLegal</c>/<c>ordem</c>)
    /// como candidato a referência de regra, exigindo a tripla <c>{codigo, versao, hash}</c> —
    /// um metadado de fato não é uma referência de regra, e usar a chave qualificada evita a
    /// falsa colisão (mesma convenção de <c>tipoDocumentoCodigo</c>/<c>modalidadeCodigo</c>).
    /// </para>
    /// </remarks>
    private static JsonArray SerializarMetadadosFatos(IReadOnlyDictionary<string, MetadadoFatoCongelado>? metadadosFatosCongelados)
    {
        if (metadadosFatosCongelados is null || metadadosFatosCongelados.Count == 0)
        {
            return [];
        }

        JsonArray array = [];
        foreach (MetadadoFatoCongelado metadado in metadadosFatosCongelados.Values.OrderBy(static m => m.Codigo, StringComparer.Ordinal))
        {
            array.Add(new JsonObject
            {
                ["fatoCodigo"] = HashCanonicalComputer.NormalizeNfc(metadado.Codigo),
                ["dominio"] = HashCanonicalComputer.NormalizeNfc(metadado.Dominio),
                ["origem"] = HashCanonicalComputer.NormalizeNfc(metadado.Origem),
                ["cardinalidade"] = HashCanonicalComputer.NormalizeNfc(metadado.Cardinalidade),
                ["pontoResolucao"] = HashCanonicalComputer.NormalizeNfc(metadado.PontoResolucao),
                ["binding"] = HashCanonicalComputer.NormalizeNfc(metadado.Binding),
                ["valoresDominio"] = metadado.ValoresDominio is { } valores
                    ? new JsonArray([.. valores.Select(static v => JsonValue.Create(HashCanonicalComputer.NormalizeNfc(v)))])
                    : null,
                ["valoresDominioDeclarados"] = metadado.ValoresDominioDeclarados is { } declarados
                    ? new JsonArray([.. declarados.Select(static d => (JsonNode)new JsonObject
                    {
                        ["valorCodigo"] = HashCanonicalComputer.NormalizeNfc(d.Codigo),
                        ["descricao"] = d.Descricao is { } descricao ? HashCanonicalComputer.NormalizeNfc(descricao) : null,
                    })])
                    : null,
            });
        }

        return array;
    }

    /// <summary>
    /// Story #923 — a TOPOLOGIA da árvore de satisfação (<see cref="NoExigencia"/>, Stories
    /// #920/#921/#922): raízes ordenadas por <see cref="NoExigencia.Ordem"/> — determinístico
    /// sem chave de conteúdo (D9 não se aplica aqui, ao contrário de <c>exigencias</c>/
    /// <c>regrasEliminacao</c>): <c>ux_nos_exigencia_raiz_ordem</c>/<c>ux_nos_exigencia_irmaos_ordem</c>
    /// (unique index, <c>NoExigenciaConfiguration</c>) já garantem <c>Ordem</c> única entre
    /// raízes e entre irmãos, então não há empate possível a resolver por conteúdo.
    /// </summary>
    private static JsonArray SerializarArvoreSatisfacao(ProcessoSeletivo processo) =>
        new([.. processo.RaizesDeExigencia.OrderBy(static r => r.Ordem).Select(static r => (JsonNode)SerializarNo(r))]);

    /// <summary>
    /// Um nó, recursivamente — mesmo formato (campos e nomes) de <c>NoExigenciaDto</c>
    /// (<c>ObterProcessoSeletivoQueryHandler.ProjectNoExigencia</c>), inclusive o token de
    /// <see cref="TipoNo"/> ("FOLHA"/"E"/"OU"). <see cref="NoExigencia.DocumentoExigidoId"/>
    /// (só em folha) referencia o MESMO <c>exigenciaId</c> já congelado em
    /// <c>documentosExigidos.exigencias[]</c> — não duplica o conteúdo da exigência aqui.
    /// Todo campo é emitido sempre, com <see langword="null"/> explícito onde não se aplica
    /// ao tipo do nó — nunca omitido (o envelope preserva a diferença entre "null" e "chave
    /// ausente", ADR-0109 D4).
    /// </summary>
    private static JsonObject SerializarNo(NoExigencia no) => new()
    {
        ["id"] = no.Id,
        ["ordem"] = no.Ordem,
        ["tipo"] = no.Tipo.ToCodigo(),
        ["exigenciaId"] = no.Tipo == TipoNo.Folha ? no.DocumentoExigidoId : null,
        ["quantidadeMinima"] = no.QuantidadeMinima,
        ["consequencia"] = no.Consequencia is { } consequencia ? HashCanonicalComputer.NormalizeNfc(consequencia) : null,
        ["basesLegais"] = SerializarBasesLegaisDeNo(no.BasesLegaisResolvidas()),
        ["chaveDistincao"] = no.ChaveDistincao?.ToCodigo(),
        ["dataReferencia"] = no.DataReferencia?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["ocorrenciasEsperadas"] = no.OcorrenciasEsperadas is { } ocorrencias
            ? new JsonArray([.. ocorrencias.Select(static o => JsonValue.Create(HashCanonicalComputer.NormalizeNfc(o)))])
            : null,
        ["repetePorEntidade"] = no.RepetePorEntidade?.ToCodigo(),
        ["filhos"] = new JsonArray([.. no.Filhos.OrderBy(static f => f.Ordem).Select(static f => (JsonNode)SerializarNo(f))]),
    };

    /// <summary>
    /// Base legal PRÓPRIA de um grupo <see cref="TipoNo.GrupoOu"/> — mesmo shape de
    /// <see cref="SerializarBasesLegais"/> (a de <see cref="DocumentoExigido"/>), tipo
    /// diferente (<see cref="NoExigenciaBaseLegal"/>): mesma razão de duas classes
    /// concretas separadas em vez de herança (ver <see cref="NoExigencia"/>).
    /// </summary>
    private static JsonArray SerializarBasesLegaisDeNo(IEnumerable<NoExigenciaBaseLegal> basesLegais) =>
        OrdenarPorConteudo(basesLegais.Select(static b => new JsonObject
        {
            ["referencia"] = HashCanonicalComputer.NormalizeNfc(b.Referencia),
            ["abrangencia"] = b.Abrangencia.ToCodigo(),
            ["status"] = b.Status.ToCodigo(),
            ["observacao"] = b.Observacao is { } observacao ? HashCanonicalComputer.NormalizeNfc(observacao) : null,
        }));

    /// <summary>
    /// <see cref="DocumentoExigido"/> não tem chave de negócio única (nada impede duas
    /// exigências para a mesma fase e o mesmo tipo de documento, com gatilhos distintos).
    /// Ordena pela chave de negócio PARCIAL (fase + tipo de documento — o caso comum, sem
    /// empate) e, no raro caso de empate, pela chave de conteúdo do restante do item —
    /// mesma regra de desempate por conteúdo (ADR-0109 D9) — (nunca por
    /// <c>exigenciaId</c> — um Guid v7 novo a cada <c>DefinirDocumentosExigidos</c>
    /// tornaria a ordem não-determinística entre configurações equivalentes, o mesmo
    /// problema que a chave de conteúdo evita em <see cref="OrdenarPorConteudo"/>).
    /// </summary>
    private static JsonArray SerializarExigencias(IReadOnlyCollection<DocumentoExigido> exigencias)
    {
        IOrderedEnumerable<DocumentoExigido> ordenadas = exigencias
            .OrderBy(static e => e.ExigidoNaFaseId)
            .ThenBy(static e => e.TipoDocumentoOrigemId)
            .ThenBy(
                static e => PerfilAtual.Serializar(SerializarExigenciaSemIdentidade(e)),
                ComparadorLexicograficoDeBytes.Instancia)
            // Achado de revisão (Story #554, PR #903): duas exigências byte-idênticas em
            // todo o resto (mesma fase, mesmo tipo, mesmo conteúdo) empatam na chave de
            // conteúdo acima — e, ao contrário de regrasEliminacao (sem identidade), o Id
            // aqui É congelado no envelope (exigenciaId, CA-09), então usá-lo como
            // desempate FINAL não fere D9: não afeta a ordem de exigências
            // GENUINAMENTE distintas (a chave de conteúdo já as discrimina), só torna
            // determinística a ordem do caso raro de duplicata verdadeira — sem isso, a
            // ordem de materialização do EF (sem ORDER BY no Include) poderia produzir
            // bytes diferentes para a MESMA configuração persistida entre leituras.
            .ThenBy(static e => e.Id);

        return new JsonArray([.. ordenadas.Select(static e =>
        {
            JsonObject item = SerializarExigenciaSemIdentidade(e);
            item.Insert(0, "exigenciaId", JsonValue.Create(e.Id));
            return (JsonNode)item;
        })]);
    }

    private static JsonObject SerializarExigenciaSemIdentidade(DocumentoExigido exigencia) => new()
    {
        ["tipoDocumentoOrigemId"] = exigencia.TipoDocumentoOrigemId,
        ["tipoDocumentoCodigo"] = HashCanonicalComputer.NormalizeNfc(exigencia.TipoDocumentoCodigo),
        ["tipoDocumentoNome"] = HashCanonicalComputer.NormalizeNfc(exigencia.TipoDocumentoNome),
        ["tipoDocumentoCategoria"] = HashCanonicalComputer.NormalizeNfc(exigencia.TipoDocumentoCategoria),
        ["exigidoNaFaseId"] = exigencia.ExigidoNaFaseId,
        ["aplicabilidade"] = exigencia.Aplicabilidade.ToString(),
        ["obrigatorio"] = exigencia.Obrigatorio,
        ["consequenciaIndeferimento"] = exigencia.ConsequenciaIndeferimento is { } consequencia
            ? HashCanonicalComputer.NormalizeNfc(consequencia)
            : null,
        ["grupoSatisfacaoId"] = exigencia.GrupoSatisfacaoId,
        ["condicaoGatilho"] = SerializarCondicaoGatilho(exigencia.Condicoes),
        ["basesLegais"] = SerializarBasesLegais(exigencia.BasesLegaisResolvidas()),
        ["idadeMaximaEmissao"] = exigencia.IdadeMaximaEmissao is { } idade ? SerializarIdadeMaximaEmissao(idade) : null,
        ["formatosPermitidos"] = SerializarFormatosPermitidos(exigencia.FormatosPermitidos),
        ["tamanhoMaximoBytes"] = exigencia.TamanhoMaximoBytes,
    };

    /// <summary>
    /// <see cref="FormatosPermitidos"/> (Story #918) substitui o campo singular
    /// <c>formatoPermitido</c> — objeto sempre presente (o VO é obrigatório), com
    /// <c>lista</c> nula ⟺ <c>qualquer</c> verdadeiro.
    /// </summary>
    private static JsonObject SerializarFormatosPermitidos(FormatosPermitidos formatosPermitidos) => new()
    {
        ["qualquer"] = formatosPermitidos.Qualquer,
        ["lista"] = formatosPermitidos.Lista is { } lista
            ? new JsonArray([.. lista.Select(static e => new JsonObject
            {
                ["formato"] = e.Formato.ToCodigo(),
                ["tamanhoMaximoBytesMax"] = e.TamanhoMaximoBytesMax,
            })])
            : null,
    };

    /// <summary>
    /// O predicado DNF (PR #896, ADR-0111): OU de cláusulas, E de condições dentro de cada
    /// uma. Cláusulas ordenadas por <c>Clausula</c> (ordinal semântico — o mesmo que
    /// <see cref="ValueObjects.PredicadoDnf.CriarDeCondicoesAgrupadas"/> usa para agrupar);
    /// condições dentro da MESMA cláusula não têm ordinal próprio, então usam a chave de
    /// conteúdo (D9), igual às demais coleções sem chave natural.
    /// </summary>
    private static JsonArray? SerializarCondicaoGatilho(IReadOnlyCollection<CondicaoGatilho> condicoes)
    {
        if (condicoes.Count == 0)
        {
            return null;
        }

        JsonArray clausulas = [];
        foreach (IGrouping<int, CondicaoGatilho> clausula in condicoes.GroupBy(static c => c.Clausula).OrderBy(static g => g.Key))
        {
            clausulas.Add(OrdenarPorConteudo(clausula.Select(static c => new JsonObject
            {
                ["fato"] = HashCanonicalComputer.NormalizeNfc(c.Fato),
                ["operador"] = c.Operador.ToCodigo(),
                ["valor"] = JsonNode.Parse(c.Valor.GetRawText()),
            })));
        }

        return clausulas;
    }

    /// <summary>
    /// Só bases legais <c>RESOLVIDO</c> (PR #898, issue #549) — uma <c>PENDENTE</c> não é
    /// evidência jurídica ainda, e o gate de publicação (<c>ValidadorBaseLegalExigencias</c>)
    /// já provou que existe ao menos uma resolvida por exigência que determina resultado
    /// antes deste ponto.
    /// </summary>
    private static JsonArray SerializarBasesLegais(IEnumerable<DocumentoExigidoBaseLegal> basesLegais) =>
        OrdenarPorConteudo(basesLegais.Select(static b => new JsonObject
        {
            ["referencia"] = HashCanonicalComputer.NormalizeNfc(b.Referencia),
            // Wire format canônico (ToCodigo/FromCodigo, não ToString — convenção do
            // repo para enums de comando/envelope estabelecida a partir da PR #898/PR #900,
            // que criaram TipoAbrangenciaCodigo/StatusBaseLegalCodigo/etc. como fonte
            // única do token; a exigencias[] é a primeira consumidora deles no envelope).
            ["abrangencia"] = b.Abrangencia.ToCodigo(),
            // Sempre RESOLVIDO — só bases resolvidas chegam aqui (BasesLegaisResolvidas()
            // já filtrou). Mantido explícito por paridade estrutural, mesmo raciocínio de
            // "aprovada" em obrigatoriedades[]: o campo não pode vir diferente num
            // snapshot real, mas omiti-lo esconderia essa garantia do próprio documento.
            ["status"] = b.Status.ToCodigo(),
            ["observacao"] = b.Observacao is { } observacao ? HashCanonicalComputer.NormalizeNfc(observacao) : null,
        }));

    private static JsonObject SerializarIdadeMaximaEmissao(IdadeMaximaEmissao idade) => new()
    {
        ["valor"] = idade.Valor,
        ["unidade"] = idade.Unidade.ToCodigo(),
        ["referenciaTipo"] = idade.ReferenciaTipo.ToCodigo(),
        ["data"] = idade.Data?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["referenciaFaseId"] = idade.ReferenciaFaseId,
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
                // Story #554 (PR #903, bump 1.2), achado de revisão: id congelado para que
                // exigidoNaFaseId/referenciaTemporalFatos.faseId resolvam mesmo quando a
                // sombra de verificação (RestauradorDeConfiguracao) reidrata sem nenhuma
                // fase viva rastreada para reconciliar por Ordem. Só a 1.2 escreve esta
                // chave — o encoder 1.1 congelado (EnvelopeCodecV11) não a tinha.
                ["id"] = fase.Id,
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
            static item => PerfilAtual.Serializar(item),
            ComparadorLexicograficoDeBytes.Instancia);

        return new JsonArray([.. ordenados.Select(static item => (JsonNode)item)]);
    }

    private static JsonObject SerializarReferenciaRegra(ReferenciaRegra regra) => new()
    {
        ["codigo"] = regra.Codigo,
        ["versao"] = regra.Versao,
        ["hash"] = regra.Hash,
    };

    /// <summary>
    /// Os fatos que o processo coleta do candidato (Story #928, §7.4), ordenados pela <c>Ordem</c>
    /// de coleta (total e única — a mesma que dá sentido a "fato anterior"). A pré-condição de cada
    /// fato é a mesma forma DNF de <see cref="SerializarCondicaoGatilho"/> — <c>null</c> quando o
    /// fato é coletado incondicionalmente.
    /// </summary>
    internal static JsonArray SerializarFatosColetados(IEnumerable<FatoColetado> fatos)
    {
        JsonArray array = [];
        foreach (FatoColetado fato in fatos.OrderBy(static f => f.Ordem))
        {
            array.Add(new JsonObject
            {
                ["fatoCodigo"] = HashCanonicalComputer.NormalizeNfc(fato.FatoCodigo),
                ["ordem"] = fato.Ordem,
                ["precondicao"] = SerializarDnf(fato.Precondicoes.Select(
                    static c => (c.Clausula, c.Fato, c.Operador, c.Valor))),
            });
        }

        return array;
    }

    /// <summary>
    /// As regras de derivação dos fatos derivados do processo (Story #928, §7.4), ordenadas pelo
    /// <c>codigoFato</c> derivado (chave de negócio única no processo). As regras de cada fato saem
    /// pela sua <c>Ordem</c> (total e única na configuração); a regra âncora (incondicional) tem
    /// <c>quando</c> nulo — nunca um predicado vazio, que avaliaria falso.
    /// </summary>
    internal static JsonArray SerializarRegrasDerivacao(IEnumerable<ConfiguracaoDerivacaoFato> regrasDerivacao)
    {
        JsonArray array = [];
        foreach (ConfiguracaoDerivacaoFato config in regrasDerivacao.OrderBy(static c => c.CodigoFato, StringComparer.Ordinal))
        {
            JsonArray regras = [];
            foreach (RegraDerivacaoConfigurada regra in config.Regras.OrderBy(static r => r.Ordem))
            {
                regras.Add(new JsonObject
                {
                    ["ordem"] = regra.Ordem,
                    ["contribui"] = HashCanonicalComputer.NormalizeNfc(regra.Contribui),
                    ["quando"] = SerializarDnf(regra.Condicoes.Select(
                        static c => (c.Clausula, c.Fato, c.Operador, c.Valor))),
                });
            }

            array.Add(new JsonObject
            {
                ["codigoFato"] = HashCanonicalComputer.NormalizeNfc(config.CodigoFato),
                ["regras"] = regras,
            });
        }

        return array;
    }

    /// <summary>
    /// Um predicado DNF <c>{fato, operador, valor}</c> na mesma forma que
    /// <see cref="SerializarCondicaoGatilho"/> — OU de cláusulas, E de condições dentro de cada uma:
    /// cláusulas ordenadas por <c>Clausula</c>, condições dentro da cláusula pela chave de conteúdo
    /// (não têm ordinal próprio). <see langword="null"/> quando não há condição nenhuma.
    /// </summary>
    private static JsonArray? SerializarDnf(
        IEnumerable<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> condicoes)
    {
        List<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> lista = [.. condicoes];
        if (lista.Count == 0)
        {
            return null;
        }

        JsonArray clausulas = [];
        foreach (IGrouping<int, (int Clausula, string Fato, Operador Operador, JsonElement Valor)> clausula
            in lista.GroupBy(static c => c.Clausula).OrderBy(static g => g.Key))
        {
            clausulas.Add(OrdenarPorConteudo(clausula.Select(static c => new JsonObject
            {
                ["fato"] = HashCanonicalComputer.NormalizeNfc(c.Fato),
                ["operador"] = c.Operador.ToCodigo(),
                ["valor"] = JsonNode.Parse(c.Valor.GetRawText()),
            })));
        }

        return clausulas;
    }

    /// <summary>
    /// O grafo de dependência conjunto (Story #928, §6/§7.4) congelado como testemunho: nós, arestas
    /// e ordem topológica total. É recomputado da configuração viva (<see cref="ProcessoSeletivo.ConstruirGrafoDependencia"/>);
    /// um ciclo é invariante quebrada — o gate de publicação já o recusou antes de canonicalizar
    /// (mesma disciplina de <see cref="SerializarAtendimento"/>: nunca se congela um grafo cíclico
    /// em silêncio).
    /// </summary>
    private static JsonObject SerializarGrafoDependencia(ProcessoSeletivo processo)
    {
        Result<GrafoDependenciaConjunta> grafo = processo.ConstruirGrafoDependencia();
        if (grafo.IsFailure)
        {
            throw new InvalidOperationException(
                "Canonicalização de processo cujo grafo de dependência conjunto forma um ciclo — o gate de "
                + $"publicação deveria tê-lo recusado antes deste ponto: {grafo.Error!.Message}");
        }

        return SerializarGrafoDependencia(grafo.Value!);
    }

    /// <summary>
    /// A projeção pura do grafo conjunto num <see cref="JsonObject"/> — reusada pelo decoder para
    /// recomputar o testemunho a partir das partes reidratadas e comparar bytes com o congelado. Os
    /// nós, arestas e a ordem topológica saem na ordem canônica que <see cref="GrafoDependenciaConjunta"/>
    /// já devolve (por ordem efetiva de coleta, depois <c>(Classe, Codigo)</c>) — não se reordena aqui.
    /// Cada nó é <c>tipoDeNo/codigo</c> — a identidade escopada ao processo <b>por construção</b> (o
    /// envelope pertence a um único processo, <c>VersaoConfiguracao.ProcessoSeletivoId</c>), sem repetir
    /// o Id da raiz em cada nó.
    /// </summary>
    internal static JsonObject SerializarGrafoDependencia(GrafoDependenciaConjunta grafo) => new()
    {
        ["nos"] = new JsonArray([.. grafo.Nos.Select(no => (JsonNode)new JsonObject
        {
            ["idCanonico"] = RotuloCanonico(no),
        })]),
        ["arestas"] = new JsonArray([.. grafo.Arestas.Select(aresta => (JsonNode)new JsonObject
        {
            ["tipo"] = aresta.Tipo.ToCodigo(),
            ["origem"] = RotuloCanonico(aresta.Origem),
            ["destino"] = RotuloCanonico(aresta.Destino),
        })]),
        ["ordemTopologica"] = new JsonArray(
            [.. grafo.OrdemTopologica.Select(no => (JsonNode?)JsonValue.Create(RotuloCanonico(no)))]),
    };

    private static string RotuloCanonico(NoGrafoDependencia no) => $"{no.Classe.ToCodigo()}/{no.Codigo}";

    /// <summary>
    /// O conjunto de códigos de modalidade ofertados pelo processo (Story #928, §7.4) — o domínio
    /// usado na interseção do fato derivado <c>MODALIDADE</c>. Normalizado NFC antes do <c>Distinct</c>
    /// e ordenado ordinal (para códigos ASCII atuais coincide, mas fecha o contrato declarado).
    /// </summary>
    internal static JsonArray SerializarModalidadesOfertadas(IEnumerable<ConfiguracaoDistribuicaoVagas> distribuicao)
    {
        IEnumerable<string> codigos = distribuicao
            .SelectMany(static d => d.Modalidades)
            .Select(static m => HashCanonicalComputer.NormalizeNfc(m.Codigo))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static c => c, StringComparer.Ordinal);

        return new JsonArray([.. codigos.Select(static c => (JsonNode?)JsonValue.Create(c))]);
    }
}
