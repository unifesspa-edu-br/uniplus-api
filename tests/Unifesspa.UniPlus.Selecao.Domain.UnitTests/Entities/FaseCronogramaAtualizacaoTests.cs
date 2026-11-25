namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.FaseDeTesteDaAtualizacao;

/// <summary>
/// Caracterização do comportamento de <see cref="FaseCronograma"/> na reconciliação por <c>AtualizarDe</c>.
/// </summary>
public sealed class FaseCronogramaAtualizacaoTests
{
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
        produto.Papel.Should().Be(PapelDaPublicacaoPrevista.Definitivo);
    }

    [Fact(DisplayName = "Preliminar e definitiva da mesma matéria não casam com a mesma linha viva")]
    public void AtualizarDe_MesmaMateriaComDoisPapeis_ConsomeACadaLinhaUmaVez()
    {
        FaseCronograma viva = Fase(new() { Produtos = [Preliminar(Resultado)] });
        Guid idPreliminarVivo = viva.Produtos.Single().Id;
        ProdutoDaFase definitivoNovo = Definitivo(Resultado);
        FaseCronograma nova = Fase(new() { Produtos = [definitivoNovo, Preliminar(Resultado)] });

        Atualizar(viva, nova);

        viva.Produtos.Select(static p => p.Papel).Should().Equal(PapelDaPublicacaoPrevista.Definitivo, PapelDaPublicacaoPrevista.Preliminar);
        viva.Produtos.Select(static p => p.Id).Should().Equal(definitivoNovo.Id, idPreliminarVivo);
    }

    [Fact(DisplayName = "Com as duas linhas vivas da matéria, cada papel mantém o próprio Id, na ordem que chegou")]
    public void AtualizarDe_DuasLinhasVivasEmOrdemTrocada_CadaPapelMantemOSeuId()
    {
        FaseCronograma viva = Fase(new() { Produtos = [Preliminar(Resultado), Definitivo(Resultado)] });
        Guid idPreliminar = viva.Produtos.Single(static p => p.Papel == PapelDaPublicacaoPrevista.Preliminar).Id;
        Guid idDefinitivo = viva.Produtos.Single(static p => p.Papel == PapelDaPublicacaoPrevista.Definitivo).Id;
        FaseCronograma nova = Fase(new() { Produtos = [Definitivo(Resultado), Preliminar(Resultado)] });

        Atualizar(viva, nova);

        viva.Produtos.Select(static p => p.Id).Should().Equal(idDefinitivo, idPreliminar);
    }

    [Fact(DisplayName = "A linha viva que a fase nova não declara sai da coleção")]
    public void AtualizarDe_LinhaQueNaoChega_SaiDaColecao()
    {
        FaseCronograma viva = Fase(new() { Produtos = [Preliminar(Resultado), Definitivo(Resultado)] });
        Guid idDefinitivo = viva.Produtos.Single(static p => p.Papel == PapelDaPublicacaoPrevista.Definitivo).Id;
        FaseCronograma nova = Fase(new() { Produtos = [Definitivo(Resultado)] });

        Atualizar(viva, nova);

        viva.Produtos.Should().ContainSingle().Which.Id.Should().Be(idDefinitivo);
    }

    // ── AtualizarDe: reconciliação das bancas pelo id ──

    [Fact(DisplayName = "A banca devolvida com o id dela mantém a linha viva, leva a ela o que mudou e continua com a identidade")]
    public void AtualizarDe_BancaComOIdDaViva_MantemALinhaViva()
    {
        FaseCronograma viva = Fase(new() { Bancas = [Banca(TipoDeBanca.AnaliseDocumental, "RENDA")] });
        BancaRequerida linhaViva = viva.BancasRequeridas.Single();
        Guid novaOrigem = Guid.CreateVersion7();
        BancaRequerida declarada = BancaQueSubstitui(linhaViva.Id, novaOrigem, TipoDeBanca.Heteroidentificacao, "PPI");

        Atualizar(viva, Fase(new() { Bancas = [declarada] }));

        BancaRequerida banca = viva.BancasRequeridas.Should().ContainSingle().Subject;
        banca.Should().BeSameAs(linhaViva);
        banca.Tipo.Codigo.Should().Be(TipoDeBanca.Heteroidentificacao);
        banca.Tipo.OrigemId.Should().Be(novaOrigem);
        banca.CandidatosAtendidos.Select(static c => c.Codigo).Should().Equal("PPI");
        banca.FaseCronogramaId.Should().Be(viva.Id);
    }

    [Fact(DisplayName = "A banca devolvida sem o id dela é criada de novo, e a viva sai")]
    public void AtualizarDe_BancaSemId_RecriaABanca()
    {
        FaseCronograma viva = Fase(new() { Bancas = [Banca(TipoDeBanca.AnaliseDocumental, "RENDA")] });
        Guid idDaViva = viva.BancasRequeridas.Single().Id;

        Atualizar(viva, Fase(new() { Bancas = [Banca(TipoDeBanca.AnaliseDocumental, "RENDA")] }));

        viva.BancasRequeridas.Single().Id.Should().NotBe(idDaViva);
    }

    [Fact(DisplayName = "Reordenar duas bancas do mesmo tipo troca as posições e mantém o id de cada uma")]
    public void AtualizarDe_BancasDoMesmoTipoReordenadas_TrocamDePosicao()
    {
        FaseCronograma viva = Fase(new() { Bancas = [Banca(TipoDeBanca.AnaliseDocumental, "RENDA"), Banca(TipoDeBanca.AnaliseDocumental, "PCD")] });
        BancaRequerida renda = viva.BancasRequeridas.Single(static b => b.CandidatosAtendidos.Single().Codigo == "RENDA");
        BancaRequerida pcd = viva.BancasRequeridas.Single(static b => b.CandidatosAtendidos.Single().Codigo == "PCD");

        Atualizar(viva, Fase(new() { Bancas = [NoLugarDe(pcd), NoLugarDe(renda)] }));

        pcd.PosicaoNoTipo.Should().Be(1);
        renda.PosicaoNoTipo.Should().Be(2);
        viva.BancasRequeridas.Should().BeEquivalentTo([pcd, renda]);
    }

    [Fact(DisplayName = "As bancas da fase saem na ordem de leitura: código do tipo e, dentro dele, posição, qualquer que seja a ordem em que chegaram")]
    public void BancasRequeridas_SaemPeloTipoEPelaPosicao()
    {
        FaseCronograma fase = Fase(new()
        {
            Bancas = [Banca(TipoDeBanca.Heteroidentificacao, "PPI"), Banca(TipoDeBanca.AnaliseDocumental, "RENDA"), Banca(TipoDeBanca.AnaliseDocumental, "PCD")],
        });

        fase.BancasRequeridas.Select(static b => b.CandidatosAtendidos.Single().Codigo).Should().Equal("RENDA", "PCD", "PPI");
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
        viva.Declaracao().Recusas.Should().BeEmpty();
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
        viva.Declaracao().Recusas.Should().BeEmpty();
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
        BancaRequerida bancaAntiga = Banca(TipoDeBanca.AnaliseDocumental);
        BancaRequerida bancaNova = Banca(TipoDeBanca.Heteroidentificacao);
        FaseCronograma viva = Fase(new() { Bancas = [bancaAntiga] });

        Atualizar(viva, Fase(new() { Bancas = [bancaNova] }));

        viva.BancasRequeridas.Should().ContainSingle().Which.Should().BeSameAs(bancaNova);
        bancaNova.FaseCronogramaId.Should().Be(viva.Id);
    }

    private static FaseCronograma FaseNovaComPeriodoEmOutroFuso()
    {
        TimeSpan offset = TimeSpan.FromHours(-3);
        return Fase(new()
        {
            Codigo = "FASE_NOVA",
            OrigemData = OrigemDataFase.NoEdital,
            Inicio = InicioDaJanela.ToOffset(offset),
            Fim = InicioDaJanela.AddDays(2).ToOffset(offset),
        });
    }

    [Fact(DisplayName = "O período da fase fica em UTC e o código é copiado")]
    public void AtualizarDe_PeriodoComDeslocamento_FicaEmUtc()
    {
        FaseCronograma viva = Fase(new());

        Atualizar(viva, FaseNovaComPeriodoEmOutroFuso());

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
        EmiteParecerIndividual = true,
        ColetaInscricao = true,
        Produtos = [Preliminar(Resultado)],
        Unidade = UnidadesDeTeste.Cga,
    });

    [Fact(DisplayName = "A fase viva recebe os demais dados da fase nova")]
    public void AtualizarDe_DadosDaFaseNova_SaoCopiadosParaAViva()
    {
        FaseCronograma nova = FaseNovaComDadosDiferentes();
        FaseCronograma viva = Fase(new());

        Atualizar(viva, nova);

        (viva.Ordem, viva.EmiteParecerIndividual, viva.ColetaInscricao)
            .Should().Be((3, true, true));
        viva.FaseCanonicaOrigemId.Should().Be(nova.FaseCanonicaOrigemId);
        viva.UnidadeResponsavel.Should().Be(UnidadesDeTeste.Cga);
    }

    [Fact(DisplayName = "A fase nova sem unidade responsável deixa a fase viva sem unidade")]
    public void AtualizarDe_FaseNovaSemUnidade_LimpaAUnidadeDaViva()
    {
        FaseCronograma viva = Fase(new());

        Atualizar(viva, Fase(new() { Unidade = null }));

        viva.UnidadeResponsavel.Should().BeNull();
    }
}
