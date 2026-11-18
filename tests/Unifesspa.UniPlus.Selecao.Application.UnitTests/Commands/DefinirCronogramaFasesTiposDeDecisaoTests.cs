namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FasesDoCronogramaDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;

/// <summary>
/// Os tipos de decisão de cada fase são conferidos pelo domínio, na criação da fase, e a recusa deles se acumula com a
/// das demais violações da mesma fase e com a das outras fases. Só o procedimento de recurso, que lê os tipos
/// declarados, deixa de ser avaliado quando os tipos são recusados.
/// </summary>
public sealed class DefinirCronogramaFasesTiposDeDecisaoTests
{
    private const string Classificacao = TipoMotivoDeDecisaoCodigo.Classificacao;
    private const string Isencao = TipoMotivoDeDecisaoCodigo.IsencaoDaTaxa;
    private const string Heteroidentificacao = TipoMotivoDeDecisaoCodigo.Heteroidentificacao;
    private const string TipoForaDoVocabulario = "RECURSO_DA_BANCA";

    private static FaseCronogramaInput FaseDoCadastro(CenarioDoCronogramaDeFases cenario, int ordem, params string?[] tipos) =>
        cenario.CadastrarFase(FaseCanonicaDoResultadoFinal()) with { Ordem = ordem, TiposDeDecisao = tipos };

    private static FaseCronogramaInput FaseSemCadastro(int ordem, params string?[] tipos) =>
        EntradaDaFase(Guid.CreateVersion7()) with { Ordem = ordem, TiposDeDecisao = tipos };

    private static (string? Campo, string Codigo)[] Recusas(Result resultado) =>
        [.. resultado.Errors.Select(static e => (e.Field, e.Error.Code))];

    private static FaseCronogramaInput[] FasesComTiposEmVariasPosicoes(CenarioDoCronogramaDeFases cenario) =>
    [
        FaseDoCadastro(cenario, 1, TipoForaDoVocabulario),
        FaseDoCadastro(cenario, 2, Classificacao),
        FaseDoCadastro(cenario, 3, Isencao, Classificacao, Isencao),
        FaseDoCadastro(cenario, 4, null, Classificacao),
    ];

    private static CenarioDoCronogramaDeFases CenarioComTipoDeAtoDaClassificacao() =>
        CenarioDoCronogramaDeFases.EmRascunho().ComTipoDeAto(TipoAtoDaClassificacao()).ComRegraNoCatalogo(RegraDePrazoDeRecurso())
            .ComRegraNoCatalogo(RegraDoProcedimentoDeRecalculo());

    private static FaseCronogramaInput FaseComProcedimentoDaClassificacao(
        CenarioDoCronogramaDeFases cenario, params string?[] tiposDeDecisao) =>
        cenario.CadastrarFase(FaseCanonicaRecorrivel()) with
        {
            Produtos = ProdutosPreliminarEDefinitivo(),
            TiposDeDecisao = tiposDeDecisao,
            RegraRecurso = RecursoAncoradoNaClassificacao() with
            {
                Procedimentos = [new(Classificacao, null, RegraProcedimentoRecursoCodigo.Recalculo, "v1")],
            },
        };

