namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

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
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), 1, [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.RegraArredondamento.Should().NotBeNull();
    }

    [Fact(DisplayName = "Criar com CLASSIFICACAO-IMPORTADA e sem arredondamento tem sucesso (INV-B8)")]
    public void Criar_Importada_SemArredondamento_Sucesso()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoImportada(), regraArredondamento: null, casasArredondamento: null, RegraOrdemAlocacao(), 2, [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.RegraArredondamento.Should().BeNull();
    }

    [Fact(DisplayName = "Criar com CLASSIFICACAO-IMPORTADA e arredondamento informado falha (INV-B8)")]
    public void Criar_Importada_ComArredondamento_Falha()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoImportada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), 1, [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoClassificacao.ArredondamentoIndevido");
    }

    [Fact(DisplayName = "Criar com FORMULA-MEDIA-PONDERADA sem arredondamento falha (INV-B8) — acumula os dois campos")]
    public void Criar_MediaPonderada_SemArredondamento_Falha()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), regraArredondamento: null, casasArredondamento: null, RegraOrdemAlocacao(), 1, [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []);

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
            RegraCalculoMediaPonderada(), RegraArredondamento(), 0, RegraOrdemAlocacao(), 1, [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []);

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
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), nOpcoes, [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoClassificacao.NOpcoesInvalido");
    }

    [Fact(DisplayName = "Criar com CLASSIFICACAO-IMPORTADA e regra de eliminação informada falha (INV-B8)")]
    public void Criar_Importada_ComEliminacao_Falha()
    {
        RegraEliminacao eliminacao = Eliminacao(RegraEliminacaoCodigo.ElimZeroEmArea);

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoImportada(), regraArredondamento: null, casasArredondamento: null, RegraOrdemAlocacao(), 1, [eliminacao], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoClassificacao.EliminacaoIndevida");
    }

    [Fact(DisplayName = "Criar com CLASSIFICACAO-IMPORTADA, regra ENEM e BaseadoEmEnem=true ainda falha por EliminacaoIndevida (precedência)")]
    public void Criar_Importada_ComEliminacaoEnemEBaseadoEmEnem_FalhaPorEliminacaoIndevida()
    {
        // EliminacaoIndevida (INV-B8: importada não aceita NENHUMA eliminação) precede o
        // gate ENEM novo — mesmo com BaseadoEmEnem=true, uma classificação importada com
        // ELIM-CORTE-REDACAO é recusada pelo motivo INV-B8, não pelo motivo ENEM.
        RegraEliminacao eliminacao = Eliminacao(RegraEliminacaoCodigo.ElimCorteRedacao);

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoImportada(), regraArredondamento: null, casasArredondamento: null, RegraOrdemAlocacao(), 1, [eliminacao], baseadoEmEnem: true, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoClassificacao.EliminacaoIndevida");
    }

    [Fact(DisplayName = "Criar com lista de eliminação vincula os filhos à configuração")]
    public void Criar_ComEliminacao_Vincula()
    {
        RegraEliminacao eliminacao = Eliminacao(RegraEliminacaoCodigo.ElimZeroEmArea);

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), 1, [eliminacao], baseadoEmEnem: true,
            QuadroPesoAreaEnemDeTeste.Resolucao, QuadroPesoAreaEnemDeTeste.Completo());

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.RegrasEliminacao.Should().ContainSingle();
        resultado.Value.RegrasEliminacao.Single().ConfiguracaoClassificacaoId.Should().Be(resultado.Value.Id);
    }

    // ── BaseadoEmEnem: a invariante que fecha as duas ramificações por TipoProcesso (#850) ──

    [Fact(DisplayName = "Criar seta BaseadoEmEnem no valor informado")]
    public void Criar_SetaBaseadoEmEnem()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), 1, [], baseadoEmEnem: true,
            QuadroPesoAreaEnemDeTeste.Resolucao, QuadroPesoAreaEnemDeTeste.Completo());

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.BaseadoEmEnem.Should().BeTrue();
    }

    [Theory(DisplayName = "Criar com eliminação que depende do ENEM e BaseadoEmEnem=false é recusado — independente de TipoProcesso")]
    [InlineData(RegraEliminacaoCodigo.ElimCorteRedacao)]
    [InlineData(RegraEliminacaoCodigo.ElimZeroEmArea)]
    [InlineData(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem)]
    public void Criar_EliminacaoEnemForaDeProcessoEnem_Recusa(string codigoRegra)
    {
        RegraEliminacao eliminacao = Eliminacao(codigoRegra);

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), 1, [eliminacao], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EliminacaoEnemForaDeProcessoEnem");
    }

    [Theory(DisplayName = "Criar com eliminação que depende do ENEM e BaseadoEmEnem=true tem sucesso — independente de TipoProcesso")]
    [InlineData(RegraEliminacaoCodigo.ElimCorteRedacao)]
    [InlineData(RegraEliminacaoCodigo.ElimZeroEmArea)]
    [InlineData(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem)]
    public void Criar_EliminacaoEnemEmProcessoEnem_Sucesso(string codigoRegra)
    {
        RegraEliminacao eliminacao = Eliminacao(codigoRegra);

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), 1, [eliminacao], baseadoEmEnem: true,
            QuadroPesoAreaEnemDeTeste.Resolucao, QuadroPesoAreaEnemDeTeste.Completo());

        resultado.IsSuccess.Should().BeTrue();
    }

    private static RegraEliminacao Eliminacao(string codigoRegra) =>
        RegraEliminacao.Criar(
            ReferenciaRegra.Criar(codigoRegra, "v1", new string('a', 64)).Value!,
            ArgsDaRegra(codigoRegra)).Value!;

    private static ArgsRegraEliminacao ArgsDaRegra(string codigoRegra) => codigoRegra switch
    {
        RegraEliminacaoCodigo.ElimCorteRedacao => new ArgsElimCorteRedacao(400m),
        RegraEliminacaoCodigo.ElimZeroEmArea => new ArgsElimZeroEmArea(),
        RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem => new ArgsElimFaltaEmDiaDeProvaEnem(),
        _ => throw new ArgumentOutOfRangeException(nameof(codigoRegra), codigoRegra, "Código de regra ENEM desconhecido no teste."),
    };

    [Fact(DisplayName = "Cada repetição da falta em dia de prova do ENEM é recusada no próprio item, acumulando com as demais violações")]
    public void Criar_FaltaEmDiaDeProvaEnemRepetida_Recusa()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), 3,
            [
                Eliminacao(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem),
                Eliminacao(RegraEliminacaoCodigo.ElimZeroEmArea),
                Eliminacao(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem),
                Eliminacao(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem),
            ], baseadoEmEnem: true,
            QuadroPesoAreaEnemDeTeste.Resolucao, QuadroPesoAreaEnemDeTeste.Completo());

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors
            .Where(e => e.Error.Code == "ConfiguracaoClassificacao.FaltaEmDiaDeProvaEnemRepetida")
            .Select(e => e.Field)
            .Should().Equal(["regrasEliminacao[2]", "regrasEliminacao[3]"], "a primeira declaração vale; cada repetição é recusada no próprio item");
        resultado.Errors.Select(e => e.Error.Code).Should().Contain("ConfiguracaoClassificacao.NOpcoesInvalido");
    }

    [Fact(DisplayName = "Na classificação importada, a falta em dia de prova repetida só leva a recusa da lista inteira")]
    public void Criar_ImportadaComFaltaEmDiaDeProvaEnemRepetida_RecusaSoAListaInteira()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoImportada(), regraArredondamento: null, casasArredondamento: null, RegraOrdemAlocacao(), 1,
            [
                Eliminacao(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem),
                Eliminacao(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem),
            ], baseadoEmEnem: true, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            [("regrasEliminacao", "ConfiguracaoClassificacao.EliminacaoIndevida")],
            "a importada não admite eliminação nenhuma, e é isso que o operador precisa corrigir");
    }

    [Fact(DisplayName = "Sem ENEM, a falta em dia de prova repetida sai junto com a recusa do ENEM desmarcado")]
    public void Criar_SemEnemComFaltaEmDiaDeProvaEnemRepetida_AcumulaAsDuas()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), 1,
            [
                Eliminacao(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem),
                Eliminacao(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem),
            ], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []);

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
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), 1,
            [
                Eliminacao(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem),
                Eliminacao(RegraEliminacaoCodigo.ElimZeroEmArea),
            ], baseadoEmEnem: true,
            QuadroPesoAreaEnemDeTeste.Resolucao, QuadroPesoAreaEnemDeTeste.Completo());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "ADR-0125: NOpcoesInvalido, ArredondamentoObrigatorio e CasasArredondamentoObrigatorio acumulam no mesmo lote")]
    public void Criar_NOpcoesInvalidoESemArredondamento_AcumulaAsTresViolacoes()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), regraArredondamento: null, casasArredondamento: null, RegraOrdemAlocacao(), 3, [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "ConfiguracaoClassificacao.NOpcoesInvalido",
            "ConfiguracaoClassificacao.ArredondamentoObrigatorio",
            "ConfiguracaoClassificacao.CasasArredondamentoObrigatorio",
        ]);
    }

    [Fact(DisplayName = "ADR-0125: ArredondamentoIndevido, CasasArredondamentoIndevido, EliminacaoIndevida e EliminacaoEnemForaDeProcessoEnem acumulam no mesmo lote")]
    public void Criar_ImportadaComArredondamentoEEliminacaoEnemSemBaseadoEmEnem_AcumulaAsQuatroViolacoes()
    {
        RegraEliminacao eliminacao = Eliminacao(RegraEliminacaoCodigo.ElimZeroEmArea);

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoImportada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), 1, [eliminacao], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []);

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
            RegraCalculoImportada(), regraArredondamento: null, casasArredondamento: 2, RegraOrdemAlocacao(), 1, [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be("casasArredondamento");
        resultado.Errors[0].Error.Code.Should().Be("ConfiguracaoClassificacao.CasasArredondamentoIndevido");
    }

    [Fact(DisplayName = "ValidarNOpcoesAlocacao sem violação retorna lote vazio")]
    public void ValidarNOpcoesAlocacao_SemViolacao_Vazio()
    {
        List<FieldError> erros = ConfiguracaoClassificacao.ValidarNOpcoesAlocacao(1);

        erros.Should().BeEmpty();
    }

    // ── Resolução de Pesos por Área e quadro congelado ──

    [Fact(DisplayName = "Classificação baseada em ENEM com cálculo local guarda a resolução aparada e vincula os grupos do quadro")]
    public void Criar_EnemLocalComResolucao_GuardaResolucaoEVinculaOQuadro()
    {
        IReadOnlyList<GrupoPesoAreaEnemCongelado> quadro = QuadroPesoAreaEnemDeTeste.Completo();

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), 1, [], baseadoEmEnem: true,
            $"  {QuadroPesoAreaEnemDeTeste.Resolucao} ", quadro);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.ResolucaoPesoAreaEnem.Should().Be(QuadroPesoAreaEnemDeTeste.Resolucao);
        resultado.Value.QuadroPesoAreaEnem.Should().HaveCount(4)
            .And.OnlyContain(g => g.ConfiguracaoClassificacaoId == resultado.Value.Id);
    }

    [Theory(DisplayName = "Classificação baseada em ENEM com cálculo local sem resolução é recusada no campo da resolução")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_EnemLocalSemResolucao_Recusa(string? resolucao)
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), 1, [], baseadoEmEnem: true,
            resolucao, QuadroPesoAreaEnemDeTeste.Completo());

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(e =>
            e.Field == "resolucaoPesoAreaEnem" && e.Error.Code == "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemObrigatoria");
    }

    [Fact(DisplayName = "ADR-0125: resolução ausente acumula com as demais violações da classificação")]
    public void Criar_EnemLocalSemResolucaoENOpcoesInvalido_AcumulaAsDuas()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), 3, [], baseadoEmEnem: true, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: []);

        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "ConfiguracaoClassificacao.NOpcoesInvalido",
            "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemObrigatoria",
        ]);
    }

    [Fact(DisplayName = "Resolução em classificação que não é baseada em ENEM é recusada como indevida")]
    public void Criar_ResolucaoSemEnem_RecusaComoIndevida()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), 1, [], baseadoEmEnem: false,
            QuadroPesoAreaEnemDeTeste.Resolucao, QuadroPesoAreaEnemDeTeste.Completo());

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(e =>
            e.Field == "resolucaoPesoAreaEnem" && e.Error.Code == "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemIndevida");
    }

    [Fact(DisplayName = "Resolução em classificação importada é recusada como indevida, mesmo baseada em ENEM")]
    public void Criar_ResolucaoEmImportada_RecusaComoIndevida()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoImportada(), regraArredondamento: null, casasArredondamento: null, RegraOrdemAlocacao(), 1, [], baseadoEmEnem: true,
            QuadroPesoAreaEnemDeTeste.Resolucao, QuadroPesoAreaEnemDeTeste.Completo());

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(e => e.Error.Code == "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemIndevida");
    }

    [Fact(DisplayName = "Quadro sem resolução, fora de ENEM, também é recusado como indevido")]
    public void Criar_QuadroSemResolucaoForaDeEnem_RecusaComoIndevido()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), 1, [], baseadoEmEnem: false,
            resolucaoPesoAreaEnem: null, QuadroPesoAreaEnemDeTeste.Completo());

        resultado.Errors.Should().ContainSingle(e => e.Error.Code == "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemIndevida");
    }

    [Fact(DisplayName = "Resolução declarada sem nenhum grupo congelado é recusada")]
    public void Criar_ResolucaoComQuadroVazio_Recusa()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), 1, [], baseadoEmEnem: true,
            QuadroPesoAreaEnemDeTeste.Resolucao, []);

        resultado.Errors.Should().ContainSingle(e => e.Error.Code == "ConfiguracaoClassificacao.QuadroPesoAreaEnemVazio");
    }

    [Fact(DisplayName = "Quadro com grupo repetido é recusado, nomeando o grupo")]
    public void Criar_QuadroComGrupoRepetido_Recusa()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), 1, [], baseadoEmEnem: true,
            QuadroPesoAreaEnemDeTeste.Resolucao,
            [QuadroPesoAreaEnemDeTeste.Grupo("TECNOLOGICA", "Tecnológica"), QuadroPesoAreaEnemDeTeste.Grupo("TECNOLOGICA", "Tecnológica")]);

        FieldError erro = resultado.Errors.Should().ContainSingle().Subject;
        erro.Error.Code.Should().Be("ConfiguracaoClassificacao.QuadroPesoAreaEnemGrupoRepetido");
        erro.Error.Message.Should().Contain("Tecnológica");
    }

    [Fact(DisplayName = "Resolução acima da largura da coluna é recusada")]
    public void Criar_ResolucaoLongaDemais_Recusa()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), 1, [], baseadoEmEnem: true,
            new string('R', ConfiguracaoClassificacao.ResolucaoPesoAreaEnemMaxLength + 1), QuadroPesoAreaEnemDeTeste.Completo());

        resultado.Errors.Should().ContainSingle(e => e.Error.Code == "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemInvalida");
    }

    [Theory(DisplayName = "O quadro de pesos por área é exigido só na classificação baseada em ENEM pela média ponderada")]
    [InlineData(RegraCalculoCodigo.FormulaMediaPonderada, true, true)]
    [InlineData(RegraCalculoCodigo.FormulaMediaPonderada, false, false)]
    [InlineData(RegraCalculoCodigo.ClassificacaoImportada, true, false)]
    [InlineData(RegraCalculoCodigo.ClassificacaoImportada, false, false)]
    public void ExigeQuadroPesoAreaEnem_SoNaMediaPonderadaDoEnem(string regraCalculo, bool baseadoEmEnem, bool esperado)
    {
        ReferenciaRegra regra = ReferenciaRegra.Criar(regraCalculo, "v1", new string('f', 64)).Value!;

        ConfiguracaoClassificacao.ExigeQuadroPesoAreaEnem(regra, baseadoEmEnem).Should().Be(esperado);
    }

    [Theory(DisplayName = "A forma da resolução se confere sem a classificação: ausente passa, caractere invisível ou acima da coluna não")]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("Res. 805/2024", null)]
    [InlineData("  Resolução com exatamente quarenta caract  ", null)]
    [InlineData("805\u0000", "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemInvalida")]
    [InlineData("Res.\n805/2024", "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemInvalida")]
    [InlineData("Res. \u202E4202/508", "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemInvalida")]
    [InlineData("Res.\u2028805", "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemInvalida")]
    [InlineData("Resolução com quarenta e um caracteres ..", "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemInvalida")]
    public void ValidarResolucaoPesoAreaEnem_ConfereAForma(string? resolucao, string? codigoEsperado)
    {
        List<FieldError> erros = ConfiguracaoClassificacao.ValidarResolucaoPesoAreaEnem(resolucao);

        if (codigoEsperado is null)
        {
            erros.Should().BeEmpty();
        }
        else
        {
            erros.Should().ContainSingle(e => e.Field == "resolucaoPesoAreaEnem" && e.Error.Code == codigoEsperado);
        }
    }

    [Fact(DisplayName = "A resolução maior que o teto é recusada como inválida")]
    public void ValidarResolucaoPesoAreaEnem_ResolucaoMuitoGrande_Recusa() =>
        ConfiguracaoClassificacao.ValidarResolucaoPesoAreaEnem(new string('R', 1_000_000) + "\n1")
            .Should().ContainSingle(e => e.Error.Code == "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemInvalida");

    [Fact(DisplayName = "A resolução decomposta que, em NFC, cabe na coluna é aceita")]
    public void ValidarResolucaoPesoAreaEnem_DecompostaQueCabeEmNfc_Aceita()
    {
        // U+1F86 decompõe em quatro caracteres: 160 digitados, 40 congelados.
        string decomposta = string.Concat(Enumerable.Repeat("\u03B1\u0313\u0342\u0345", 40));

        ConfiguracaoClassificacao.ValidarResolucaoPesoAreaEnem(decomposta).Should().BeEmpty();
    }

    [Fact(DisplayName = "A resolução é gravada aparada e em NFC, a forma do cadastro e do envelope")]
    public void Criar_GravaAResolucaoEmNfc()
    {
        string decomposta = "Resolução 805/2024".Normalize(System.Text.NormalizationForm.FormD);

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), 1, [], baseadoEmEnem: true,
            $" {decomposta} ", QuadroPesoAreaEnemDeTeste.Completo());

        resultado.Value!.ResolucaoPesoAreaEnem.Should().Be(
            "Resolução 805/2024".Normalize(System.Text.NormalizationForm.FormC),
            "o cadastro grava em NFC e o envelope emite em NFC: sem a mesma forma aqui, o valor muda num ciclo de retificação");
    }

    [Fact(DisplayName = "Resolução com surrogate sem par é recusada como inválida, sem exceção da normalização")]
    public void Criar_ResolucaoComSurrogateSemPar_Recusa()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), 1, [], baseadoEmEnem: true,
            "Res." + (char)0xD800, QuadroPesoAreaEnemDeTeste.Completo());

        resultado.Errors.Should().ContainSingle(e => e.Error.Code == "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemInvalida");
    }

    [Fact(DisplayName = "Sem as consequências da resolução recusada saem só a forma repetida e o quadro vazio, e o resto fica, inclusive no mesmo campo")]
    public void SemConsequenciasDaResolucaoRecusada_TiraSoAsConsequencias()
    {
        FieldError invalida = new(ConfiguracaoClassificacao.CampoResolucaoPesoAreaEnem, new DomainError("ConfiguracaoClassificacao.ResolucaoPesoAreaEnemInvalida", "x"));
        FieldError quadroVazio = new(ConfiguracaoClassificacao.CampoResolucaoPesoAreaEnem, new DomainError("ConfiguracaoClassificacao.QuadroPesoAreaEnemVazio", "x"));
        FieldError outraNoMesmoCampo = new(ConfiguracaoClassificacao.CampoResolucaoPesoAreaEnem, new DomainError("ConfiguracaoClassificacao.QuadroPesoAreaEnemGrupoRepetido", "x"));
        FieldError casas = new("casasArredondamento", new DomainError("ConfiguracaoClassificacao.CasasArredondamentoObrigatorio", "x"));

        ConfiguracaoClassificacao.SemConsequenciasDaResolucaoRecusada([invalida, quadroVazio, outraNoMesmoCampo, casas])
            .Should().Equal(outraNoMesmoCampo, casas);
    }

    [Fact(DisplayName = "Não-caractere na resolução é recusado como inválido, sem exceção da normalização")]
    public void ValidarResolucaoPesoAreaEnem_NaoCaractere_Recusa() =>
        ConfiguracaoClassificacao.ValidarResolucaoPesoAreaEnem("Res. 805" + (char)0xFFFE)
            .Should().ContainSingle(e => e.Error.Code == "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemInvalida");

    [Fact(DisplayName = "O grupo repetido é ecoado sem caractere invisível")]
    public void Criar_QuadroComGrupoRepetido_EcoaORotuloSaneado()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(
            RegraCalculoMediaPonderada(), RegraArredondamento(), 2, RegraOrdemAlocacao(), 1, [], baseadoEmEnem: true,
            QuadroPesoAreaEnemDeTeste.Resolucao,
            [QuadroPesoAreaEnemDeTeste.Grupo("TECNOLOGICA", "Tecno\u202Elógica"), QuadroPesoAreaEnemDeTeste.Grupo("TECNOLOGICA", "Tecno\u202Elógica")]);

        string mensagem = resultado.Errors.Single(e => e.Error.Code == "ConfiguracaoClassificacao.QuadroPesoAreaEnemGrupoRepetido").Error.Message;
        mensagem.Should().Contain("Tecno?lógica").And.NotContain("\u202E");
    }
}
