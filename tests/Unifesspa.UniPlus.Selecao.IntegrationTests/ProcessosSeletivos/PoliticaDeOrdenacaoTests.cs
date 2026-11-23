namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDaPoliticaDeOrdenacao;

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
///   <item><term><c>etapas</c> (processo sem etapa)</term><description>Cenário de aceite: lista vazia canonicaliza sem recusa — <see cref="Etapas_ProcessoSemEtapaAlguma_ArrayVazioNadaRecusado"/></description></item>
///   <item><term><c>criteriosDesempate</c></term><description>Ordem semântica vs. conteúdo — <see cref="CriteriosDesempate_OrdemGovernaSobreConteudo"/></description></item>
///   <item><term><c>cronogramaFases.fases</c></term><description>Ordem semântica vs. conteúdo — <see cref="CronogramaFases_Fases_OrdemGovernaSobreConteudo"/></description></item>
///   <item><term><c>cronogramaFases.fases[].bancasRequeridas</c></term><description>Identidade de origem vs. conteúdo — <see cref="CronogramaFases_BancasRequeridas_IdentidadeDeOrigemGovernaSobreConteudo"/></description></item>
///   <item><term><c>fatosColetados</c></term><description>Ordem semântica vs. conteúdo — <see cref="FatosColetados_OrdemGovernaSobreConteudo"/></description></item>
///   <item><term><c>regrasDerivacao[].regras</c></term><description>Ordem semântica vs. conteúdo — <see cref="RegrasDerivacao_RegrasInternas_OrdemGovernaSobreConteudo"/></description></item>
///   <item><term><c>cascataRemanejamento.ordens[].destinos</c></term><description>Ordem semântica vs. conteúdo — <see cref="CascataRemanejamento_DestinosDaMesmaOrigem_OrdemGovernaSobreConteudo"/></description></item>
///   <item><term><c>cascataRemanejamento.ordens</c></term><description>Identidade de negócio (código da origem, ordinal) vs. ordem física de entrada — <see cref="CascataRemanejamento_Origens_OrdenaAlfabeticamentePorCodigo"/></description></item>
///   <item><term><c>regrasDeComprovacao</c></term><description>Ordem das regras e seus itens — <see cref="RegrasDeComprovacao_OrdemDeclaradaPrevalece"/></description></item>
///   <item><term><c>documentosExigidos.metadadosFatos[].valoresDominioDeclarados</c></term><description>Ordem semântica vs. Codigo — <see cref="MetadadosFatos_ValoresDominioDeclarados_OrdemGovernaSobreCodigo"/></description></item>
///   <item><term><c>atendimento.condicoes</c>/<c>recursos</c>/<c>tiposDeficiencia</c></term><description>Identidade de origem vs. conteúdo, oráculo próprio para os três arrays — <see cref="AtendimentoEspecializado_Condicoes_IdentidadeDeOrigemGovernaSobreConteudo"/></description></item>
///   <item><term><c>documentosExigidos.exigencias</c></term><description>Identidade parcial (fase + tipo) vs. conteúdo — <see cref="DocumentosExigidos_Exigencias_IdentidadeParcialGovernaSobreConteudo"/></description></item>
///   <item><term><c>documentosExigidos.exigencias</c> (empate de identidade)</term><description>Conteúdo vs. Id — <see cref="DocumentosExigidos_Exigencias_EmpateDeIdentidade_DesempataPeloConteudo"/></description></item>
///   <item><term><c>grafoDependencia.nos</c>/<c>arestas</c>/<c>ordemTopologica</c></term><description>DELEGAÇÃO — ordem derivada por <see cref="Domain.ValueObjects.GrafoDependenciaConjunta"/> e apenas preservada — <see cref="GrafoDependencia_OrdemTopologica_PreservaAOrdemDoDominio_NaoReordenaAlfabeticamente"/></description></item>
///   <item><term><c>fada.municipios</c></term><description>CodigoIbge empatado vs. ordem física de entrada — <see cref="BonusRegional_MunicipiosComMesmoCodigoIbge_DesempataPorNomeEUf"/></description></item>
/// </list>
/// <para>
/// <b>Fora do escopo desta suíte, por já terem oráculo de sequência exata próprio</b> (chave de
/// conteúdo, ADR-0109 D9 — a camada que este arquivo não repete): <c>classificacao.regrasEliminacao</c>
/// (<see cref="EnvelopeCanonicoGoldenTests.Envelope_IndependeDaOrdemDeCriacao"/>) e os seis conjuntos
/// da issue #1067 — <c>ocorrenciasEsperadas</c>,
/// <c>formatosPermitidos.lista</c>, <c>obrigatoriedades</c>, os dois <c>predicado.parametros</c> e
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
    // ── camada 1 — Ordem semântica ──

    [Fact(DisplayName = "etapas: Ordem informada governa sobre conteúdo e Id — oráculo de sequência exata")]
    public void Etapas_OrdemGovernaSobreConteudoEId()
    {
        // Id CONTRÁRIO à Ordem também: Zeta (Ordem 1) recebe o Id MAIOR, Alfa (Ordem 2) o MENOR —
        // se o Id decidisse a posição, o resultado seria [Alfa, Zeta], o mesmo "errado" que o
        // conteúdo (nome) daria.
        EtapaProcesso zeta = EtapaReidratada(IdFixo(2), "Zeta", 1);
        EtapaProcesso alfa = EtapaReidratada(IdFixo(1), "Alfa", 2);
        AfirmarQueNomeEIdContrariamAOrdem(zeta, alfa);

        JsonArray etapasJson = ArrayCanonicalizado(Montar(etapas: [alfa, zeta]), "etapas");

        etapasJson.Should().HaveCount(2, "pré-condição: o caminho existe e tem os dois itens sob teste");
        etapasJson.Select(static e => e!["nome"]!.GetValue<string>()).Should().Equal(
            ["Zeta", "Alfa"],
            "Ordem (1 para Zeta, 2 para Alfa) governa a posição — nem o nome nem o Id, que apontariam para o oposto");
    }

    private static void AfirmarQueNomeEIdContrariamAOrdem(EtapaProcesso zeta, EtapaProcesso alfa)
    {
        new[] { zeta, alfa }.OrderBy(static e => e.Nome, StringComparer.Ordinal).Select(static e => e.Nome)
            .Should().Equal(["Alfa", "Zeta"], "pré-condição: ordenar pelo nome (proxy de conteúdo) dá o oposto do oráculo de Ordem abaixo");
        new[] { zeta, alfa }.OrderBy(static e => e.Id).Select(static e => e.Nome)
            .Should().Equal(["Alfa", "Zeta"], "pré-condição: ordenar pelo Id dá o oposto do oráculo de Ordem abaixo");
    }

    private static FaseCronograma FaseDeInscricao() => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: "INSCRICAO", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: true, ColetaInscricao: true,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("INSCRICAO", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

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
        FaseCronograma faseSemAgruparEtapas = FaseDeInscricao();

        JsonArray etapasJson = ArrayCanonicalizado(Montar(etapas: [], cronogramaFases: [faseSemAgruparEtapas]), "etapas");

        etapasJson.Should().BeEmpty(
            "um processo classificado por nota importada (RegraCalculoCodigo.ClassificacaoImportada, usado pelo processo mínimo " +
            "desta suíte) não tem etapa pontuada — a canonicalização emite o array vazio em vez de recusar ou lançar");
    }

    [Fact(DisplayName = "criteriosDesempate: Ordem governa sobre o conteúdo da regra — oráculo de sequência exata")]
    public void CriteriosDesempate_OrdemGovernaSobreConteudo()
    {
        CriterioDesempate criterioIdoso = CriterioDesempate.Criar(2, Regra(CriterioDesempateCodigo.Idoso, 'd'), new ParametrosDesempateIdoso(60)).Value!;
        CriterioDesempate criterioMaiorIdade = CriterioDesempate.Criar(1, Regra(CriterioDesempateCodigo.MaiorIdade, 'a'), new ParametrosDesempateMaiorIdade()).Value!;

        new[] { criterioIdoso, criterioMaiorIdade }.OrderBy(static c => c.Regra.Codigo, StringComparer.Ordinal).Select(static c => c.Regra.Codigo)
            .Should().Equal(
                [CriterioDesempateCodigo.Idoso, CriterioDesempateCodigo.MaiorIdade],
                "pré-condição: ordenar pelo código da regra (proxy de conteúdo) dá o oposto do oráculo de Ordem abaixo");

        JsonArray criteriosJson = ArrayCanonicalizado(Montar(criteriosDesempate: [criterioIdoso, criterioMaiorIdade]), "criteriosDesempate");

        criteriosJson.Should().HaveCount(2, "pré-condição: o caminho existe e tem os dois itens sob teste");
        criteriosJson.Select(static c => c!["regra"]!["codigo"]!.GetValue<string>()).Should().Equal(
            [CriterioDesempateCodigo.MaiorIdade, CriterioDesempateCodigo.Idoso],
            "Ordem (1 para MaiorIdade, 2 para Idoso) governa a posição — o código da regra, em ordem alfabética, apontaria para o oposto");
    }

    private static EtapaProcesso EtapaReidratada(Guid id, string nome, int ordem) =>
        EtapaProcesso.Reidratar(ChaveLocal.Criar(nome == "Zeta" ? "etapa_zeta" : "etapa_alfa").Value!,
            id,
            new IdentificacaoDaEtapa(
                Nome: nome,
                Carater: CaraterEtapa.Classificatoria,
                TipoEtapa: TipoEtapaDeProvaObjetiva(),
                Ordem: ordem,
                FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null),
            itens: null);
}
