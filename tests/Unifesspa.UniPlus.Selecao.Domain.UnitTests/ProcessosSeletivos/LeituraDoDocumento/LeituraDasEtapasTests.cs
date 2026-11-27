namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.DocumentoLidoDeTeste;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.EtapasLidasDeTeste;

/// <summary>
/// A etapa entra com o tipo do cadastro e a ordem igual à posição mais 1; a que o domínio recusa sai da lista e conserva a posição de
/// origem, que as causas das etapas seguintes apontam, e as recusas de todas as etapas se acumulam.
/// </summary>
public sealed class LeituraDasEtapasTests
{
    [Fact(DisplayName = "A etapa adotada tem a ordem igual à posição no documento mais 1, o tipo do cadastro e a fase traduzida")]
    public void Etapa_Adotada_TemOrdemTipoEFase()
    {
        EtapasLidas lidas = Ler([Etapa(0, "etapa_a", fase: FaseDaAvaliacao), Etapa(1, "etapa_b")], Fase(0, FaseDaAvaliacao, "INSCRICAO"));

        lidas.Etapas.Select(static etapa => (etapa.Chave.Valor, etapa.Ordem)).Should().Equal(("etapa_a", 1), ("etapa_b", 2));
        lidas.Etapas[0].TipoEtapa.Codigo.Should().Be(TipoDeEtapa);
        lidas.Etapas[0].FaseCodigo.Should().Be("INSCRICAO");
        lidas.Etapas[1].FaseCodigo.Should().BeNull();
        lidas.ChavesRecusadas.Should().BeEmpty();
        PartesRecusadas(lidas).Should().BeEmpty();
    }

    [Fact(DisplayName = "O identificador em memória de cada etapa adotada se acha pela chave que o documento declarou")]
    public void Etapas_Adotadas_TemIdentificadorPelaChave()
    {
        EtapasLidas lidas = Ler(Etapa(0, "etapa_a"), Etapa(1, "etapa_b"));

        lidas.IdPelaChave.Keys.Should().BeEquivalentTo("etapa_a", "etapa_b");
        lidas.Etapas.Should().OnlyContain(etapa => lidas.IdPelaChave.Values.Contains(etapa.Id));
    }

    [Fact(DisplayName = "O tipo de etapa inexistente recusa a etapa inteira no campo do tipo, sem o texto digitado, e ela sai da lista")]
    public void TipoDeEtapa_Inexistente_RecusaAEtapaInteira()
    {
        EtapasLidas lidas = Ler(Etapa(0, "etapa_a"), Etapa(1, "etapa_b", tipo: "TIPO-DIGITADO"), Etapa(2, "etapa_c"));

        lidas.Etapas.Select(static etapa => etapa.Chave.Valor).Should().Equal("etapa_a", "etapa_c");
        lidas.ChavesRecusadas.Should().BeEquivalentTo("etapa_b");
        lidas.IdPelaChave.Keys.Should().NotContain("etapa_b");
        PartesRecusadas(lidas).Should().Equal(("/etapas/1/tipoEtapaCodigo", ProcessoSeletivoErrorCodes.TipoEtapaNaoEncontradoOuInativo));
        Mensagens(lidas).Should().NotContain(mensagem => mensagem.Contains("DIGITADO", StringComparison.Ordinal));
        Caminhos(lidas).Should().Equal("etapas");
    }

    [Fact(DisplayName = "A etapa sem código de tipo é recusada no campo do tipo, como a de tipo inexistente")]
    public void TipoDeEtapa_Ausente_RecusaAEtapaInteira()
    {
        EtapasLidas lidas = Ler(Etapa(0, "etapa_a", tipo: null));

        lidas.Etapas.Should().BeEmpty();
        PartesRecusadas(lidas).Should().Equal(("/etapas/0/tipoEtapaCodigo", ProcessoSeletivoErrorCodes.TipoEtapaNaoEncontradoOuInativo));
    }

    [Fact(DisplayName = "A etapa recusada conserva a posição de origem: a causa da etapa seguinte aponta a posição enviada, e a ordem dela tem a lacuna")]
    public void EtapaRecusada_ConservaAPosicaoDeOrigemDasSeguintes()
    {
        EtapaNoDocumento seguinte = Etapa(2, "etapa_c").ComBancas(BancaDaEtapa(0, "banca_a", "TIPO-DIGITADO"));

        EtapasLidas lidas = Ler(Etapa(0, "etapa_a", tipo: "TIPO-DIGITADO"), Etapa(1, "etapa_b"), seguinte);

        lidas.Etapas.Select(static etapa => etapa.Ordem).Should().Equal(2, 3);
        PartesRecusadas(lidas).Should().Equal(
            ("/etapas/0/tipoEtapaCodigo", ProcessoSeletivoErrorCodes.TipoEtapaNaoEncontradoOuInativo),
            ("/etapas/2/bancas/0/tipoBancaCodigo", EtapaProcessoErrorCodes.TipoBancaNaoEncontradoOuInativo));
    }

