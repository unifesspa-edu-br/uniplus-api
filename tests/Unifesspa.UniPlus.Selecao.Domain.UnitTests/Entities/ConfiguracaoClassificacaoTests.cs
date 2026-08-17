namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class ConfiguracaoClassificacaoTests
{
    private static ReferenciaRegra RegraCalculoMediaPonderada() =>
        ReferenciaRegra.Criar(RegraCalculoCodigo.FormulaMediaPonderada, "v1", new string('a', 64)).Value!;

    private static ReferenciaRegra RegraCalculoImportada() =>
        ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", new string('b', 64)).Value!;

    private static ReferenciaRegra RegraArredondamento() =>
        ReferenciaRegra.Criar(RegraArredondamentoCodigo.PrecisaoTruncar, "v1", new string('c', 64)).Value!;

    private static ReferenciaRegra RegraOrdemAlocacao() =>
        ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "v1", new string('d', 64)).Value!;

    [Fact(DisplayName = "Criar com FORMULA-MEDIA-PONDERADA e arredondamento tem sucesso")]
    public void Criar_MediaPonderadaComArredondamento_Sucesso()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), 1, [], baseadoEmEnem: false);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.RegraArredondamento.Should().NotBeNull();
    }

    [Fact(DisplayName = "Criar com CLASSIFICACAO-IMPORTADA e sem arredondamento tem sucesso (INV-B8)")]
    public void Criar_Importada_SemArredondamento_Sucesso()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoImportada(), regraArredondamento: null, casasArredondamento: null, RegraOrdemAlocacao(), 2, [], baseadoEmEnem: false);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.RegraArredondamento.Should().BeNull();
    }

    [Fact(DisplayName = "Criar com CLASSIFICACAO-IMPORTADA e arredondamento informado falha (INV-B8)")]
    public void Criar_Importada_ComArredondamento_Falha()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoImportada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), 1, [], baseadoEmEnem: false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoClassificacao.ArredondamentoIndevido");
    }

    [Fact(DisplayName = "Criar com FORMULA-MEDIA-PONDERADA sem arredondamento falha (INV-B8) — acumula os dois campos")]
    public void Criar_MediaPonderada_SemArredondamento_Falha()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), regraArredondamento: null, casasArredondamento: null, RegraOrdemAlocacao(), 1, [], baseadoEmEnem: false);

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
            RegraCalculoMediaPonderada(), RegraArredondamento(), 0, RegraOrdemAlocacao(), 1, [], baseadoEmEnem: false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be("casasArredondamento");
        resultado.Errors[0].Error.Code.Should().Be("ConfiguracaoClassificacao.CasasArredondamentoObrigatorio");
    }

    [Theory(DisplayName = "Criar com NOpcoesAlocacao fora de {1,2} falha")]
    [InlineData(0)]
    [InlineData(3)]
    public void Criar_NOpcoesInvalido_Falha(int nOpcoes)
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), nOpcoes, [], baseadoEmEnem: false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoClassificacao.NOpcoesInvalido");
    }

    [Fact(DisplayName = "Criar com CLASSIFICACAO-IMPORTADA e regra de eliminação informada falha (INV-B8)")]
    public void Criar_Importada_ComEliminacao_Falha()
    {
        RegraEliminacao eliminacao = RegraEliminacao.Criar(
            ReferenciaRegra.Criar(RegraEliminacaoCodigo.ElimZeroEmArea, "v1", new string('f', 64)).Value!,
            new ArgsElimZeroEmArea()).Value!;

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoImportada(), regraArredondamento: null, casasArredondamento: null, RegraOrdemAlocacao(), 1, [eliminacao], baseadoEmEnem: false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoClassificacao.EliminacaoIndevida");
    }

    [Fact(DisplayName = "Criar com CLASSIFICACAO-IMPORTADA, regra ENEM e BaseadoEmEnem=true ainda falha por EliminacaoIndevida (precedência)")]
    public void Criar_Importada_ComEliminacaoEnemEBaseadoEmEnem_FalhaPorEliminacaoIndevida()
    {
        // EliminacaoIndevida (INV-B8: importada não aceita NENHUMA eliminação) precede o
        // gate ENEM novo — mesmo com BaseadoEmEnem=true, uma classificação importada com
        // ELIM-CORTE-REDACAO é recusada pelo motivo INV-B8, não pelo motivo ENEM.
        RegraEliminacao eliminacao = RegraEliminacao.Criar(
            ReferenciaRegra.Criar(RegraEliminacaoCodigo.ElimCorteRedacao, "v1", new string('a', 64)).Value!,
            new ArgsElimCorteRedacao(400m)).Value!;

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoImportada(), regraArredondamento: null, casasArredondamento: null, RegraOrdemAlocacao(), 1, [eliminacao], baseadoEmEnem: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoClassificacao.EliminacaoIndevida");
    }

    [Fact(DisplayName = "Criar com lista de eliminação vincula os filhos à configuração")]
    public void Criar_ComEliminacao_Vincula()
    {
        RegraEliminacao eliminacao = RegraEliminacao.Criar(
            ReferenciaRegra.Criar(RegraEliminacaoCodigo.ElimZeroEmArea, "v1", new string('e', 64)).Value!,
            new ArgsElimZeroEmArea()).Value!;

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), 1, [eliminacao], baseadoEmEnem: true);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.RegrasEliminacao.Should().ContainSingle();
        resultado.Value.RegrasEliminacao.Single().ConfiguracaoClassificacaoId.Should().Be(resultado.Value.Id);
    }

    // ── BaseadoEmEnem: a invariante que fecha as duas ramificações por TipoProcesso (#850) ──

    [Fact(DisplayName = "Criar seta BaseadoEmEnem no valor informado")]
    public void Criar_SetaBaseadoEmEnem()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), 1, [], baseadoEmEnem: true);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.BaseadoEmEnem.Should().BeTrue();
    }

    [Theory(DisplayName = "Criar com ELIM-CORTE-REDACAO/ELIM-ZERO-EM-AREA e BaseadoEmEnem=false é recusado — independente de TipoProcesso")]
    [InlineData(RegraEliminacaoCodigo.ElimCorteRedacao)]
    [InlineData(RegraEliminacaoCodigo.ElimZeroEmArea)]
    public void Criar_EliminacaoEnemForaDeProcessoEnem_Recusa(string codigoRegra)
    {
        RegraEliminacao eliminacao = RegraEliminacao.Criar(
            ReferenciaRegra.Criar(codigoRegra, "v1", new string('b', 64)).Value!,
            ArgsDaRegra(codigoRegra)).Value!;

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), 1, [eliminacao], baseadoEmEnem: false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EliminacaoEnemForaDeProcessoEnem");
    }

    [Theory(DisplayName = "Criar com ELIM-CORTE-REDACAO/ELIM-ZERO-EM-AREA e BaseadoEmEnem=true tem sucesso — independente de TipoProcesso")]
    [InlineData(RegraEliminacaoCodigo.ElimCorteRedacao)]
    [InlineData(RegraEliminacaoCodigo.ElimZeroEmArea)]
    public void Criar_EliminacaoEnemEmProcessoEnem_Sucesso(string codigoRegra)
    {
        RegraEliminacao eliminacao = RegraEliminacao.Criar(
            ReferenciaRegra.Criar(codigoRegra, "v1", new string('c', 64)).Value!,
            ArgsDaRegra(codigoRegra)).Value!;

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), 1, [eliminacao], baseadoEmEnem: true);

        resultado.IsSuccess.Should().BeTrue();
    }

    private static ArgsRegraEliminacao ArgsDaRegra(string codigoRegra) => codigoRegra switch
    {
        RegraEliminacaoCodigo.ElimCorteRedacao => new ArgsElimCorteRedacao(400m),
        RegraEliminacaoCodigo.ElimZeroEmArea => new ArgsElimZeroEmArea(),
        _ => throw new ArgumentOutOfRangeException(nameof(codigoRegra), codigoRegra, "Código de regra ENEM desconhecido no teste."),
    };

    [Fact(DisplayName = "ADR-0125: NOpcoesInvalido, ArredondamentoObrigatorio e CasasArredondamentoObrigatorio acumulam no mesmo lote")]
    public void Criar_NOpcoesInvalidoESemArredondamento_AcumulaAsTresViolacoes()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), regraArredondamento: null, casasArredondamento: null, RegraOrdemAlocacao(), 3, [], baseadoEmEnem: false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "ConfiguracaoClassificacao.NOpcoesInvalido",
            "ConfiguracaoClassificacao.ArredondamentoObrigatorio",
            "ConfiguracaoClassificacao.CasasArredondamentoObrigatorio",
        ]);
    }

    [Fact(DisplayName = "ADR-0125: ArredondamentoIndevido, EliminacaoIndevida e EliminacaoEnemForaDeProcessoEnem acumulam no mesmo lote")]
    public void Criar_ImportadaComArredondamentoEEliminacaoEnemSemBaseadoEmEnem_AcumulaAsTresViolacoes()
    {
        RegraEliminacao eliminacao = RegraEliminacao.Criar(
            ReferenciaRegra.Criar(RegraEliminacaoCodigo.ElimZeroEmArea, "v1", new string('f', 64)).Value!,
            new ArgsElimZeroEmArea()).Value!;

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoImportada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), 1, [eliminacao], baseadoEmEnem: false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "ConfiguracaoClassificacao.ArredondamentoIndevido",
            "ConfiguracaoClassificacao.EliminacaoIndevida",
            "ProcessoSeletivo.EliminacaoEnemForaDeProcessoEnem",
        ]);
    }

    [Fact(DisplayName = "ValidarNOpcoesAlocacao sem violação retorna lote vazio")]
    public void ValidarNOpcoesAlocacao_SemViolacao_Vazio()
    {
        List<FieldError> erros = ConfiguracaoClassificacao.ValidarNOpcoesAlocacao(1);

        erros.Should().BeEmpty();
    }
}
