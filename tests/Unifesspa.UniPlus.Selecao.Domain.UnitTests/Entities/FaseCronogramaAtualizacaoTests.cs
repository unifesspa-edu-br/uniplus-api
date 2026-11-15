namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// Caracterização do comportamento de <see cref="FaseCronograma"/> na reconciliação por
/// <c>AtualizarDe</c>, na ordem das recusas acumuladas por <see cref="FaseCronograma.Criar"/> e no
/// tratamento do código com espaços nas pontas.
/// </summary>
public sealed class FaseCronogramaAtualizacaoTests
{
    private const string Resultado = "RESULTADO_CLASSIFICACAO";
    private static readonly DateTimeOffset InicioDaJanela = new(2027, 1, 25, 11, 0, 0, TimeSpan.Zero);

    private sealed record DadosDaFase
    {
        public int Ordem { get; init; } = 1;
        public string Codigo { get; init; } = "RESULTADO_PRELIMINAR";
        public OrigemDataFase OrigemData { get; init; } = OrigemDataFase.NaExecucao;
        public bool ColetaInscricao { get; init; }
        public bool ColetaSolicitacaoIsencao { get; init; }
        public DateTimeOffset? Inicio { get; init; }
        public DateTimeOffset? Fim { get; init; }
        public IReadOnlyList<ProdutoDaFase> Produtos { get; init; } = [];
        public string? FaseConcluinteCodigo { get; init; }
        public bool EmiteParecerIndividual { get; init; }
        public IReadOnlyList<BancaRequerida> Bancas { get; init; } = [];
        public RegraRecursoFase? RegraRecurso { get; init; }
        public UnidadeResponsavelSnapshot? Unidade { get; init; } = UnidadesDeTeste.Ceps;
    }

    private static Result<FaseCronograma> Criar(DadosDaFase dados) => FaseCronograma.Criar(
        dados.Ordem, Guid.CreateVersion7(), dados.Codigo, dados.OrigemData,
        agrupaEtapas: false, permiteComplementacao: false,
        dados.ColetaInscricao, dados.ColetaSolicitacaoIsencao, dados.Inicio, dados.Fim,
        dados.Produtos, dados.FaseConcluinteCodigo, dados.EmiteParecerIndividual,
        dados.Bancas, dados.RegraRecurso, dados.Unidade);

    private static FaseCronograma Fase(DadosDaFase dados) => Criar(dados).Value!;

    private static FaseCronograma Reidratar(Guid id, DadosDaFase dados) => FaseCronograma.Reidratar(
        id, dados.Ordem, Guid.CreateVersion7(), dados.Codigo, dados.OrigemData,
        agrupaEtapas: false, permiteComplementacao: false,
        dados.ColetaInscricao, dados.ColetaSolicitacaoIsencao, dados.Inicio, dados.Fim,
        dados.Produtos, dados.FaseConcluinteCodigo, dados.EmiteParecerIndividual,
        dados.Bancas, dados.RegraRecurso, dados.Unidade);

    private static void Atualizar(FaseCronograma viva, FaseCronograma nova) =>
        viva.AtualizarDe(nova, [.. nova.Produtos]);

    private static RegraRecursoFase Recurso(Guid produtoAncoraId) => RegraRecursoFase.Criar(
        ReferenciaRegra.Criar(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('a', 64)).Value!,
        new ArgsRegraPrazoRecurso(48m, UnidadePrazo.Horas, null, null, false, null, null),
        produtoAncoraId).Value!;

    private static BancaRequerida Banca(string codigo, params string[] categorias) =>
        BancaRequerida.Criar(
            Guid.CreateVersion7(), codigo, "Banca de teste",
            [.. categorias.Select(static c => CategoriaJulgada.Criar(Guid.CreateVersion7(), c))]);

    private static ProdutoDaFase Preliminar(string ato) => ProdutoDaFase.Criar(ato, PapelProdutoFase.Preliminar);

    private static ProdutoDaFase Definitivo(string ato) => ProdutoDaFase.Criar(ato, PapelProdutoFase.Definitivo);

    private static (string? Campo, string Codigo)[] Recusas(Result<FaseCronograma> resultado) =>
        [.. resultado.Errors.Select(static e => (e.Field, e.Error.Code))];

    // ── AtualizarDe: reconciliação dos produtos ──

    [Fact(DisplayName = "O produto que só trocou de papel mantém o Id da linha viva")]
    public void AtualizarDe_ProdutoQueTrocouDePapel_MantemOId()
    {
        FaseCronograma viva = Fase(new() { Produtos = [Preliminar("GABARITO")] });
        Guid idDaLinha = viva.Produtos.Single().Id;
        FaseCronograma nova = Fase(new() { Produtos = [Definitivo("GABARITO")] });

        Atualizar(viva, nova);

        ProdutoDaFase produto = viva.Produtos.Should().ContainSingle().Subject;
        produto.Id.Should().Be(idDaLinha);
        produto.Papel.Should().Be(PapelProdutoFase.Definitivo);
    }

