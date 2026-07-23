namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Globalization;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Codec da versão <c>1.3</c> do envelope (Story #919, RN08, ADR-0109 D1): acrescenta o
/// bloco <c>documentosExigidos.metadadosFatos</c> — o metadado (domínio, cardinalidade,
/// origem, binding, ponto de resolução, descrições por valor) de cada fato do candidato
/// citado em alguma condição de gatilho, congelado ao lado da condição bruta
/// <c>{fato, operador, valor}</c> que já era congelada desde a 1.2.
/// </summary>
/// <remarks>
/// <para>
/// <b>Encoder congelado (Story #923, bump para 1.4 — ADR-0109 D1):</b> até aqui, este
/// método delegava a <see cref="SnapshotPublicacaoCanonicalizer"/>, que era "o
/// canonicalizador de hoje". Com o bump para 1.4 (acréscimo do bloco
/// <c>arvoreSatisfacao</c>), <see cref="SnapshotPublicacaoCanonicalizer"/> passou a
/// emitir 1.4 — é ele quem os handlers de escrita injetam como o encoder vivo. Este
/// método é agora a ÚNICA fonte de verdade de como um envelope 1.3 é produzido: uma
/// cópia autossuficiente do que o canonicalizador emitia neste instante, para que o
/// round-trip das versões 1.3 já publicadas continue verificável para sempre, imune a
/// qualquer refactor futuro do canonicalizador vivo — exatamente o que aconteceu com
/// <see cref="EnvelopeCodecV12"/> no bump anterior (1.2 → 1.3).
/// </para>
/// <para>
/// O decoder reaproveita os métodos <c>internal</c> de <see cref="EnvelopeCodecV11"/>
/// (via <see cref="EnvelopeCodecV12"/>, que já os reaproveita) para os 11 blocos cuja
/// FORMA não mudou desde a 1.1 — <c>documentosExigidos</c> (o bloco cuja forma muda
/// nesta versão) ganha um leitor próprio aqui, que por sua vez reaproveita
/// <see cref="EnvelopeCodecV12.LerExigencias"/>, <see cref="EnvelopeCodecV12.LerObrigatoriedades"/>
/// e <see cref="EnvelopeCodecV12.LerReferenciaTemporalFatosPolitica"/> (as sub-chaves cuja
/// forma NÃO muda) e só acrescenta <c>LerMetadadosFatos</c>. O decoder 1.3 NÃO ganha
/// <c>arvoreSatisfacao</c> — a 1.3 nunca teve essa chave, e um envelope histórico "1.3"
/// não a tem nos bytes (por isso <c>GrafoConfiguracao.NosExigencia</c> é sempre <c>[]</c>
/// aqui, igual à 1.1/1.2).
/// </para>
/// </remarks>
public sealed class EnvelopeCodecV13 : IEnvelopeCodec
{
    private static readonly string[] Stubs =
    [
        "formulario",
        "cascataRemanejamento",
        "divulgacao",
        "identidadesUnidade",
    ];

    private static readonly string[] BlocosReais =
    [
        "periodo",
        "etapas",
        "distribuicao",
        "modalidades",
        "ofertas",
        "atendimento",
        "bonusRegional",
        "criteriosDesempate",
        "classificacao",
        "hashesEdital",
        "cronogramaFases",
        "documentosExigidos",
        "vagas",
    ];

    public string SchemaVersion => "1.3";

    public IPerfilCanonico Perfil => PerfilCanonicoV1.Instancia;

    public string AlgoritmoHash => Perfil.Algoritmo;

    public bool TemEncoder => true;

    public bool TemDecoder => true;

    public string? MotivoDaRecusa => null;

    public Result<EnvelopeReidratado> Decodificar(VersaoConfiguracao versao)
    {
        ArgumentNullException.ThrowIfNull(versao);

        Result<JsonObject> parse = EnvelopeCodecV11.Parsear(Perfil, versao.ConfiguracaoCongeladaCanonica);
        if (parse.IsFailure)
        {
            return Result<EnvelopeReidratado>.Failure(parse.Error!);
        }

        JsonObject payload = parse.Value!;
        LeitorEnvelope leitor = new();

        bool temRetificacao = payload.ContainsKey("retificacao");
        string[] chavesEsperadas = temRetificacao
            ? [.. BlocosReais, .. Stubs, "retificacao"]
            : [.. BlocosReais, .. Stubs];

        leitor.ExigirChaves(payload, "$", chavesEsperadas);
        foreach (string stub in Stubs)
        {
            leitor.ExigirStub(payload, stub);
        }

        DadosEdital? dados = EnvelopeCodecV11.LerDadosEdital(leitor, payload, out string hashDocumento);
        IReadOnlyList<EtapaProcesso> etapas = EnvelopeCodecV11.LerEtapas(leitor, payload);
        IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicao = EnvelopeCodecV11.LerDistribuicao(leitor, payload);
        OfertaAtendimentoEspecializado? atendimento = EnvelopeCodecV11.LerAtendimento(leitor, payload);
        ConfiguracaoBonusRegional? bonus = EnvelopeCodecV11.LerBonusRegional(leitor, payload);
        IReadOnlyList<CriterioDesempate> desempate = EnvelopeCodecV11.LerCriteriosDesempate(leitor, payload);
        ConfiguracaoClassificacao? classificacao = EnvelopeCodecV11.LerClassificacao(leitor, payload);
        IReadOnlyList<FaseCronograma> cronogramaFases = EnvelopeCodecV11.LerCronogramaFases(leitor, payload, comId: true);
        (ResultadoConformidade? conformidade, IReadOnlyList<DocumentoExigido> documentosExigidos, ReferenciaTemporalFatos? referenciaTemporalFatos,
            IReadOnlyDictionary<string, MetadadoFatoCongelado>? metadadosFatosCongelados) = LerDocumentosExigidos(leitor, payload);
        RetificacaoInfo? retificacao = temRetificacao ? EnvelopeCodecV11.LerRetificacao(leitor, payload) : null;

        if (leitor.Falhou)
        {
            return leitor.Falha<EnvelopeReidratado>();
        }

        if (EnvelopeCodecV11.VerificarCoerenciaComAVersao(versao, hashDocumento, retificacao) is { } incoerencia)
        {
            return Result<EnvelopeReidratado>.Failure(incoerencia);
        }

        GrafoConfiguracao grafo = new(
            etapas, atendimento!, distribuicao, bonus, desempate, classificacao!, cronogramaFases,
            documentosExigidos, [], referenciaTemporalFatos);
        return Result<EnvelopeReidratado>.Success(
            new EnvelopeReidratado(grafo, dados!, hashDocumento, retificacao, conformidade, metadadosFatosCongelados));
    }

    /// <summary>
    /// Story #919: a única leitura de bloco que difere de <see cref="EnvelopeCodecV12"/>.
    /// <c>exigencias</c>/<c>obrigatoriedades</c>/<c>referenciaTemporalFatos</c> mantêm a
    /// MESMA forma da 1.2 (reaproveitam os leitores de <see cref="EnvelopeCodecV12"/>);
    /// <c>metadadosFatos</c> é a chave nova (RN08). <c>internal</c>: a forma de
    /// <c>documentosExigidos</c> não muda entre a 1.3 e a 1.4 (Story #923 acrescenta
    /// <c>arvoreSatisfacao</c> como bloco de topo IRMÃO, não como mudança deste bloco) —
    /// <see cref="EnvelopeCodecV14"/> reaproveita este leitor tal qual, mesma técnica de
    /// <see cref="EnvelopeCodecV12"/> para os leitores que sobrevivem a um bump.
    /// </summary>
    internal static (
        ResultadoConformidade? Conformidade,
        IReadOnlyList<DocumentoExigido> DocumentosExigidos,
        ReferenciaTemporalFatos? ReferenciaTemporalFatos,
        IReadOnlyDictionary<string, MetadadoFatoCongelado>? MetadadosFatosCongelados)
        LerDocumentosExigidos(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "documentosExigidos", "$");
        if (leitor.Falhou)
        {
            return (null, [], null, null);
        }

        leitor.ExigirChaves(
            bloco, "documentosExigidos",
            "exigencias", "obrigatoriedades", "referenciaTemporalFatos", "dataReferenciaFatos", "metadadosFatos");

        IReadOnlyList<DocumentoExigido> exigencias = EnvelopeCodecV12.LerExigencias(leitor, bloco);
        if (leitor.Falhou)
        {
            return (null, [], null, null);
        }

        ResultadoConformidade? conformidade = EnvelopeCodecV12.LerObrigatoriedades(leitor, bloco);
        if (leitor.Falhou)
        {
            return (null, [], null, null);
        }

        ReferenciaTemporalFatos? referenciaTemporalFatos = EnvelopeCodecV12.LerReferenciaTemporalFatosPolitica(leitor, bloco);
        if (leitor.Falhou)
        {
            return (null, [], null, null);
        }

        // A data resolvida só é lida para participar do payload fechado (ExigirChaves
        // acima já a exige); a prova de que ela bate com a política é o round-trip
        // reidratar→recanonicalizar, não uma comparação aqui.
        leitor.DataOpcional(bloco, "dataReferenciaFatos", "documentosExigidos");
        if (leitor.Falhou)
        {
            return (null, [], null, null);
        }

        IReadOnlyDictionary<string, MetadadoFatoCongelado> metadadosFatos = LerMetadadosFatos(leitor, bloco);

        return leitor.Falhou
            ? (null, [], null, null)
            : (conformidade, exigencias, referenciaTemporalFatos, metadadosFatos);
    }

    /// <summary>
    /// Simétrico de <c>SnapshotPublicacaoCanonicalizer.SerializarMetadadosFatos</c>: array
    /// ordenado por <c>codigo</c> (o encoder já ordena — este leitor não reordena, só
    /// decodifica item a item), chaves fechadas por item. Código duplicado no array é
    /// envelope malformado (o encoder nunca emite duas entradas para o mesmo fato).
    /// </summary>
    private static Dictionary<string, MetadadoFatoCongelado> LerMetadadosFatos(LeitorEnvelope leitor, JsonObject bloco)
    {
        JsonArray array = leitor.Array(bloco, "metadadosFatos", "documentosExigidos");
        if (leitor.Falhou)
        {
            return new Dictionary<string, MetadadoFatoCongelado>(StringComparer.Ordinal);
        }

        Dictionary<string, MetadadoFatoCongelado> metadados = new(StringComparer.Ordinal);
        for (int i = 0; i < array.Count; i++)
        {
            string path = $"documentosExigidos.metadadosFatos[{i}]";
            JsonObject item = leitor.ItemObjeto(array, i, "documentosExigidos.metadadosFatos");
            leitor.ExigirChaves(
                item, path,
                "fatoCodigo", "dominio", "origem", "cardinalidade", "pontoResolucao", "binding",
                "valoresDominio", "valoresDominioDeclarados");

            string codigo = leitor.TextoNaoVazio(item, "fatoCodigo", path);
            string dominio = leitor.TextoNaoVazio(item, "dominio", path);
            string origem = leitor.TextoNaoVazio(item, "origem", path);
            string cardinalidade = leitor.TextoNaoVazio(item, "cardinalidade", path);
            string pontoResolucao = leitor.TextoNaoVazio(item, "pontoResolucao", path);
            string binding = leitor.TextoNaoVazio(item, "binding", path);
            if (leitor.Falhou)
            {
                return metadados;
            }

            IReadOnlyList<string>? valoresDominio = LerValoresDominio(leitor, item, path);
            if (leitor.Falhou)
            {
                return metadados;
            }

            IReadOnlyList<ValorDominioDeclaradoCongelado>? valoresDeclarados = LerValoresDominioDeclarados(leitor, item, path);
            if (leitor.Falhou)
            {
                return metadados;
            }

            if (!metadados.TryAdd(codigo, new MetadadoFatoCongelado(
                codigo, dominio, origem, cardinalidade, pontoResolucao, binding, valoresDominio, valoresDeclarados)))
            {
                leitor.Propagar<MetadadoFatoCongelado>(new DomainError(
                    ErrosCodecEnvelope.EnvelopeMalformado,
                    $"'{path}': o fato '{codigo}' aparece mais de uma vez em 'metadadosFatos' — cada fato tem no máximo um metadado congelado."));
                return metadados;
            }
        }

        return metadados;
    }

    /// <summary>
    /// <c>valoresDominio</c> — <see langword="null"/> significante (categórico de escopo
    /// dinâmico, booleano ou numérico), array quando o fato é categórico estático. Mesma
    /// técnica de <c>EnvelopeCodecV12.LerFormatosPermitidos</c> para o campo nulo-ou-array:
    /// leitura do <see cref="JsonNode"/> bruto, não <see cref="LeitorEnvelope.Array"/> (que
    /// rejeitaria <see langword="null"/>).
    /// </summary>
    private static IReadOnlyList<string>? LerValoresDominio(LeitorEnvelope leitor, JsonObject item, string pathPai)
    {
        string path = $"{pathPai}.valoresDominio";
        if (item["valoresDominio"] is not JsonNode node)
        {
            return null;
        }

        if (node is not JsonArray)
        {
            return leitor.Propagar<IReadOnlyList<string>>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}' deveria ser um array de textos ou null."));
        }

        return leitor.Textos(item, "valoresDominio", pathPai);
    }

    /// <summary>Mesma técnica de <see cref="LerValoresDominio"/> para o campo nulo-ou-array.</summary>
    private static IReadOnlyList<ValorDominioDeclaradoCongelado>? LerValoresDominioDeclarados(LeitorEnvelope leitor, JsonObject item, string pathPai)
    {
        string path = $"{pathPai}.valoresDominioDeclarados";
        if (item["valoresDominioDeclarados"] is not JsonNode node)
        {
            return null;
        }

        if (node is not JsonArray array)
        {
            return leitor.Propagar<IReadOnlyList<ValorDominioDeclaradoCongelado>>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}' deveria ser um array ou null."));
        }

        List<ValorDominioDeclaradoCongelado> valores = [];
        for (int i = 0; i < array.Count; i++)
        {
            string itemPath = $"{path}[{i}]";
            JsonObject valorItem = leitor.ItemObjeto(array, i, path);
            leitor.ExigirChaves(valorItem, itemPath, "valorCodigo", "descricao");

            string codigoValor = leitor.TextoNaoVazio(valorItem, "valorCodigo", itemPath);
            string? descricao = leitor.TextoOpcional(valorItem, "descricao", itemPath);
            if (leitor.Falhou)
            {
                return null;
            }

            valores.Add(new ValorDominioDeclaradoCongelado(codigoValor, descricao));
        }

        return valores;
    }


    // ────────────────────────────────────────────────────────────────────────────
    // Encoder congelado 1.3 (ver o comentário de classe acima) — cópia
    // autossuficiente do que SnapshotPublicacaoCanonicalizer emitia antes do bump
    // para 1.4. Sufixo "V13" nos nomes: intencional, mesma convenção do sufixo
    // "V12" em EnvelopeCodecV12 — nunca confundir com a lógica viva (que evolui).
    // ────────────────────────────────────────────────────────────────────────────

    private const int EscalaPadraoV13 = 4;
    private const int EscalaPercentualV13 = 2;

    private static readonly JsonObject NaoConstruidoV13 = new() { ["status"] = "nao_construido" };

    public SnapshotCanonico Codificar(EntradaCanonicalizacao entrada)
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
            ["periodo"] = SerializarPeriodoV13(dados),
            ["etapas"] = SerializarEtapasV13(processo),
            ["vagas"] = SerializarVagasV13(processo),
            ["distribuicao"] = SerializarDistribuicaoV13(processo),
            ["modalidades"] = SerializarModalidadesV13(processo),
            ["ofertas"] = SerializarOfertasV13(processo),
            ["atendimento"] = SerializarAtendimentoV13(processo),
            ["bonusRegional"] = SerializarBonusRegionalV13(processo),
            ["criteriosDesempate"] = SerializarCriteriosDesempateV13(processo),
            ["classificacao"] = SerializarClassificacaoV13(processo),
            ["hashesEdital"] = SerializarHashesEditalV13(dados, entrada.HashDocumento),
            ["documentosExigidos"] = SerializarDocumentosExigidosV13(processo, entrada.Conformidade, entrada.MetadadosFatosCongelados),
            ["formulario"] = NaoConstruidoV13.DeepClone(),
            ["cascataRemanejamento"] = NaoConstruidoV13.DeepClone(),
            ["divulgacao"] = NaoConstruidoV13.DeepClone(),
            ["cronogramaFases"] = SerializarCronogramaFasesV13(processo),
            ["identidadesUnidade"] = NaoConstruidoV13.DeepClone(),
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

        byte[] bytes = PerfilCanonicoV1.Instancia.Serializar(payload);
        return new SnapshotCanonico(bytes, SchemaVersion, AlgoritmoHash);
    }

    private static JsonObject SerializarPeriodoV13(DadosEdital dados) => new()
    {
        ["numero"] = dados.Numero is { } numero ? HashCanonicalComputer.NormalizeNfc(numero) : null,
        ["inicio"] = dados.PeriodoInscricaoInicio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["fim"] = dados.PeriodoInscricaoFim.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    };

    private static JsonArray SerializarEtapasV13(ProcessoSeletivo processo)
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
                ["peso"] = etapa.Peso is { } peso ? HashCanonicalComputer.SerializeDecimalCanonical(peso, EscalaPadraoV13) : null,
                ["notaMinima"] = etapa.NotaMinima is { } notaMinima ? HashCanonicalComputer.SerializeDecimalCanonical(notaMinima, EscalaPadraoV13) : null,
                ["ordem"] = etapa.Ordem,
            });
        }

        return array;
    }

    private static JsonArray SerializarDistribuicaoV13(ProcessoSeletivo processo)
    {
        JsonArray array = [];
        foreach (ConfiguracaoDistribuicaoVagas configuracao in OrdenarPorOfertaCursoOrigemIdV13(processo.DistribuicaoVagas))
        {
            array.Add(new JsonObject
            {
                ["ofertaCursoOrigemId"] = configuracao.OfertaCursoOrigemId,
                ["voBase"] = configuracao.VoBase,
                ["pr"] = HashCanonicalComputer.SerializeDecimalCanonical(configuracao.Pr, EscalaPadraoV13),
                ["regraDistribuicao"] = SerializarReferenciaRegraV13(configuracao.RegraDistribuicao),
                ["regraAjuste"] = configuracao.RegraAjuste is { } regraAjuste ? SerializarReferenciaRegraV13(regraAjuste) : null,
                ["referenciaDemografica"] = configuracao.ReferenciaDemografica is { } referencia
                    ? SerializarReferenciaDemograficaV13(referencia)
                    : null,
            });
        }

        return array;
    }

    /// <summary>
    /// O quadro de vagas (issue #848/ADR-0115) — output derivado, congelado
    /// separadamente dos insumos (<see cref="SerializarDistribuicaoV13"/>) para
    /// que a prova de reprodutibilidade não seja tautológica: recomputa-se o
    /// quadro a partir dos insumos congelados e compara-se a este bloco.
    /// </summary>
    private static JsonArray SerializarVagasV13(ProcessoSeletivo processo)
    {
        JsonArray array = [];
        foreach (ConfiguracaoDistribuicaoVagas configuracao in OrdenarPorOfertaCursoOrigemIdV13(processo.DistribuicaoVagas))
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

    private static JsonObject SerializarReferenciaDemograficaV13(ReferenciaReservaDemograficaSnapshot referencia) => new()
    {
        ["origemId"] = referencia.OrigemId,
        ["censoReferencia"] = HashCanonicalComputer.NormalizeNfc(referencia.CensoReferencia),
        ["ppiPercentual"] = HashCanonicalComputer.SerializeDecimalCanonical(referencia.PpiPercentual, EscalaPercentualV13),
        ["quilombolaPercentual"] = HashCanonicalComputer.SerializeDecimalCanonical(referencia.QuilombolaPercentual, EscalaPercentualV13),
        ["pcdPercentual"] = HashCanonicalComputer.SerializeDecimalCanonical(referencia.PcdPercentual, EscalaPercentualV13),
        ["baseLegal"] = HashCanonicalComputer.NormalizeNfc(referencia.BaseLegal),
    };

    private static JsonArray SerializarModalidadesV13(ProcessoSeletivo processo)
    {
        JsonArray array = [];
        foreach (ConfiguracaoDistribuicaoVagas configuracao in OrdenarPorOfertaCursoOrigemIdV13(processo.DistribuicaoVagas))
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
    private static IOrderedEnumerable<ConfiguracaoDistribuicaoVagas> OrdenarPorOfertaCursoOrigemIdV13(
        IEnumerable<ConfiguracaoDistribuicaoVagas> distribuicoes) =>
        distribuicoes.OrderBy(static d => d.OfertaCursoOrigemId);

    private static JsonArray SerializarOfertasV13(ProcessoSeletivo processo)
    {
        IEnumerable<string> ofertaIds = processo.DistribuicaoVagas
            .Select(static d => d.OfertaCursoOrigemId.ToString())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static id => id, StringComparer.Ordinal);

        return new JsonArray([.. ofertaIds.Select(static id => (JsonNode?)JsonValue.Create(id))]);
    }

    private static JsonObject SerializarAtendimentoV13(ProcessoSeletivo processo)
    {
        // Um bloco REAL nunca emite `nao_construido` (ADR-0100 item 10, decisão D8). O atendimento é
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

    private static JsonObject SerializarBonusRegionalV13(ProcessoSeletivo processo)
    {
        if (processo.BonusRegional is not { } bonus)
        {
            return new JsonObject { ["presente"] = false };
        }

        return new JsonObject
        {
            ["presente"] = true,
            ["regra"] = SerializarReferenciaRegraV13(bonus.Regra),
            ["fator"] = HashCanonicalComputer.SerializeDecimalCanonical(bonus.Fator, EscalaPadraoV13),
            ["teto"] = bonus.Teto is { } teto ? HashCanonicalComputer.SerializeDecimalCanonical(teto, EscalaPadraoV13) : null,
            ["municipioConvenio"] = bonus.MunicipioConvenio is { } municipio ? HashCanonicalComputer.NormalizeNfc(municipio) : null,
            ["baseLegal"] = bonus.BaseLegal is { } baseLegal ? HashCanonicalComputer.NormalizeNfc(baseLegal) : null,
        };
    }

    private static JsonArray SerializarCriteriosDesempateV13(ProcessoSeletivo processo)
    {
        JsonArray array = [];
        foreach (CriterioDesempate criterio in processo.CriteriosDesempate.OrderBy(static c => c.Ordem))
        {
            array.Add(new JsonObject
            {
                ["ordem"] = criterio.Ordem,
                ["regra"] = SerializarReferenciaRegraV13(criterio.Regra),
                ["args"] = SerializarArgsCriterioDesempateV13(criterio.Args),
            });
        }

        return array;
    }

    private static JsonObject SerializarArgsCriterioDesempateV13(ArgsCriterioDesempate args) => args switch
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

    private static JsonObject SerializarClassificacaoV13(ProcessoSeletivo processo)
    {
        // Mesma regra de SerializarAtendimentoV13 acima (nenhum bloco real vira stub). A classificação é o bloco que
        // determina o resultado do certame; congelá-la como `nao_construido` num
        // documento juridicamente vinculante é o pior modo de falha do envelope.
        if (processo.Classificacao is not { } classificacao)
        {
            throw new InvalidOperationException(
                "Canonicalização de processo sem configuração de classificação — o gate de conformidade deveria ter recusado a transição antes deste ponto.");
        }

        return new JsonObject
        {
            ["regraCalculo"] = SerializarReferenciaRegraV13(classificacao.RegraCalculo),
            ["regraArredondamento"] = classificacao.RegraArredondamento is { } arredondamento
                ? SerializarReferenciaRegraV13(arredondamento)
                : null,
            ["casasArredondamento"] = classificacao.CasasArredondamento,
            ["regraOrdemAlocacao"] = SerializarReferenciaRegraV13(classificacao.RegraOrdemAlocacao),
            ["nOpcoesAlocacao"] = classificacao.NOpcoesAlocacao,
            // RegrasEliminacao não tem chave de negócio única (cardinalidade
            // múltipla: duas ELIM-NOTA-MINIMA-ETAPA distintas são válidas, ex. PS
            // Convênios). Ordenar por `Id` era determinístico entre leituras da
            // mesma linha, mas NÃO entre configurações equivalentes — dois processos
            // com as mesmas regras inseridas em ordem inversa recebem Guids v7
            // distintos e produziriam envelopes distintos para a mesma configuração.
            // A ordenação é pela CHAVE DE CONTEÚDO: os bytes canônicos do próprio
            // item. O envelope passa a depender só do que ele diz.
            ["regrasEliminacao"] = OrdenarPorConteudoV13(classificacao.RegrasEliminacao
                .Select(static r => new JsonObject
                {
                    ["regra"] = SerializarReferenciaRegraV13(r.Regra),
                    ["args"] = SerializarArgsRegraEliminacaoV13(r.Args),
                })),
        };
    }

    private static JsonObject SerializarArgsRegraEliminacaoV13(ArgsRegraEliminacao args) => args switch
    {
        ArgsElimNotaMinimaEtapa notaMinima => new JsonObject
        {
            ["etapaRef"] = notaMinima.EtapaRef,
            ["notaMinima"] = HashCanonicalComputer.SerializeDecimalCanonical(notaMinima.NotaMinima, EscalaPadraoV13),
        },
        ArgsElimCorteRedacao corteRedacao => new JsonObject
        {
            ["minimo"] = HashCanonicalComputer.SerializeDecimalCanonical(corteRedacao.Minimo, EscalaPadraoV13),
        },
        ArgsElimZeroEmArea => [],
        _ => throw new InvalidOperationException($"Variante de {nameof(ArgsRegraEliminacao)} não reconhecida: {args.GetType()}."),
    };

    private static JsonObject SerializarHashesEditalV13(DadosEdital dados, string hashEdital) => new()
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
    private static JsonObject SerializarDocumentosExigidosV13(
        ProcessoSeletivo processo,
        ResultadoConformidade? conformidade,
        IReadOnlyDictionary<string, MetadadoFatoCongelado>? metadadosFatosCongelados) => new()
        {
            ["exigencias"] = SerializarExigenciasV13(processo.DocumentosExigidos),
            ["obrigatoriedades"] = SerializarObrigatoriedadesV13(conformidade),
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
            ["metadadosFatos"] = SerializarMetadadosFatosV13(metadadosFatosCongelados),
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
    /// (ADR-0042, mesmo tratamento que <see cref="SerializarObrigatoriedadesV13"/> recebe para
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
    private static JsonArray SerializarMetadadosFatosV13(IReadOnlyDictionary<string, MetadadoFatoCongelado>? metadadosFatosCongelados)
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
    /// <see cref="DocumentoExigido"/> não tem chave de negócio única (nada impede duas
    /// exigências para a mesma fase e o mesmo tipo de documento, com gatilhos distintos).
    /// Ordena pela chave de negócio PARCIAL (fase + tipo de documento — o caso comum, sem
    /// empate) e, no raro caso de empate, pela chave de conteúdo do restante do item —
    /// mesma regra de desempate por conteúdo de <see cref="OrdenarPorConteudoV13"/> (ADR-0109 D9)
    /// (nunca por <c>exigenciaId</c> — um Guid v7 novo a cada <c>DefinirDocumentosExigidos</c>
    /// tornaria a ordem não-determinística entre configurações equivalentes).
    /// </summary>
    private static JsonArray SerializarExigenciasV13(IReadOnlyCollection<DocumentoExigido> exigencias)
    {
        IOrderedEnumerable<DocumentoExigido> ordenadas = exigencias
            .OrderBy(static e => e.ExigidoNaFaseId)
            .ThenBy(static e => e.TipoDocumentoOrigemId)
            .ThenBy(static e => System.Text.Encoding.UTF8.GetString(
                PerfilCanonicoV1.Instancia.Serializar(SerializarExigenciaSemIdentidadeV13(e))),
                StringComparer.Ordinal)
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
            JsonObject item = SerializarExigenciaSemIdentidadeV13(e);
            item.Insert(0, "exigenciaId", JsonValue.Create(e.Id));
            return (JsonNode)item;
        })]);
    }

    private static JsonObject SerializarExigenciaSemIdentidadeV13(DocumentoExigido exigencia) => new()
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
        ["condicaoGatilho"] = SerializarCondicaoGatilhoV13(exigencia.Condicoes),
        ["basesLegais"] = SerializarBasesLegaisV13(exigencia.BasesLegaisResolvidas()),
        ["idadeMaximaEmissao"] = exigencia.IdadeMaximaEmissao is { } idade ? SerializarIdadeMaximaEmissaoV13(idade) : null,
        ["formatosPermitidos"] = SerializarFormatosPermitidosV13(exigencia.FormatosPermitidos),
        ["tamanhoMaximoBytes"] = exigencia.TamanhoMaximoBytes,
    };

    /// <summary>
    /// <see cref="FormatosPermitidos"/> (Story #918) substitui o campo singular
    /// <c>formatoPermitido</c> — objeto sempre presente (o VO é obrigatório), com
    /// <c>lista</c> nula ⟺ <c>qualquer</c> verdadeiro.
    /// </summary>
    private static JsonObject SerializarFormatosPermitidosV13(FormatosPermitidos formatosPermitidos) => new()
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
    private static JsonArray? SerializarCondicaoGatilhoV13(IReadOnlyCollection<CondicaoGatilho> condicoes)
    {
        if (condicoes.Count == 0)
        {
            return null;
        }

        JsonArray clausulas = [];
        foreach (IGrouping<int, CondicaoGatilho> clausula in condicoes.GroupBy(static c => c.Clausula).OrderBy(static g => g.Key))
        {
            clausulas.Add(OrdenarPorConteudoV13(clausula.Select(static c => new JsonObject
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
    private static JsonArray SerializarBasesLegaisV13(IEnumerable<DocumentoExigidoBaseLegal> basesLegais) =>
        OrdenarPorConteudoV13(basesLegais.Select(static b => new JsonObject
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

    private static JsonObject SerializarIdadeMaximaEmissaoV13(IdadeMaximaEmissao idade) => new()
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
    private static JsonArray SerializarObrigatoriedadesV13(ResultadoConformidade? conformidade)
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
                ["predicado"] = SerializarPredicadoObrigatoriedadeV13(regra.Predicado),
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
    /// <see cref="SerializarArgsCriterioDesempateV13"/> e <see cref="SerializarArgsRegraEliminacaoV13"/>
    /// — nunca um discriminador JSON solto.
    /// </summary>
    private static JsonObject SerializarPredicadoObrigatoriedadeV13(PredicadoObrigatoriedade predicado)
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
    private static JsonObject SerializarCronogramaFasesV13(ProcessoSeletivo processo) => new()
    {
        ["origemCandidatos"] = processo.OrigemCandidatos.ToString(),
        ["fases"] = SerializarFasesCronogramaV13(processo),
    };

    private static JsonArray SerializarFasesCronogramaV13(ProcessoSeletivo processo)
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
                ["bancasRequeridas"] = SerializarBancasRequeridasV13(fase),
                ["regraRecurso"] = fase.RegraRecurso is { } regraRecurso ? SerializarRegraRecursoFaseV13(regraRecurso) : null,
            });
        }

        return array;
    }

    private static JsonArray SerializarBancasRequeridasV13(FaseCronograma fase)
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

    private static JsonObject SerializarRegraRecursoFaseV13(RegraRecursoFase regraRecurso) => new()
    {
        ["regra"] = SerializarReferenciaRegraV13(regraRecurso.Regra),
        ["args"] = SerializarArgsRegraPrazoRecursoV13(regraRecurso.Args),
    };

    private static JsonObject SerializarArgsRegraPrazoRecursoV13(ArgsRegraPrazoRecurso args) => new()
    {
        ["prazoValor"] = HashCanonicalComputer.SerializeDecimalCanonical(args.PrazoValor, EscalaPadraoV13),
        ["prazoUnidade"] = args.PrazoUnidade.ToString(),
        ["atoAncoraCodigo"] = HashCanonicalComputer.NormalizeNfc(args.AtoAncoraCodigo),
        ["suspensividadePrimeiraInstanciaValor"] = args.SuspensividadePrimeiraInstanciaValor is { } v1
            ? HashCanonicalComputer.SerializeDecimalCanonical(v1, EscalaPadraoV13)
            : null,
        ["suspensividadePrimeiraInstanciaUnidade"] = args.SuspensividadePrimeiraInstanciaUnidade?.ToString(),
        ["suspensividadeSegundaInstanciaValor"] = args.SuspensividadeSegundaInstanciaValor is { } v2
            ? HashCanonicalComputer.SerializeDecimalCanonical(v2, EscalaPadraoV13)
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
    private static JsonArray OrdenarPorConteudoV13(IEnumerable<JsonObject> itens)
    {
        IOrderedEnumerable<JsonObject> ordenados = itens.OrderBy(
            static item => System.Text.Encoding.UTF8.GetString(PerfilCanonicoV1.Instancia.Serializar(item)),
            StringComparer.Ordinal);

        return new JsonArray([.. ordenados.Select(static item => (JsonNode)item)]);
    }

    private static JsonObject SerializarReferenciaRegraV13(ReferenciaRegra regra) => new()
    {
        ["codigo"] = regra.Codigo,
        ["versao"] = regra.Versao,
        ["hash"] = regra.Hash,
    };
}
