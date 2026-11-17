namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class ConfiguracaoDeclaradaTests
{
    private static EtapaProcesso Etapa(decimal peso) => EtapaProcesso.Criar(
        new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: null),
        new DeclaracaoDaAvaliacaoDaEtapa(Peso: peso, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

    [Fact(DisplayName = "A declaração traz a configuração do processo no instante em que foi tirada")]
    public void Declaracao_TrazAConfiguracaoDoProcesso()
    {
        ProcessoSeletivo processo = ConfiguracaoDeclaradaDeTeste.NovoProcesso();
        processo.DefinirEtapas([Etapa(peso: 1m)]).IsSuccess.Should().BeTrue();

        ConfiguracaoDeclarada declarada = processo.Declaracao();

        declarada.Identidade.OrigemCandidatos.Should().Be(processo.OrigemCandidatos);
        declarada.Avaliacao.Etapas.Should().BeEquivalentTo(processo.Etapas);
    }

    [Fact(DisplayName = "A declaração tirada não muda quando o processo muda depois")]
    public void Declaracao_NaoMudaQuandoOProcessoMuta()
    {
        ProcessoSeletivo processo = ConfiguracaoDeclaradaDeTeste.NovoProcesso();
        ConfiguracaoDeclarada antes = processo.Declaracao();

        processo.DefinirEtapas([Etapa(peso: 1m)]).IsSuccess.Should().BeTrue();

        antes.Avaliacao.Etapas.Should().BeEmpty();
        processo.Declaracao().Avaliacao.Etapas.Should().HaveCount(1);
    }

    [Fact(DisplayName = "A declaração entrega as listas do processo como cópias que não são as listas vivas")]
    public void Declaracao_EntregaCopiasDasListas()
    {
        ProcessoSeletivo processo = ConfiguracaoDeclaradaDeTeste.NovoProcesso();
        processo.DefinirEtapas([Etapa(peso: 1m)]).IsSuccess.Should().BeTrue();

        IReadOnlyList<EtapaProcesso> declaradas = processo.Declaracao().Avaliacao.Etapas;

        declaradas.Should().NotBeSameAs(processo.Etapas);
        declaradas.Should().NotBeAssignableTo<List<EtapaProcesso>>();
    }

    [Fact(DisplayName = "O processo sem coleta nem oferta declara só o que tem")]
    public void Vazia_NaoTemItensNemVagas()
    {
        ConfiguracaoDeclarada declarada = ConfiguracaoDeclaradaDeTeste.Vazia();

        declarada.Coleta.Itens.Should().BeEmpty();
        declarada.Vagas.HaVagasOfertadas.Should().BeFalse();
        declarada.Coleta.FormularioDe(FinalidadeFormulario.Inscricao).Should().BeNull();
    }

    [Fact(DisplayName = "O divisor da média soma os pesos das etapas que compõem a nota")]
    public void CalcularDivisorMedia_SomaOsPesosDasEtapasQueComponhemANota()
    {
        ConfiguracaoDeclarada declarada = ConfiguracaoDeclaradaDeTeste.ComEtapas(Etapa(peso: 2m));

        declarada.Avaliacao.CalcularDivisorMedia().Should().Be(2m);
    }
}
