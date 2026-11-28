namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaConfiguracaoClassificacao;

public sealed class ConfiguracaoClassificacaoConferirCamposTests
{
    private static readonly ParametrosRegraEliminacao FaltaEmDiaDeProva = new ParametrosEliminacaoPorFaltaEmDiaDeProvaDoEnem();

    private static readonly ParametrosRegraEliminacao ZeroEmArea = new ParametrosEliminacaoPorZeroEmAreaDoEnem();

    private static CamposDaClassificacao Local() => new()
    {
        RegraCalculo = RegraCalculoMediaPonderada(),
        ArredondamentoDeclarado = true,
        CasasArredondamento = 2,
        Eliminacoes = EliminacoesDeclaradas.NasPosicoes([]),
        BaseadoEmEnem = false,
        EscalaNotaFinal = 100m,
    };

    private static (string? Campo, string Codigo)[] Recusas(CamposDaClassificacao campos) =>
        [.. ConfiguracaoClassificacao.ConferirCampos(campos).Select(static erro => (erro.Field, erro.Error.Code))];

    [Fact(DisplayName = "Campos coerentes não têm recusa")]
    public void CamposCoerentes_NaoRecusam() =>
        Recusas(Local()).Should().BeEmpty();

    [Fact(DisplayName = "Sem a regra de cálculo confirmada só se confere o valor da escala")]
    public void SemRegraDeCalculo_ConfereSoOValorDaEscala() =>
        Recusas(Local() with { RegraCalculo = null, ArredondamentoDeclarado = false, CasasArredondamento = null, EscalaNotaFinal = 0m })
            .Should().Equal(("escalaNotaFinal", "ConfiguracaoClassificacao.EscalaNotaFinalInvalida"));

    [Fact(DisplayName = "Sem a regra de cálculo confirmada a lista de eliminações não é recusada como indevida")]
    public void SemRegraDeCalculo_NaoRecusaEliminacaoComoIndevida() =>
        Recusas(Local() with { RegraCalculo = null, Eliminacoes = EliminacoesDeclaradas.NasPosicoes([ZeroEmArea]), BaseadoEmEnem = true })
            .Should().BeEmpty();

    [Fact(DisplayName = "Sem a regra de cálculo confirmada a eliminação do ENEM continua exigindo classificação baseada em ENEM")]
    public void SemRegraDeCalculo_ConfereADependenciaDoEnem() =>
        Recusas(Local() with { RegraCalculo = null, Eliminacoes = EliminacoesDeclaradas.NasPosicoes([ZeroEmArea]) })
            .Should().Equal(("regrasEliminacao", "ProcessoSeletivo.EliminacaoEnemForaDeProcessoEnem"));

    [Fact(DisplayName = "Sem a regra de cálculo confirmada as repetições da lista não são conferidas")]
    public void SemRegraDeCalculo_NaoConfereRepeticoes() =>
        Recusas(Local() with { RegraCalculo = null, Eliminacoes = EliminacoesDeclaradas.NasPosicoes([FaltaEmDiaDeProva, FaltaEmDiaDeProva]), BaseadoEmEnem = true })
            .Should().BeEmpty();

    [Fact(DisplayName = "Sem a regra de cálculo confirmada a resolução de Pesos por Área não é exigida nem recusada")]
    public void SemRegraDeCalculo_NaoConfereAResolucao() =>
        Recusas(Local() with { RegraCalculo = null, BaseadoEmEnem = true, ResolucaoPesoAreaEnem = null }).Should().BeEmpty();

    [Fact(DisplayName = "Arredondamento declarado e não confirmado no catálogo não é recusado como obrigatório")]
    public void ArredondamentoDeclarado_NaoEObrigatorioMesmoSemConfirmacao() =>
        Recusas(Local() with { ArredondamentoDeclarado = true }).Should().BeEmpty();

    [Fact(DisplayName = "Cálculo local sem arredondamento declarado recusa o arredondamento")]
    public void CalculoLocalSemArredondamento_Recusa() =>
        Recusas(Local() with { ArredondamentoDeclarado = false })
            .Should().Equal(("regraArredondamentoCodigo", "ConfiguracaoClassificacao.ArredondamentoObrigatorio"));

    [Fact(DisplayName = "Classificação importada com arredondamento declarado recusa o arredondamento, resolvido ou não")]
    public void ImportadaComArredondamentoDeclarado_Recusa() =>
        Recusas(Local() with { RegraCalculo = RegraCalculoImportada(), CasasArredondamento = null, EscalaNotaFinal = null })
            .Should().Equal(("regraArredondamentoCodigo", "ConfiguracaoClassificacao.ArredondamentoIndevido"));

    [Fact(DisplayName = "A lista que o pedido não informou e o baseadoEmEnem ausente são recusados")]
    public void AusenciaDaListaEDoBaseadoEmEnem_Recusa() =>
        Recusas(Local() with { Eliminacoes = null, BaseadoEmEnem = null }).Should().Equal(
            ("baseadoEmEnem", CodigosDeValidacao.Obrigatorio),
            ("regrasEliminacao", CodigosDeValidacao.Obrigatorio));

    [Fact(DisplayName = "A eliminação recusada deixa a posição vazia: a repetição é recusada na posição real do pedido")]
    public void PosicaoVazia_MantemAPosicaoDosDemaisItens()
    {
        ParametrosRegraEliminacao ausencia = new ParametrosEliminacaoPorAusenciaNaEtapa(Guid.CreateVersion7());

        Recusas(Local() with { Eliminacoes = EliminacoesDeclaradas.NasPosicoes([ausencia, null, ausencia]) })
            .Should().Equal(("regrasEliminacao[2]", "ConfiguracaoClassificacao.AusenciaNaEtapaRepetida"));
    }

    [Fact(DisplayName = "A eliminação recusada não gera repetição falsa nem deixa de contar na classificação importada")]
    public void PosicaoVazia_ContaNaEliminacaoIndevidaDaImportada()
    {
        CamposDaClassificacao importada = Local() with
        {
            RegraCalculo = RegraCalculoImportada(),
            ArredondamentoDeclarado = false,
            CasasArredondamento = null,
            EscalaNotaFinal = null,
            Eliminacoes = EliminacoesDeclaradas.NasPosicoes([null]),
        };

        Recusas(importada).Should().Equal(("regrasEliminacao", "ConfiguracaoClassificacao.EliminacaoIndevida"));
    }

    [Fact(DisplayName = "As eliminações das regras criadas têm uma posição para cada regra")]
    public void DasRegras_TemUmaPosicaoParaCadaRegra()
    {
        EliminacoesDeclaradas declaradas = EliminacoesDeclaradas.DasRegras(Eliminacoes(RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem.Codigo(), RegraDeEliminacaoPorCalculo.FaltaEmDiaDeProvaDoEnem.Codigo()));

        declaradas.Quantidade.Should().Be(2);
        declaradas.Montadas.Select(static item => item.Posicao).Should().Equal(0, 1);
    }
}
