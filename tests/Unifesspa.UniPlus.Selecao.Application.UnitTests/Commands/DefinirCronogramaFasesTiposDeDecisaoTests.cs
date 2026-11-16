namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FasesDoCronogramaDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;

/// <summary>
/// Os tipos de decisão de cada fase são conferidos por uma passada pura sobre todo o payload, depois do processo, da
/// precondição e da leitura das fases iniciadas e antes de qualquer cadastro: havendo recusa de tipos em alguma fase, o
/// lote delas sai sozinho, por posição, e nenhum cadastro é consultado.
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

    private static (string? Campo, string Codigo)[] Recusas(Result<MutacaoAceita> resultado) =>
        [.. resultado.Errors.Select(static e => (e.Field, e.Error.Code))];

    private static FaseCronogramaInput[] FasesComTiposEmVariasPosicoes() =>
    [
        FaseSemCadastro(1, TipoForaDoVocabulario),
        FaseSemCadastro(2, Classificacao),
        FaseSemCadastro(3, Isencao, Classificacao, Isencao),
        FaseSemCadastro(4, null, Classificacao),
    ];

    private static void NenhumCadastroFoiConsultado(CenarioDoCronogramaDeFases cenario)
    {
        object[] cadastros =
        [
            cenario.FaseCanonicaReader, cenario.UnidadeResponsavel, cenario.TipoBancaReader,
            cenario.CategoriaDocumentoReader, cenario.RegraCatalogoReader, cenario.TipoAtoPublicadoReader,
        ];

        cadastros.SelectMany(static cadastro => cadastro.ReceivedCalls()).Should().BeEmpty();
    }

    private static CenarioDoCronogramaDeFases CenarioDaFaseConforme()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho().ComTipoDeAto(TipoAtoDaClassificacao());
        cenario.Processo.DefinirAtendimentoEspecializado(AtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);
        return cenario;
    }

    private static CenarioDoCronogramaDeFases CenarioEmRetificacao()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao versaoAtual) = ProcessoPublicado();
        AbrirRetificacao(processo, versaoAtual);
        return CenarioDoCronogramaDeFases.Para(processo).ComFasesIniciadas(FasesIniciadas.Nenhuma);
    }

    private static Task<Result<MutacaoAceita>> DefinirComTipoInvalidoAsync(CenarioDoCronogramaDeFases cenario) =>
        cenario.DefinirAsync(new DefinirCronogramaFasesCommand(
            cenario.Processo.Id, [FaseSemCadastro(1, TipoForaDoVocabulario)], PrecondicaoIfMatch.Curinga));

    [Fact(DisplayName = "A fase declara os tipos de decisão e a leitura os devolve em ordem canônica, do comando ao DTO")]
    public async Task Handle_FaseComTipos_GravaEDevolveEmOrdemCanonica()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDaFaseConforme();
        FaseCronogramaInput fase = FaseDoCadastro(cenario, 1, Isencao, Classificacao, Heteroidentificacao);

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(fase);

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

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(fase);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        cenario.Processo.CronogramaFases.Single().TiposDeDecisao.Should().BeEmpty();
    }

    [Fact(DisplayName = "Tipos inválidos em várias fases saem juntos, por posição de fase e de tipo, sem consultar cadastro")]
    public async Task Handle_TiposInvalidosEmVariasFases_RecusaPorPosicaoSemConsultarCadastro()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho();

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(FasesComTiposEmVariasPosicoes());

        Recusas(resultado).Should().Equal(
            ("fases[0].tiposDeDecisao[0]", MotivoDeDecisaoErrorCodes.TipoInvalido),
            ("fases[2].tiposDeDecisao[2]", MotivoDeDecisaoErrorCodes.TipoDeDecisaoDuplicado),
            ("fases[3].tiposDeDecisao[0]", MotivoDeDecisaoErrorCodes.TipoInvalido));
        NenhumCadastroFoiConsultado(cenario);
    }

    [Fact(DisplayName = "Fase anterior com cadastro inexistente e tipo inválido na seguinte: só os tipos são recusados")]
    public async Task Handle_CanonicaInexistenteNaFaseZeroETipoInvalidoNaFaseUm_RecusaSoOsTipos()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho();
        FaseCronogramaInput[] fases = [FaseSemCadastro(1, Classificacao), FaseSemCadastro(2, TipoForaDoVocabulario)];

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(fases);

        Recusas(resultado).Should().Equal(("fases[1].tiposDeDecisao[0]", MotivoDeDecisaoErrorCodes.TipoInvalido));
        NenhumCadastroFoiConsultado(cenario);
    }

    [Fact(DisplayName = "A recusa dos tipos não muda o processo nem grava")]
    public async Task Handle_TiposInvalidos_NaoMutaOProcessoNemGrava()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho();

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(FaseSemCadastro(1, TipoForaDoVocabulario));

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

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(fase);

        Recusas(resultado).Should().Equal(("fases[0].produtos[0].papel", "ProdutoDaFase.PapelEmAtoQueNaoEhResultado"));
    }

    [Fact(DisplayName = "Processo inexistente vence os tipos inválidos")]
    public async Task Handle_ProcessoInexistenteComTiposInvalidos_RecusaPeloProcesso()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho().SemOProcessoNoRepositorio();

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(FaseSemCadastro(1, TipoForaDoVocabulario));

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Precondição recusada vence os tipos inválidos")]
    public async Task Handle_PrecondicaoRecusadaComTiposInvalidos_RecusaPelaPrecondicao()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao versaoAtual) = ProcessoPublicado();
        AbrirRetificacao(processo, versaoAtual);
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.Para(processo);

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(FaseSemCadastro(1, TipoForaDoVocabulario));

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("Precondicao.Requerida");
    }

    [Fact(DisplayName = "Com a retificação aberta, as fases iniciadas são lidas antes de os tipos inválidos serem recusados")]
    public async Task Handle_RetificacaoAbertaComTiposInvalidos_LeAsFasesIniciadasERecusaOsTipos()
    {
        CenarioDoCronogramaDeFases cenario = CenarioEmRetificacao();

        Result<MutacaoAceita> resultado = await DefinirComTipoInvalidoAsync(cenario);

        await cenario.FasesIniciadas.Received(1).ObterAsync(cenario.Processo.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        Recusas(resultado).Should().Equal(("fases[0].tiposDeDecisao[0]", MotivoDeDecisaoErrorCodes.TipoInvalido));
    }

    [Fact(DisplayName = "A falha na leitura das fases iniciadas vence os tipos inválidos")]
    public async Task Handle_FalhaNaLeituraDasFasesIniciadasComTiposInvalidos_PropagaAFalhaDaLeitura()
    {
        CenarioDoCronogramaDeFases cenario = CenarioEmRetificacao();
        cenario.FasesIniciadas.ObterAsync(default, default, default)
            .ThrowsAsyncForAnyArgs(new InvalidOperationException("leitura das fases iniciadas indisponível"));

        Func<Task> definicao = () => DefinirComTipoInvalidoAsync(cenario);

        await definicao.Should().ThrowAsync<InvalidOperationException>();
        await cenario.UnitOfWork.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }
}
