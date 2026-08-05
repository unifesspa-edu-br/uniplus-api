namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Globalization;
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
        "(?<![0-9a-fA-F])[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}(?![0-9a-fA-F])",
        RegexOptions.None,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Guid na forma "N" (32 hex, sem hífens) — como o nó de exigência do bloco
    /// <c>grafoDependencia</c> serializa a identidade da exigência (<c>EXIGENCIA/&lt;id:N&gt;</c>). Os
    /// <see cref="Regex"/> negados nas bordas impedem casar um trecho de 32 de dentro de um hash de 64
    /// hex — que também é conteúdo e nunca é um Guid.
    /// </summary>
    private static readonly Regex GuidFormaNPattern = new(
        "(?<![0-9a-fA-F])[0-9a-fA-F]{32}(?![0-9a-fA-F])",
        RegexOptions.None,
        TimeSpan.FromSeconds(1));

    /// <summary>Ids de referência cross-módulo — <b>conteúdo</b>, e portanto NÃO normalizados.</summary>
    private static readonly HashSet<Guid> IdsDeConteudo =
    [
        OfertaCursoFixa,
        ModalidadeFixa,
        DocumentoFixo,
        TipoDocumentoFixo,
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

        // Token por IDENTIDADE de Guid (não por texto): o mesmo id de exigência aparece hifenado em
        // documentosExigidos.exigencias[].exigenciaId E na forma "N" no nó do grafo — as duas
        // aparições têm de virar o MESMO token para preservar a referência (ADR-0109 D3). Guid.Parse
        // aceita ambas as formas, então a chave é o Guid canônico.
        Dictionary<Guid, string> tokens = [];

        string Substituir(Match match)
        {
            if (!Guid.TryParse(match.Value, CultureInfo.InvariantCulture, out Guid id))
            {
                return match.Value;
            }

            if (IdsDeConteudo.Contains(id))
            {
                return match.Value;
            }

            if (!tokens.TryGetValue(id, out string? token))
            {
                token = $"<<id-{tokens.Count + 1}>>";
                tokens[id] = token;
            }

            return token;
        }

        // A forma hifenada primeiro (a ordem de aparição define a numeração dos tokens, estável entre
        // regenerações); depois a forma "N", que reusa o token do id já visto.
        json = GuidPattern.Replace(json, Substituir);
        return GuidFormaNPattern.Replace(json, Substituir);
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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Referencia 2026", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!);

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
            regrasEliminacao: [], baseadoEmEnem: false).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

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

        processo.DefinirDocumentosExigidos(
            [NoExigencia.CriarFolha(DocumentoExigidoDeReferencia(fase.Id), 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.DataEspecifica, new DateOnly(2026, 1, 31), null).Value!,
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        // Coleta de fatos + derivação de MODALIDADE (Story #928, §7.4). O documento de referência é
        // gatado por MODALIDADE (derivado), então congelar a derivação é o que fecha as QUATRO
        // classes de aresta do grafo conjunto no mesmo snapshot — produção (campo→fato),
        // pré-condição (COR_RACA gata RENDA), derivação (RENDA→MODALIDADE) e gatilho
        // (MODALIDADE→exigência). MODALIDADE só contribui AC, a única modalidade ofertada.
        processo.DefinirFatosColetados([
            FatoColetado.Criar("COR_RACA", 0, "Cor ou raça", TipoRenderizacao.SelecaoUnica, obrigatorio: true, null).Value!,
            FatoColetado.Criar("RENDA", 1, "Faixa de renda familiar", TipoRenderizacao.SelecaoUnica, obrigatorio: false, [
                CondicaoPrecondicaoFato.Criar(0, "COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("PRETA")).Value!,
            ]).Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // Formulário de inscrição (Story #559): título e termo de aceite presentes — o bloco
        // "formulario" deixou de ser stub nesta Story, e a fixture precisa congelar a forma real,
        // não só o caso degenerado dos dois campos nulos.
        processo.DefinirFormulario(
            "Formulário de Inscrição",
            "Declaro que as informações prestadas são verdadeiras.",
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirRegrasDerivacao([
            ConfiguracaoDerivacaoFato.Criar("MODALIDADE", [
                RegraDerivacaoConfigurada.Criar(0, "AC", null).Value!,
                RegraDerivacaoConfigurada.Criar(1, "AC", [
                    CondicaoRegraDerivacao.Criar(0, "RENDA", Operador.Igual, JsonSerializer.SerializeToElement("ATE_1_SM")).Value!,
                ]).Value!,
            ]).Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        return processo;
    }

    /// <summary>
    /// Exigência CONDICIONAL rica — todas as 12 dimensões da forma 1.2 preenchidas
    /// (condição de gatilho, base legal resolvida, idade de emissão, formatos permitidos e
    /// tamanho máximo global), para que a golden fixture congele a serialização completa do
    /// item. <c>FormatosPermitidos</c> (Story #918) traz DOIS itens — um com teto por
    /// formato, outro sem — para exercitar as duas variantes da lista na mesma fixture.
    /// </summary>
    /// <remarks>
    /// O gatilho cita DOIS fatos na mesma cláusula (AND): <c>MODALIDADE</c> (categórico
    /// derivado/escopo-processo — <c>valoresDominioDeclarados</c> nulo em
    /// <c>documentosExigidos.metadadosFatos</c>) e <c>COR_RACA</c> (categórico ESTÁTICO —
    /// <c>valoresDominioDeclarados</c> populado). Sem o segundo, a variante populada daquele
    /// bloco não teria cobertura alguma na golden fixture (achado 8).
    /// </remarks>
    private static DocumentoExigido DocumentoExigidoDeReferencia(Guid exigidoNaFaseId)
    {
        CondicaoGatilho condicaoModalidade = CondicaoGatilho.Criar(
            0, "MODALIDADE", Operador.Igual, JsonSerializer.SerializeToElement("AC")).Value!;
        CondicaoGatilho condicaoCorRaca = CondicaoGatilho.Criar(
            0, "COR_RACA", Operador.Em, JsonSerializer.SerializeToElement(new[] { "PRETA", "PARDA" })).Value!;
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
            condicoes: [condicaoModalidade, condicaoCorRaca],
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

    /// <summary>
    /// Metadado dos fatos citados na condição de gatilho de <see cref="DocumentoExigidoDeReferencia"/>
    /// (Story #919, RN08) — exercita <c>metadadosFatos</c> com DOIS itens reais na golden fixture.
    /// MODALIDADE é derivado e de escopo-processo (ADR-0116): seus valores vêm da oferta congelada,
    /// então <c>valoresDominio</c> e <c>valoresDominioDeclarados</c> são nulos. COR_RACA é categórico
    /// ESTÁTICO: <c>valoresDominioDeclarados</c> é populado — a variante que, sem este fato no
    /// gatilho, não teria cobertura alguma na golden fixture (achado 8). A inserção fora de ordem
    /// (PRETA antes de BRANCA) e a ordem não-zero de PRETA/PARDA provam que é o ENCODER — não a
    /// ordem de inserção neste dicionário de teste — quem ordena por <c>Ordem</c>/<c>Codigo</c>.
    /// </summary>
    internal static Dictionary<string, MetadadoFatoCongelado> MetadadosFatosDeReferencia() =>
        new Dictionary<string, MetadadoFatoCongelado>(StringComparer.Ordinal)
        {
            ["MODALIDADE"] = new MetadadoFatoCongelado(
                Codigo: "MODALIDADE",
                Dominio: "CATEGORICO",
                Origem: "DERIVADO",
                Cardinalidade: "MULTIVALORADO",
                PontoResolucao: "INSCRICAO",
                Binding: "REGRA_DERIVACAO:MODALIDADE",
                ValoresDominio: null,
                ValoresDominioDeclarados: null),
            ["COR_RACA"] = new MetadadoFatoCongelado(
                Codigo: "COR_RACA",
                Dominio: "CATEGORICO",
                Origem: "DECLARADO",
                Cardinalidade: "ESCALAR",
                PontoResolucao: "INSCRICAO",
                Binding: "CAMPO_INSCRICAO:COR_RACA",
                ValoresDominio: ["BRANCA", "PRETA", "PARDA"],
                ValoresDominioDeclarados: [
                    new ValorDominioDeclaradoCongelado("PRETA", "Autodeclaração de cor/raça preta.", 1),
                    new ValorDominioDeclaradoCongelado("BRANCA", "Autodeclaração de cor/raça branca.", 0),
                    new ValorDominioDeclaradoCongelado("PARDA", "Autodeclaração de cor/raça parda.", 2),
                ]),
        };

    /// <summary>
    /// Os valores selecionáveis dos dois fatos coletados de <see cref="ProcessoDeReferencia"/>
    /// (COR_RACA, RENDA — ambos <c>SELECAO_UNICA</c>, issue #1059) — totalidade exigida pelo
    /// encoder (D1-ter). COR_RACA reusa o MESMO vocabulário de <see cref="MetadadosFatosDeReferencia"/>
    /// — é o mesmo fato do catálogo, congelado nos dois blocos.
    /// </summary>
    internal static Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> ValoresSelecionaveisDeReferencia() =>
        new Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>(StringComparer.Ordinal)
        {
            ["COR_RACA"] = [
                new ValorDominioDeclaradoCongelado("PRETA", "Autodeclaração de cor/raça preta.", 1),
                new ValorDominioDeclaradoCongelado("BRANCA", "Autodeclaração de cor/raça branca.", 0),
                new ValorDominioDeclaradoCongelado("PARDA", "Autodeclaração de cor/raça parda.", 2),
            ],
            ["RENDA"] = [
                new ValorDominioDeclaradoCongelado("ATE_1_SM", "Renda familiar per capita de até 1 salário mínimo.", 0),
                new ValorDominioDeclaradoCongelado("ACIMA_1_SM", "Renda familiar per capita acima de 1 salário mínimo.", 1),
            ],
        };

    internal static SnapshotCanonico CanonicalizarReferencia() =>
        Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(
            ProcessoDeReferencia(), DadosDeReferencia(), HashFixo,
            MetadadosFatosCongelados: MetadadosFatosDeReferencia(),
            ValoresSelecionaveisCongelados: ValoresSelecionaveisDeReferencia()));

    /// <summary>
    /// O agregado de referência COM a cascata de remanejamento configurada (Story #575) —
    /// a matriz legal completa (8×7, fallback AC). A oferta de referência é institucional
    /// (não federal): a canonicalização é pura (ADR-0109 D6) e não valida
    /// <c>PendenciaDaCascata</c>, então este agregado não precisa ser publicável — ele só
    /// existe para congelar a forma <c>presente:true</c> do bloco <c>cascataRemanejamento</c>.
    /// </summary>
    private static ProcessoSeletivo ProcessoDeReferenciaComCascata()
    {
        ProcessoSeletivo processo = ProcessoDeReferencia();
        processo.DefinirCascataRemanejamento(CascataDeReferencia(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        return processo;
    }

    /// <summary>A matriz legal completa (8×7), fallback AC — mesma forma semeada em REMANEJ-CASCATA-LEI-12711 v1.</summary>
    private static ConfiguracaoCascataRemanejamento CascataDeReferencia()
    {
        IReadOnlyList<string> origens = ModalidadesFederaisLei12711.Codigos;
        List<DestinoRemanejamento> destinos = [];
        foreach (string origem in origens)
        {
            string[] destinosDaOrigem = [.. origens.Where(o => o != origem)];
            for (int i = 0; i < destinosDaOrigem.Length; i++)
            {
                destinos.Add(DestinoRemanejamento.Criar(origem, i + 1, destinosDaOrigem[i]).Value!);
            }
        }

        return ConfiguracaoCascataRemanejamento.Criar(
            Regra(RegraRemanejamentoCodigo.Cascata, "1"), ModalidadesFederaisLei12711.Ac, destinos).Value!;
    }

    private static SnapshotCanonico CanonicalizarReferenciaComCascata() =>
        Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(
            ProcessoDeReferenciaComCascata(), DadosDeReferencia(), HashFixo,
            MetadadosFatosCongelados: MetadadosFatosDeReferencia(),
            ValoresSelecionaveisCongelados: ValoresSelecionaveisDeReferencia()));

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

    /// <summary>
    /// Story #575: o bloco <c>cascataRemanejamento</c> saiu de stub (<c>nao_construido</c>) para
    /// bloco real com dois estados — <c>Envelope_BateGoldenFixture</c> acima já cobre a ausência
    /// (<c>{"presente":false}</c>, a forma do processo de referência); esta fixture congela a
    /// presença (<c>{"presente":true,...}</c>, matriz legal completa) byte a byte.
    /// </summary>
    [Fact(DisplayName = "Envelope_ComCascata_BateGoldenFixture — o envelope com cascata presente é byte-idêntico à fixture congelada")]
    public void Envelope_ComCascata_BateGoldenFixture()
    {
        SnapshotCanonico canonico = CanonicalizarReferenciaComCascata();
        string atual = NormalizarIds(canonico.Bytes);

        if (Environment.GetEnvironmentVariable("UPDATE_ENVELOPE_FIXTURE") == "1")
        {
            string destino = CaminhoDaFixtureCascataNoFonte();
            Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
            File.WriteAllText(destino, atual + Environment.NewLine);
        }

        atual.Should().Be(LerFixtureCascata(),
            "o envelope com cascata presente mudou de forma sem que a golden fixture fosse atualizada.");
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
        IReadOnlyDictionary<string, MetadadoFatoCongelado> metadados = MetadadosFatosDeReferencia();
        IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresSelecionaveis = ValoresSelecionaveisDeReferencia();

        byte[] primeira = Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(
            processo, dados, HashFixo, MetadadosFatosCongelados: metadados, ValoresSelecionaveisCongelados: valoresSelecionaveis)).Bytes;
        byte[] segunda = Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(
            processo, dados, HashFixo, MetadadosFatosCongelados: metadados, ValoresSelecionaveisCongelados: valoresSelecionaveis)).Bytes;

        segunda.Should().Equal(primeira, "a projeção é pura — mesma entrada, mesmos bytes");
    }

    // ── Issue #1059 (UNI-REQ-0072) — totalidade: o encoder LANÇA quando falta entrada para um fato de seleção ──

    [Fact(DisplayName = "SerializarFatosColetados lança quando um fato de seleção coletado não tem entrada no dicionário de valores selecionáveis")]
    public void SerializarFatosColetados_SemEntradaParaFatoDeSelecao_Lanca()
    {
        // O processo de referência coleta COR_RACA e RENDA, ambos SELECAO_UNICA — canonicalizar
        // SEM passar ValoresSelecionaveisCongelados (dicionário nulo) tem de LANÇAR, nunca emitir
        // "valoresSelecionaveis": [] por omissão. É a contraprova de D1-ter: sem esta guarda, um
        // handler que esquecesse de resolver o dicionário publicaria um seletor MUDO, e a prova
        // de round-trip passaria mesmo assim — o round-trip prova preservação do que foi emitido,
        // não completude do que devia ser.
        ProcessoSeletivo processo = ProcessoDeReferencia();
        DadosEdital dados = DadosDeReferencia();

        Action canonicalizarSemDicionario = () => Canonicalizer.Canonicalizar(
            new EntradaCanonicalizacao(processo, dados, HashFixo, MetadadosFatosCongelados: MetadadosFatosDeReferencia()));

        canonicalizarSemDicionario.Should().Throw<InvalidOperationException>()
            .WithMessage("*COR_RACA*",
                "COR_RACA é o primeiro fato de seleção coletado (Ordem 0) — a ausência de entrada para ele é o " +
                "primeiro erro de programação que o encoder encontra");
    }

    // ── Issue #1059 (UNI-REQ-0072) — ordem: bate com a origem, é canônica e desempata por código ──

    [Fact(DisplayName = "valoresSelecionaveis[].ordem e metadadosFatos[].valoresDominioDeclarados[].ordem batem com a Ordem de origem, ordenados por Ordem/Codigo — independente da ordem de inserção no dicionário")]
    public void Ordem_BateComAOrigemEEhCanonica_NosDoisBlocos()
    {
        JsonObject envelope = EnvelopeComoObjeto();

        JsonObject fatoCorRaca = envelope["fatosColetados"]!.AsArray()
            .Single(f => f!["fatoCodigo"]!.GetValue<string>() == "COR_RACA")!.AsObject();
        JsonArray valoresSelecionaveisCorRaca = fatoCorRaca["valoresSelecionaveis"]!.AsArray();

        // A entrada de teste insere PRETA (ordem 1) ANTES de BRANCA (ordem 0) — se o encoder
        // dependesse da ordem de inserção do dicionário, o array sairia [PRETA, BRANCA]. Ele sai
        // [BRANCA, PRETA, PARDA]: por Ordem ascendente, não por ordem de chegada.
        valoresSelecionaveisCorRaca.Select(v => v!["valorCodigo"]!.GetValue<string>())
            .Should().Equal(["BRANCA", "PRETA", "PARDA"]);
        valoresSelecionaveisCorRaca.Select(v => v!["ordem"]!.GetValue<int>())
            .Should().Equal([0, 1, 2], "ordem bate com a Ordem de origem de cada valor — 0 para BRANCA, 1 para PRETA, 2 para PARDA");

        JsonObject metadadoCorRaca = envelope["documentosExigidos"]!["metadadosFatos"]!.AsArray()
            .Single(m => m!["fatoCodigo"]!.GetValue<string>() == "COR_RACA")!.AsObject();
        JsonArray declaradosCorRaca = metadadoCorRaca["valoresDominioDeclarados"]!.AsArray();

        declaradosCorRaca.Select(v => v!["valorCodigo"]!.GetValue<string>())
            .Should().Equal(["BRANCA", "PRETA", "PARDA"],
                "o mesmo vocabulário do MESMO fato do catálogo, congelado nos DOIS blocos, na MESMA ordem canônica");
        declaradosCorRaca.Select(v => v!["ordem"]!.GetValue<int>()).Should().Equal([0, 1, 2]);
    }

    [Fact(DisplayName = "Dois valores com a MESMA Ordem desempatam por Codigo de forma determinística — nos dois blocos")]
    public void Ordem_ComEmpate_DesempataPorCodigoDeFormaDeterministica()
    {
        IReadOnlyList<ValorDominioDeclaradoCongelado> empatados =
        [
            new ValorDominioDeclaradoCongelado("ZETA", "Zeta.", 0),
            new ValorDominioDeclaradoCongelado("ALFA", "Alfa.", 0),
        ];

        ProcessoSeletivo processo = ProcessoDeReferencia();
        DadosEdital dados = DadosDeReferencia();
        IReadOnlyDictionary<string, MetadadoFatoCongelado> metadados = MetadadosFatosDeReferencia();

        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresSelecionaveis =
            new(ValoresSelecionaveisDeReferencia()) { ["COR_RACA"] = empatados };

        SnapshotCanonico canonico = Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(
            processo, dados, HashFixo, MetadadosFatosCongelados: metadados, ValoresSelecionaveisCongelados: valoresSelecionaveis));

        JsonArray valoresCorRaca = EnvelopeCodecRoundTripTests.Envelope(canonico)["fatosColetados"]!.AsArray()
            .Single(f => f!["fatoCodigo"]!.GetValue<string>() == "COR_RACA")!["valoresSelecionaveis"]!.AsArray();

        valoresCorRaca.Select(v => v!["valorCodigo"]!.GetValue<string>()).Should().Equal(
            ["ALFA", "ZETA"],
            "empate de Ordem (ambos 0) desempata por Codigo ordinal — 'ALFA' antes de 'ZETA', " +
            "independente da ordem em que os dois entraram na lista");
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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Ordem", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!);

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
            regrasEliminacao: eliminacoes, baseadoEmEnem: true);
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

    // ── Fixture da variante COM cascata (Story #575) — nome próprio, fora da chave por
    // schema_version: é uma segunda fixture da MESMA versão de schema, não uma versão nova. ──

    private const string NomeDaFixtureCascata = "envelope-0.0.5-cascata.json";

    private static string CaminhoDaFixtureCascata() => Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
        "ProcessosSeletivos",
        "Fixtures",
        NomeDaFixtureCascata);

    private static string CaminhoDaFixtureCascataNoFonte([CallerFilePath] string origem = "") => Path.Combine(
        Path.GetDirectoryName(origem)!,
        "Fixtures",
        NomeDaFixtureCascata);

    private static string LerFixtureCascata() => File.ReadAllText(CaminhoDaFixtureCascata()).Trim();
}
