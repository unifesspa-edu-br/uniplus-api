namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.ClassificacaoDoDocumentoDeTeste;

public sealed class LeituraDasRegrasDeEliminacaoTests
{
    private static RegraDeEliminacaoNoDocumento NotaMinima(int posicao, string? etapa = EtapaAdotada) =>
        new(posicao, Referencia(RegraDeEliminacaoPorCalculo.NotaMinimaNaEtapa), etapa, null, 5m, null);

    private static ItensLidosDaClassificacao<Domain.Entities.RegraEliminacao> Ler(params RegraDeEliminacaoNoDocumento[] regras) =>
        LeituraDasRegrasDeEliminacao.Ler(regras, Etapas(), Cadastros());

    [Fact(DisplayName = "A regra que o catálogo não tem fica fora, sem repetir o código digitado, e as outras seguem adotadas")]
    public void RegraForaDoCatalogo_FicaForaComARecusaNoPonteiroDaRegra()
    {
        RegraDeEliminacaoNoDocumento desconhecida = new(1, new(RegraDigitada, "v1"), null, null, null, null);

        ItensLidosDaClassificacao<Domain.Entities.RegraEliminacao> lidas = Ler(NotaMinima(0), desconhecida);

        lidas.Itens.Select(static item => item.PosicaoNoDocumento).Should().Equal(0);
        Causas(lidas.Recusadas).Should().Equal(("/classificacao/regrasEliminacao/1/regra/codigo", "RegraEliminacao.RegraNaoEncontrada"));
        lidas.Recusadas.Single().Causa.Error.Message.Should().NotContain(RegraDigitada);
        lidas.Recusadas.Single().Caminho.Should().Be("classificacao.regrasEliminacao");
    }

    [Fact(DisplayName = "A regra do catálogo que não é de eliminação é recusada como tipo inválido")]
    public void RegraDeOutroTipo_RecusaComoTipoInvalido()
    {
        RegraDeEliminacaoNoDocumento deCalculo = new(0, Referencia(RegraDeCalculoDaNota.MediaPonderada), null, null, null, null);

        Causas(Ler(deCalculo).Recusadas).Should().Equal(("/classificacao/regrasEliminacao/0/regra/codigo", "RegraEliminacao.RegraTipoInvalido"));
    }

    [Fact(DisplayName = "A etapa citada que a leitura adotou vira o id em memória dela nos parâmetros da regra")]
    public void EtapaAdotada_ViraOIdEmMemoria()
    {
        EtapasLidas etapas = Etapas();

        ItensLidosDaClassificacao<Domain.Entities.RegraEliminacao> lidas =
            LeituraDasRegrasDeEliminacao.Ler([NotaMinima(0)], etapas, Cadastros());

        lidas.Recusadas.Should().BeEmpty();
        lidas.Itens.Single().Item.Parametros.EtapaReferenciada().Should().Be(etapas.IdPelaChave[EtapaAdotada]);
    }

    [Fact(DisplayName = "A etapa que o documento não tem recusa a regra sem repetir a chave digitada")]
    public void EtapaInexistente_RecusaSemOTextoEnviado()
    {
        ItensLidosDaClassificacao<Domain.Entities.RegraEliminacao> lidas = Ler(NotaMinima(0), NotaMinima(1, ChaveDigitada));

        lidas.Itens.Should().ContainSingle();
        Causas(lidas.Recusadas).Should().Equal(("/classificacao/regrasEliminacao/1/parametros/etapaChave", "ProcessoSeletivo.EtapaRefEliminacaoInexistente"));
        lidas.Recusadas.Single().Causa.Error.Message.Should().NotContain(ChaveDigitada).And.Contain("posição 2");
    }

    [Fact(DisplayName = "A regra que cita etapa que a leitura recusou fica fora sem causa nova")]
    public void EtapaRecusadaNaLeitura_FicaForaSemCausa()
    {
        ItensLidosDaClassificacao<Domain.Entities.RegraEliminacao> lidas = Ler(NotaMinima(0, EtapaRecusada));

        lidas.Itens.Should().BeEmpty();
        lidas.Recusadas.Should().BeEmpty();
    }

    [Theory(DisplayName = "Os parâmetros que o código da regra não aceita, ou que faltam, recusam a regra no item")]
    [InlineData("NotaMinimaSemEtapa", "RegraEliminacao.EtapaRefENotaMinimaObrigatorios")]
    [InlineData("ZeroComNotaMinima", "RegraEliminacao.ParametrosIncompativeisComRegra")]
    [InlineData("CorteSemMinimo", "RegraEliminacao.AreaEMinimoObrigatorios")]
    [InlineData("AusenciaSemEtapa", "RegraEliminacao.EtapaRefObrigatorio")]
    [InlineData("CorteNegativo", "RegraEliminacao.MinimoInvalido")]
    public void Parametros_ForaDoQueARegraAceita_Recusa(string caso, string codigo)
    {
        RegraDeEliminacaoNoDocumento regra = caso switch
        {
            "NotaMinimaSemEtapa" => NotaMinima(0, etapa: null),
            "ZeroComNotaMinima" => new(0, Referencia(RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem), null, null, 5m, null),
            "CorteSemMinimo" => new(0, Referencia(RegraDeEliminacaoPorCalculo.CorteEmAreaDoEnem), null, "REDACAO", null, null),
            "AusenciaSemEtapa" => new(0, Referencia(RegraDeEliminacaoPorCalculo.AusenciaNaEtapa), null, null, null, null),
            _ => new(0, Referencia(RegraDeEliminacaoPorCalculo.CorteEmAreaDoEnem), null, "REDACAO", null, -1m),
        };

        Causas(Ler(regra).Recusadas).Should().Equal(("/classificacao/regrasEliminacao/0", codigo));
    }

    [Fact(DisplayName = "O corte em área e a falta em dia de prova do ENEM são adotados com os parâmetros do próprio código")]
    public void CorteEFalta_SaoAdotados()
    {
        ItensLidosDaClassificacao<Domain.Entities.RegraEliminacao> lidas = Ler(
            new(0, Referencia(RegraDeEliminacaoPorCalculo.CorteEmAreaDoEnem), null, "REDACAO", null, 400m),
            new(1, Referencia(RegraDeEliminacaoPorCalculo.FaltaEmDiaDeProvaDoEnem), null, null, null, null),
            new(2, Referencia(RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem), null, null, null, null));

        lidas.Recusadas.Should().BeEmpty();
        lidas.Itens.Select(static item => item.Item.Parametros.RegraCorrespondente()).Should().Equal(
            RegraDeEliminacaoPorCalculo.CorteEmAreaDoEnem, RegraDeEliminacaoPorCalculo.FaltaEmDiaDeProvaDoEnem, RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem);
    }

    [Fact(DisplayName = "A origem da lista guarda a posição em que cada regra adotada veio, sem a recusada")]
    public void Origem_GuardaAPosicaoDeCadaRegraAdotada()
    {
        ItensLidosDaClassificacao<Domain.Entities.RegraEliminacao> lidas = Ler(NotaMinima(0, EtapaRecusada), NotaMinima(1), NotaMinima(2));

        OrigemNoDocumento origem = lidas.Origem(LeituraDasRegrasDeEliminacao.Lista);

        origem.PosicaoDeOrigem(LeituraDasRegrasDeEliminacao.Lista, 0).Should().Be(1);
        origem.PosicaoDeOrigem(LeituraDasRegrasDeEliminacao.Lista, 1).Should().Be(2);
    }
}