    [Fact(DisplayName = "Preliminar e definitiva da mesma matéria não casam com a mesma linha viva")]
    public void AtualizarDe_MesmaMateriaComDoisPapeis_ConsomeACadaLinhaUmaVez()
    {
        FaseCronograma viva = Fase(new() { Produtos = [Preliminar(Resultado)] });
        Guid idPreliminarVivo = viva.Produtos.Single().Id;
        ProdutoDaFase definitivoNovo = Definitivo(Resultado);
        FaseCronograma nova = Fase(new() { Produtos = [definitivoNovo, Preliminar(Resultado)] });

        Atualizar(viva, nova);

        viva.Produtos.Select(static p => p.Papel).Should().Equal(PapelProdutoFase.Definitivo, PapelProdutoFase.Preliminar);
        viva.Produtos.Select(static p => p.Id).Should().Equal(definitivoNovo.Id, idPreliminarVivo);
    }

    [Fact(DisplayName = "Com as duas linhas vivas da matéria, cada papel mantém o próprio Id, na ordem que chegou")]
    public void AtualizarDe_DuasLinhasVivasEmOrdemTrocada_CadaPapelMantemOSeuId()
    {
        FaseCronograma viva = Fase(new() { Produtos = [Preliminar(Resultado), Definitivo(Resultado)] });
        Guid idPreliminar = viva.Produtos.Single(static p => p.Papel == PapelProdutoFase.Preliminar).Id;
        Guid idDefinitivo = viva.Produtos.Single(static p => p.Papel == PapelProdutoFase.Definitivo).Id;
        FaseCronograma nova = Fase(new() { Produtos = [Definitivo(Resultado), Preliminar(Resultado)] });

        Atualizar(viva, nova);

        viva.Produtos.Select(static p => p.Id).Should().Equal(idDefinitivo, idPreliminar);
    }

    [Fact(DisplayName = "A linha viva que a fase nova não declara sai da coleção")]
    public void AtualizarDe_LinhaQueNaoChega_SaiDaColecao()
    {
        FaseCronograma viva = Fase(new() { Produtos = [Preliminar(Resultado), Definitivo(Resultado)] });
        Guid idDefinitivo = viva.Produtos.Single(static p => p.Papel == PapelProdutoFase.Definitivo).Id;
        FaseCronograma nova = Fase(new() { Produtos = [Definitivo(Resultado)] });

        Atualizar(viva, nova);

        viva.Produtos.Should().ContainSingle().Which.Id.Should().Be(idDefinitivo);
    }

    // ── AtualizarDe: âncora do recurso ──

    [Fact(DisplayName = "A âncora do recurso é reapontada para a linha viva que casou pelo par ato e papel")]
    public void AtualizarDe_AncoraNaInstanciaNova_ReapontaParaALinhaViva()
    {
        FaseCronograma viva = Fase(new() { Produtos = [Preliminar(Resultado)] });
        Guid idDaLinhaViva = viva.Produtos.Single().Id;
        ProdutoDaFase preliminarNovo = Preliminar(Resultado);
        FaseCronograma nova = Fase(new() { Produtos = [preliminarNovo], RegraRecurso = Recurso(preliminarNovo.Id) });

        Atualizar(viva, nova);

        viva.RegraRecurso!.ProdutoAncoraId.Should().Be(idDaLinhaViva);
        viva.RegraRecurso.FaseCronogramaId.Should().Be(viva.Id);
        viva.ViolacaoDaAncoraDoRecurso().Should().BeNull();
    }

    [Fact(DisplayName = "A âncora segue a linha viva que trocou de papel para preliminar")]
    public void AtualizarDe_AncoraEmLinhaQueTrocouDePapel_ReapontaParaALinhaViva()
    {
        FaseCronograma viva = Fase(new() { Produtos = [Definitivo(Resultado)] });
        Guid idDaLinhaViva = viva.Produtos.Single().Id;
        ProdutoDaFase preliminarNovo = Preliminar(Resultado);
        FaseCronograma nova = Fase(new() { Produtos = [preliminarNovo], RegraRecurso = Recurso(preliminarNovo.Id) });

        Atualizar(viva, nova);

        viva.RegraRecurso!.ProdutoAncoraId.Should().Be(idDaLinhaViva);
        viva.ViolacaoDaAncoraDoRecurso().Should().BeNull();
    }

