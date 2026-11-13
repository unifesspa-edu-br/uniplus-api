namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FasesDoCronogramaDeTeste;

/// <summary>
/// A regra de recurso da fase: o <see cref="DefinirCronogramaFasesCommandHandler"/> confere a regra contra o
/// catálogo de regras e a âncora contra o catálogo de tipos de ato, e congela cada âncora no produto da sua
/// própria fase.
/// </summary>
public sealed class DefinirCronogramaFasesAncoraDoRecursoTests
{
    [Fact(DisplayName = "CA-02/D9: referenciar uma regra de OUTRO TipoRegra em RegraRecurso é recusado com RegraRecursoFase.RegraCatalogoInvalida")]
    public async Task Handle_RegraRecursoDeTipoIncompativel_Recusa()
    {
        CenarioDoCronogramaDeFases cenario = CenarioComTipoDaClassificacao(TipoAtoDaClassificacao()).ComRegraNoCatalogo(RegraDeBonus());
        FaseCronogramaInput fase = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal()) with
        {
            RegraRecurso = RecursoAncoradoNaClassificacao() with { RegraCodigo = RegraDeBonus().Codigo },
        };

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(fase);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraRecursoFase.RegraCatalogoInvalida");
    }

    [Fact(DisplayName = "CA-18: âncora cujo tipo de ato CONGELA configuração é recusada com ProcessoSeletivo.TipoQueCongelaDeclaradoComoResultado")]
    public async Task Handle_AncoraEmAtoCongelante_Recusa()
    {
        CenarioDoCronogramaDeFases cenario = CenarioComTipoDaClassificacao(TipoAtoDaClassificacao() with { CongelaConfiguracao = true });
        FaseCronogramaInput fase = FaseRecorrivelAncorada(cenario, ProdutoDaClassificacao(PapelProdutoFaseCodigo.Preliminar));

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(fase);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.TipoQueCongelaDeclaradoComoResultado");
    }

    [Fact(DisplayName = "CA-08: âncora cujo tipo de ato tem efeito IRREVERSÍVEL é recusada com RegraRecursoFase.AncoraEmAtoIrreversivel")]
    public async Task Handle_AncoraEmAtoIrreversivel_Recusa()
    {
        CenarioDoCronogramaDeFases cenario = CenarioComTipoDaClassificacao(TipoAtoDaClassificacao() with { EfeitoIrreversivel = true });
        FaseCronogramaInput fase = FaseRecorrivelAncorada(cenario, ProdutoDaClassificacao(PapelProdutoFaseCodigo.Preliminar));

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(fase);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraRecursoFase.AncoraEmAtoIrreversivel",
            "o ato que encerra a matéria não se desfaz, e o recurso cabível é contra o resultado que o fundamenta");
    }

    [Fact(DisplayName = "CA-08 (contraprova): âncora em ato reversível e não congelante é aceita")]
    public async Task Handle_AncoraEmAtoReversivel_Aceita()
    {
        CenarioDoCronogramaDeFases cenario = CenarioComTipoDaClassificacao(TipoAtoDaClassificacao());
        FaseCronogramaInput ancorada = FaseRecorrivelAncorada(cenario, ProdutosPreliminarEDefinitivo());
        FaseCronogramaInput fase = ancorada with { RegraRecurso = RecursoAncoradoNaClassificacao() with { AdmiteInstanciaSuperior = true } };

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(fase);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        cenario.Processo.CronogramaFases.Should().ContainSingle().Which.Produtos.Should().HaveCount(2);
        cenario.Processo.CronogramaFases.Single().RegraRecurso!.Args.AdmiteInstanciaSuperior.Should().BeTrue(
            "a declaração é da janela, e o handler a leva do payload, não a fixa");
    }

    // As duas primeiras fases publicam RESULTADO_CLASSIFICACAO e admitem recurso: a situação observada em
    // homologação, em que o código do ato sozinho não diz de qual das duas publicações o prazo do candidato conta.
    [Fact(DisplayName = "CA-01: duas fases que publicam o MESMO tipo de ato ancoram cada uma no produto da sua própria fase")]
    public async Task Handle_DuasFasesComOMesmoAtoAncora_ResolveCadaAncoraNaPropriaFase()
    {
        CenarioDoCronogramaDeFases cenario = CenarioComTipoDaClassificacao(TipoAtoDaClassificacao());
        FaseCronogramaInput biopsicossocial = FaseDeRecursoSobAClassificacao(cenario, FaseCanonicaRecorrivel() with { Codigo = "AVALIACAO_BIOPSICOSSOCIAL" }, ordem: 1);
        FaseCronogramaInput preliminar = FaseDeRecursoSobAClassificacao(cenario, FaseCanonicaRecorrivel(), ordem: 2);
        FaseCronogramaInput conclusiva = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal()) with { Ordem = 3 };

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(biopsicossocial, preliminar, conclusiva);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        CadaRegraAncoraNoProdutoDaPropriaFase(cenario.Processo);
    }

    private static CenarioDoCronogramaDeFases CenarioComTipoDaClassificacao(TipoAtoPublicadoView tipo) =>
        CenarioDoCronogramaDeFases.EmRascunho().ComTipoDeAto(tipo).ComRegraNoCatalogo(RegraDePrazoDeRecurso());

    private static FaseCronogramaInput FaseRecorrivelAncorada(CenarioDoCronogramaDeFases cenario, params ProdutoDaFaseInput[] produtos) =>
        cenario.CadastrarFase(FaseCanonicaRecorrivel()) with
        {
            Produtos = produtos,
            RegraRecurso = RecursoAncoradoNaClassificacao(),
        };

    private static FaseCronogramaInput FaseDeRecursoSobAClassificacao(CenarioDoCronogramaDeFases cenario, FaseCanonicaView faseCanonica, int ordem) =>
        cenario.CadastrarFase(faseCanonica) with
        {
            Ordem = ordem,
            Produtos = [ProdutoDaClassificacao(PapelProdutoFaseCodigo.Preliminar)],
            FaseConcluinteCodigo = "RESULTADO_FINAL",
            RegraRecurso = RecursoAncoradoNaClassificacao(),
        };

    private static void CadaRegraAncoraNoProdutoDaPropriaFase(ProcessoSeletivo processo)
    {
        FaseCronograma[] recorriveis = [.. processo.CronogramaFases.Where(f => f.RegraRecurso is not null).OrderBy(f => f.Ordem)];
        recorriveis.Should().HaveCount(2);
        foreach (FaseCronograma fase in recorriveis)
        {
            fase.RegraRecurso!.ProdutoAncoraId.Should().Be(fase.Produtos.Single().Id,
                "cada regra resolve para o produto da SUA fase, e nenhuma para a publicação da outra");
        }

        recorriveis[0].RegraRecurso!.ProdutoAncoraId.Should().NotBe(recorriveis[1].RegraRecurso!.ProdutoAncoraId);
    }
}
