namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ConfiguracaoClassificacaoDeTeste;

public sealed class ConfiguracaoClassificacaoTests
{
    [Fact(DisplayName = "Criar com FORMULA-MEDIA-PONDERADA e arredondamento tem sucesso")]
    public void Criar_MediaPonderadaComArredondamento_Sucesso()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.RegraArredondamento.Should().NotBeNull();
    }

    [Fact(DisplayName = "Criar com FORMULA-MEDIA-PONDERADA sem a escala da nota final tem sucesso: o rascunho a tolera ausente")]
    public void Criar_MediaPonderada_SemEscalaDaNotaFinal_Sucesso()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.EscalaNotaFinal.Should().BeNull();
    }

    [Theory(DisplayName = "Criar com a escala da nota final menor ou igual a zero falha no campo escalaNotaFinal")]
    [InlineData(0)]
    [InlineData(-10)]
    public void Criar_EscalaDaNotaFinalNaoPositiva_Falha(int escala)
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: escala);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be("escalaNotaFinal");
        resultado.Errors[0].Error.Code.Should().Be("ConfiguracaoClassificacao.EscalaNotaFinalInvalida");
    }

    [Theory(DisplayName = "Criar confere a escala da nota final com o que a NotaFinal comporta: 99999,9999 passa; 100000 e a quinta casa decimal não")]
    [InlineData("99999.9999", true)]
    [InlineData("100000", false)]
    [InlineData("100.00001", false)]
    public void Criar_EscalaDaNotaFinal_ConfereOLimiteDaNotaFinal(string escala, bool valida)
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [],
            escalaNotaFinal: decimal.Parse(escala, System.Globalization.CultureInfo.InvariantCulture));

        resultado.IsSuccess.Should().Be(valida);
        if (!valida)
        {
            resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("escalaNotaFinal");
            resultado.Errors[0].Error.Code.Should().Be("ConfiguracaoClassificacao.EscalaNotaFinalInvalida");
            resultado.Errors[0].Error.Message.Should().Contain("5 dígitos inteiros e 4 casas decimais");
        }
    }

    [Fact(DisplayName = "Criar com a escala da nota final decimal positiva congela o valor")]
    public void Criar_EscalaDaNotaFinalPositiva_CongelaOValor()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: 1000m);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.EscalaNotaFinal.Should().Be(1000m);
    }

    [Fact(DisplayName = "Criar com CLASSIFICACAO-IMPORTADA e a escala da nota final informada falha (INV-B8)")]
    public void Criar_Importada_ComEscalaDaNotaFinal_Falha()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoImportada(), regraArredondamento: null, casasArredondamento: null, RegraOrdemAlocacao(), [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: 100m);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be("escalaNotaFinal");
        resultado.Errors[0].Error.Code.Should().Be("ConfiguracaoClassificacao.EscalaNotaFinalIndevida");
    }

    [Fact(DisplayName = "Criar com CLASSIFICACAO-IMPORTADA e sem arredondamento tem sucesso (INV-B8)")]
    public void Criar_Importada_SemArredondamento_Sucesso()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoImportada(), regraArredondamento: null, casasArredondamento: null, RegraOrdemAlocacao(), [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.RegraArredondamento.Should().BeNull();
    }

    [Fact(DisplayName = "Criar com CLASSIFICACAO-IMPORTADA e arredondamento informado falha (INV-B8)")]
    public void Criar_Importada_ComArredondamento_Falha()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoImportada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoClassificacao.ArredondamentoIndevido");
    }

    [Fact(DisplayName = "Criar com FORMULA-MEDIA-PONDERADA sem arredondamento falha (INV-B8) — acumula os dois campos")]
    public void Criar_MediaPonderada_SemArredondamento_Falha()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), regraArredondamento: null, casasArredondamento: null, RegraOrdemAlocacao(), [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "ConfiguracaoClassificacao.ArredondamentoObrigatorio",
            "ConfiguracaoClassificacao.CasasArredondamentoObrigatorio",
        ]);
    }

    [Fact(DisplayName = "ADR-0125: regra de arredondamento válida com casas inválidas recusa só CasasArredondamentoObrigatorio (achado de revisão)")]
    public void Criar_MediaPonderada_ComRegraArredondamentoECasasInvalidas_RecusaSoCasas()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 0, RegraOrdemAlocacao(), [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be("casasArredondamento");
        resultado.Errors[0].Error.Code.Should().Be("ConfiguracaoClassificacao.CasasArredondamentoObrigatorio");
    }

    [Fact(DisplayName = "Criar com CLASSIFICACAO-IMPORTADA e regra de eliminação informada falha (INV-B8)")]
    public void Criar_Importada_ComEliminacao_Falha()
    {
        RegraEliminacao eliminacao = Eliminacao(RegraEliminacaoCodigo.ElimZeroEmArea);

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoImportada(), regraArredondamento: null, casasArredondamento: null, RegraOrdemAlocacao(), [eliminacao], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoClassificacao.EliminacaoIndevida");
    }

    [Fact(DisplayName = "Criar com CLASSIFICACAO-IMPORTADA, regra ENEM e BaseadoEmEnem=true ainda falha por EliminacaoIndevida (precedência)")]
    public void Criar_Importada_ComEliminacaoEnemEBaseadoEmEnem_FalhaPorEliminacaoIndevida()
    {
        // EliminacaoIndevida (INV-B8: importada não aceita NENHUMA eliminação) precede o
        // gate ENEM novo — mesmo com BaseadoEmEnem=true, uma classificação importada com
        // ELIM-CORTE-REDACAO é recusada pelo motivo INV-B8, não pelo motivo ENEM.
        RegraEliminacao eliminacao = Eliminacao(RegraEliminacaoCodigo.ElimCorteEmArea);

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoImportada(), regraArredondamento: null, casasArredondamento: null, RegraOrdemAlocacao(), [eliminacao], baseadoEmEnem: true, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoClassificacao.EliminacaoIndevida");
    }

    [Fact(DisplayName = "Criar com lista de eliminação vincula os filhos à configuração")]
    public void Criar_ComEliminacao_Vincula()
    {
        RegraEliminacao eliminacao = Eliminacao(RegraEliminacaoCodigo.ElimZeroEmArea);

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), [eliminacao], baseadoEmEnem: true,
            QuadroPesoAreaEnemDeTeste.Resolucao, QuadroPesoAreaEnemDeTeste.Completo(), escalaNotaFinal: null);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.RegrasEliminacao.Should().ContainSingle();
        resultado.Value.RegrasEliminacao.Single().ConfiguracaoClassificacaoId.Should().Be(resultado.Value.Id);
    }

    // ── BaseadoEmEnem: a invariante que fecha as duas ramificações por TipoProcesso (#850) ──

    [Fact(DisplayName = "Criar seta BaseadoEmEnem no valor informado")]
    public void Criar_SetaBaseadoEmEnem()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), [], baseadoEmEnem: true,
            QuadroPesoAreaEnemDeTeste.Resolucao, QuadroPesoAreaEnemDeTeste.Completo(), escalaNotaFinal: null);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.BaseadoEmEnem.Should().BeTrue();
    }

    [Theory(DisplayName = "Criar com eliminação que depende do ENEM e BaseadoEmEnem=false é recusado — independente de TipoProcesso")]
    [InlineData(RegraEliminacaoCodigo.ElimCorteEmArea)]
    [InlineData(RegraEliminacaoCodigo.ElimZeroEmArea)]
    [InlineData(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem)]
    public void Criar_EliminacaoEnemForaDeProcessoEnem_Recusa(string codigoRegra)
    {
        RegraEliminacao eliminacao = Eliminacao(codigoRegra);

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), [eliminacao], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EliminacaoEnemForaDeProcessoEnem");
    }

    [Theory(DisplayName = "Criar com eliminação que depende do ENEM e BaseadoEmEnem=true tem sucesso — independente de TipoProcesso")]
    [InlineData(RegraEliminacaoCodigo.ElimCorteEmArea)]
    [InlineData(RegraEliminacaoCodigo.ElimZeroEmArea)]
    [InlineData(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem)]
    public void Criar_EliminacaoEnemEmProcessoEnem_Sucesso(string codigoRegra)
    {
        RegraEliminacao eliminacao = Eliminacao(codigoRegra);

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), [eliminacao], baseadoEmEnem: true,
            QuadroPesoAreaEnemDeTeste.Resolucao, QuadroPesoAreaEnemDeTeste.Completo(), escalaNotaFinal: null);

        resultado.IsSuccess.Should().BeTrue();
    }

    private static RegraEliminacao Eliminacao(string codigoRegra) =>
        RegraEliminacao.Criar(
            ReferenciaRegra.Criar(codigoRegra, "v1", new string('a', 64)).Value!,
            ArgsDaRegra(codigoRegra)).Value!;

    private static ArgsRegraEliminacao ArgsDaRegra(string codigoRegra) => codigoRegra switch
    {
        RegraEliminacaoCodigo.ElimCorteEmArea => new ArgsElimCorteEmArea("REDACAO", 400m),
        RegraEliminacaoCodigo.ElimZeroEmArea => new ArgsElimZeroEmArea(),
        RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem => new ArgsElimFaltaEmDiaDeProvaEnem(),
        _ => throw new ArgumentOutOfRangeException(nameof(codigoRegra), codigoRegra, "Código de regra ENEM desconhecido no teste."),
    };

    [Fact(DisplayName = "Cada repetição da falta em dia de prova do ENEM é recusada no próprio item")]
    public void Criar_FaltaEmDiaDeProvaEnemRepetida_Recusa()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(),
            [
                Eliminacao(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem),
                Eliminacao(RegraEliminacaoCodigo.ElimZeroEmArea),
                Eliminacao(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem),
                Eliminacao(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem),
            ], baseadoEmEnem: true,
            QuadroPesoAreaEnemDeTeste.Resolucao, QuadroPesoAreaEnemDeTeste.Completo(), escalaNotaFinal: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors
            .Where(e => e.Error.Code == "ConfiguracaoClassificacao.FaltaEmDiaDeProvaEnemRepetida")
            .Select(e => e.Field)
            .Should().Equal(["regrasEliminacao[2]", "regrasEliminacao[3]"], "a primeira declaração vale; cada repetição é recusada no próprio item");
    }

    [Fact(DisplayName = "Na classificação importada, a falta em dia de prova repetida só leva a recusa da lista inteira")]
    public void Criar_ImportadaComFaltaEmDiaDeProvaEnemRepetida_RecusaSoAListaInteira()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoImportada(), regraArredondamento: null, casasArredondamento: null, RegraOrdemAlocacao(),
            [
                Eliminacao(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem),
                Eliminacao(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem),
            ], baseadoEmEnem: true, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            [("regrasEliminacao", "ConfiguracaoClassificacao.EliminacaoIndevida")],
            "a importada não admite eliminação nenhuma, e é isso que o operador precisa corrigir");
    }

    [Fact(DisplayName = "Sem ENEM, a falta em dia de prova repetida sai junto com a recusa do ENEM desmarcado")]
    public void Criar_SemEnemComFaltaEmDiaDeProvaEnemRepetida_AcumulaAsDuas()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(),
            [
                Eliminacao(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem),
                Eliminacao(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem),
            ], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            [
                ("regrasEliminacao", "ProcessoSeletivo.EliminacaoEnemForaDeProcessoEnem"),
                ("regrasEliminacao[1]", "ConfiguracaoClassificacao.FaltaEmDiaDeProvaEnemRepetida"),
            ],
            "marcar o ENEM corrige a primeira recusa, e a repetição continua lá: são violações independentes");
    }

    [Fact(DisplayName = "Falta em dia de prova do ENEM declarada uma vez, junto de outra regra ENEM, é aceita")]
    public void Criar_FaltaEmDiaDeProvaEnemUnicaComZeroEmArea_Sucesso()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(),
            [
                Eliminacao(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem),
                Eliminacao(RegraEliminacaoCodigo.ElimZeroEmArea),
            ], baseadoEmEnem: true,
            QuadroPesoAreaEnemDeTeste.Resolucao, QuadroPesoAreaEnemDeTeste.Completo(), escalaNotaFinal: null);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    private static RegraEliminacao AusenciaNaEtapa(Guid etapaId) =>
        RegraEliminacao.Criar(
            ReferenciaRegra.Criar(RegraEliminacaoCodigo.ElimAusenciaNaEtapa, "v1", new string('a', 64)).Value!,
            new ArgsElimAusenciaNaEtapa(etapaId)).Value!;

    [Fact(DisplayName = "A eliminação por ausência repetida para a mesma etapa é recusada no próprio item; etapas distintas valem")]
    public void Criar_AusenciaNaMesmaEtapaRepetida_Recusa()
    {
        Guid etapa = Guid.CreateVersion7();

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(),
            [AusenciaNaEtapa(etapa), AusenciaNaEtapa(Guid.CreateVersion7()), AusenciaNaEtapa(etapa)],
            baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null);

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            [("regrasEliminacao[2]", "ConfiguracaoClassificacao.AusenciaNaEtapaRepetida")]);
    }

    [Fact(DisplayName = "ADR-0125: ArredondamentoObrigatorio e CasasArredondamentoObrigatorio acumulam no mesmo lote")]
    public void Criar_SemArredondamento_AcumulaAsDuasViolacoes()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), regraArredondamento: null, casasArredondamento: null, RegraOrdemAlocacao(), [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "ConfiguracaoClassificacao.ArredondamentoObrigatorio",
            "ConfiguracaoClassificacao.CasasArredondamentoObrigatorio",
        ]);
    }

    [Fact(DisplayName = "ADR-0125: ArredondamentoIndevido, CasasArredondamentoIndevido, EliminacaoIndevida e EliminacaoEnemForaDeProcessoEnem acumulam no mesmo lote")]
    public void Criar_ImportadaComArredondamentoEEliminacaoEnemSemBaseadoEmEnem_AcumulaAsQuatroViolacoes()
    {
        RegraEliminacao eliminacao = Eliminacao(RegraEliminacaoCodigo.ElimZeroEmArea);

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoImportada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), [eliminacao], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "ConfiguracaoClassificacao.ArredondamentoIndevido",
            "ConfiguracaoClassificacao.CasasArredondamentoIndevido",
            "ConfiguracaoClassificacao.EliminacaoIndevida",
            "ProcessoSeletivo.EliminacaoEnemForaDeProcessoEnem",
        ]);
    }

    [Fact(DisplayName = "ADR-0125: regra de arredondamento importada com só casas informadas recusa só CasasArredondamentoIndevido (achado de revisão)")]
    public void Criar_ImportadaComSoCasasInformadas_RecusaSoCasas()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoImportada(), regraArredondamento: null, casasArredondamento: 2, RegraOrdemAlocacao(), [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be("casasArredondamento");
        resultado.Errors[0].Error.Code.Should().Be("ConfiguracaoClassificacao.CasasArredondamentoIndevido");
    }
}
