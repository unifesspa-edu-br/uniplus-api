namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using static Unifesspa.UniPlus.Autorizacao.Domain.UnitTests.ApoioDoPedido;

/// <summary>O tipo e o ator de cada evento que o pedido conta na trilha da concessão.</summary>
public sealed class TiposDosEventosDoPedidoTests
{
    private static readonly Dictionary<ResultadoDaDecisao, string> TipoDeCadaResultado = new()
    {
        [ResultadoDaDecisao.Aprovado] = "PEDIDO_APROVADO_NA_ETAPA",
        [ResultadoDaDecisao.Recusado] = "PEDIDO_RECUSADO_NA_ETAPA",
        [ResultadoDaDecisao.PerfilAlterado] = "PERFIL_ALTERADO_ENTRE_ETAPAS",
        [ResultadoDaDecisao.PrazoVencido] = "PRAZO_VENCIDO_ENTRE_ETAPAS",
        [ResultadoDaDecisao.TermoExigido] = "PEDIDO_DEVOLVIDO_POR_TERMO_EXIGIDO",
    };

    public static TheoryData<ResultadoDaDecisao> Resultados => [.. Enum.GetValues<ResultadoDaDecisao>()];

    [Theory(DisplayName = "Cada resultado de decisão do pedido vira o seu tipo de evento, decidido por uma pessoa")]
    [MemberData(nameof(Resultados))]
    public void Decisao_CadaResultado_ViraOSeuTipo(ResultadoDaDecisao resultado)
    {
        PedidoDeConcessao pedido = PedidoComDecisao(resultado);

        EventoDaTrilhaDto evento = EventosDoPedido.Montar(pedido, []).Single(e => e.Tipo.Codigo == TipoDeCadaResultado[resultado]);

        DecisaoDoPedido decisao = pedido.Decisoes.Single(d => d.Resultado == resultado);
        (evento.Ator.Codigo, evento.Autor).Should().Be(("HUMANO", new IdentidadeDto(decisao.DecisorEmissor, decisao.DecisorContaId, decisao.DecisorNome)));
    }

    [Fact(DisplayName = "Cada resultado de decisão do pedido tem um tipo de evento diferente")]
    public void Decisao_TiposDeEvento_SaoDistintos()
    {
        TipoDeCadaResultado.Keys.Should().BeEquivalentTo(Enum.GetValues<ResultadoDaDecisao>());
        TipoDeCadaResultado.Values.Should().OnlyHaveUniqueItems();
    }

    [Fact(DisplayName = "O veredicto do verificador sobre o anexo é do sistema, com o verificador como autor")]
    public void Anexo_VerificadoERecusado_SaoDoSistema()
    {
        PedidoDeConcessao pedido = PedidoComAnexoRecusadoEVerificado();

        EventoDaTrilhaDto[] veredictos = [.. EventosDoPedido.Montar(pedido, []).Where(e => e.Tipo.Codigo is "ANEXO_VERIFICADO" or "ANEXO_RECUSADO")];

        veredictos.Select(static e => (e.Tipo.Codigo, e.Ator.Codigo)).Should().BeEquivalentTo([("ANEXO_RECUSADO", "SISTEMA"), ("ANEXO_VERIFICADO", "SISTEMA")]);
        veredictos.Should().OnlyContain(static e => e.Autor != null && e.Autor.ContaId == "verificador");
    }

    [Fact(DisplayName = "O envio do anexo é do solicitante, uma pessoa")]
    public void Anexo_Enviado_EDaPessoa()
    {
        PedidoDeConcessao pedido = PedidoComAnexoRecusadoEVerificado();

        EventoDaTrilhaDto enviado = EventosDoPedido.Montar(pedido, []).First(static e => e.Tipo.Codigo == "ANEXO_ENVIADO");

        (enviado.Ator.Codigo, enviado.Autor!.ContaId).Should().Be(("HUMANO", Betina.Identidade.ContaId));
    }

    private static PedidoDeConcessao PedidoComDecisao(ResultadoDaDecisao resultado) => resultado switch
    {
        ResultadoDaDecisao.Aprovado => Aprovado(),
        ResultadoDaDecisao.Recusado => Recusado(),
        ResultadoDaDecisao.PerfilAlterado => ComPerfilAlterado(),
        ResultadoDaDecisao.PrazoVencido => ComPrazoVencido(),
        ResultadoDaDecisao.TermoExigido => ComTermoExigido(),
        _ => throw new ArgumentOutOfRangeException(nameof(resultado), resultado, null),
    };

    private static PedidoDeConcessao Aprovado()
    {
        PedidoDeConcessao pedido = PedidoDeConcessaoBuilder.Pedido().AguardandoGestor();
        pedido.Aprovar(Diogo.Identidade, Interno, Contexto, Agora);

        return pedido;
    }

    private static PedidoDeConcessao Recusado()
    {
        PedidoDeConcessao pedido = PedidoDeConcessaoBuilder.Pedido().AguardandoGestor();
        pedido.Recusar(Diogo.Identidade, "O pedido não se justifica.", Interno, Agora);

        return pedido;
    }

    private static PedidoDeConcessao ComPerfilAlterado()
    {
        PedidoDeConcessao pedido = PedidoDeConcessaoBuilder.Pedido().Protegido().AguardandoGestor();
        pedido.Aprovar(Diogo.Identidade, Sensivel, Contexto, Agora, Agora.AddMonths(3));
        pedido.Aprovar(Marlene.Identidade, new(["selecao:atendimento:analisar"], "SENSIVEL", true, null), Contexto, Agora.AddHours(1));

        return pedido;
    }

    private static PedidoDeConcessao ComPrazoVencido()
    {
        PedidoDeConcessao pedido = PedidoDeConcessaoBuilder.Pedido().Protegido().AguardandoGestor();
        pedido.Aprovar(Diogo.Identidade, Sensivel, Contexto, Agora, Agora.AddDays(1));
        pedido.Aprovar(Marlene.Identidade, Sensivel, Contexto, Agora.AddDays(2));

        return pedido;
    }

    private static PedidoDeConcessao ComTermoExigido()
    {
        PedidoDeConcessao pedido = PedidoDeConcessaoBuilder.Pedido().AguardandoGestor();
        pedido.Aprovar(Diogo.Identidade, Sensivel, Contexto, Agora);

        return pedido;
    }

    private static PedidoDeConcessao PedidoComAnexoRecusadoEVerificado()
    {
        PedidoDeConcessao pedido = PedidoDeConcessaoBuilder.Pedido().DeclarandoDocumento().Protegido().Criado();
        AnexoDoPedido recusado = pedido.NovoAnexo(1024).Value!;
        pedido.ConfirmarAnexo(recusado.Id, "\"etag\"", Agora);
        recusado.RegistrarRecusado(Agora.AddMinutes(1));
        AnexoDoPedido verificado = pedido.NovoAnexo(1024).Value!;
        pedido.ConfirmarAnexo(verificado.Id, "\"etag\"", Agora.AddMinutes(2));
        verificado.RegistrarVerificado(new string('a', 64), Agora.AddMinutes(3));

        return pedido;
    }
}
