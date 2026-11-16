namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.FaseDeTesteDaAtualizacao;

/// <summary>
/// Caracterização de <see cref="FaseCronograma"/> na ordem das recusas acumuladas por <see cref="FaseCronograma.Criar"/>, nas
/// finalidades que a fase atende e no tratamento do código com espaços nas pontas.
/// </summary>
public sealed class FaseCronogramaCriacaoTests
{
    // ── Criar: ordem das recusas acumuladas ──

    private static Result<FaseCronograma> FaseComJanelaInvertidaEPublicacoesERecorteRecusados() => Criar(new()
    {
        Inicio = InicioDaJanela.AddDays(2),
        Fim = InicioDaJanela,
        Produtos = [ProdutoDaFase.Criar("COMUNICADO", null), ProdutoDaFase.Criar("COMUNICADO", null)],
        EmiteParecerIndividual = true,
        RegraRecurso = Recurso(Guid.CreateVersion7()),
        Bancas = [Banca("BANCA_ANALISE_DOCUMENTAL"), Banca("BANCA_ANALISE_DOCUMENTAL")],
    });

    [Fact(DisplayName = "As recusas acumuladas seguem a ordem: janela, publicações, âncora do recurso e recorte")]
    public void Criar_VariasRecusas_SeguemAOrdemDaJanelaPublicacoesAncoraERecorte()
    {
        Result<FaseCronograma> resultado = FaseComJanelaInvertidaEPublicacoesERecorteRecusados();

        Recusas(resultado).Should().Equal(
            ("fim", "FaseCronograma.JanelaInvertida"),
            ("produtos", "FaseCronograma.AtoDuplicadoNaFase"),
            ("emiteParecerIndividual", "FaseCronograma.ParecerIndividualSemResultado"),
            ("produtos", "RegraRecursoFase.FaseSemProdutoPreliminar"),
            ("bancasRequeridas[0].categoriasDocumentoIds", "BancaRequerida.RecorteDeCompetenciaObrigatorio"),
            ("bancasRequeridas[1].categoriasDocumentoIds", "BancaRequerida.RecorteDeCompetenciaObrigatorio"));
    }

    [Fact(DisplayName = "A janela obrigatória antecede a âncora, e a categoria repetida no recorte antecede o recorte duplicado")]
    public void Criar_JanelaObrigatoriaAncoraECategoriaRepetida_SeguemAOrdem()
    {
        Result<FaseCronograma> resultado = Criar(new()
        {
            OrigemData = OrigemDataFase.NoEdital,
            Produtos = [Preliminar(Resultado)],
            RegraRecurso = Recurso(Guid.CreateVersion7()),
            Bancas = [Banca("BANCA_A", "RENDA"), Banca("BANCA_A", "RENDA"), Banca("BANCA_B", "RENDA", "RENDA")],
        });

        Recusas(resultado).Should().Equal(
            (null, "FaseCronograma.JanelaObrigatoriaEmDataNoEdital"),
            ("regraRecurso.atoAncoraCodigo", "RegraRecursoFase.AncoraNaoEhProdutoPreliminarDaFase"),
            ("bancasRequeridas[2].categoriasDocumentoIds", "BancaRequerida.CategoriaDuplicadaNoRecorte"),
            ("bancasRequeridas[1].categoriasDocumentoIds", "BancaRequerida.RecorteDeCompetenciaDuplicado"));
    }

    [Fact(DisplayName = "O par repetido A, B, B, A informa o ato A, a primeira ocorrência repetida")]
    public void Criar_ParRepetidoEmAbba_InformaAPrimeiraOcorrencia()
    {
        Result<FaseCronograma> resultado = Criar(new()
        {
            Produtos = [Preliminar("ATO_A"), Preliminar("ATO_B"), Preliminar("ATO_B"), Preliminar("ATO_A")],
        });

        Recusas(resultado).Should().ContainSingle().Which.Codigo.Should().Be("FaseCronograma.AtoDuplicadoNaFase");
        resultado.Error!.Message.Should().Contain("'ATO_A'").And.NotContain("'ATO_B'");
    }

    [Fact(DisplayName = "A fábrica recusa o recorte de competência na banca de análise de recursos, no campo do recorte")]
    public void Criar_BancaDeRecursosComRecorte_RecusaORecorte() =>
        Recusas(Criar(new() { Bancas = [Banca(TipoBancaCodigo.AnaliseDeRecursos, "RENDA")] })).Should().Equal(
            ("bancasRequeridas[0].categoriasDocumentoIds", "Banca.RecorteEmBancaDeRecursos"));

    // ── FinalidadesQueAtende ──

    [Fact(DisplayName = "As finalidades que a fase atende saem na ordem inscrição, isenção e habilitação")]
    public void FinalidadesQueAtende_FaseDeTodas_SaiNaOrdemInscricaoIsencaoHabilitacao()
    {
        FaseCronograma fase = Fase(new()
        {
            Codigo = FormularioProcesso.CodigoFaseHabilitacao,
            ColetaInscricao = true,
            ColetaSolicitacaoIsencao = true,
        });

        fase.FinalidadesQueAtende().Should().Equal(
            FinalidadeFormulario.Inscricao, FinalidadeFormulario.IsencaoTaxa, FinalidadeFormulario.Habilitacao);
    }

    [Fact(DisplayName = "A fase que não coleta nada nem é a de habilitação não atende finalidade alguma")]
    public void FinalidadesQueAtende_FaseSemColetaNemHabilitacao_NaoAtendeNenhuma()
    {
        FaseCronograma fase = Fase(new());

        fase.FinalidadesQueAtende().Should().BeEmpty();
        fase.Atende(FinalidadeFormulario.Inscricao).Should().BeFalse();
    }

