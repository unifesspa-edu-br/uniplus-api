namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

/// <summary>
/// <b>Golden fixture do envelope canônico</b> (ADR-0109 D2/D3).
/// </summary>
/// <remarks>
/// <para>
/// O envelope é o artefato de maior peso jurídico do módulo — a evidência que
/// sustenta o resultado do certame — e era, até esta suíte, <b>o único contrato
/// do repositório sem gate de regressão</b>: <c>SnapshotVigenteDto.Configuracao</c>
/// é um <c>JsonNode</c>, e o schema gerado na baseline OpenAPI é literalmente
/// <c>"JsonNode": {}</c>. Um stub virando objeto rico, uma chave nova, uma troca
/// de <c>null</c> explícito por omissão: <b>nada disso aparece no diff da baseline</b>.
/// </para>
/// <para>
/// A fixture compara <b>byte a byte</b>. Os três canários provam que ela protege
/// de fato — uma fixture que não falha quando o envelope muda não é gate, é
/// decoração.
/// </para>
/// <para>
/// A projeção é pura (ADR-0109 D6), então esta suíte não precisa de banco.
/// </para>
/// </remarks>
public sealed class EnvelopeCanonicoGoldenTests
{
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizer = new();

    /// <summary>Exposto para <c>EnvelopeCodecRoundTripTests.GoldenRica12_*</c> — mesmo hash fixo, mesma fixture.</summary>
    internal static readonly string HashFixo = new('a', 64);

    private static readonly Guid OfertaCursoFixa = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ModalidadeFixa = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid DocumentoFixo = new("33333333-3333-3333-3333-333333333333");

    /// <summary>Id de origem do <c>TipoDocumento</c> (snapshot-copy cross-módulo, ADR-0061) — Story #554, PR #903.</summary>
    private static readonly Guid TipoDocumentoFixo = new("44444444-4444-4444-4444-444444444444");

    private static readonly Regex GuidPattern = new(
        "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.None,
        TimeSpan.FromSeconds(1));

    /// <summary>Ids de referência cross-módulo — <b>conteúdo</b>, e portanto NÃO normalizados.</summary>
    private static readonly HashSet<string> IdsDeConteudo =
    [
        OfertaCursoFixa.ToString(),
        ModalidadeFixa.ToString(),
        DocumentoFixo.ToString(),
        TipoDocumentoFixo.ToString(),
    ];

    /// <summary>
    /// Normalização referencial (ADR-0109 D3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Só os ids <b>voláteis</b> — os Guid v7 que a entidade gera a cada execução —
    /// são normalizados. Cada um vira um token distinto por ordem de primeira
    /// aparição (<c>&lt;&lt;id-1&gt;&gt;</c>, <c>&lt;&lt;id-2&gt;&gt;</c>…), de modo que
    /// <b>igualdade e referência são preservadas</b>: o <c>etapaRef</c> que aponta para
    /// uma etapa continua apontando para o mesmo token que o <c>id</c> dela.
    /// </para>
    /// <para>
    /// Os ids de <b>conteúdo</b> (oferta de curso, modalidade, documento — referências
    /// cross-módulo, snapshot-copy) ficam <b>literais</b>. Zerar tudo indistintamente
    /// tornaria a fixture cega justamente ao que ela deveria proteger: trocar
    /// <c>modalidadeOrigemId</c> por <c>ofertaCursoOrigemId</c>, ou gravar
    /// <see cref="Guid.Empty"/>, passaria despercebido.
    /// </para>
    /// </remarks>
    private static string NormalizarIds(byte[] bytes)
    {
        string json = Encoding.UTF8.GetString(bytes);

        Dictionary<string, string> tokens = new(StringComparer.Ordinal);

        return GuidPattern.Replace(json, match =>
        {
            if (IdsDeConteudo.Contains(match.Value))
            {
                return match.Value;
            }

            if (!tokens.TryGetValue(match.Value, out string? token))
            {
                token = $"<<id-{tokens.Count + 1}>>";
                tokens[match.Value] = token;
            }

            return token;
        });
    }

