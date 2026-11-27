namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.VagasDoDocumentoDeTeste;

public sealed class LeituraDasDistribuicoesDeVagasTests
{
    private static DistribuicoesLidas Ler(params DistribuicaoNoDocumento[] distribuicoes) =>
        LeituraDasDistribuicoesDeVagas.Ler(distribuicoes, Cadastros());

    [Fact(DisplayName = "A distribuição conforme vira a configuração da oferta, com as modalidades e o quadro que a declaração dá")]
    public void DistribuicaoConforme_ViraAConfiguracaoDaOferta()
    {
        DistribuicoesLidas lidas = Ler(Distribuicao(0));

        lidas.Recusadas.Should().BeEmpty();
        Domain.Entities.ConfiguracaoDistribuicaoVagas distribuicao = lidas.Adotadas.Single().Distribuicao!;
        distribuicao.Modalidades.Select(static modalidade => modalidade.Codigo).Should().Equal(Ampla, AmplaComDeficiencia);
        distribuicao.TotalDeVagasDoEdital.Should().Be(10);
    }

    [Fact(DisplayName = "A origem guarda a posição enviada de cada distribuição adotada e de cada uma das modalidades dela")]
    public void Origem_GuardaAPosicaoEnviada()
    {
        DistribuicaoNoDocumento comModalidadesFora = Distribuicao(4) with { Modalidades = [new(2, Ampla, 6), new(7, AmplaComDeficiencia, 4)] };

        Domain.ProcessosSeletivos.FasesEEtapas.OrigemNoDocumento origem = Ler(Distribuicao(1, TextoDigitado), comModalidadesFora).Origem();

        origem.PosicaoDeOrigem(LeituraDasDistribuicoesDeVagas.Lista, 0).Should().Be(4);
        origem.PosicaoDeOrigem($"{LeituraDasDistribuicoesDeVagas.Lista}[0].modalidades", 1).Should().Be(7);
    }

    [Fact(DisplayName = "A oferta que o cadastro não tem recusa a distribuição no campo da oferta, na posição enviada e sem repetir o texto")]
    public void OfertaInexistente_RecusaNoCampoDaOferta()
    {
        DistribuicoesLidas lidas = Ler(Distribuicao(0), Distribuicao(3, TextoDigitado));

        lidas.Adotadas.Select(static adotada => adotada.PosicaoNoDocumento).Should().Equal(0);
        Causas(lidas.Recusadas).Should().Equal(("/distribuicaoVagas/3/ofertaCursoCodigo", "ConfiguracaoDistribuicaoVagas.OfertaCursoNaoEncontrada"));
        lidas.Recusadas.Single().Causa.Error.Message.Should().NotContain(TextoDigitado);
        lidas.Recusadas.Single().Caminho.Should().Be(LeituraDasDistribuicoesDeVagas.Lista);
        lidas.Recusadas.Single().PosicaoDeOrigem.Should().Be(3);
    }

    [Theory(DisplayName = "A regra de distribuição que o catálogo não tem, ou que é de outro tipo, recusa no campo da regra")]
    [InlineData(false, "ConfiguracaoDistribuicaoVagas.RegraDistribuicaoNaoEncontrada")]
    [InlineData(true, "ConfiguracaoDistribuicaoVagas.RegraDistribuicaoTipoInvalido")]
    public void RegraDeDistribuicao_ForaDoCatalogoOuDeOutroTipo_Recusa(bool deOutroTipo, string codigo)
    {
        DistribuicaoNoDocumento distribuicao = Distribuicao(0) with { RegraDeDistribuicao = deOutroTipo ? Remanejamento : new(TextoDigitado, "v1") };

        DistribuicoesLidas lidas = Ler(distribuicao);

        Causas(lidas.Recusadas).Should().Equal(("/distribuicaoVagas/0/regraDistribuicao/codigo", codigo));
        lidas.Recusadas.Single().Causa.Error.Message.Should().NotContain(TextoDigitado);
    }

    [Fact(DisplayName = "A regra de ajuste e a referência demográfica que o cadastro não tem recusam cada uma no campo do código dela")]
    public void AjusteEReferencia_Inexistentes_RecusamNoCampoDoCodigo()
    {
        DistribuicaoNoDocumento distribuicao = Distribuicao(2) with { RegraDeAjuste = new(TextoDigitado, "v1"), ReferenciaDemograficaCodigo = TextoDigitado };

        Causas(Ler(distribuicao).Recusadas).Should().Equal(
            ("/distribuicaoVagas/2/regraAjuste/codigo", "ConfiguracaoDistribuicaoVagas.RegraAjusteNaoEncontrada"),
            ("/distribuicaoVagas/2/referenciaDemograficaCodigo", "ConfiguracaoDistribuicaoVagas.ReferenciaDemograficaNaoEncontrada"));
    }