    [Fact(DisplayName = "A âncora que não está na coleção recebida fica como estava")]
    public void AtualizarDe_AncoraForaDaColecaoRecebida_FicaComoEstava()
    {
        FaseCronograma viva = Fase(new() { Produtos = [Preliminar(Resultado)] });
        ProdutoDaFase preliminarNovo = Preliminar(Resultado);
        FaseCronograma nova = Fase(new() { Produtos = [preliminarNovo], RegraRecurso = Recurso(preliminarNovo.Id) });

        viva.AtualizarDe(nova, []);

        viva.RegraRecurso!.ProdutoAncoraId.Should().Be(preliminarNovo.Id);
        viva.Produtos.Should().BeEmpty();
    }

    [Fact(DisplayName = "A fase nova sem regra de recurso deixa a fase viva sem regra")]
    public void AtualizarDe_FaseNovaSemRegraDeRecurso_RemoveARegraDaViva()
    {
        ProdutoDaFase preliminar = Preliminar(Resultado);
        FaseCronograma viva = Fase(new() { Produtos = [preliminar], RegraRecurso = Recurso(preliminar.Id) });

        Atualizar(viva, Fase(new() { Produtos = [Preliminar(Resultado)] }));

        viva.RegraRecurso.Should().BeNull();
    }

    // ── AtualizarDe: bancas, janela, código e demais dados ──

    [Fact(DisplayName = "As bancas da fase viva são trocadas pelas da fase nova e ficam vinculadas a ela")]
    public void AtualizarDe_BancasDaFaseNova_SubstituemEVinculam()
    {
        BancaRequerida bancaAntiga = Banca("BANCA_ANALISE_DOCUMENTAL");
        BancaRequerida bancaNova = Banca("BANCA_HETEROIDENTIFICACAO");
        FaseCronograma viva = Fase(new() { Bancas = [bancaAntiga] });

        Atualizar(viva, Fase(new() { Bancas = [bancaNova] }));

        viva.BancasRequeridas.Should().ContainSingle().Which.Should().BeSameAs(bancaNova);
        bancaNova.FaseCronogramaId.Should().Be(viva.Id);
    }

    private static FaseCronograma FaseNovaComJanelaEmOffsetEmCodigoComEspacos()
    {
        TimeSpan offset = TimeSpan.FromHours(-3);
        return Fase(new()
        {
            Codigo = "  FASE_NOVA  ",
            OrigemData = OrigemDataFase.NoEdital,
            Inicio = InicioDaJanela.ToOffset(offset),
            Fim = InicioDaJanela.AddDays(2).ToOffset(offset),
        });
    }

    [Fact(DisplayName = "A janela da fase viva fica em UTC e o código fica aparado")]
    public void AtualizarDe_JanelaComOffsetECodigoComEspacos_FicaEmUtcEAparado()
    {
        FaseCronograma viva = Fase(new());

        Atualizar(viva, FaseNovaComJanelaEmOffsetEmCodigoComEspacos());

        viva.Codigo.Should().Be("FASE_NOVA");
        viva.Inicio.Should().Be(InicioDaJanela);
        viva.Fim.Should().Be(InicioDaJanela.AddDays(2));
        viva.Inicio!.Value.Offset.Should().Be(TimeSpan.Zero);
        viva.Fim!.Value.Offset.Should().Be(TimeSpan.Zero);
    }

    private static FaseCronograma FaseNovaComDadosDiferentes() => Fase(new()
    {
        Ordem = 3,
        Codigo = "RESULTADO_FINAL",
        FaseConcluinteCodigo = "   ",
        EmiteParecerIndividual = true,
        ColetaInscricao = true,
        Produtos = [Preliminar(Resultado)],
        Unidade = UnidadesDeTeste.Cga,
    });

    [Fact(DisplayName = "A fase viva recebe os demais dados da fase nova, com a concluinte em branco virando ausente")]
    public void AtualizarDe_DadosDaFaseNova_SaoCopiadosParaAViva()
    {
        FaseCronograma nova = FaseNovaComDadosDiferentes();
        FaseCronograma viva = Fase(new());

        Atualizar(viva, nova);

        (viva.Ordem, viva.FaseConcluinteCodigo, viva.EmiteParecerIndividual, viva.ColetaInscricao)
            .Should().Be((3, null, true, true));
        viva.FaseCanonicaOrigemId.Should().Be(nova.FaseCanonicaOrigemId);
        viva.UnidadeResponsavel.Should().Be(UnidadesDeTeste.Cga);
    }

    [Fact(DisplayName = "A fase nova sem unidade responsável deixa a fase viva sem unidade")]
    public void AtualizarDe_FaseNovaSemUnidade_LimpaAUnidadeDaViva()
    {
        FaseCronograma viva = Fase(new());

        Atualizar(viva, Fase(new() { Unidade = null }));

        viva.UnidadeResponsavel.Should().BeNull();
        viva.UnidadeResponsavelOrigemId.Should().BeNull();
    }

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
