namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// Issue #1069 — <b>Prova B</b> da política de ordenação: oráculo de <b>sequência exata</b>, com
/// chaves em conflito, para cada camada da composição hierárquica (ADR-0109, emenda #1069).
/// </summary>
/// <remarks>
/// <para>
/// <b>Por que esta prova existe ao lado de <see cref="EnvelopeCanonicoPermutacaoTests"/> (Prova
/// A):</b> permutar a ORDEM FÍSICA de entrada de um array governado por <c>Ordem</c> produz os
/// MESMOS bytes — o canonicalizador reordena por <c>Ordem</c> justamente para isso. A Prova A prova
/// essa invariância; ela não consegue distinguir "a política correta (Ordem) está em vigor" de "o
/// array foi ordenado por conteúdo ou por Id" — as três convergiriam para o mesmo resultado se as
/// entidades de teste tivessem <c>Ordem</c>, conteúdo e Id correlacionados. Trocar só os valores de
/// <c>Ordem</c> e olhar os bytes mudarem também não prova nada: o campo <c>ordem</c> é emitido
/// dentro do próprio item, então os bytes mudam de qualquer jeito, mesmo que a POSIÇÃO no array
/// tenha passado a ser decidida por outra coisa.
/// </para>
/// <para>
/// Por isso cada linha do manifesto abaixo monta dados em que <c>Ordem</c>, identidade de negócio,
/// conteúdo e Id apontam para sequências DIFERENTES, e afirma a sequência exata que a política
/// correta produz — nunca só "os bytes mudaram". Trocar a política de produção por qualquer uma das
/// alternativas descartadas faz o teste correspondente falhar.
/// </para>
/// <para>
/// <b>Manifesto de caminhos cobertos</b> (achado 2.4 do parecer da issue): cada linha lista o
/// caminho JSON, a camada da composição hierárquica que a rege (ADR-0109, emenda #1069), o oráculo
/// (chaves colocadas em conflito) e a precondição que a linha verifica antes de afirmar qualquer
/// coisa — o caminho existe, há ao menos dois itens com chaves relevantes diferentes, a sequência
/// que a política errada produziria de fato diverge da correta, e o oráculo não é trivial.
/// </para>
/// <list type="table">
///   <listheader><term>Caminho</term><description>Camada — conflito — teste</description></listheader>
///   <item><term><c>etapas</c></term><description>Ordem semântica vs. conteúdo e Id — <see cref="Etapas_OrdemGovernaSobreConteudoEId"/></description></item>
///   <item><term><c>etapas</c> (sem <c>Ordem</c>)</term><description>Conteúdo vs. Id — <see cref="Etapas_SemOrdem_ConteudoGovernaSobreId"/></description></item>
///   <item><term><c>etapas</c> (duplicata verdadeira)</term><description>Id como desempate técnico alcançável — <see cref="Etapas_DuplicataVerdadeira_IdEstabilizaComoDesempateFinal"/></description></item>
///   <item><term><c>etapas</c> (processo sem etapa)</term><description>Cenário de aceite: lista vazia canonicaliza sem recusa — <see cref="Etapas_ProcessoSemEtapaAlguma_ArrayVazioNadaRecusado"/></description></item>
///   <item><term><c>criteriosDesempate</c></term><description>Ordem semântica vs. conteúdo — <see cref="CriteriosDesempate_OrdemGovernaSobreConteudo"/></description></item>
///   <item><term><c>cronogramaFases.fases</c></term><description>Ordem semântica vs. conteúdo — <see cref="CronogramaFases_Fases_OrdemGovernaSobreConteudo"/></description></item>
///   <item><term><c>cronogramaFases.fases[].bancasRequeridas</c></term><description>Identidade de origem vs. conteúdo — <see cref="CronogramaFases_BancasRequeridas_IdentidadeDeOrigemGovernaSobreConteudo"/></description></item>
///   <item><term><c>fatosColetados</c></term><description>Ordem semântica vs. conteúdo — <see cref="FatosColetados_OrdemGovernaSobreConteudo"/></description></item>
///   <item><term><c>regrasDerivacao[].regras</c></term><description>Ordem semântica vs. conteúdo — <see cref="RegrasDerivacao_RegrasInternas_OrdemGovernaSobreConteudo"/></description></item>
///   <item><term><c>cascataRemanejamento.ordens[].destinos</c></term><description>Ordem semântica vs. conteúdo — <see cref="CascataRemanejamento_DestinosDaMesmaOrigem_OrdemGovernaSobreConteudo"/></description></item>
///   <item><term><c>cascataRemanejamento.ordens</c></term><description>Identidade de negócio (código da origem, ordinal) vs. ordem física de entrada — <see cref="CascataRemanejamento_Origens_OrdenaAlfabeticamentePorCodigo"/></description></item>
///   <item><term><c>arvoreSatisfacao</c> (raízes)</term><description>Ordem semântica vs. tipo do nó — <see cref="ArvoreSatisfacao_RaizesEFilhos_OrdemGovernaSobreConteudo"/></description></item>
///   <item><term><c>arvoreSatisfacao[].filhos</c></term><description>Ordem semântica vs. Id do nó (identidade técnica), com duas raízes e dois irmãos — <see cref="ArvoreSatisfacao_RaizesEFilhos_OrdemGovernaSobreConteudo"/></description></item>
///   <item><term><c>documentosExigidos.metadadosFatos[].valoresDominioDeclarados</c></term><description>Ordem semântica vs. Codigo — <see cref="MetadadosFatos_ValoresDominioDeclarados_OrdemGovernaSobreCodigo"/></description></item>
///   <item><term><c>atendimento.condicoes</c>/<c>recursos</c>/<c>tiposDeficiencia</c></term><description>Identidade de origem vs. conteúdo, oráculo próprio para os três arrays — <see cref="OfertaAtendimento_Condicoes_IdentidadeDeOrigemGovernaSobreConteudo"/></description></item>
///   <item><term><c>documentosExigidos.exigencias</c></term><description>Identidade parcial (fase + tipo) vs. conteúdo — <see cref="DocumentosExigidos_Exigencias_IdentidadeParcialGovernaSobreConteudo"/></description></item>
///   <item><term><c>documentosExigidos.exigencias</c> (empate de identidade)</term><description>Conteúdo vs. Id — <see cref="DocumentosExigidos_Exigencias_EmpateDeIdentidade_DesempataPeloConteudo"/></description></item>
///   <item><term><c>grafoDependencia.nos</c>/<c>arestas</c>/<c>ordemTopologica</c></term><description>DELEGAÇÃO — ordem derivada por <see cref="Domain.ValueObjects.GrafoDependenciaConjunta"/> e apenas preservada — <see cref="GrafoDependencia_OrdemTopologica_PreservaAOrdemDoDominio_NaoReordenaAlfabeticamente"/></description></item>
///   <item><term><c>bonusRegional.municipios</c></term><description>CodigoIbge empatado vs. ordem física de entrada — <see cref="BonusRegional_MunicipiosComMesmoCodigoIbge_DesempataPorNomeEUf"/></description></item>
/// </list>
/// <para>
/// <b>Fora do escopo desta suíte, por já terem oráculo de sequência exata próprio</b> (chave de
/// conteúdo, ADR-0109 D9 — a camada que este arquivo não repete): <c>classificacao.regrasEliminacao</c>
/// (<see cref="EnvelopeCanonicoGoldenTests.Envelope_IndependeDaOrdemDeCriacao"/>) e os seis conjuntos
/// da issue #1067 — <c>ocorrenciasEsperadas</c>,
/// <c>formatosPermitidos.lista</c>, <c>obrigatoriedades</c>, os dois <c>predicado.args</c> e
/// <c>valoresDominio</c> — todos em <c>OrdenacaoDeConjuntosCanonicosTests</c>. Repetir esses oráculos
/// aqui duplicaria a fonte da política sem acrescentar cobertura (achado 7.2 do parecer da issue).
/// </para>
/// <para>
/// A projeção é pura (ADR-0109 D6) — nenhum teste aqui precisa de banco. O <c>permutar</c> de
/// <see cref="CorpusEnvelope"/> continua significando só ordem física de entrada (Prova A); a
/// mutação de chaves em conflito desta suíte é gerada à parte, com processos mínimos próprios.
/// </para>
/// </remarks>
public sealed partial class PoliticaDeOrdenacaoTests
{
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizador = new();

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(semente, 64)).Value!;

    private static DadosEdital Dados() => DadosEdital.Criar(
        numero: "001/2026",
        periodoInscricaoInicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
        periodoInscricaoFim: new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
        documentoEditalId: Guid.CreateVersion7()).Value!;

    private static EntradaCanonicalizacao Entrada(
        ProcessoSeletivo processo,
        IReadOnlyDictionary<string, MetadadoFatoCongelado>? metadadosFatos = null) =>
        new(processo, Dados(), new string('0', 64), FusoInstitucional.ZoneId, MetadadosFatosCongelados: metadadosFatos,
            ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados());

    /// <summary>
    /// Um Guid com só o último dígito hexadecimal variando por <paramref name="sufixo"/> — as
    /// demais posições fixas garantem que <see cref="Guid.CompareTo(Guid)"/> segue a ordem numérica
    /// intuitiva do sufixo (mesmo padrão de <c>CorpusEnvelope.EtapaId</c>).
    /// </summary>
    private static Guid IdFixo(int sufixo) => new($"a0000000-0000-4000-8000-00000000000{sufixo:x}");

    private static ConfiguracaoDistribuicaoVagas DistribuicaoMinima() => ConfiguracaoDistribuicaoVagas.Criar(
        ofertaCursoOrigemId: Guid.CreateVersion7(),
        voBase: 40,
        pr: 1m,
        regraDistribuicao: Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'),
        regraAjuste: null,
        referenciaDemografica: null,
        modalidades: [
            ModalidadeSelecionada.Criar(
                Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo, null,
                RegraRemanejamentoModalidade.Nenhuma, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 40).Value!,
        ], ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;

    private static FaseCronograma FaseMinima() => FaseCronograma.Criar(
        ordem: 1,
        faseCanonicaOrigemId: Guid.CreateVersion7(),
        codigo: "INSCRICAO",
        origemData: OrigemDataFase.NoEdital,
        agrupaEtapas: true,
        permiteComplementacao: true,
        coletaInscricao: true, coletaSolicitacaoIsencao: false,
        inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
        produtos: [ProdutoDaFase.Criar("INSCRICAO", PapelProdutoFase.Definitivo)],
        faseConcluinteCodigo: null,
        emiteParecerIndividual: false,
        bancasRequeridas: [],
        regraRecurso: null,
        unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;

    /// <summary>
    /// Um processo mínimo e válido para <see cref="SnapshotPublicacaoCanonicalizer.Canonicalizar"/>,
    /// com um parâmetro por coleção sob teste — cada um substitui o default minimalista quando
    /// informado. Não publica nem valida pendências de conformidade: a canonicalização é pura
    /// (ADR-0109 D6) e cada teste chama <see cref="Canonicalizador"/> diretamente.
    /// </summary>
    private static ProcessoSeletivo Montar(
        IReadOnlyList<EtapaProcesso>? etapas = null,
        OfertaAtendimentoEspecializado? ofertaAtendimento = null,
        IReadOnlyList<CriterioDesempate>? criteriosDesempate = null,
        IReadOnlyList<FaseCronograma>? cronogramaFases = null,
        IReadOnlyList<NoExigencia>? documentosExigidos = null,
        IReadOnlyList<FatoColetado>? fatosColetados = null,
        IReadOnlyList<ConfiguracaoDerivacaoFato>? regrasDerivacao = null,
        ConfiguracaoCascataRemanejamento? cascata = null)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Política de Ordenação", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());


        processo.DefinirOfertaAtendimento(
            ofertaAtendimento ?? OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirDistribuicaoVagas([DistribuicaoMinima()], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        if (criteriosDesempate is not null)
        {
            processo.DefinirCriteriosDesempate(criteriosDesempate, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        }

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            regraCalculo: Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'),
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'),
            regrasEliminacao: [],
            baseadoEmEnem: false,
            resolucaoPesoAreaEnem: null,
            quadroPesoAreaEnem: [], escalaNotaFinal: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeCurso(1, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirCronogramaFases(
            cronogramaFases ?? [FaseMinima()], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas(etapas ?? [
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 1m, ordem: 1, faseCodigo: (cronogramaFases ?? [FaseMinima()])[0].Codigo).Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        if (documentosExigidos is not null)
        {
            processo.DefinirDocumentosExigidos(documentosExigidos, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        }

        if (fatosColetados is not null)
        {
            processo.DefinirItens(fatosColetados, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        }

        if (regrasDerivacao is not null)
        {
            processo.DefinirRegrasDerivacao(regrasDerivacao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        }

        if (cascata is not null)
        {
            processo.DefinirCascataRemanejamento(cascata, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        }

        return processo;
    }

    // ── camada 1 — Ordem semântica ──

    [Fact(DisplayName = "etapas: Ordem informada governa sobre conteúdo e Id — oráculo de sequência exata")]
    public void Etapas_OrdemGovernaSobreConteudoEId()
    {
        // Id CONTRÁRIO à Ordem também: Zeta (Ordem 1) recebe o Id MAIOR, Alfa (Ordem 2) o MENOR —
        // se o Id decidisse a posição, o resultado seria [Alfa, Zeta], o mesmo "errado" que o
        // conteúdo (nome) daria.
        EtapaProcesso zeta = EtapaProcesso.Reidratar(IdFixo(2), "Zeta", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 1m, notaMinima: null, ordem: 1);
        EtapaProcesso alfa = EtapaProcesso.Reidratar(IdFixo(1), "Alfa", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 1m, notaMinima: null, ordem: 2);

        new[] { zeta, alfa }.OrderBy(static e => e.Nome, StringComparer.Ordinal).Select(static e => e.Nome)
            .Should().Equal(["Alfa", "Zeta"], "pré-condição: ordenar pelo nome (proxy de conteúdo) dá o oposto do oráculo de Ordem abaixo");
        new[] { zeta, alfa }.OrderBy(static e => e.Id).Select(static e => e.Nome)
            .Should().Equal(["Alfa", "Zeta"], "pré-condição: ordenar pelo Id dá o oposto do oráculo de Ordem abaixo");

        ProcessoSeletivo processo = Montar(etapas: [alfa, zeta]);

        JsonArray etapasJson = EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))["etapas"]!.AsArray();

        etapasJson.Should().HaveCount(2, "pré-condição: o caminho existe e tem os dois itens sob teste");
        etapasJson.Select(static e => e!["nome"]!.GetValue<string>()).Should().Equal(
            ["Zeta", "Alfa"],
            "Ordem (1 para Zeta, 2 para Alfa) governa a posição — nem o nome nem o Id, que apontariam para o oposto");
    }

    [Fact(DisplayName = "etapas: sem Ordem, o conteúdo governa sobre o Id — a posição depende do que a etapa diz, não do Guid")]
    public void Etapas_SemOrdem_ConteudoGovernaSobreId()
    {
        EtapaProcesso zeta = EtapaProcesso.Reidratar(IdFixo(1), "Zeta", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 1m, notaMinima: null, ordem: null);
        EtapaProcesso alfa = EtapaProcesso.Reidratar(IdFixo(2), "Alfa", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 1m, notaMinima: null, ordem: null);

        new[] { zeta, alfa }.OrderBy(static e => e.Id).Select(static e => e.Nome)
            .Should().Equal(["Zeta", "Alfa"], "pré-condição: ordenar pelo Id (a política ANTIGA) dá o oposto do oráculo de conteúdo abaixo");

        ProcessoSeletivo processo = Montar(etapas: [zeta, alfa]);

        JsonArray etapasJson = EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))["etapas"]!.AsArray();

        etapasJson.Select(static e => e!["nome"]!.GetValue<string>()).Should().Equal(
            ["Alfa", "Zeta"],
            "sem Ordem, a posição depende do CONTEÚDO (dentro da chave sem id) — não do Guid, que apontaria para [Zeta, Alfa]");
    }

    [Fact(DisplayName = "etapas: duplicata verdadeira (mesmo conteúdo, sem Ordem) desempata por Id — o desempate final é alcançável, não código morto")]
    public void Etapas_DuplicataVerdadeira_IdEstabilizaComoDesempateFinal()
    {
        // O MESMO TipoEtapaSnapshot para as duas etapas — não um TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), ..., notaDeOrigemNoEnem: false)
        // por etapa (flaky sob CORRIDA, issue #1115): duas chamadas independentes gerariam origemId
        // distintos, e origemId entra em SerializarTipoEtapa/SemId — as etapas deixariam de ser
        // conteudisticamente idênticas, e o ThenBy de CONTEÚDO (não o de Id, que é o objeto real
        // desta prova) desempataria a posição, tornando o teste dependente da ordem de criação dos Guid v7.
        TipoEtapaSnapshot tipoEtapa = TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;
        EtapaProcesso etapaComIdMenor = EtapaProcesso.Reidratar(IdFixo(1), "Prova Objetiva", CaraterEtapa.Classificatoria, tipoEtapa, peso: 1m, notaMinima: null, ordem: null);
        EtapaProcesso etapaComIdMaior = EtapaProcesso.Reidratar(IdFixo(2), "Prova Objetiva", CaraterEtapa.Classificatoria, tipoEtapa, peso: 1m, notaMinima: null, ordem: null);

        etapaComIdMenor.Nome.Should().Be(etapaComIdMaior.Nome,
            "pré-condição: as duas etapas são conteudisticamente IDÊNTICAS (mesmo nome/caráter/peso/notaMinima, ambas sem Ordem) — a chave de conteúdo empata e sobra o Id");

        ProcessoSeletivo processo = Montar(etapas: [etapaComIdMaior, etapaComIdMenor]);

        JsonArray etapasJson = EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))["etapas"]!.AsArray();

        etapasJson.Select(static e => e!["id"]!.GetValue<Guid>()).Should().Equal(
            [IdFixo(1), IdFixo(2)],
            "duas etapas classificatórias com o MESMO nome/caráter/peso/notaMinima (o domínio só recusa Ordem INFORMADA duplicada, " +
            "e o índice único do banco admite múltiplos NULL) são aceitas — o Id estabiliza a alcançabilidade dessa duplicata");
    }

    /// <summary>
    /// Cenário de aceite da issue #1069: um processo cuja classificação vem de <b>nota importada</b>
    /// (SiSU) não precisa de etapa nenhuma — <see cref="Domain.Entities.ProcessoSeletivo.DefinirEtapas"/>
    /// aceita lista vazia (§3.5, Story #851) porque a guarda "ao menos uma etapa compõe a nota" só vale
    /// QUANDO há etapas. A canonicalização não pode recusar nem lançar: <c>etapas</c> sai como array
    /// vazio, forma fechada como qualquer outro bloco do envelope.
    /// </summary>
    [Fact(DisplayName = "etapas: processo sem etapa alguma (nota importada) canonicaliza com array vazio — nada é recusado")]
    public void Etapas_ProcessoSemEtapaAlguma_ArrayVazioNadaRecusado()
    {
        // A fase precisa de agrupaEtapas: false — o bicondicional fase×etapa (CA-14, Story #851)
        // recusa uma fase que agrupa etapas quando o processo não tem etapa pontuada nenhuma; o
        // padrão de Montar() (FaseMinima, agrupaEtapas: true) pressupõe ao menos uma.
        FaseCronograma faseSemAgruparEtapas = FaseCronograma.Criar(
            ordem: 1, faseCanonicaOrigemId: Guid.CreateVersion7(), codigo: "INSCRICAO",
            origemData: OrigemDataFase.NoEdital, agrupaEtapas: false, permiteComplementacao: true, coletaInscricao: true, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            produtos: [ProdutoDaFase.Criar("INSCRICAO", PapelProdutoFase.Definitivo)], faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;

        ProcessoSeletivo processo = Montar(etapas: [], cronogramaFases: [faseSemAgruparEtapas]);

        JsonArray etapasJson = EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))["etapas"]!.AsArray();

        etapasJson.Should().BeEmpty(
            "um processo classificado por nota importada (RegraCalculoCodigo.ClassificacaoImportada, usado pelo processo mínimo " +
            "desta suíte) não tem etapa pontuada — a canonicalização emite o array vazio em vez de recusar ou lançar");
    }

    [Fact(DisplayName = "criteriosDesempate: Ordem governa sobre o conteúdo da regra — oráculo de sequência exata")]
    public void CriteriosDesempate_OrdemGovernaSobreConteudo()
    {
        CriterioDesempate criterioIdoso = CriterioDesempate.Criar(2, Regra(CriterioDesempateCodigo.Idoso, 'd'), new ArgsDesempateIdoso(60)).Value!;
        CriterioDesempate criterioMaiorIdade = CriterioDesempate.Criar(1, Regra(CriterioDesempateCodigo.MaiorIdade, 'a'), new ArgsDesempateMaiorIdade()).Value!;

        new[] { criterioIdoso, criterioMaiorIdade }.OrderBy(static c => c.Regra.Codigo, StringComparer.Ordinal).Select(static c => c.Regra.Codigo)
            .Should().Equal(
                [CriterioDesempateCodigo.Idoso, CriterioDesempateCodigo.MaiorIdade],
                "pré-condição: ordenar pelo código da regra (proxy de conteúdo) dá o oposto do oráculo de Ordem abaixo");

        ProcessoSeletivo processo = Montar(criteriosDesempate: [criterioIdoso, criterioMaiorIdade]);

        JsonArray criteriosJson = EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))["criteriosDesempate"]!.AsArray();

        criteriosJson.Should().HaveCount(2, "pré-condição: o caminho existe e tem os dois itens sob teste");
        criteriosJson.Select(static c => c!["regra"]!["codigo"]!.GetValue<string>()).Should().Equal(
            [CriterioDesempateCodigo.MaiorIdade, CriterioDesempateCodigo.Idoso],
            "Ordem (1 para MaiorIdade, 2 para Idoso) governa a posição — o código da regra, em ordem alfabética, apontaria para o oposto");
    }
}