    private static CenarioDoCronogramaDeFases CenarioDaFaseConforme()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho().ComTipoDeAto(TipoAtoDaClassificacao());
        cenario.Processo.DefinirAtendimentoEspecializado(AtendimentoEspecializado.Criar([], [], []).Value!);
        return cenario;
    }

    [Fact(DisplayName = "A fase declara os tipos de decisão e a leitura os devolve em ordem canônica, do comando ao DTO")]
    public async Task Handle_FaseComTipos_GravaEDevolveEmOrdemCanonica()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDaFaseConforme();
        FaseCronogramaInput fase = FaseDoCadastro(cenario, 1, Isencao, Classificacao, Heteroidentificacao);

        Result resultado = await cenario.DefinirFasesAsync(fase);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        FaseCronogramaDto dto = ProjecaoDaFaseDoCronograma.ProjetarFase(cenario.Processo.CronogramaFases.Single());
        dto.Publicacoes.TiposDeDecisao.Should().Equal(Classificacao, Heteroidentificacao, Isencao);
        await cenario.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A fase que não declara tipo é gravada com a declaração vazia")]
    public async Task Handle_FaseSemTipos_GravaDeclaracaoVazia()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDaFaseConforme();
        FaseCronogramaInput fase = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal());

        Result resultado = await cenario.DefinirFasesAsync(fase);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        cenario.Processo.CronogramaFases.Single().TiposDeDecisao.Should().BeEmpty();
    }

    [Fact(DisplayName = "Tipos inválidos em várias fases saem juntos, por posição de fase e de tipo")]
    public async Task Handle_TiposInvalidosEmVariasFases_RecusaPorPosicao()
    {
        CenarioDoCronogramaDeFases cenario = CenarioComTipoDeAtoDaClassificacao();

        Result resultado = await cenario.DefinirFasesAsync(FasesComTiposEmVariasPosicoes(cenario));

        Recusas(resultado).Should().Equal(
            ("fases[0].tiposDeDecisao[0]", MotivoDeDecisaoErrorCodes.TipoInvalido),
            ("fases[2].tiposDeDecisao[2]", MotivoDeDecisaoErrorCodes.TipoDeDecisaoDuplicado),
            ("fases[3].tiposDeDecisao[0]", MotivoDeDecisaoErrorCodes.TipoInvalido));
    }

    [Fact(DisplayName = "Fase com tipo inválido e janela invertida devolve as duas recusas, a da janela primeiro")]
    public async Task Handle_TipoInvalidoEJanelaInvertidaNaMesmaFase_RecusaAsDuasNaOrdemDaDeclaracao()
    {
        CenarioDoCronogramaDeFases cenario = CenarioComTipoDeAtoDaClassificacao();
        FaseCronogramaInput fase = FaseDoCadastro(cenario, 1, TipoForaDoVocabulario) with
        {
            Inicio = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
            Fim = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };

        Result resultado = await cenario.DefinirFasesAsync(fase);

        Recusas(resultado).Should().Equal(
            ("fases[0].fim", "FaseCronograma.JanelaInvertida"),
            ("fases[0].tiposDeDecisao[0]", MotivoDeDecisaoErrorCodes.TipoInvalido));
        resultado.Error!.Code.Should().Be("FaseCronograma.JanelaInvertida");
    }

    [Fact(DisplayName = "Tipo inválido na fase 0 não impede o julgamento da fase 1, que recusa o ato repetido")]
    public async Task Handle_TipoInvalidoNaFaseZeroEAtoRepetidoNaFaseUm_RecusaAsDuas()
    {
        CenarioDoCronogramaDeFases cenario = CenarioComTipoDeAtoDaClassificacao();
        FaseCronogramaInput comTipoInvalido = FaseDoCadastro(cenario, 1, TipoForaDoVocabulario);
        FaseCronogramaInput comAtoRepetido = FaseDoCadastro(cenario, 2) with
        {
            Produtos = [ProdutoDaClassificacao(PapelProdutoFaseCodigo.Definitivo), ProdutoDaClassificacao(PapelProdutoFaseCodigo.Definitivo)],
        };

        Result resultado = await cenario.DefinirFasesAsync(comTipoInvalido, comAtoRepetido);

        Recusas(resultado).Should().Equal(
            ("fases[0].tiposDeDecisao[0]", MotivoDeDecisaoErrorCodes.TipoInvalido),
            ("fases[1].produtos", "FaseCronograma.AtoDuplicadoNaFase"));
    }

    [Fact(DisplayName = "Tipo com erro de digitação recusa só o tipo: o procedimento do tipo certo não é avaliado")]
    public async Task Handle_TipoComErroDeDigitacaoEProcedimentoDoTipoCerto_RecusaSoOTipo()
    {
        CenarioDoCronogramaDeFases cenario = CenarioComTipoDeAtoDaClassificacao();

        Result resultado = await cenario.DefinirFasesAsync(FaseComProcedimentoDaClassificacao(cenario, "CLASIFICACAO"));

        Recusas(resultado).Should().Equal(("fases[0].tiposDeDecisao[0]", MotivoDeDecisaoErrorCodes.TipoInvalido));
    }

    [Fact(DisplayName = "Tipos válidos sem o tipo do procedimento continuam recusando o procedimento")]
    public async Task Handle_TiposValidosSemOTipoDoProcedimento_RecusaOProcedimento()
    {
        CenarioDoCronogramaDeFases cenario = CenarioComTipoDeAtoDaClassificacao();

        Result resultado = await cenario.DefinirFasesAsync(FaseComProcedimentoDaClassificacao(cenario, Isencao));

        Recusas(resultado).Should().Equal((
            "fases[0].regraRecurso.procedimentos[0].tipoDeDecisao", CodigosDoProcedimentoDeRecurso.DaFase.ParaDecisaoNaoDeclarada));
    }

    [Fact(DisplayName = "Fase anterior com cadastro inexistente interrompe a montagem: a recusa de tipo da fase seguinte não é julgada")]
    public async Task Handle_CanonicaInexistenteNaFaseZeroETipoInvalidoNaFaseUm_DevolveAInterrupcaoDaFaseZero()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho();
        FaseCronogramaInput[] fases = [FaseSemCadastro(1, Classificacao), FaseSemCadastro(2, TipoForaDoVocabulario)];

        Result resultado = await cenario.DefinirFasesAsync(fases);

        Recusas(resultado).Should().Equal(("fases[0].faseCanonicaId", "FaseCronograma.FaseCanonicaNaoEncontrada"));
    }

    [Fact(DisplayName = "A recusa dos tipos não muda o processo nem grava")]
    public async Task Handle_TiposInvalidos_NaoMutaOProcessoNemGrava()
    {
        CenarioDoCronogramaDeFases cenario = CenarioComTipoDeAtoDaClassificacao();

        Result resultado = await cenario.DefinirFasesAsync(FaseDoCadastro(cenario, 1, TipoForaDoVocabulario));

        resultado.IsFailure.Should().BeTrue();
        cenario.Processo.CronogramaFases.Should().BeEmpty();
        await cenario.UnitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }

    [Fact(DisplayName = "Com tipos válidos, o produto recusado segue sendo a recusa da fase")]
    public async Task Handle_TiposValidosEProdutoRecusado_MantemARecusaDoProduto()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho().ComTipoDeAto(TipoAtoDoComunicado());
        FaseCronogramaInput fase = FaseDoCadastro(cenario, 1, Classificacao) with
        {
            Produtos = [ProdutoDoComunicado(PapelProdutoFaseCodigo.Definitivo)],
        };

        Result resultado = await cenario.DefinirFasesAsync(fase);

        Recusas(resultado).Should().Equal(("fases[0].produtos[0].papel", "ProdutoDaFase.PapelEmAtoQueNaoEhResultado"));
    }

    [Fact(DisplayName = "Processo inexistente vence os tipos inválidos")]
    public async Task Handle_ProcessoInexistenteComTiposInvalidos_RecusaPeloProcesso()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho().SemOProcessoNoRepositorio();

        Result resultado = await cenario.DefinirFasesAsync(FaseSemCadastro(1, TipoForaDoVocabulario));

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

}
