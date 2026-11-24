namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A leitura da classificação devolve o cálculo, o arredondamento, as eliminações e o quadro de pesos por
/// área como foram configurados, e os critérios de desempate na ordem declarada, cada um só com os dados
/// da sua regra.
/// </summary>
public sealed class ProjecaoDaClassificacaoTests
{
    private static readonly string HashDoCalculo = new('a', 64);
    private static readonly string HashDoArredondamento = new('b', 64);
    private static readonly string HashDaAlocacao = new('c', 64);
    private static readonly string HashDoCriterio = new('d', 64);

    [Fact(DisplayName = "Processo sem classificação configurada devolve classificação ausente")]
    public void Projetar_SemClassificacao_DevolveNulo()
    {
        ProcessoSeletivo processo = CenariosDeDefinirEtapas.ProcessoSemEtapas();

        ProjecaoDaClassificacao.Projetar(processo).Should().BeNull();
    }

    [Fact(DisplayName = "O cálculo local devolve as regras de cálculo, arredondamento e alocação, as casas e a escala da nota")]
    public void Projetar_CalculoLocal_DevolveAsReferenciasAsCasasEAEscala()
    {
        ConfiguracaoClassificacaoDto dto = ProjetarClassificacao(Declaracao(baseadoEmEnem: false));

        dto.RegraCalculo.Should().Be(new ReferenciaRegraDto(RegraDeCalculoDaNota.MediaPonderada.Codigo(), "v1", HashDoCalculo));
        dto.RegraArredondamento.Should().Be(new ReferenciaRegraDto(RegraDeArredondamentoDaNota.Truncar.Codigo(), "v1", HashDoArredondamento));
        dto.RegraOrdemAlocacao.Should().Be(new ReferenciaRegraDto(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), "v1", HashDaAlocacao));
        dto.CasasArredondamento.Should().Be(2);
        dto.EscalaNotaFinal.Should().Be(100m);
    }

    [Fact(DisplayName = "A classificação importada não tem regra de arredondamento, casas nem escala da nota")]
    public void Projetar_ClassificacaoImportada_DevolveArredondamentoEEscalaAusentes()
    {
        DeclaracaoDaClassificacao importada = Declaracao(baseadoEmEnem: false) with
        {
            RegraCalculo = ReferenciaRegra.De(RegraDeCalculoDaNota.ClassificacaoImportada.Codigo(), "v1", HashDoCalculo),
            RegraArredondamento = null,
            CasasArredondamento = null,
            EscalaNotaFinal = null,
        };

        ConfiguracaoClassificacaoDto dto = ProjetarClassificacao(importada);

        dto.RegraCalculo.Codigo.Should().Be(RegraDeCalculoDaNota.ClassificacaoImportada.Codigo());
        dto.RegraArredondamento.Should().BeNull();
        dto.CasasArredondamento.Should().BeNull();
        dto.EscalaNotaFinal.Should().BeNull();
    }

    [Fact(DisplayName = "Sem distribuição de vagas a concorrência dupla não se aplica")]
    public void Projetar_SemDistribuicaoDeVagas_ConcorrenciaDuplaNaoSeAplica()
    {
        ConfiguracaoClassificacaoDto dto = ProjetarClassificacao(Declaracao(baseadoEmEnem: false));

        dto.ConcorrenciaDuplaAplicavel.Should().BeFalse();
    }

    [Fact(DisplayName = "A eliminação por nota mínima devolve a etapa e a nota, e a por ausência devolve só a etapa")]
    public void Projetar_EliminacoesDeEtapa_DevolvemAEtapaESoANotaQuandoHouver()
    {
        ProcessoSeletivo processo = ProcessoComUmaEtapa();
        Guid etapaId = processo.Etapas.Single().Id;

        ConfiguracaoClassificacaoDto dto = ProjetarClassificacao(processo, Declaracao(baseadoEmEnem: false) with
        {
            RegrasEliminacao =
            [
                Eliminacao(RegraDeEliminacaoPorCalculo.NotaMinimaNaEtapa.Codigo(), new ParametrosEliminacaoPorNotaMinimaNaEtapa(etapaId, 3m)),
                Eliminacao(RegraDeEliminacaoPorCalculo.AusenciaNaEtapa.Codigo(), new ParametrosEliminacaoPorAusenciaNaEtapa(etapaId)),
            ],
        });

        dto.RegrasEliminacao[0].Should().BeEquivalentTo(new { EtapaRef = etapaId, NotaMinima = 3m, Minimo = (decimal?)null, AreaCodigo = (string?)null });
        dto.RegrasEliminacao[1].Should().BeEquivalentTo(new { EtapaRef = etapaId, NotaMinima = (decimal?)null, Minimo = (decimal?)null, AreaCodigo = (string?)null });
    }

    [Fact(DisplayName = "A eliminação por corte em área devolve a área e o mínimo, sem etapa nem nota")]
    public void Projetar_EliminacaoPorCorteEmArea_DevolveAAreaEOMinimo()
    {
        ConfiguracaoClassificacaoDto dto = ProjetarClassificacao(Declaracao(baseadoEmEnem: true) with
        {
            RegrasEliminacao = [Eliminacao(RegraDeEliminacaoPorCalculo.CorteEmAreaDoEnem.Codigo(), new ParametrosEliminacaoPorCorteEmAreaDoEnem("REDACAO", 400m))],
        });

        dto.RegrasEliminacao.Should().ContainSingle().Which.Should()
            .BeEquivalentTo(new { EtapaRef = (Guid?)null, NotaMinima = (decimal?)null, Minimo = (decimal?)400m, AreaCodigo = (string?)"REDACAO" });
    }

    [Fact(DisplayName = "As eliminações do ENEM sem parâmetros (nota zero e falta em dia de prova) saem só com a regra")]
    public void Projetar_EliminacoesDoEnemSemParametros_DevolvemSoARegra()
    {
        ConfiguracaoClassificacaoDto dto = ProjetarClassificacao(Declaracao(baseadoEmEnem: true) with
        {
            RegrasEliminacao =
            [
                Eliminacao(RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem.Codigo(), new ParametrosEliminacaoPorZeroEmAreaDoEnem()),
                Eliminacao(RegraDeEliminacaoPorCalculo.FaltaEmDiaDeProvaDoEnem.Codigo(), new ParametrosEliminacaoPorFaltaEmDiaDeProvaDoEnem()),
            ],
        });

        dto.RegrasEliminacao.Select(static r => r.Regra.Codigo)
            .Should().Equal(RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem.Codigo(), RegraDeEliminacaoPorCalculo.FaltaEmDiaDeProvaDoEnem.Codigo());
        dto.RegrasEliminacao.Should().OnlyContain(static r => r.EtapaRef == null && r.NotaMinima == null && r.Minimo == null && r.AreaCodigo == null);
    }

    [Fact(DisplayName = "Sem resolução de pesos por área a classificação devolve o quadro vazio")]
    public void Projetar_SemResolucaoDePesos_DevolveQuadroVazio()
    {
        ConfiguracaoClassificacaoDto dto = ProjetarClassificacao(Declaracao(baseadoEmEnem: false));

        dto.ResolucaoPesoAreaEnem.Should().BeNull();
        dto.QuadroPesoAreaEnem.Should().BeEmpty();
    }

    [Fact(DisplayName = "Com resolução de pesos por área o quadro sai por código de grupo e as áreas por código, com peso e corte")]
    public void Projetar_ComResolucaoDePesos_DevolveQuadroOrdenadoComPesoECorte()
    {
        ConfiguracaoClassificacaoDto dto = ProjetarClassificacao(
            Declaracao(baseadoEmEnem: true) with { QuadroPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Completo(inverterOrdem: true) });

        dto.ResolucaoPesoAreaEnem.Should().Be(QuadroPesoAreaEnemDeTeste.Resolucao);
        dto.QuadroPesoAreaEnem.Select(static g => g.GrupoAreaEnem.Codigo)
            .Should().Equal("HUMANISTICA_I", "HUMANISTICA_II", "SAUDE_E_BIOLOGICAS", "TECNOLOGICA");
        dto.QuadroPesoAreaEnem[0].Areas.Select(static a => a.Codigo)
            .Should().Equal("CIENCIAS_DA_NATUREZA", "CIENCIAS_HUMANAS", "LINGUAGENS", "MATEMATICA", "REDACAO");
        dto.QuadroPesoAreaEnem[0].Areas[4].Should().Be(new AreaPesoAreaEnemCongeladaDto("REDACAO", "Redação", 2.00m, 400m));
    }

    [Fact(DisplayName = "Sem critérios de desempate a lista sai vazia")]
    public void ProjetarCriteriosDeDesempate_SemCriterios_DevolveListaVazia()
    {
        ProcessoSeletivo processo = CenariosDeDefinirEtapas.ProcessoSemEtapas();

        ProjecaoDaClassificacao.ProjetarCriteriosDeDesempate(processo).Should().BeEmpty();
    }

    [Fact(DisplayName = "Os critérios de desempate saem pela ordem declarada, mesmo guardados fora de ordem")]
    public void ProjetarCriteriosDeDesempate_ForaDeOrdem_SaemPelaOrdemDeclarada()
    {
        ProcessoSeletivo processo = ProcessoComUmaEtapa();
        processo.DefinirCriteriosDesempate(
        [
            Criterio(3, RegraDeDesempate.Idoso.Codigo(), new ParametrosDesempateIdoso(60)),
            Criterio(1, RegraDeDesempate.MaiorIdade.Codigo(), new ParametrosDesempateMaiorIdade()),
            Criterio(2, RegraDeDesempate.MaiorNotaNaEtapa.Codigo(), new ParametrosDesempateMaiorNotaEtapa(processo.Etapas.Single().Id)),
        ]).IsSuccess.Should().BeTrue();

        IReadOnlyList<CriterioDesempateDto> dtos = ProjecaoDaClassificacao.ProjetarCriteriosDeDesempate(processo);

        dtos.Select(static d => (d.Ordem, d.Regra.Codigo)).Should().Equal(
            (1, RegraDeDesempate.MaiorIdade.Codigo()), (2, RegraDeDesempate.MaiorNotaNaEtapa.Codigo()), (3, RegraDeDesempate.Idoso.Codigo()));
    }

    [Fact(DisplayName = "O desempate por maior idade não tem parâmetros e sai só com a regra")]
    public void ProjetarCriteriosDeDesempate_MaiorIdade_SaiSemParametros()
    {
        CriterioDesempateDto dto = ProjetarUnicoCriterio(
            CenariosDeDefinirEtapas.ProcessoSemEtapas(), Criterio(1, RegraDeDesempate.MaiorIdade.Codigo(), new ParametrosDesempateMaiorIdade()));

        dto.Should().Be(new CriterioDesempateDto(
            dto.Id, 1, new ReferenciaRegraDto(RegraDeDesempate.MaiorIdade.Codigo(), "v1", HashDoCriterio),
            new ParametrosDoCriterioDesempateDto(null, null, null, null, null, null)));
    }

    [Fact(DisplayName = "O desempate por maior nota na etapa devolve a etapa")]
    public void ProjetarCriteriosDeDesempate_MaiorNotaNaEtapa_DevolveAEtapa()
    {
        ProcessoSeletivo processo = ProcessoComUmaEtapa();
        Guid etapaId = processo.Etapas.Single().Id;

        CriterioDesempateDto dto = ProjetarUnicoCriterio(
            processo, Criterio(1, RegraDeDesempate.MaiorNotaNaEtapa.Codigo(), new ParametrosDesempateMaiorNotaEtapa(etapaId)));

        dto.Parametros.EtapaRef.Should().Be(etapaId);
        dto.Parametros.IdadeMinima.Should().BeNull();
        dto.Parametros.Areas.Should().BeNull();
    }

    [Fact(DisplayName = "O desempate por idoso devolve a idade mínima")]
    public void ProjetarCriteriosDeDesempate_Idoso_DevolveAIdadeMinima()
    {
        CriterioDesempateDto dto = ProjetarUnicoCriterio(
            CenariosDeDefinirEtapas.ProcessoSemEtapas(), Criterio(1, RegraDeDesempate.Idoso.Codigo(), new ParametrosDesempateIdoso(60)));

        dto.Parametros.IdadeMinima.Should().Be(60);
        dto.Parametros.EtapaRef.Should().BeNull();
        dto.Parametros.Fato.Should().BeNull();
    }

    [Fact(DisplayName = "O desempate por predicado de fato devolve o fato, o operador e o valor")]
    public void ProjetarCriteriosDeDesempate_PredicadoDeFato_DevolveFatoOperadorEValor()
    {
        CriterioDesempateDto dto = ProjetarUnicoCriterio(
            CenariosDeDefinirEtapas.ProcessoSemEtapas(), CriterioDePredicado("PROFESSOR_RURAL", Operador.Igual, true));

        dto.Parametros.Fato.Should().Be("PROFESSOR_RURAL");
        dto.Parametros.Operador.Should().Be("IGUAL");
        dto.Parametros.Valor.Should().Be("true");
        dto.Parametros.EtapaRef.Should().BeNull();
    }

    [Theory(DisplayName = "O valor da condição do predicado mantém o tipo: texto entre aspas, número e booleano sem aspas")]
    [InlineData("18", "\"18\"")]
    [InlineData(18, "18")]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    [InlineData("PRETA", "\"PRETA\"")]
    public void ProjetarCriteriosDeDesempate_ValorDaCondicao_MantemOTipoNoTextoJson(object valor, string textoEsperado)
    {
        CriterioDesempateDto dto = ProjetarUnicoCriterio(
            CenariosDeDefinirEtapas.ProcessoSemEtapas(), CriterioDePredicado("FATO_DE_TESTE", Operador.Igual, valor));

        dto.Parametros.Valor.Should().Be(textoEsperado);
    }

    [Fact(DisplayName = "O valor da condição com vários itens sai como lista em texto JSON")]
    public void ProjetarCriteriosDeDesempate_OperadorEm_DevolveOValorComoListaJson()
    {
        CriterioDesempateDto dto = ProjetarUnicoCriterio(
            CenariosDeDefinirEtapas.ProcessoSemEtapas(), CriterioDePredicado("MODALIDADE", Operador.Em, new[] { "LB_PPI", "AC" }));

        dto.Parametros.Operador.Should().Be("EM");
        dto.Parametros.Valor.Should().Be("[\"LB_PPI\",\"AC\"]");
    }

    [Fact(DisplayName = "O desempate por maior nota na área do ENEM devolve as áreas na ordem declarada")]
    public void ProjetarCriteriosDeDesempate_AreasDoEnem_DevolveAsAreasNaOrdemDeclarada()
    {
        ProcessoSeletivo processo = ProcessoComUmaEtapa();
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(Declaracao(baseadoEmEnem: true)).Value!).IsSuccess.Should().BeTrue();
        CriterioDesempate criterio = Criterio(
            1, RegraDeDesempate.MaiorNotaPorAreaDoEnem.Codigo(), new ParametrosDesempateMaiorNotaAreaEnem(["REDACAO", "MATEMATICA", "LINGUAGENS"]));

        CriterioDesempateDto dto = ProjetarUnicoCriterio(processo, criterio);

        dto.Id.Should().Be(criterio.Id);
        dto.Parametros.Areas.Should().Equal("REDACAO", "MATEMATICA", "LINGUAGENS");
        dto.Parametros.EtapaRef.Should().BeNull();
    }

    private static ProcessoSeletivo ProcessoComUmaEtapa() =>
        CenariosDeDefinirEtapas.ProcessoComAEtapa(CenariosDeDefinirEtapas.EtapaDeProvaObjetiva("Prova objetiva"));

    private static ConfiguracaoClassificacaoDto ProjetarClassificacao(DeclaracaoDaClassificacao declaracao) =>
        ProjetarClassificacao(ProcessoComUmaEtapa(), declaracao);

    private static ConfiguracaoClassificacaoDto ProjetarClassificacao(ProcessoSeletivo processo, DeclaracaoDaClassificacao declaracao)
    {
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(declaracao).Value!).IsSuccess.Should().BeTrue();
        ConfiguracaoClassificacaoDto? dto = ProjecaoDaClassificacao.Projetar(processo);

        dto.Should().NotBeNull();

        return dto!;
    }

    private static CriterioDesempateDto ProjetarUnicoCriterio(ProcessoSeletivo processo, CriterioDesempate criterio)
    {
        processo.DefinirCriteriosDesempate([criterio]).IsSuccess.Should().BeTrue();

        return ProjecaoDaClassificacao.ProjetarCriteriosDeDesempate(processo).Should().ContainSingle().Which;
    }

    private static DeclaracaoDaClassificacao Declaracao(bool baseadoEmEnem) => new()
    {
        RegraCalculo = ReferenciaRegra.De(RegraDeCalculoDaNota.MediaPonderada.Codigo(), "v1", HashDoCalculo),
        RegraArredondamento = ReferenciaRegra.De(RegraDeArredondamentoDaNota.Truncar.Codigo(), "v1", HashDoArredondamento),
        CasasArredondamento = 2,
        RegraOrdemAlocacao = ReferenciaRegra.De(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), "v1", HashDaAlocacao),
        BaseadoEmEnem = baseadoEmEnem,
        ResolucaoPesoAreaEnem = baseadoEmEnem ? QuadroPesoAreaEnemDeTeste.Resolucao : null,
        QuadroPesoAreaEnem = baseadoEmEnem ? QuadroPesoAreaEnemDeTeste.Completo() : [],
        EscalaNotaFinal = 100m,
    };

    private static RegraEliminacao Eliminacao(string codigo, ParametrosRegraEliminacao parametros) =>
        RegraEliminacao.Criar(ReferenciaRegra.De(codigo, "v1", HashDoCalculo), parametros).Value!;

    private static CriterioDesempate Criterio(int ordem, string codigo, ParametrosCriterioDesempate parametros) =>
        CriterioDesempate.Criar(ordem, ReferenciaRegra.De(codigo, "v1", HashDoCriterio), parametros).Value!;

    private static CriterioDesempate CriterioDePredicado(string fato, Operador operador, object valor) =>
        Criterio(1, RegraDeDesempate.CondicaoSobreDado.Codigo(), new ParametrosDesempatePredicadoFato(
            CondicaoDnf.Criar(fato, operador, JsonSerializer.SerializeToElement(valor)).Value!));
}