    [Fact(DisplayName = "A etapa recusada acumula também as recusas das outras partes dela, na ordem tipo, fase e bancas")]
    public void EtapaRecusada_AcumulaAsRecusasDasOutrasPartes()
    {
        EtapaNoDocumento etapa = Etapa(0, "etapa_a", tipo: "TIPO-DIGITADO", fase: "chave_digitada").ComBancas(BancaDaEtapa(0, "banca_a", "TIPO-DIGITADO"));

        EtapasLidas lidas = Ler(etapa);

        PartesRecusadas(lidas).Should().Equal(
            ("/etapas/0/tipoEtapaCodigo", ProcessoSeletivoErrorCodes.TipoEtapaNaoEncontradoOuInativo),
            ("/etapas/0/faseChave", "ProcessoSeletivo.EtapaSemFaseNoCronograma"),
            ("/etapas/0/bancas/0/tipoBancaCodigo", EtapaProcessoErrorCodes.TipoBancaNaoEncontradoOuInativo));
    }

    [Fact(DisplayName = "A fase que a etapa cita e o documento não tem recusa o campo da fase, e a etapa fica adotada sem fase")]
    public void FaseInexistente_RecusaOCampoDaFaseEMantemAEtapa()
    {
        EtapasLidas lidas = Ler(Etapa(0, "etapa_a", fase: "chave_digitada"));

        lidas.Etapas.Should().ContainSingle().Which.FaseCodigo.Should().BeNull();
        PartesRecusadas(lidas).Should().Equal(("/etapas/0/faseChave", "ProcessoSeletivo.EtapaSemFaseNoCronograma"));
        Caminhos(lidas).Should().Equal("etapas[0].faseCodigo");
    }

    [Fact(DisplayName = "A fase que a leitura recusou não gera causa nova na etapa que a cita")]
    public void FaseRecusadaPelaLeitura_NaoGeraCausaNova()
    {
        EtapasLidas lidas = Ler([Etapa(0, "etapa_a", fase: FaseDaAvaliacao)], Fase(0, FaseDaAvaliacao, "FASE-DIGITADA"));

        lidas.Etapas.Should().ContainSingle().Which.FaseCodigo.Should().BeNull();
        PartesRecusadas(lidas).Should().BeEmpty();
    }

    [Fact(DisplayName = "As recusas de duas etapas se acumulam na ordem em que o documento as traz")]
    public void Recusas_DeDuasEtapas_AcumulamNaOrdemDoDocumento()
    {
        EtapaNoDocumento primeira = Etapa(0, "etapa_a").ComBancas(BancaDaEtapa(0, "banca_a", "TIPO-DIGITADO"));
        EtapaNoDocumento segunda = Etapa(1, "etapa_b").Publicando(new ProdutoNoDocumento(0, "ATO-DIGITADO", null));

        EtapasLidas lidas = Ler(primeira, segunda);

        PartesRecusadas(lidas).Select(static parte => parte.Ponteiro).Should().Equal(
            "/etapas/0/bancas/0/tipoBancaCodigo", "/etapas/1/publicacoesPrevistas/0/atoCodigo");
        lidas.Recusas.Select(static recusa => recusa.PosicaoNoDocumento).Should().Equal(0, 0);
    }

    [Fact(DisplayName = "A janela própria e o parecer individual da etapa passam para a etapa adotada")]
    public void Etapa_Adotada_TemJanelaEParecer()
    {
        DateTimeOffset inicio = new(2026, 12, 1, 9, 0, 0, TimeSpan.Zero);
        EtapaNoDocumento etapa = Etapa(0, "etapa_a") with { Janela = new(inicio, inicio.AddDays(1)), EmiteParecerIndividual = true, TiposDeDecisao = ["NOTA_DA_ETAPA"] };

        EtapaProcesso adotada = Ler(etapa).Etapas.Single();

        adotada.Inicio.Should().Be(inicio);
        adotada.Fim.Should().Be(inicio.AddDays(1));
        adotada.EmiteParecerIndividual.Should().BeTrue();
        adotada.TiposDeDecisao.Should().ContainSingle();
    }

    [Fact(DisplayName = "As origens trazem a posição de cada etapa adotada pela ordem canônica e as partes recusadas")]
    public void Origens_LigamAPosicaoCanonicaAPosicaoEnviada()
    {
        EtapasLidas lidas = Ler(Etapa(0, "etapa_a", tipo: "TIPO-DIGITADO"), Etapa(1, "etapa_b"));

        Origem(lidas).PosicaoDeOrigem("etapas", 0).Should().Be(1);
        Origem(lidas).TemParteRecusadaEm("etapas").Should().BeTrue();
    }
}