    [Fact(DisplayName = "O código da habilitação com espaços nas pontas é aparado e a fase atende a habilitação")]
    public void FinalidadesQueAtende_CodigoDaHabilitacaoComEspacos_AtendeAHabilitacao()
    {
        FaseCronograma fase = Fase(new() { Codigo = $"  {FormularioProcesso.CodigoFaseHabilitacao}  " });

        fase.Atende(FinalidadeFormulario.Habilitacao).Should().BeTrue();
    }

    [Fact(DisplayName = "O código da habilitação em minúsculas não é a habilitação")]
    public void FinalidadesQueAtende_CodigoDaHabilitacaoEmMinusculas_NaoAtendeAHabilitacao()
    {
        FaseCronograma fase = Fase(new() { Codigo = FormularioProcesso.CodigoFaseHabilitacao.ToLowerInvariant() });

        fase.Atende(FinalidadeFormulario.Habilitacao).Should().BeFalse();
    }

    // ── Criar e Reidratar com o código com espaços nas pontas ──

    [Fact(DisplayName = "Criar guarda o código aparado, mas a recusa cita o código como veio")]
    public void Criar_CodigoComEspacos_GuardaAparadoERecusaCitaComoVeio()
    {
        Result<FaseCronograma> recusada = Criar(new()
        {
            Codigo = "  FASE_TESTE  ",
            Inicio = InicioDaJanela.AddDays(1),
            Fim = InicioDaJanela,
        });
        Result<FaseCronograma> aceita = Criar(new() { Codigo = "  FASE_TESTE  " });

        recusada.Error!.Message.Should().Contain("'  FASE_TESTE  '");
        aceita.Value!.Codigo.Should().Be("FASE_TESTE");
    }

    private static Result<FaseCronograma> CriarRecusadaPelaJanelaComFilhas(
        ProdutoDaFase produto, BancaRequerida banca, RegraRecursoFase regra)
    {
        return Criar(new()
        {
            Inicio = InicioDaJanela.AddDays(1),
            Fim = InicioDaJanela,
            Produtos = [produto],
            Bancas = [banca],
            RegraRecurso = regra,
        });
    }

    [Fact(DisplayName = "Criar recusada não vincula produtos, bancas nem regra de recurso à fase")]
    public void Criar_Recusada_NaoVinculaAsFilhas()
    {
        ProdutoDaFase produto = Preliminar(Resultado);
        BancaRequerida banca = Banca("BANCA_ANALISE_DOCUMENTAL");
        RegraRecursoFase regra = Recurso(produto.Id);

        Result<FaseCronograma> resultado = CriarRecusadaPelaJanelaComFilhas(produto, banca, regra);

        resultado.IsFailure.Should().BeTrue();
        (produto.FaseCronogramaId, banca.FaseCronogramaId, regra.FaseCronogramaId)
            .Should().Be((Guid.Empty, Guid.Empty, Guid.Empty));
    }

    [Fact(DisplayName = "Criar com sucesso vincula produtos, bancas e regra de recurso à fase criada")]
    public void Criar_Aceita_VinculaAsFilhas()
    {
        ProdutoDaFase produto = Preliminar(Resultado);
        BancaRequerida banca = Banca("BANCA_ANALISE_DOCUMENTAL");
        RegraRecursoFase regra = Recurso(produto.Id);

        FaseCronograma fase = Fase(new() { Produtos = [produto], Bancas = [banca], RegraRecurso = regra });

        (produto.FaseCronogramaId, banca.FaseCronogramaId, regra.FaseCronogramaId)
            .Should().Be((fase.Id, fase.Id, fase.Id));
    }

    [Fact(DisplayName = "Reidratar guarda o código aparado, preserva o Id e vincula as filhas a ele")]
    public void Reidratar_CodigoComEspacos_GuardaAparadoEVinculaAoIdPreservado()
    {
        Guid id = Guid.CreateVersion7();
        ProdutoDaFase produto = Preliminar(Resultado);

        FaseCronograma fase = Reidratar(id, new() { Codigo = "  FASE_TESTE  ", Produtos = [produto] });

        (fase.Id, fase.Codigo, produto.FaseCronogramaId).Should().Be((id, "FASE_TESTE", id));
    }

    [Fact(DisplayName = "Reidratar com âncora incoerente lança com o código citado como veio")]
    public void Reidratar_AncoraIncoerenteComCodigoComEspacos_LancaCitandoOCodigoComoVeio()
    {
        ProdutoDaFase produto = Preliminar(Resultado);

        Action act = () => Reidratar(Guid.CreateVersion7(), new()
        {
            Codigo = "  FASE_TESTE  ",
            Produtos = [produto],
            RegraRecurso = Recurso(Guid.CreateVersion7()),
        });

        act.Should().Throw<ArgumentException>().WithParameterName("regraRecurso")
            .Which.Message.Should().Contain("'  FASE_TESTE  '");
    }

    [Theory(DisplayName = "Criar e Reidratar com código em branco lançam, sem acumular recusa")]
    [InlineData("")]
    [InlineData("   ")]
    public void CriarEReidratar_CodigoEmBranco_Lancam(string codigoEmBranco)
    {
        Action criar = () => Criar(new() { Codigo = codigoEmBranco });
        Action reidratar = () => Reidratar(Guid.CreateVersion7(), new() { Codigo = codigoEmBranco });

        criar.Should().Throw<ArgumentException>().WithParameterName("codigo");
        reidratar.Should().Throw<ArgumentException>().WithParameterName("codigo");
    }
}
