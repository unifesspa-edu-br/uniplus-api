namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

public sealed class CoerenciaDaAvaliacaoDaEtapaTests
{
    private static (string? Campo, string Codigo)[] Pares(IEnumerable<FieldError> erros) =>
        [.. erros.Select(static e => (e.Field, e.Error.Code))];

    private static List<FieldError> DaEtapaHumana(
        string? regra = null, decimal? passo = null, bool declaraItens = false, bool declaraEscala = false) =>
        CoerenciaDaAvaliacaoDaEtapa.Validar(
            notaDeOrigemNoEnem: false, passo, FormaDeAvaliacao.Humana, regra, declaraItens, declaraEscala);

    [Fact(DisplayName = "A etapa de nota do ENEM recusa a forma de avaliação declarada")]
    public void Validar_EtapaDoEnemComForma_RecusaAForma()
    {
        List<FieldError> erros = CoerenciaDaAvaliacaoDaEtapa.Validar(
            notaDeOrigemNoEnem: true, passoDoLancamento: null, FormaDeAvaliacao.Objetiva, regraConsolidacaoCodigo: null);

        Pares(erros).Should().Equal(("formaDeAvaliacao", EtapaProcesso.AvaliacaoNaEtapaDoEnem));
    }

    [Fact(DisplayName = "A etapa de nota do ENEM recusa a regra de consolidação declarada")]
    public void Validar_EtapaDoEnemComRegra_RecusaARegra()
    {
        List<FieldError> erros = CoerenciaDaAvaliacaoDaEtapa.Validar(
            notaDeOrigemNoEnem: true, passoDoLancamento: null, forma: null, RegraConsolidacaoCodigo.AvaliacaoUnica);

        Pares(erros).Should().Equal(("regraConsolidacao", EtapaProcesso.AvaliacaoNaEtapaDoEnem));
    }

    [Fact(DisplayName = "A etapa de nota do ENEM recusa itens de avaliação")]
    public void Validar_EtapaDoEnemComItens_RecusaOsItens()
    {
        List<FieldError> erros = CoerenciaDaAvaliacaoDaEtapa.Validar(
            notaDeOrigemNoEnem: true, passoDoLancamento: null, forma: null, regraConsolidacaoCodigo: null, declaraItens: true);

        Pares(erros).Should().Equal(("itensDeAvaliacao", EtapaProcesso.ItensNaEtapaDoEnem));
    }

    [Fact(DisplayName = "A etapa de nota do ENEM recusa a escala de atendimento")]
    public void Validar_EtapaDoEnemComEscala_RecusaAEscala()
    {
        List<FieldError> erros = CoerenciaDaAvaliacaoDaEtapa.Validar(
            notaDeOrigemNoEnem: true, passoDoLancamento: null, forma: null, regraConsolidacaoCodigo: null, declaraEscala: true);

        Pares(erros).Should().Equal(("escalaDeAtendimento", EtapaProcesso.ItensNaEtapaDoEnem));
    }

    [Fact(DisplayName = "A etapa de nota do ENEM não repete as demais recusas de avaliação")]
    public void Validar_EtapaDoEnem_NaoRepeteAsDemaisRecusas()
    {
        List<FieldError> erros = CoerenciaDaAvaliacaoDaEtapa.Validar(
            notaDeOrigemNoEnem: true,
            passoDoLancamento: 0.5m,
            FormaDeAvaliacao.Objetiva,
            RegraConsolidacaoCodigo.CorrecaoIndependente,
            declaraItens: false,
            declaraEscala: true);

        Pares(erros).Should().Equal(
            ("formaDeAvaliacao", EtapaProcesso.AvaliacaoNaEtapaDoEnem),
            ("regraConsolidacao", EtapaProcesso.AvaliacaoNaEtapaDoEnem),
            ("escalaDeAtendimento", EtapaProcesso.ItensNaEtapaDoEnem));
    }

    [Theory(DisplayName = "A regra de consolidação só se aplica à forma humana")]
    [InlineData(FormaDeAvaliacao.Objetiva)]
    [InlineData(null)]
    public void Validar_RegraForaDaFormaHumana_Recusa(FormaDeAvaliacao? forma)
    {
        List<FieldError> erros = CoerenciaDaAvaliacaoDaEtapa.Validar(
            notaDeOrigemNoEnem: false, passoDoLancamento: null, forma, RegraConsolidacaoCodigo.AvaliacaoUnica);

        Pares(erros).Should().Equal(("regraConsolidacao", EtapaProcesso.RegraDeConsolidacaoForaDaFormaHumana));
    }