    private static ReferenciaRegra Regra(string codigo, string hashSeed) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(hashSeed[0], 64)).Value!;

    /// <summary>Agregado de referência — conforme, com as 13 dimensões reais preenchidas.</summary>
    /// <remarks>
    /// Story #554 (PR #903, bump 1.2): o cronograma, a exigência documental rica e a
    /// referência temporal de fatos foram acrescentados para que a golden fixture
    /// exercite de fato <c>SerializarExigencias</c>/<c>SerializarCondicaoGatilho</c>/
    /// <c>SerializarBasesLegais</c>/<c>SerializarIdadeMaximaEmissao</c> — uma
    /// <c>exigencias[]</c> sempre vazia não provaria nada sobre a forma nova.
    /// </remarks>
    internal static ProcessoSeletivo ProcessoDeReferencia()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Referencia 2026", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);

        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1),
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirDistribuicaoVagas([DistribuicaoDeReferencia()], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            regraCalculo: Regra(RegraCalculoCodigo.ClassificacaoImportada, "b"),
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "c"),
            nOpcoesAlocacao: 1,
            regrasEliminacao: []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        FaseCronograma fase = FaseCronograma.Criar(
            ordem: 1,
            faseCanonicaOrigemId: Guid.CreateVersion7(),
            codigo: "INSCRICAO",
            donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Propria,
            agrupaEtapas: true,
            permiteComplementacao: false,
            produzResultado: true,
            resultadoDefinitivo: true,
            coletaInscricao: true,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            atoProduzidoCodigo: "INSCRICAO",
            atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [],
            regraRecurso: null).Value!;
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirDocumentosExigidos([DocumentoExigidoDeReferencia(fase.Id)], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.DataEspecifica, new DateOnly(2026, 1, 31), null).Value!,
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        return processo;
    }

    /// <summary>
    /// Exigência CONDICIONAL rica — todas as 12 dimensões da forma 1.2 preenchidas
    /// (condição de gatilho, base legal resolvida, idade de emissão, formatos permitidos e
    /// tamanho máximo global), para que a golden fixture congele a serialização completa do
    /// item. <c>FormatosPermitidos</c> (Story #918) traz DOIS itens — um com teto por
    /// formato, outro sem — para exercitar as duas variantes da lista na mesma fixture.
    /// </summary>
    private static DocumentoExigido DocumentoExigidoDeReferencia(Guid exigidoNaFaseId)
    {
        CondicaoGatilho condicao = CondicaoGatilho.Criar(
            0, "MODALIDADE", Operador.Igual, JsonSerializer.SerializeToElement("AC")).Value!;
        DocumentoExigidoBaseLegal baseLegal = DocumentoExigidoBaseLegal.Criar(
            "Res. Unifesspa 532/2021, art. 12", TipoAbrangencia.InternaNorma, StatusBaseLegal.Resolvido, "Norma interna do certame").Value!;
        IdadeMaximaEmissao idadeMaximaEmissao = IdadeMaximaEmissao.Criar(
            90, UnidadeIdade.Dias, ReferenciaTipoIdadeEmissao.FimInscricao, null, null).Value!;
        FormatosPermitidos formatosPermitidos = FormatosPermitidos.Criar(
            qualquer: false,
            entradas: [("PDF", 5_000_000), ("JPEG", null)]).Value!;

        return DocumentoExigido.Criar(
            exigidoNaFaseId,
            tipoDocumentoOrigemId: TipoDocumentoFixo,
            tipoDocumentoCodigo: "COMPROVANTE_RESIDENCIA",
            tipoDocumentoNome: "Comprovante de residência",
            tipoDocumentoCategoria: "PESSOAL",
            aplicabilidade: Aplicabilidade.Condicional,
            obrigatorio: true,
            consequenciaIndeferimento: "ELIMINA",
            grupoSatisfacaoId: null,
            condicoes: [condicao],
            basesLegais: [baseLegal],
            idadeMaximaEmissao: idadeMaximaEmissao,
            formatosPermitidos: formatosPermitidos,
            tamanhoMaximoBytes: 5_000_000).Value!;
    }

    private static ConfiguracaoDistribuicaoVagas DistribuicaoDeReferencia() =>
        ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: OfertaCursoFixa,
            voBase: 40,
            pr: 1m,
            regraDistribuicao: Regra(RegraDistribuicaoVagasCodigo.Institucional, "a"),
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [
                ModalidadeSelecionada.Criar(
                    modalidadeOrigemId: ModalidadeFixa,
                    codigo: "AC",
                    descricao: null,
                    naturezaLegal: NaturezaLegalModalidade.Ampla,
                    composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
                    composicaoOrigemCodigo: null,
                    regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
                    remanejamentoDestino: null,
                    remanejamentoPar: null,
                    remanejamentoFallback: null,
                    criteriosCumulativos: [],
                    acaoQuandoIndeferido: null,
                    baseLegal: "Res. Unifesspa 532/2021",
                    quantidadeDeclarada: 40).Value!,
            ]).Value!;

    internal static DadosEdital DadosDeReferencia() => DadosEdital.Criar(
        numero: "001/2026",
        periodoInscricaoInicio: new DateOnly(2026, 1, 1),
        periodoInscricaoFim: new DateOnly(2026, 1, 31),
        documentoEditalId: DocumentoFixo).Value!;

    internal static SnapshotCanonico CanonicalizarReferencia() =>
        Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(
            ProcessoDeReferencia(), DadosDeReferencia(), HashFixo));

    // ── CA-03 — política: toda schema_version declarada tem a sua fixture ──

    [Fact(DisplayName = "SchemaVersion_TemFixtureCorrespondente — bumpar a versão sem criar a fixture quebra o build")]
    public void SchemaVersion_TemFixtureCorrespondente()
    {
        string versao = CanonicalizarReferencia().SchemaVersion;
        string caminho = CaminhoDaFixture(versao);

        File.Exists(caminho).Should().BeTrue(
            $"a schema_version corrente é '{versao}' e toda versão declarada precisa da sua golden fixture " +
            $"(esperada em '{caminho}'). Bumpar sem congelar a forma nova deixaria o envelope sem gate.");
    }

    // ── CA-04 — a fixture compara byte a byte ──

    [Fact(DisplayName = "Envelope_BateGoldenFixture — o envelope de referência é byte-idêntico à fixture congelada")]
    public void Envelope_BateGoldenFixture()
    {
        SnapshotCanonico canonico = CanonicalizarReferencia();
        string atual = NormalizarIds(canonico.Bytes);

        // Regeneração explícita, no mesmo espírito de UPDATE_OPENAPI_BASELINE:
        //   UPDATE_ENVELOPE_FIXTURE=1 dotnet test --filter Envelope_BateGoldenFixture
        // e o diff da fixture entra no PR — a mudança do envelope passa a ser
        // visível na revisão, que é todo o ponto.
        if (Environment.GetEnvironmentVariable("UPDATE_ENVELOPE_FIXTURE") == "1")
        {
            string destino = CaminhoDaFixtureNoFonte(canonico.SchemaVersion);
            Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
            File.WriteAllText(destino, atual + Environment.NewLine);
        }

        atual.Should().Be(LerFixture(canonico.SchemaVersion),
            "o envelope mudou de forma sem que a golden fixture fosse atualizada. Se a mudança é intencional, " +
            "bumpe a schema_version e congele a forma nova numa fixture própria.");
    }

    // ── CA-04 (canários) — a fixture PROTEGE de fato ──

    [Fact(DisplayName = "Canario_ChaveNova — acrescentar uma chave ao envelope faz a fixture falhar")]
    public void Canario_ChaveNova()
    {
        JsonObject adulterado = EnvelopeComoObjeto();
        adulterado["blocoIntruso"] = "x";

        BytesNormalizados(adulterado).Should().NotBe(LerFixture(CanonicalizarReferencia().SchemaVersion),
            "uma chave nova no envelope TEM de fazer a fixture falhar — se não faz, a fixture não é um gate");
    }

    [Fact(DisplayName = "Canario_StubViraObjeto — um stub virando objeto rico faz a fixture falhar")]
    public void Canario_StubViraObjeto()
    {
        JsonObject adulterado = EnvelopeComoObjeto();
        adulterado["documentosExigidos"] = new JsonObject { ["exigencias"] = new JsonArray() };

        BytesNormalizados(adulterado).Should().NotBe(LerFixture(CanonicalizarReferencia().SchemaVersion),
            "um stub que vira conteúdo real é mudança de FORMA — a fixture tem de acusar");
    }

    [Fact(DisplayName = "Canario_NullVirandoOmissao — trocar null explícito por omissão faz a fixture falhar")]
    public void Canario_NullVirandoOmissao()
    {
        JsonObject adulterado = EnvelopeComoObjeto();

        // `casasArredondamento` é `null` EXPLÍCITO no envelope de referência
        // (classificação importada não arredonda localmente). Omiti-la é
        // exatamente a mudança que o item 4 da ADR-0100 prescreveria — e que o
        // caminho do snapshot NÃO faz.
        adulterado["classificacao"]!.AsObject().Should().ContainKey("casasArredondamento",
            "pré-condição do canário: a chave tem de existir como null explícito para que removê-la seja um teste");
        adulterado["classificacao"]!.AsObject().Remove("casasArredondamento");

        BytesNormalizados(adulterado).Should().NotBe(LerFixture(CanonicalizarReferencia().SchemaVersion),
            "o envelope preserva `null` explícito (ADR-0109 D4). Omitir a chave muda os bytes — e o hash. " +
            "É por isso que a emenda ao item 4 da ADR-0100 é restrita ao caminho de hash de ENTIDADE.");
    }

    // ── CA-05 — a ordem de INSERÇÃO não muda o envelope (chave de conteúdo) ──

    [Fact(DisplayName = "Envelope_IndependeDaOrdemDeCriacao — duas configurações equivalentes produzem bytes idênticos")]
    public void Envelope_IndependeDaOrdemDeCriacao()
    {
        // DOIS processos com a MESMA configuração, mas com as regras de eliminação
        // CRIADAS em ordem inversa — logo com Guids v7 em ordem inversa.
        //
        // É isto que a ordenação por `Id` NÃO resolvia: os Ids crescem com a ordem de
        // criação, então o array sairia [corte, zero] num processo e [zero, corte] no
        // outro — bytes distintos para configurações equivalentes. Reusar as mesmas
        // entidades nos dois lados faria o teste passar com a implementação ANTIGA,
        // e um teste que não falha sem o fix não testa nada.
        byte[] ordemA = CanonicalizarComEliminacoes(corteRedacaoPrimeiro: true);
        byte[] ordemB = CanonicalizarComEliminacoes(corteRedacaoPrimeiro: false);

        NormalizarIds(ordemB).Should().Be(NormalizarIds(ordemA),
            "duas configurações EQUIVALENTES têm de produzir o mesmo envelope. Ordenar por `Id` (Guid v7) fazia a " +
            "identidade técnica da linha vazar para dentro do hash. A ordenação é pela chave de CONTEÚDO (ADR-0109 D9).");
    }

    // ── CA-06 — determinismo ──

    [Fact(DisplayName = "Envelope_EDeterministico — canonicalizar duas vezes o mesmo agregado produz bytes idênticos")]
    public void Envelope_EDeterministico()
    {
        ProcessoSeletivo processo = ProcessoDeReferencia();
        DadosEdital dados = DadosDeReferencia();

        byte[] primeira = Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(processo, dados, HashFixo)).Bytes;
        byte[] segunda = Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(processo, dados, HashFixo)).Bytes;

        segunda.Should().Equal(primeira, "a projeção é pura — mesma entrada, mesmos bytes");
    }

    // ── CA-12 — toda referência a regra é a tripla {codigo, versao, hash} ──

    [Fact(DisplayName = "Envelope_ReferenciasDeRegraSaoTripla — toda regra congelada carrega codigo, versao e hash")]
    public void Envelope_ReferenciasDeRegraSaoTripla()
    {
        // O candidato é identificado por ter `codigo` — NÃO por já ter a tripla
        // completa. Exigir as três chaves para depois afirmar que elas existem seria
        // circular: uma referência incompleta ficaria de fora da amostra e o teste
        // passaria justamente no caso que deveria pegar.
        List<(string Caminho, JsonObject Objeto)> candidatos = [];
        ColetarCandidatosAReferencia(EnvelopeComoObjeto(), "$", candidatos);

        candidatos.Should().NotBeEmpty(
            "o envelope de referência congela ao menos a regra de distribuição, a de cálculo e a de ordem de alocação");

        foreach ((string caminho, JsonObject objeto) in candidatos)
        {
            objeto.Should().ContainKey("versao",
                $"a referência de regra em '{caminho}' precisa da tripla completa — é ela que garante que uma NOVA " +
                "versão de regra não retroage a um processo já publicado");
            objeto.Should().ContainKey("hash",
                $"a referência de regra em '{caminho}' precisa da tripla completa (o hash é content-addressable)");
        }
    }

    /// <summary>
    /// Candidato a referência de regra = qualquer objeto com a chave <c>codigo</c>.
    /// Deliberadamente frouxo: é a asserção, não o coletor, que exige a tripla.
    /// </summary>
    private static void ColetarCandidatosAReferencia(
        JsonNode? node,
        string caminho,
        List<(string Caminho, JsonObject Objeto)> acumulador)
    {
        switch (node)
        {
            case JsonObject obj:
                if (obj.ContainsKey("codigo") && !obj.ContainsKey("naturezaLegal") && !obj.ContainsKey("ordem"))
                {
                    // `naturezaLegal` distingue a MODALIDADE (que também tem `codigo`,
                    // mas não é referência de regra) de uma referência do rol. `ordem`
                    // distingue a FASE do cronograma (Story #554, PR #903) pela mesma razão —
                    // nenhuma referência de regra tem `ordem`.
                    acumulador.Add((caminho, obj));
                }

                foreach (KeyValuePair<string, JsonNode?> kvp in obj)
                {
                    ColetarCandidatosAReferencia(kvp.Value, $"{caminho}.{kvp.Key}", acumulador);
                }

                break;

            case JsonArray arr:
                for (int i = 0; i < arr.Count; i++)
                {
                    ColetarCandidatosAReferencia(arr[i], $"{caminho}[{i}]", acumulador);
                }

                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Processo com cálculo local e duas regras de eliminação sem chave de negócio
    /// natural (cardinalidade múltipla). O parâmetro inverte a ORDEM DE CRIAÇÃO das
    /// entidades — e, com ela, a ordem dos Guid v7. A configuração resultante é a
    /// mesma; só a identidade técnica das linhas difere.
    /// </summary>
    /// <remarks>
    /// As duas regras escolhidas (<c>ELIM-CORTE-REDACAO</c> e <c>ELIM-ZERO-EM-AREA</c>)
    /// <b>não referenciam etapa</b> nos seus args — o que mantém o teste sobre a única
    /// variável que ele quer isolar: a ordem de criação.
    /// </remarks>
    private static byte[] CanonicalizarComEliminacoes(bool corteRedacaoPrimeiro)
    {
        // SiSU é baseado em ENEM — é o que admite as duas regras abaixo.
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Ordem", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);

        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1),
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirDistribuicaoVagas([DistribuicaoDeReferencia()], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // A ORDEM DE CRIAÇÃO é a variável — os Guid v7 nascem crescentes.
        List<RegraEliminacao> eliminacoes = [];

        RegraEliminacao CriarCorte() => RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimCorteRedacao, "e"),
            new ArgsElimCorteRedacao(400m)).Value!;

        RegraEliminacao CriarZero() => RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimZeroEmArea, "f"),
            new ArgsElimZeroEmArea()).Value!;

        if (corteRedacaoPrimeiro)
        {
            eliminacoes.Add(CriarCorte());
            eliminacoes.Add(CriarZero());
        }
        else
        {
            eliminacoes.Add(CriarZero());
            eliminacoes.Add(CriarCorte());
        }

        // Cálculo local exige regra de precisão declarada (INV-B8).
        Result<ConfiguracaoClassificacao> classificacao = ConfiguracaoClassificacao.Criar(
            regraCalculo: Regra(RegraCalculoCodigo.FormulaMediaPonderada, "b"),
            regraArredondamento: Regra(RegraArredondamentoCodigo.PrecisaoTruncar, "d"),
            casasArredondamento: 2,
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "c"),
            nOpcoesAlocacao: 1,
            regrasEliminacao: eliminacoes);
        classificacao.IsSuccess.Should().BeTrue(classificacao.Error?.Message);

        processo.DefinirClassificacao(classificacao.Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        return Canonicalizer.Canonicalizar(
            new EntradaCanonicalizacao(processo, DadosDeReferencia(), HashFixo)).Bytes;
    }

    private static JsonObject EnvelopeComoObjeto() =>
        JsonNode.Parse(Encoding.UTF8.GetString(CanonicalizarReferencia().Bytes))!.AsObject();

    private static string BytesNormalizados(JsonObject envelope) =>
        NormalizarIds(HashCanonicalComputer.ComputeSnapshotBytes(envelope));

    private static string LerFixture(string schemaVersion) =>
        File.ReadAllText(CaminhoDaFixture(schemaVersion)).Trim();

    /// <summary>
    /// Nome do arquivo da fixture. A versão é tratada como <b>nome</b>, nunca como
    /// caminho: um separador ou uma raiz em <paramref name="schemaVersion"/> faria o
    /// <see cref="Path.Combine(string[])"/> descartar os segmentos anteriores em
    /// silêncio e o teste passar a ler outro arquivo.
    /// </summary>
    private static string NomeDaFixture(string schemaVersion) =>
        $"envelope-{Path.GetFileName(schemaVersion)}.json";

    private static string CaminhoDaFixture(string schemaVersion) => Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
        "ProcessosSeletivos",
        "Fixtures",
        NomeDaFixture(schemaVersion));

    /// <summary>Caminho da fixture na ÁRVORE-FONTE — só usado na regeneração explícita.</summary>
    private static string CaminhoDaFixtureNoFonte(string schemaVersion, [CallerFilePath] string origem = "") => Path.Combine(
        Path.GetDirectoryName(origem)!,
        "Fixtures",
        NomeDaFixture(schemaVersion));
}