    [Fact(DisplayName = "A modalidade que o cadastro não tem recusa no código dela, na posição enviada da modalidade")]
    public void ModalidadeInexistente_RecusaNaPosicaoEnviadaDaModalidade()
    {
        DistribuicaoNoDocumento distribuicao = Distribuicao(0) with { Modalidades = [new(2, Ampla, 6), new(5, TextoDigitado, 4)] };

        DistribuicoesLidas lidas = Ler(distribuicao);

        Causas(lidas.Recusadas).Should().Equal(("/distribuicaoVagas/0/modalidades/5/codigo", "ConfiguracaoDistribuicaoVagas.ModalidadeNaoEncontrada"));
        lidas.Recusadas.Single().Causa.Error.Message.Should().NotContain(TextoDigitado);
    }

    [Fact(DisplayName = "As recusas de cadastro e da declaração de uma distribuição voltam juntas, cadastro primeiro")]
    public void Recusas_Acumulam_ComOCadastroPrimeiro()
    {
        DistribuicaoNoDocumento distribuicao = Distribuicao(1, TextoDigitado) with { VagasOfertadasBase = 0, Modalidades = [new(0, TextoDigitado, 6)] };

        Causas(Ler(distribuicao).Recusadas).Should().Equal(
            ("/distribuicaoVagas/1/ofertaCursoCodigo", "ConfiguracaoDistribuicaoVagas.OfertaCursoNaoEncontrada"),
            ("/distribuicaoVagas/1/modalidades/0/codigo", "ConfiguracaoDistribuicaoVagas.ModalidadeNaoEncontrada"),
            ("/distribuicaoVagas/1/vagasOfertadasBase", "ConfiguracaoDistribuicaoVagas.VagasOfertadasBaseInvalido"));
    }

    [Fact(DisplayName = "O dado que o cadastro recusou não gera a causa da regra que o lê")]
    public void RegraRecusada_NaoGeraCausaDeRamo()
    {
        DistribuicaoNoDocumento distribuicao = Distribuicao(0) with { RegraDeDistribuicao = new(TextoDigitado, "v1") };

        Causas(Ler(distribuicao).Recusadas).Should().Equal(("/distribuicaoVagas/0/regraDistribuicao/codigo", "ConfiguracaoDistribuicaoVagas.RegraDistribuicaoNaoEncontrada"));
    }

    [Fact(DisplayName = "A quantidade negativa aponta a posição enviada da modalidade e não a da lista que a declaração entrega")]
    public void QuantidadeNegativa_ApontaAPosicaoEnviadaDaModalidade()
    {
        DistribuicaoNoDocumento distribuicao = Distribuicao(0) with { Modalidades = [new(2, Ampla, 6), new(7, AmplaComDeficiencia, -4)] };

        Causas(Ler(distribuicao).Recusadas).Should().Contain(("/distribuicaoVagas/0/modalidades/7/quantidadeDeclarada", "ConfiguracaoDistribuicaoVagas.QuantidadeVagaNegativa"));
    }

    [Fact(DisplayName = "O quadro que não completa as vagas ofertadas recusa a distribuição inteira e a outra oferta segue adotada")]
    public void QuadroQueNaoCompleta_RecusaSoAquelaOferta()
    {
        DistribuicaoNoDocumento incompleta = Distribuicao(1, OfertaDeMedicina) with { VagasOfertadasBase = 20 };

        DistribuicoesLidas lidas = Ler(Distribuicao(0), incompleta);

        lidas.Adotadas.Should().ContainSingle();
        Causas(lidas.Recusadas).Should().Equal(("/distribuicaoVagas/1", "ConfiguracaoDistribuicaoVagas.QuadroNaoCompletaAsVagasOfertadasBase"));
    }

    [Fact(DisplayName = "O ajuste do quadro de tipo desconhecido recusa no campo do ajuste, sem montar o quadro")]
    public void AjusteDeTipoDesconhecido_RecusaNoCampoDoAjuste()
    {
        DistribuicaoNoDocumento distribuicao = Distribuicao(0) with { RegraDeAjuste = Ajuste, AjusteDoQuadro = new(TextoDigitado, null, []) };

        DistribuicoesLidas lidas = Ler(distribuicao);

        Causas(lidas.Recusadas).Should().Equal(("/distribuicaoVagas/0/ajusteDoQuadro", "ConfiguracaoDistribuicaoVagas.RegraAjusteTipoInvalido"));
        lidas.Recusadas.Single().Causa.Error.Message.Should().NotContain(TextoDigitado);
    }

    [Fact(DisplayName = "A lista de distribuições vazia não tem o que ler nem recusar")]
    public void ListaVazia_NaoTemRecusa()
    {
        DistribuicoesLidas lidas = Ler();

        lidas.Adotadas.Should().BeEmpty();
        lidas.Recusadas.Should().BeEmpty();
    }
}