    [Fact(DisplayName = "A regra de consolidação fora do rol conhecido é recusada e a mensagem a nomeia")]
    public void Validar_RegraForaDoRol_RecusaNomeandoARegra()
    {
        List<FieldError> erros = DaEtapaHumana(regra: "CONSOLIDACAO-INEXISTENTE");

        Pares(erros).Should().Equal(("regraConsolidacao", EtapaProcesso.RegraDeConsolidacaoInvalida));
        erros.Single().Error.Message.Should().Contain("'CONSOLIDACAO-INEXISTENTE'");
    }

    [Fact(DisplayName = "A regra do rol na forma humana não é recusada")]
    public void Validar_RegraDoRolNaFormaHumana_NaoRecusa()
    {
        DaEtapaHumana(regra: RegraConsolidacaoCodigo.CorrecaoIndependente).Should().BeEmpty();
    }

    [Fact(DisplayName = "O passo do lançamento não se aplica à forma objetiva")]
    public void Validar_PassoNaFormaObjetiva_Recusa()
    {
        List<FieldError> erros = CoerenciaDaAvaliacaoDaEtapa.Validar(
            notaDeOrigemNoEnem: false, passoDoLancamento: 0.5m, FormaDeAvaliacao.Objetiva, regraConsolidacaoCodigo: null);

        Pares(erros).Should().Equal(("passoDoLancamento", EtapaProcesso.PassoNaFormaObjetiva));
    }

    [Fact(DisplayName = "A escala de atendimento sem itens de avaliação é recusada")]
    public void Validar_EscalaSemItens_Recusa()
    {
        Pares(DaEtapaHumana(declaraEscala: true)).Should().Equal(("escalaDeAtendimento", EtapaProcesso.EscalaSemItens));
    }

    [Fact(DisplayName = "A escala de atendimento não se aplica à forma objetiva")]
    public void Validar_EscalaNaFormaObjetiva_Recusa()
    {
        List<FieldError> erros = CoerenciaDaAvaliacaoDaEtapa.Validar(
            notaDeOrigemNoEnem: false, passoDoLancamento: null, FormaDeAvaliacao.Objetiva, regraConsolidacaoCodigo: null,
            declaraItens: true, declaraEscala: true);

        Pares(erros).Should().Equal(("escalaDeAtendimento", EtapaProcesso.EscalaNaFormaObjetiva));
    }

    [Fact(DisplayName = "Com escala de atendimento a etapa não declara passo do lançamento")]
    public void Validar_EscalaComPasso_Recusa()
    {
        List<FieldError> erros = DaEtapaHumana(passo: 0.5m, declaraItens: true, declaraEscala: true);

        Pares(erros).Should().Equal(("passoDoLancamento", EtapaProcesso.PassoComEscalaDeAtendimento));
    }

    [Fact(DisplayName = "As recusas da escala de atendimento acumulam: sem itens, forma objetiva e passo, nesta ordem")]
    public void Validar_EscalaComTodasAsContradicoes_AcumulaNaOrdem()
    {
        List<FieldError> erros = CoerenciaDaAvaliacaoDaEtapa.Validar(
            notaDeOrigemNoEnem: false, passoDoLancamento: 0.5m, FormaDeAvaliacao.Objetiva, regraConsolidacaoCodigo: null,
            declaraItens: false, declaraEscala: true);

        Pares(erros).Should().Equal(
            ("passoDoLancamento", EtapaProcesso.PassoNaFormaObjetiva),
            ("escalaDeAtendimento", EtapaProcesso.EscalaSemItens),
            ("escalaDeAtendimento", EtapaProcesso.EscalaNaFormaObjetiva),
            ("passoDoLancamento", EtapaProcesso.PassoComEscalaDeAtendimento));
    }

    [Fact(DisplayName = "A etapa sem forma declarada e sem nada a coerir não é recusada: a falta é do gate de publicação")]
    public void Validar_EtapaSemNadaDeclarado_NaoRecusa()
    {
        CoerenciaDaAvaliacaoDaEtapa.Validar(
            notaDeOrigemNoEnem: false, passoDoLancamento: null, forma: null, regraConsolidacaoCodigo: null).Should().BeEmpty();
    }
}
