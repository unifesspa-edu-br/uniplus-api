namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using TextoDoPedido = Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos.TextoDoAviso;

public sealed class RedacaoDosAvisosTests
{
    private const string Perfil = "Análise de atendimento";
    private const string Representante = "Representante do controlador";
    private const string Link = "https://uniplus.test/acessos/perfis/1/janela";

    public static TheoryData<TipoDeAviso> Tipos => [.. Enum.GetValues<TipoDeAviso>()];

    private static ModeloDeAviso Modelo(TipoDeAviso tipo, ContagensDoAviso? contagens = null, FormaDoAviso? forma = null) =>
        new(tipo, Perfil, 3, ["Operar inscrições"], Link, contagens, new DateTimeOffset(2026, 11, 12, 3, 0, 0, TimeSpan.Zero), Forma: forma, HorasRestantes: 24);

    [Theory(DisplayName = "Todo tipo de aviso tem assunto, abertura e finalidade")]
    [MemberData(nameof(Tipos))]
    public void TodoTipo_TemTexto(TipoDeAviso tipo)
    {
        RedacaoDoAviso.Assunto(tipo).Should().NotBeNullOrWhiteSpace();
        RedacaoDoAviso.CorpoEmTexto(Modelo(tipo)).Should().Contain(Perfil).And.Contain(Link);
        tipo.Finalidade().Should().StartWith("perfil.");
    }

    [Fact(DisplayName = "O prazo vai em horário de Brasília")]
    public void Prazo_EmHorarioDeBrasilia()
    {
        string corpo = RedacaoDoAviso.CorpoEmTexto(Modelo(TipoDeAviso.JanelaDeMigracaoFaltam24Horas));

        corpo.Should().Contain("Prazo: 12/11/2026 00:00 (Brasília)").And.Contain("Horas restantes: 24");
    }

    [Fact(DisplayName = "O aviso de lote com recusas traz as quantidades e não traz beneficiário, CPF nem motivo")]
    public void LoteComRecusas_SemDadoPessoal()
    {
        ModeloDeAviso modelo = Modelo(TipoDeAviso.LoteEnviadoComRecusas, new ContagensDoAviso(Revogadas: 2));

        foreach (string corpo in new[] { RedacaoDoAviso.CorpoEmTexto(modelo), RedacaoDoAviso.CorpoEmHtml(modelo), RedacaoDoAviso.Assunto(modelo.Tipo) })
        {
            corpo.Should().NotContain("CPF").And.NotContain("@");
        }

        RedacaoDoAviso.CorpoEmTexto(modelo).Should().Contain("Revogadas: 2");
    }

    [Theory(DisplayName = "A abertura do aviso de revogação depende de como a concessão perdeu o acesso")]
    [InlineData(FormaDoAviso.Recusa, "recusada")]
    [InlineData(FormaDoAviso.FimDoPrazo, "prazo")]
    [InlineData(FormaDoAviso.EncerramentoManual, "encerrou")]
    public void Revogacao_PelaForma(FormaDoAviso forma, string trecho)
    {
        string corpo = RedacaoDoAviso.CorpoEmTexto(Modelo(TipoDeAviso.ConcessaoRevogada, forma: forma));

        corpo.Should().Contain(trecho);
    }

    [Fact(DisplayName = "Os avisos da etapa protegida nomeiam o Representante do controlador, no masculino genérico")]
    public void EtapaProtegida_NomeiaORepresentante()
    {
        ModeloDeAviso migracoes = Modelo(TipoDeAviso.MigracoesAguardandoProtecaoDeDados, new ContagensDoAviso(Aguardando: 2));
        ModeloDeAviso apta = Modelo(TipoDeAviso.VersaoPendenteApta) with { Etapa = EtapaDaVersao.ProtecaoDeDados };
        TextoDoPedido pedido = TextosDoAvisoDoPedido.Da(SituacaoDoPedido.AguardandoProtecaoDeDados);

        RedacaoDoAviso.Assunto(migracoes.Tipo).Should().Contain(Representante);
        RedacaoDoAviso.CorpoEmTexto(migracoes).Should().Contain($"Aguardando o {Representante}: 2");
        RedacaoDoAviso.CorpoEmTexto(apta).Should().Contain($"Etapa: {Representante}");
        new[] { pedido.Assunto, pedido.Abertura, pedido.Situacao }.Should().OnlyContain(t => t.Contains(Representante, StringComparison.Ordinal));
        pedido.Finalidade.Should().Be("pedido.fila_da_protecao_de_dados", "o código da finalidade não muda com o texto");
    }
}
