namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Domain.UnitTests.PedidoDeConcessaoBuilder;

/// <summary>
/// A designação de Representante do controlador não tem prazo, como a de gestor de acessos: ela só termina pela
/// revogação, que confere a regra do último Representante (UNI-REQ-0200, UNI-REQ-0394). O prazo é recusado na sugestão do
/// pedido, na aprovação do gestor, na segunda etapa do pedido que já trazia o prazo de antes da regra e na própria
/// concessão; o Encarregado de dados continua com prazo opcional.
/// </summary>
public sealed class PrazoDoRepresentanteTests : PedidoDeConcessaoTestBase
{
    private static readonly OrigemDaConcessao Origem = new(TipoDeOrigem.Pedido, Guid.NewGuid());

    [Fact(DisplayName = "Sugerir fim de vigência no pedido de Representante é recusado no campo da sugestão")]
    public void Criar_SugestaoParaRepresentante_Recusa()
    {
        Result<PedidoDeConcessao> resultado = PedidoDeConcessao.Criar(
            new PartesDoPedido(Betina, Leandro.Email), Representante(), EscopoDaConcessao.DaInstituicao(),
            new DeclaracaoDoPedido(Justificativa, Agora.AddMonths(6), true), Dominios, Catalogo, Agora);

        FieldError erro = resultado.Errors.Should().ContainSingle().Which;
        (erro.Field, erro.Error.Code, erro.Error.Message).Should().Be(("validaAteSugerida", PedidoErrorCodes.ValidadeInvalida, MensagensDoPrazo.PerfilSemPrazo));
    }

    [Fact(DisplayName = "O gestor não define fim de vigência ao aprovar o pedido de Representante, e nada é registrado")]
    public void Aprovar_GestorComValidadeParaRepresentante_Recusa()
    {
        PedidoDeConcessao pedido = Pedido().Protegido().AguardandoGestor();

        Result<DesfechoDaAprovacao> resultado = pedido.Aprovar(Diogo.Identidade, DeRepresentante, Contexto, Agora, Agora.AddMonths(3));

        FieldError erro = resultado.Errors.Should().ContainSingle().Which;
        (erro.Field, erro.Error.Code).Should().Be(("validaAte", PedidoErrorCodes.ValidadeInvalida));
        (pedido.Decisoes.Count, pedido.ValidaAte).Should().Be((0, (DateTimeOffset?)null));
    }

    [Fact(DisplayName = "O pedido de Representante com prazo gravado antes da regra é recusado na segunda etapa, sem registrar a decisão")]
    public void Aprovar_SegundaEtapaComPrazoAnteriorARegra_Recusa()
    {
        PedidoDeConcessao pedido = Pedido().Protegido().AguardandoGestor();
        pedido.Aprovar(Diogo.Identidade, DeRepresentante, new ContextoDaAprovacao(HaRepresentanteVigente: true), Agora)
            .Value.Should().Be(DesfechoDaAprovacao.AguardaProtecaoDeDados);
        typeof(PedidoDeConcessao).GetProperty(nameof(PedidoDeConcessao.ValidaAte))!.SetValue(pedido, Agora.AddMonths(3));

        Result<DesfechoDaAprovacao> resultado = pedido.Aprovar(Marlene.Identidade, DeRepresentante, Contexto, Agora.AddHours(1));

        FieldError erro = resultado.Errors.Should().ContainSingle().Which;
        (erro.Field, erro.Error.Code, erro.Error.Message).Should().Be(("validaAte", PedidoErrorCodes.ValidadeInvalida, MensagensDoPrazo.PerfilSemPrazo));
        (pedido.Situacao, pedido.Decisoes.Count, pedido.ValidaAte).Should().Be((SituacaoDoPedido.AguardandoProtecaoDeDados, 1, (DateTimeOffset?)Agora.AddMonths(3)));
    }

    [Fact(DisplayName = "A concessão de Representante com prazo é recusada com a mensagem que nomeia os dois perfis")]
    public void Conceder_RepresentanteComPrazo_Recusa()
    {
        Result<Concessao> resultado = Conceder(Representante(), Agora.AddDays(30));

        FieldError erro = resultado.Errors.Should().ContainSingle().Which;
        (erro.Field, erro.Error.Code, erro.Error.Message).Should().Be(("validaAte", ConcessaoErrorCodes.ValidadeInvalida, MensagensDoPrazo.PerfilSemPrazo));
    }

    [Fact(DisplayName = "A concessão de Representante sem prazo é aceita")]
    public void Conceder_RepresentanteSemPrazo_Aceita() =>
        Conceder(Representante(), null).Value!.ValidaAte.Should().BeNull();

    [Fact(DisplayName = "O Encarregado de dados continua aceitando prazo, na concessão e na aprovação do gestor")]
    public void Encarregado_ComPrazo_Aceita()
    {
        PerfilAvaliado deEncarregado = new(["autorizacao:concessoes:administrar"], "pessoal", true, TipoDeGovernanca.EncarregadoDeDados);
        PedidoDeConcessao pedido = Pedido().Protegido().AguardandoGestor();

        Conceder(PerfilDeGovernanca(TipoDeGovernanca.EncarregadoDeDados), Agora.AddDays(30)).Value!.ValidaAte.Should().Be(Agora.AddDays(30));
        pedido.Aprovar(Diogo.Identidade, deEncarregado, Contexto, Agora, Agora.AddMonths(3)).IsSuccess.Should().BeTrue();
        pedido.ValidaAte.Should().Be(Agora.AddMonths(3));
    }

    private static Result<Concessao> Conceder(Perfil perfil, DateTimeOffset? validaAte) =>
        Concessao.Conceder(
            new TitularDaConcessao(Isabella.Identidade, null), perfil, EscopoDaConcessao.DaInstituicao(), Origem,
            new AtoDoAgente(Diogo.Identidade, Agora), Catalogo, validaAte);

    private static Perfil Representante() => PerfilDeGovernanca(TipoDeGovernanca.RepresentanteDoControlador);
}
