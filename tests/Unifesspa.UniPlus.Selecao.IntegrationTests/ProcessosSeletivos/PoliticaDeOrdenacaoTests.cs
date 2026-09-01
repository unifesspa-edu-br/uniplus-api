namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

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
/// </list>
/// <para>
/// <b>Fora do escopo desta suíte, por já terem oráculo de sequência exata próprio</b> (chave de
/// conteúdo, ADR-0109 D9 — a camada que este arquivo não repete): <c>classificacao.regrasEliminacao</c>
/// (<see cref="EnvelopeCanonicoGoldenTests.Envelope_IndependeDaOrdemDeCriacao"/>) e os sete conjuntos
/// da issue #1067 — <c>criteriosCumulativos</c>, <c>ocorrenciasEsperadas</c>,
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
public sealed class PoliticaDeOrdenacaoTests
{
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizador = new();

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(semente, 64)).Value!;

    private static DadosEdital Dados() => DadosEdital.Criar(
        numero: "001/2026",
        periodoInscricaoInicio: new DateOnly(2026, 1, 1),
        periodoInscricaoFim: new DateOnly(2026, 1, 31),
        documentoEditalId: Guid.CreateVersion7()).Value!;

    private static EntradaCanonicalizacao Entrada(
        ProcessoSeletivo processo,
        IReadOnlyDictionary<string, MetadadoFatoCongelado>? metadadosFatos = null) =>
        new(processo, Dados(), new string('0', 64), FusoInstitucional.ZoneId, MetadadosFatosCongelados: metadadosFatos);

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
                RegraRemanejamentoModalidade.Nenhuma, null, null, null, [], null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 40).Value!,
        ]).Value!;

    private static FaseCronograma FaseMinima() => FaseCronograma.Criar(
        ordem: 1,
        faseCanonicaOrigemId: Guid.CreateVersion7(),
        codigo: "INSCRICAO",
        donoInstitucional: "CEPS",
        origemData: OrigemDataFase.Propria,
        agrupaEtapas: true,
        permiteComplementacao: true,
        produzResultado: true,
        resultadoDefinitivo: true,
        coletaInscricao: true,
        inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
        atoProduzidoCodigo: "INSCRICAO",
        atoProduzidoEfeitoIrreversivel: false,
        bancasRequeridas: [],
        regraRecurso: null).Value!;

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
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        processo.DefinirEtapas(etapas ?? [
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!, peso: 1m, ordem: 1).Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            ofertaAtendimento ?? OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirDistribuicaoVagas([DistribuicaoMinima()], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        if (criteriosDesempate is not null)
        {
            processo.DefinirCriteriosDesempate(criteriosDesempate, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        }

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            regraCalculo: Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'),
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'),
            nOpcoesAlocacao: 1,
            regrasEliminacao: [],
            baseadoEmEnem: false).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirCronogramaFases(
            cronogramaFases ?? [FaseMinima()], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        if (documentosExigidos is not null)
        {
            processo.DefinirDocumentosExigidos(documentosExigidos, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        }

        if (fatosColetados is not null)
        {
            processo.DefinirFatosColetados(fatosColetados, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
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
        EtapaProcesso zeta = EtapaProcesso.Reidratar(IdFixo(2), "Zeta", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!, peso: 1m, notaMinima: null, ordem: 1);
        EtapaProcesso alfa = EtapaProcesso.Reidratar(IdFixo(1), "Alfa", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!, peso: 1m, notaMinima: null, ordem: 2);

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
        EtapaProcesso zeta = EtapaProcesso.Reidratar(IdFixo(1), "Zeta", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!, peso: 1m, notaMinima: null, ordem: null);
        EtapaProcesso alfa = EtapaProcesso.Reidratar(IdFixo(2), "Alfa", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!, peso: 1m, notaMinima: null, ordem: null);

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
        // O MESMO TipoEtapaSnapshot para as duas etapas — não um TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), ...)
        // por etapa (flaky sob CORRIDA, issue #1115): duas chamadas independentes gerariam origemId
        // distintos, e origemId entra em SerializarTipoEtapa/SemId — as etapas deixariam de ser
        // conteudisticamente idênticas, e o ThenBy de CONTEÚDO (não o de Id, que é o objeto real
        // desta prova) desempataria a posição, tornando o teste dependente da ordem de criação dos Guid v7.
        TipoEtapaSnapshot tipoEtapa = TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!;
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
            ordem: 1, faseCanonicaOrigemId: Guid.CreateVersion7(), codigo: "INSCRICAO", donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Propria, agrupaEtapas: false, permiteComplementacao: true, produzResultado: true,
            resultadoDefinitivo: true, coletaInscricao: true,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            atoProduzidoCodigo: "INSCRICAO", atoProduzidoEfeitoIrreversivel: false, bancasRequeridas: [], regraRecurso: null).Value!;

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

    [Fact(DisplayName = "cronogramaFases.fases: Ordem governa sobre o conteúdo — oráculo de sequência exata")]
    public void CronogramaFases_Fases_OrdemGovernaSobreConteudo()
    {
        // Id CONTRÁRIO à Ordem também: ZETA_FASE (Ordem 1) recebe o Id MAIOR, ALFA_FASE (Ordem 2)
        // o MENOR — se o Id decidisse a posição, o resultado seria [ALFA_FASE, ZETA_FASE], o mesmo
        // "errado" que o código (proxy de conteúdo) daria.
        FaseCronograma faseZeta = FaseCronograma.Reidratar(
            id: IdFixo(2), ordem: 1, faseCanonicaOrigemId: Guid.CreateVersion7(), codigo: "ZETA_FASE",
            donoInstitucional: "CEPS", origemData: OrigemDataFase.Propria, agrupaEtapas: true,
            permiteComplementacao: true, produzResultado: true, resultadoDefinitivo: true, coletaInscricao: true,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            atoProduzidoCodigo: "ZETA_FASE", atoProduzidoEfeitoIrreversivel: false, bancasRequeridas: [], regraRecurso: null);
        FaseCronograma faseAlfa = FaseCronograma.Reidratar(
            id: IdFixo(1), ordem: 2, faseCanonicaOrigemId: Guid.CreateVersion7(), codigo: "ALFA_FASE",
            donoInstitucional: "CEPS", origemData: OrigemDataFase.Propria, agrupaEtapas: true,
            permiteComplementacao: true, produzResultado: true, resultadoDefinitivo: true, coletaInscricao: true,
            inicio: new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 2, 28, 0, 0, 0, TimeSpan.Zero),
            atoProduzidoCodigo: "ALFA_FASE", atoProduzidoEfeitoIrreversivel: false, bancasRequeridas: [], regraRecurso: null);

        new[] { faseZeta, faseAlfa }.OrderBy(static f => f.Codigo, StringComparer.Ordinal).Select(static f => f.Codigo)
            .Should().Equal(["ALFA_FASE", "ZETA_FASE"], "pré-condição: ordenar pelo código (proxy de conteúdo) dá o oposto do oráculo de Ordem abaixo");
        new[] { faseZeta, faseAlfa }.OrderBy(static f => f.Id).Select(static f => f.Codigo)
            .Should().Equal(["ALFA_FASE", "ZETA_FASE"], "pré-condição: ordenar pelo Id dá o oposto do oráculo de Ordem abaixo");

        ProcessoSeletivo processo = Montar(cronogramaFases: [faseAlfa, faseZeta]);

        JsonArray fasesJson = EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))["cronogramaFases"]!["fases"]!.AsArray();

        fasesJson.Select(static f => f!["codigo"]!.GetValue<string>()).Should().Equal(
            ["ZETA_FASE", "ALFA_FASE"],
            "Ordem (1 para ZETA_FASE, 2 para ALFA_FASE) governa a posição — o código, em ordem alfabética, apontaria para o oposto");
    }

    [Fact(DisplayName = "cronogramaFases.fases[].bancasRequeridas: identidade de origem governa sobre o conteúdo — oráculo de sequência exata")]
    public void CronogramaFases_BancasRequeridas_IdentidadeDeOrigemGovernaSobreConteudo()
    {
        BancaRequerida bancaComOrigemMenor = BancaRequerida.Criar(IdFixo(1), "ZETA_BANCA");
        BancaRequerida bancaComOrigemMaior = BancaRequerida.Criar(IdFixo(2), "ALFA_BANCA");

        new[] { bancaComOrigemMenor, bancaComOrigemMaior }.OrderBy(static b => b.Codigo, StringComparer.Ordinal).Select(static b => b.Codigo)
            .Should().Equal(["ALFA_BANCA", "ZETA_BANCA"], "pré-condição: ordenar pelo código (proxy de conteúdo) dá o oposto do oráculo de identidade abaixo");

        FaseCronograma fase = FaseCronograma.Criar(
            ordem: 1, faseCanonicaOrigemId: Guid.CreateVersion7(), codigo: "INSCRICAO", donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Propria, agrupaEtapas: true, permiteComplementacao: true, produzResultado: true,
            resultadoDefinitivo: true, coletaInscricao: true,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            atoProduzidoCodigo: "INSCRICAO", atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [bancaComOrigemMaior, bancaComOrigemMenor], regraRecurso: null).Value!;

        ProcessoSeletivo processo = Montar(cronogramaFases: [fase]);

        JsonArray bancasJson = EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))
            ["cronogramaFases"]!["fases"]![0]!["bancasRequeridas"]!.AsArray();

        bancasJson.Select(static b => b!["codigo"]!.GetValue<string>()).Should().Equal(
            ["ZETA_BANCA", "ALFA_BANCA"],
            "a identidade de origem (TipoBancaOrigemId) governa a posição — o código, em ordem alfabética, apontaria para o oposto");
    }

    [Fact(DisplayName = "fatosColetados: Ordem governa sobre o conteúdo (FatoCodigo) — oráculo de sequência exata")]
    public void FatosColetados_OrdemGovernaSobreConteudo()
    {
        FatoColetado fatoZeta = FatoColetado.Criar("ZETA_FATO", 0, "Rótulo Zeta", TipoRenderizacao.Booleano, obrigatorio: false, null).Value!;
        FatoColetado fatoAlfa = FatoColetado.Criar("ALFA_FATO", 1, "Rótulo Alfa", TipoRenderizacao.Booleano, obrigatorio: false, null).Value!;

        new[] { fatoZeta, fatoAlfa }.OrderBy(static f => f.FatoCodigo, StringComparer.Ordinal).Select(static f => f.FatoCodigo)
            .Should().Equal(["ALFA_FATO", "ZETA_FATO"], "pré-condição: ordenar pelo FatoCodigo (proxy de conteúdo) dá o oposto do oráculo de Ordem abaixo");

        ProcessoSeletivo processo = Montar(fatosColetados: [fatoAlfa, fatoZeta]);

        JsonArray fatosJson = EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))["fatosColetados"]!.AsArray();

        fatosJson.Select(static f => f!["fatoCodigo"]!.GetValue<string>()).Should().Equal(
            ["ZETA_FATO", "ALFA_FATO"],
            "Ordem (0 para ZETA_FATO, 1 para ALFA_FATO) governa a posição — o FatoCodigo, em ordem alfabética, apontaria para o oposto");
    }

    [Fact(DisplayName = "regrasDerivacao[].regras: Ordem governa sobre o conteúdo (Contribui) — oráculo de sequência exata")]
    public void RegrasDerivacao_RegrasInternas_OrdemGovernaSobreConteudo()
    {
        RegraDerivacaoConfigurada regraZeta = RegraDerivacaoConfigurada.Criar(0, "ZETA_VALOR", null).Value!;
        RegraDerivacaoConfigurada regraAlfa = RegraDerivacaoConfigurada.Criar(1, "ALFA_VALOR", null).Value!;

        new[] { regraZeta, regraAlfa }.OrderBy(static r => r.Contribui, StringComparer.Ordinal).Select(static r => r.Contribui)
            .Should().Equal(["ALFA_VALOR", "ZETA_VALOR"], "pré-condição: ordenar por Contribui (proxy de conteúdo) dá o oposto do oráculo de Ordem abaixo");

        ConfiguracaoDerivacaoFato config = ConfiguracaoDerivacaoFato.Criar("FATO_TESTE", [regraAlfa, regraZeta]).Value!;
        ProcessoSeletivo processo = Montar(regrasDerivacao: [config]);

        JsonArray regrasJson = EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))["regrasDerivacao"]![0]!["regras"]!.AsArray();

        regrasJson.Select(static r => r!["contribui"]!.GetValue<string>()).Should().Equal(
            ["ZETA_VALOR", "ALFA_VALOR"],
            "Ordem (0 para ZETA_VALOR, 1 para ALFA_VALOR) governa a posição — Contribui, em ordem alfabética, apontaria para o oposto");
    }

    [Fact(DisplayName = "cascataRemanejamento.ordens[].destinos: Ordem governa sobre o conteúdo, dentro da mesma origem — oráculo de sequência exata")]
    public void CascataRemanejamento_DestinosDaMesmaOrigem_OrdemGovernaSobreConteudo()
    {
        DestinoRemanejamento destinoZeta = DestinoRemanejamento.Criar("ORIGEM_TESTE", 1, "ZETA_DEST").Value!;
        DestinoRemanejamento destinoAlfa = DestinoRemanejamento.Criar("ORIGEM_TESTE", 2, "ALFA_DEST").Value!;

        new[] { destinoZeta, destinoAlfa }.OrderBy(static d => d.ModalidadeDestinoCodigo, StringComparer.Ordinal).Select(static d => d.ModalidadeDestinoCodigo)
            .Should().Equal(["ALFA_DEST", "ZETA_DEST"], "pré-condição: ordenar pelo código do destino (proxy de conteúdo) dá o oposto do oráculo de Ordem abaixo");

        ConfiguracaoCascataRemanejamento cascata = ConfiguracaoCascataRemanejamento.Criar(
            Regra(RegraRemanejamentoCodigo.Cascata, 'e'), "FALLBACK_TESTE", [destinoAlfa, destinoZeta]).Value!;

        ProcessoSeletivo processo = Montar(cascata: cascata);

        JsonArray destinosJson = EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))
            ["cascataRemanejamento"]!["ordens"]![0]!["destinos"]!.AsArray();

        destinosJson.Select(static d => d!.GetValue<string>()).Should().Equal(
            ["ZETA_DEST", "ALFA_DEST"],
            "Ordem (1 para ZETA_DEST, 2 para ALFA_DEST) governa a posição dentro da origem — o código, em ordem alfabética, apontaria para o oposto");
    }

    [Fact(DisplayName = "cascataRemanejamento.ordens: as origens ordenam pelo código, ordinal — não pela ordem física de entrada")]
    public void CascataRemanejamento_Origens_OrdenaAlfabeticamentePorCodigo()
    {
        DestinoRemanejamento destinoDaZeta = DestinoRemanejamento.Criar("ZETA_ORIGEM", 1, "DESTINO_COMUM_A").Value!;
        DestinoRemanejamento destinoDaAlfa = DestinoRemanejamento.Criar("ALFA_ORIGEM", 1, "DESTINO_COMUM_B").Value!;

        // Inserida fisicamente na ordem ERRADA (ZETA_ORIGEM antes de ALFA_ORIGEM) — um canonicalizador
        // que preservasse a ordem de ENTRADA reproduziria esta mesma sequência errada.
        ConfiguracaoCascataRemanejamento cascata = ConfiguracaoCascataRemanejamento.Criar(
            Regra(RegraRemanejamentoCodigo.Cascata, 'f'), "FALLBACK_TESTE", [destinoDaZeta, destinoDaAlfa]).Value!;

        ProcessoSeletivo processo = Montar(cascata: cascata);

        JsonArray ordensJson = EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))["cascataRemanejamento"]!["ordens"]!.AsArray();

        ordensJson.Should().HaveCount(2, "pré-condição: duas origens distintas sob teste");
        ordensJson.Select(static o => o!["origem"]!.GetValue<string>()).Should().Equal(
            ["ALFA_ORIGEM", "ZETA_ORIGEM"],
            "as origens ordenam por código, ordinal — não pela ordem física de entrada, que era [ZETA_ORIGEM, ALFA_ORIGEM]");
    }

    /// <summary>
    /// Combina raízes e filhos num teste só, com a precondição que o parecer da issue nomeou:
    /// <b>duas raízes e dois irmãos</b> — duas raízes de um filho cada deixariam <c>filhos</c>
    /// trivial (qualquer ordenação produziria o mesmo resultado).
    /// </summary>
    [Fact(DisplayName = "arvoreSatisfacao: raízes e filhos — Ordem governa sobre o conteúdo, com duas raízes e dois irmãos")]
    public void ArvoreSatisfacao_RaizesEFilhos_OrdemGovernaSobreConteudo()
    {
        FaseCronograma fase = FaseMinima();

        DocumentoExigido docRaizFolha = DocumentoExigido.Criar(
            fase.Id, Guid.CreateVersion7(), "RAIZ_ZETA", "Documento raiz solteira", "PESSOAL",
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null, [], [], null,
            FormatosPermitidos.Criar(qualquer: true, entradas: null).Value!, null).Value!;

        // Exigência e nó reidratados com Id FIXO e CONTRÁRIO à Ordem — não Guid.CreateVersion7(),
        // que alinharia acidentalmente os Ids com a ordem de criação (e, por tabela, com a Ordem
        // esperada): o filho ZETA (Ordem 0, deve vir PRIMEIRO) recebe o Id MAIOR; o filho ALFA
        // (Ordem 1, deve vir SEGUNDO) recebe o Id MENOR, tanto na exigência quanto no nó.
        DocumentoExigido docFilhoZeta = DocumentoExigido.Reidratar(
            IdFixo(2), fase.Id, Guid.CreateVersion7(), "FILHO_ZETA", "Documento filho zeta", "PESSOAL",
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null, grupoSatisfacaoId: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(qualquer: true, entradas: null).Value!, tamanhoMaximoBytes: null);
        DocumentoExigido docFilhoAlfa = DocumentoExigido.Reidratar(
            IdFixo(1), fase.Id, Guid.CreateVersion7(), "FILHO_ALFA", "Documento filho alfa", "PESSOAL",
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null, grupoSatisfacaoId: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(qualquer: true, entradas: null).Value!, tamanhoMaximoBytes: null);

        NoExigencia raizFolha = NoExigencia.CriarFolha(docRaizFolha, ordem: 1).Value!;
        // filhoZeta tem Ordem MENOR (0) mas Id do nó MAIOR — a chave em conflito que distingue
        // "Ordem governa" de "identidade técnica (Id) governa".
        NoExigencia filhoZeta = NoExigencia.Reidratar(
            IdFixo(4), TipoNo.Folha, ordem: 0, docFilhoZeta.Id, docFilhoZeta,
            quantidadeMinima: NoExigencia.QuantidadeMinimaPadrao, consequencia: null, chaveDistincao: null,
            dataReferencia: null, ocorrenciasEsperadas: null, repetePorEntidade: null, basesLegais: [], filhos: []);
        NoExigencia filhoAlfa = NoExigencia.Reidratar(
            IdFixo(3), TipoNo.Folha, ordem: 1, docFilhoAlfa.Id, docFilhoAlfa,
            quantidadeMinima: NoExigencia.QuantidadeMinimaPadrao, consequencia: null, chaveDistincao: null,
            dataReferencia: null, ocorrenciasEsperadas: null, repetePorEntidade: null, basesLegais: [], filhos: []);

        // Dois irmãos — não um: duas raízes de um filho cada deixaria "filhos" trivial. Inseridos na
        // ordem física ERRADA (Alfa antes de Zeta).
        NoExigencia raizGrupo = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, ordem: 0, quantidadeMinima: 1, consequencia: null, basesLegais: [],
            filhos: [filhoAlfa, filhoZeta]).Value!;

        new[] { raizFolha, raizGrupo }.OrderBy(static r => r.Tipo.ToCodigo(), StringComparer.Ordinal).Select(static r => r.Tipo.ToCodigo())
            .Should().Equal(["FOLHA", "OU"], "pré-condição: ordenar pelo tipo (proxy de conteúdo) dá o oposto do oráculo de Ordem das raízes");
        // Pré-condição calculada sobre o MESMO campo que arvoreSatisfacao[].filhos[] realmente
        // emite para o nó (o Id, técnico) — não sobre TipoDocumentoCodigo, que esse caminho não
        // emite: um mutante que ordenasse os filhos pelo Id do nó produziria exatamente esta
        // sequência oposta.
        new[] { filhoAlfa, filhoZeta }.OrderBy(static f => f.Id).Select(static f => f.DocumentoExigidoId)
            .Should().Equal(
                [docFilhoAlfa.Id, docFilhoZeta.Id],
                "pré-condição: ordenar pelo Id do nó (identidade técnica, não conteúdo) dá o oposto do oráculo de Ordem dos filhos abaixo");

        // Inseridas na ordem física ERRADA (folha, cuja Ordem é maior, antes do grupo).
        ProcessoSeletivo processo = Montar(cronogramaFases: [fase], documentosExigidos: [raizFolha, raizGrupo]);

        JsonArray arvoreJson = EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))["arvoreSatisfacao"]!.AsArray();

        arvoreJson.Should().HaveCount(2, "pré-condição: duas raízes, como o cenário exige");
        arvoreJson.Select(static r => r!["tipo"]!.GetValue<string>()).Should().Equal(
            ["OU", "FOLHA"],
            "Ordem (0 para o grupo OU, 1 para a folha solteira) governa a posição das raízes — o tipo, em ordem alfabética, apontaria para o oposto");

        JsonArray filhosJson = arvoreJson[0]!["filhos"]!.AsArray();
        filhosJson.Should().HaveCount(2, "pré-condição: dois irmãos, não um — com um só, 'filhos' seria trivial");
        filhosJson.Select(static f => f!["exigenciaId"]!.GetValue<Guid>()).Should().Equal(
            [docFilhoZeta.Id, docFilhoAlfa.Id],
            "Ordem (0 para o filho ZETA, 1 para o filho ALFA) governa a posição dos irmãos — o Id do nó, contrário à Ordem, apontaria para o oposto");
    }

    [Fact(DisplayName = "documentosExigidos.metadadosFatos[].valoresDominioDeclarados: Ordem governa sobre o Codigo — oráculo de sequência exata")]
    public void MetadadosFatos_ValoresDominioDeclarados_OrdemGovernaSobreCodigo()
    {
        // Descrições conflitantes que DIFEREM e PRECEDEM `ordem` na ordenação alfabética das
        // chaves do item canônico (`descricao` < `ordem` < `valorCodigo`) — com Descricao null
        // nos dois itens, a ordenação pelos bytes do item coincidiria com a ordenação por Ordem
        // (o primeiro byte que divergiria entre os dois itens seria já `ordem`), e um mutante que
        // trocasse a política por conteúdo passaria despercebido.
        ValorDominioDeclaradoCongelado valorZeta = new("ZETA_VALOR", "Zeta descricao", 0);
        ValorDominioDeclaradoCongelado valorAlfa = new("ALFA_VALOR", "Alfa descricao", 1);

        new[] { valorZeta, valorAlfa }.OrderBy(static v => v.Codigo, StringComparer.Ordinal).Select(static v => v.Codigo)
            .Should().Equal(["ALFA_VALOR", "ZETA_VALOR"], "pré-condição: ordenar pelo Codigo dá o oposto do oráculo de Ordem abaixo");

        // Pré-condição pelos bytes canônicos REAIS do item, na mesma forma que a produção emite
        // (`valorCodigo`/`descricao`/`ordem`, reordenados pelo perfil em `descricao`/`ordem`/
        // `valorCodigo`): um mutante que ordenasse pelo conteúdo do item divergiria já em
        // `descricao` — antes de alcançar `ordem` — e produziria a sequência oposta ao oráculo de
        // Ordem abaixo.
        byte[] bytesZeta = PerfilCanonicoV1.Instancia.Serializar(new JsonObject
        {
            ["valorCodigo"] = valorZeta.Codigo,
            ["descricao"] = valorZeta.Descricao,
            ["ordem"] = valorZeta.Ordem,
        });
        byte[] bytesAlfa = PerfilCanonicoV1.Instancia.Serializar(new JsonObject
        {
            ["valorCodigo"] = valorAlfa.Codigo,
            ["descricao"] = valorAlfa.Descricao,
            ["ordem"] = valorAlfa.Ordem,
        });
        ComparadorLexicograficoDeBytes.Instancia.Compare(bytesAlfa, bytesZeta).Should().BeLessThan(0,
            "pré-condição: os bytes canônicos reais do item (a chave `descricao`, que precede `ordem`) colocariam " +
            "ALFA_VALOR antes de ZETA_VALOR — o oposto do oráculo de Ordem abaixo — se a política fosse ordenar pelo " +
            "conteúdo do item em vez de por Ordem");

        Dictionary<string, MetadadoFatoCongelado> metadados = new(StringComparer.Ordinal)
        {
            ["COR_RACA"] = new MetadadoFatoCongelado(
                Codigo: "COR_RACA", Dominio: "CATEGORICO", Origem: "DECLARADO", Cardinalidade: "ESCALAR",
                PontoResolucao: "INSCRICAO", Binding: "CAMPO_INSCRICAO:COR_RACA",
                ValoresDominio: null, ValoresDominioDeclarados: [valorAlfa, valorZeta]),
        };

        ProcessoSeletivo processo = Montar();
        JsonArray declaradosJson = EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(processo, metadados)))
            ["documentosExigidos"]!["metadadosFatos"]![0]!["valoresDominioDeclarados"]!.AsArray();

        declaradosJson.Select(static v => v!["valorCodigo"]!.GetValue<string>()).Should().Equal(
            ["ZETA_VALOR", "ALFA_VALOR"],
            "Ordem (0 para ZETA_VALOR, 1 para ALFA_VALOR) governa a posição — o Codigo, em ordem alfabética, apontaria para o oposto");
    }

    // ── camada 2 — identidade de negócio ──

    /// <summary>
    /// Cobre os TRÊS arrays de <c>SerializarAtendimento</c> — não só <c>condicoes</c>: embora as
    /// três coleções passem pela mesma FORMA de expressão (<c>OrderBy(*OrigemId)</c>), são três
    /// expressões <c>OrderBy</c> INDEPENDENTES; regredir uma sozinha (ex.: só <c>recursos</c> para
    /// conteúdo, ou só <c>tiposDeficiencia</c> sem ordenação) não é pego por um oráculo que só
    /// observa <c>condicoes</c>. <c>tiposDeficiencia</c> exige a condição PCD ofertada (ADR-0067) —
    /// por isso a terceira condição, cujo Id de origem fica FORA do conflito Zeta/Alfa.
    /// </summary>
    [Fact(DisplayName = "atendimento.condicoes/recursos/tiposDeficiencia: identidade de origem governa sobre o conteúdo — oráculo de sequência exata para os três arrays")]
    public void OfertaAtendimento_Condicoes_IdentidadeDeOrigemGovernaSobreConteudo()
    {
        OfertaCondicao condicaoComOrigemMenor = OfertaCondicao.Criar(IdFixo(1), "COD_ZETA", "Zeta Nome");
        OfertaCondicao condicaoComOrigemMaior = OfertaCondicao.Criar(IdFixo(2), "COD_ALFA", "Alfa Nome");
        OfertaCondicao condicaoPcd = OfertaCondicao.Criar(IdFixo(9), OfertaAtendimentoEspecializado.CodigoCondicaoPcd, "Atendimento PCD");

        OfertaRecurso recursoComOrigemMenor = OfertaRecurso.Criar(IdFixo(1), "Zeta Recurso");
        OfertaRecurso recursoComOrigemMaior = OfertaRecurso.Criar(IdFixo(2), "Alfa Recurso");

        OfertaTipoDeficiencia tipoComOrigemMenor = OfertaTipoDeficiencia.Criar(IdFixo(1), "ZETA_TIPO", "Zeta Tipo");
        OfertaTipoDeficiencia tipoComOrigemMaior = OfertaTipoDeficiencia.Criar(IdFixo(2), "ALFA_TIPO", "Alfa Tipo");

        new[] { condicaoComOrigemMenor, condicaoComOrigemMaior }.OrderBy(static c => c.CondicaoNome, StringComparer.Ordinal).Select(static c => c.CondicaoNome)
            .Should().Equal(["Alfa Nome", "Zeta Nome"], "pré-condição: ordenar pelo nome (proxy de conteúdo) dá o oposto do oráculo de identidade abaixo");
        new[] { recursoComOrigemMenor, recursoComOrigemMaior }.OrderBy(static r => r.RecursoNome, StringComparer.Ordinal).Select(static r => r.RecursoNome)
            .Should().Equal(["Alfa Recurso", "Zeta Recurso"], "pré-condição: ordenar pelo nome (proxy de conteúdo) dá o oposto do oráculo de identidade abaixo");
        new[] { tipoComOrigemMenor, tipoComOrigemMaior }.OrderBy(static t => t.TipoDeficienciaNome, StringComparer.Ordinal).Select(static t => t.TipoDeficienciaNome)
            .Should().Equal(["Alfa Tipo", "Zeta Tipo"], "pré-condição: ordenar pelo nome (proxy de conteúdo) dá o oposto do oráculo de identidade abaixo");

        OfertaAtendimentoEspecializado oferta = OfertaAtendimentoEspecializado.Criar(
            condicoes: [condicaoComOrigemMaior, condicaoComOrigemMenor, condicaoPcd],
            recursos: [recursoComOrigemMaior, recursoComOrigemMenor],
            tiposDeficiencia: [tipoComOrigemMaior, tipoComOrigemMenor]).Value!;

        ProcessoSeletivo processo = Montar(ofertaAtendimento: oferta);

        JsonObject atendimentoJson = EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))["atendimento"]!.AsObject();

        atendimentoJson["condicoes"]!.AsArray().Select(static c => c!["condicaoNome"]!.GetValue<string>()).Should().Equal(
            ["Zeta Nome", "Alfa Nome", "Atendimento PCD"],
            "a identidade de origem (CondicaoOrigemId) governa a posição — o nome, em ordem alfabética, apontaria para o oposto");
        atendimentoJson["recursos"]!.AsArray().Select(static r => r!["recursoNome"]!.GetValue<string>()).Should().Equal(
            ["Zeta Recurso", "Alfa Recurso"],
            "a identidade de origem (RecursoOrigemId) governa a posição — o nome, em ordem alfabética, apontaria para o oposto. É uma " +
            "expressão OrderBy própria, independente da de condicoes — regredir só esta não seria pego pelo oráculo de condicoes");
        atendimentoJson["tiposDeficiencia"]!.AsArray().Select(static t => t!["tipoDeficienciaNome"]!.GetValue<string>()).Should().Equal(
            ["Zeta Tipo", "Alfa Tipo"],
            "a identidade de origem (TipoDeficienciaOrigemId) governa a posição — o nome, em ordem alfabética, apontaria para o oposto. É " +
            "uma expressão OrderBy própria, independente das de condicoes/recursos — regredir só esta não seria pego pelos outros oráculos");
    }

    [Fact(DisplayName = "documentosExigidos.exigencias: identidade parcial (fase + tipo) governa sobre o conteúdo — oráculo de sequência exata")]
    public void DocumentosExigidos_Exigencias_IdentidadeParcialGovernaSobreConteudo()
    {
        FaseCronograma fase = FaseMinima();

        DocumentoExigido exigenciaComOrigemMenor = DocumentoExigido.Reidratar(
            Guid.CreateVersion7(), fase.Id, IdFixo(1), "TIPO_COMUM", "Zeta Documento", "PESSOAL",
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null, grupoSatisfacaoId: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(qualquer: true, entradas: null).Value!, tamanhoMaximoBytes: null);
        DocumentoExigido exigenciaComOrigemMaior = DocumentoExigido.Reidratar(
            Guid.CreateVersion7(), fase.Id, IdFixo(2), "TIPO_COMUM", "Alfa Documento", "PESSOAL",
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null, grupoSatisfacaoId: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(qualquer: true, entradas: null).Value!, tamanhoMaximoBytes: null);

        new[] { exigenciaComOrigemMenor, exigenciaComOrigemMaior }.OrderBy(static e => e.TipoDocumentoNome, StringComparer.Ordinal)
            .Select(static e => e.TipoDocumentoNome)
            .Should().Equal(["Alfa Documento", "Zeta Documento"], "pré-condição: ordenar pelo nome (proxy de conteúdo) dá o oposto do oráculo de identidade abaixo");

        NoExigencia raizMenor = NoExigencia.CriarFolha(exigenciaComOrigemMenor, 0).Value!;
        NoExigencia raizMaior = NoExigencia.CriarFolha(exigenciaComOrigemMaior, 1).Value!;

        ProcessoSeletivo processo = Montar(cronogramaFases: [fase], documentosExigidos: [raizMaior, raizMenor]);

        JsonArray exigenciasJson = EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))["documentosExigidos"]!["exigencias"]!.AsArray();

        exigenciasJson.Should().HaveCount(2, "pré-condição: duas exigências, mesma fase, TipoDocumentoOrigemId distintos");
        exigenciasJson.Select(static e => e!["tipoDocumentoNome"]!.GetValue<string>()).Should().Equal(
            ["Zeta Documento", "Alfa Documento"],
            "a identidade de origem (TipoDocumentoOrigemId, dentro da MESMA fase) governa a posição — o conteúdo, em ordem alfabética, " +
            "apontaria para o oposto");
    }

    [Fact(DisplayName = "documentosExigidos.exigencias: no empate de identidade (mesma fase e tipo), o conteúdo desempata — não o Id")]
    public void DocumentosExigidos_Exigencias_EmpateDeIdentidade_DesempataPeloConteudo()
    {
        FaseCronograma fase = FaseMinima();
        Guid tipoDocumentoOrigemComum = Guid.CreateVersion7();

        DocumentoExigido exigenciaComIdMaior = DocumentoExigido.Reidratar(
            IdFixo(2), fase.Id, tipoDocumentoOrigemComum, "TIPO_COMUM", "Alfa Documento", "PESSOAL",
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null, grupoSatisfacaoId: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(qualquer: true, entradas: null).Value!, tamanhoMaximoBytes: null);
        DocumentoExigido exigenciaComIdMenor = DocumentoExigido.Reidratar(
            IdFixo(1), fase.Id, tipoDocumentoOrigemComum, "TIPO_COMUM", "Zeta Documento", "PESSOAL",
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null, grupoSatisfacaoId: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(qualquer: true, entradas: null).Value!, tamanhoMaximoBytes: null);

        new[] { exigenciaComIdMaior, exigenciaComIdMenor }.OrderBy(static e => e.Id).Select(static e => e.TipoDocumentoNome)
            .Should().Equal(["Zeta Documento", "Alfa Documento"], "pré-condição: ordenar por Id dá o oposto do oráculo de conteúdo abaixo");

        NoExigencia raizComIdMaior = NoExigencia.CriarFolha(exigenciaComIdMaior, 0).Value!;
        NoExigencia raizComIdMenor = NoExigencia.CriarFolha(exigenciaComIdMenor, 1).Value!;

        ProcessoSeletivo processo = Montar(cronogramaFases: [fase], documentosExigidos: [raizComIdMaior, raizComIdMenor]);

        JsonArray exigenciasJson = EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))["documentosExigidos"]!["exigencias"]!.AsArray();

        exigenciasJson.Select(static e => e!["tipoDocumentoNome"]!.GetValue<string>()).Should().Equal(
            ["Alfa Documento", "Zeta Documento"],
            "duas exigências para a MESMA fase e o MESMO tipo de documento (nada impede isso) empatam na identidade — o conteúdo resolve " +
            "o empate, não o Id, que apontaria para o oposto");
    }

    // ── delegação — grafoDependencia ──

    /// <summary>
    /// <c>B_FATO</c> (Ordem 0, sem pré-condição) produz <c>A_FATO</c> (Ordem 1, pré-condição cita
    /// <c>B_FATO</c>): o produtor tem de vir antes do consumidor na ordem topológica. Alfabeticamente
    /// "A_FATO" precede "B_FATO" — o oposto da dependência real. O manifesto promete os TRÊS arrays
    /// de <c>grafoDependencia</c> (<c>nos</c>, <c>arestas</c>, <c>ordemTopologica</c>) — este teste
    /// afirma a sequência exata dos três para o mesmo cenário, não só de <c>ordemTopologica</c>: um
    /// mutante que reordenasse <c>nos</c> ou <c>arestas</c> sozinho (alfabeticamente, ou pela ordem
    /// física de uma coleção diferente da que o domínio devolve) passaria despercebido se só
    /// <c>ordemTopologica</c> fosse observada.
    /// </summary>
    [Fact(DisplayName = "grafoDependencia.nos/arestas/ordemTopologica: delegação — a ordem produzida pelo domínio é preservada, não reordenada alfabeticamente")]
    public void GrafoDependencia_OrdemTopologica_PreservaAOrdemDoDominio_NaoReordenaAlfabeticamente()
    {
        FatoColetado fatoB = FatoColetado.Criar("B_FATO", 0, "Rótulo B", TipoRenderizacao.Booleano, obrigatorio: false, null).Value!;
        FatoColetado fatoA = FatoColetado.Criar("A_FATO", 1, "Rótulo A", TipoRenderizacao.Booleano, obrigatorio: false, [
            CondicaoPrecondicaoFato.Criar(0, "B_FATO", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!,
        ]).Value!;

        ProcessoSeletivo processo = Montar(fatosColetados: [fatoA, fatoB]);

        JsonObject grafoJson = EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))
            ["grafoDependencia"]!.AsObject();

        List<string> ordemTopologica = [.. grafoJson["ordemTopologica"]!.AsArray().Select(static n => n!.GetValue<string>())];
        ordemTopologica.Should().HaveCountGreaterThanOrEqualTo(2, "pré-condição: o caminho existe e tem nós de A_FATO e de B_FATO");

        List<string> ordenadosAlfabeticamente = [.. ordemTopologica.OrderBy(static r => r, StringComparer.Ordinal)];
        int indiceDeAAlfabetico = ordenadosAlfabeticamente.FindIndex(static r => r.Contains("A_FATO", StringComparison.Ordinal));
        int indiceDeBAlfabetico = ordenadosAlfabeticamente.FindIndex(static r => r.Contains("B_FATO", StringComparison.Ordinal));
        indiceDeAAlfabetico.Should().BeLessThan(indiceDeBAlfabetico,
            "pré-condição: reordenar os MESMOS rótulos alfabeticamente poria A_FATO antes de B_FATO — o oposto da ordem topológica " +
            "que o domínio calcula (produtor antes do consumidor)");

        // Pré-condição sobre `arestas`: as origens das duas arestas de PRODUCAO ("CAMPO/" + o
        // código do fato) ordenadas alfabeticamente começariam por A_FATO — o oposto da ordem
        // canônica abaixo, que segue a ordem de coleta efetiva do nó de origem (B_FATO, Ordem 0,
        // primeiro), não o alfabeto.
        new[] { $"CAMPO/{fatoA.FatoCodigo}", $"CAMPO/{fatoB.FatoCodigo}" }.OrderBy(static r => r, StringComparer.Ordinal).First()
            .Should().Be($"CAMPO/{fatoA.FatoCodigo}", "pré-condição: a origem das duas arestas de PRODUCAO, alfabeticamente, começaria por CAMPO/A_FATO — o oposto do oráculo abaixo");

        ordemTopologica.Should().Equal(
            ["CAMPO/B_FATO", "FATO/B_FATO", "CAMPO/A_FATO", "FATO/A_FATO"],
            "a ordem topológica é CALCULADA por GrafoDependenciaConjunta (Kahn, produtor antes do consumidor) e apenas PRESERVADA " +
            "pela projeção — nunca recalculada nem reordenada alfabeticamente aqui");

        grafoJson["nos"]!.AsArray().Select(static n => n!["idCanonico"]!.GetValue<string>()).Should().Equal(
            ["CAMPO/B_FATO", "FATO/B_FATO", "CAMPO/A_FATO", "FATO/A_FATO"],
            "os nós saem na MESMA ordem canônica que GrafoDependenciaConjunta calcula (ordem de coleta efetiva, depois Classe/Codigo) — " +
            "não a ordem alfabética, que poria CAMPO/A_FATO primeiro");

        grafoJson["arestas"]!.AsArray()
            .Select(static a => (Tipo: a!["tipo"]!.GetValue<string>(), Origem: a["origem"]!.GetValue<string>(), Destino: a["destino"]!.GetValue<string>()))
            .Should().Equal(
                [
                    ("PRODUCAO", "CAMPO/B_FATO", "FATO/B_FATO"),
                    ("PRODUCAO", "CAMPO/A_FATO", "FATO/A_FATO"),
                    ("PRECONDICAO", "FATO/B_FATO", "CAMPO/A_FATO"),
                ],
                "as arestas saem ordenadas por (Tipo, Origem, Destino) canônicos — as duas de PRODUCAO desempatam pela ordem de coleta " +
                "efetiva da origem (B_FATO antes de A_FATO), o oposto da ordem alfabética da pré-condição acima");
    }
}
