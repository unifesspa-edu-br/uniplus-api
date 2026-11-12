namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>As etapas de aprovação do pedido: o gestor, a proteção de dados, o prazo e quem pode decidir.</summary>
public sealed class PedidoDeConcessaoAprovacaoTests : PedidoDeConcessaoTestBase
{
    [Fact(DisplayName = "Pedido de perfil só interno vale com a aprovação do gestor, que conclui e grava o retrato avaliado")]
    public void Aprovar_PerfilInterno_ConcluiComOGestor()
    {
        PedidoDeConcessao pedido = Criar(Betina, Leandro.Email);
        pedido.Aceitar(Leandro, Agora);

        Result<DesfechoDaAprovacao> resultado = pedido.Aprovar(Diogo.Identidade, Interno, Contexto, Agora.AddHours(1));

        resultado.Value.Should().Be(DesfechoDaAprovacao.Concluido);
        pedido.Situacao.Should().Be(SituacaoDoPedido.Aprovado);
        pedido.EncerradoEm.Should().Be(Agora.AddHours(1));
        DecisaoDoPedido decisao = pedido.Decisoes.Single();
        decisao.Etapa.Should().Be(EtapaDoPedido.Gestor);
        decisao.Resultado.Should().Be(ResultadoDaDecisao.Aprovado);
        decisao.PermissoesAvaliadas.Should().Equal("selecao:processos:consultar");
        decisao.SensibilidadeAvaliada.Should().Be("interno");
        decisao.Dispensa.Should().BeNull();
    }

    [Fact(DisplayName = "Pedido de perfil sensível só vale depois da proteção de dados, sempre depois do gestor")]
    public void Aprovar_PerfilSensivel_PassaPelasDuasEtapas()
    {
        PedidoDeConcessao pedido = PedidoAguardandoGestor();

        Result<DesfechoDaAprovacao> gestor = pedido.Aprovar(Diogo.Identidade, Sensivel, Contexto, Agora);

        gestor.Value.Should().Be(DesfechoDaAprovacao.AguardaProtecaoDeDados);
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoProtecaoDeDados);
        pedido.EncerradoEm.Should().BeNull();
        pedido.EtapaAtual.Should().Be(EtapaDoPedido.ProtecaoDeDados);

        Result<DesfechoDaAprovacao> protecao = pedido.Aprovar(Marlene.Identidade, Sensivel, Contexto, Agora.AddHours(1));

        protecao.Value.Should().Be(DesfechoDaAprovacao.Concluido);
        pedido.Situacao.Should().Be(SituacaoDoPedido.Aprovado);
        pedido.Decisoes.Select(d => d.Etapa).Should().Equal(EtapaDoPedido.Gestor, EtapaDoPedido.ProtecaoDeDados);
    }

    [Fact(DisplayName = "A sugestão de fim de vigência do solicitante fica gravada no pedido de perfil comum")]
    public void Criar_ComSugestaoDeValidade_Grava()
    {
        Result<PedidoDeConcessao> resultado = CriarComSugestao(PerfilComum(), Agora.AddMonths(6));

        resultado.Value!.ValidaAteSugerida.Should().Be(Agora.AddMonths(6));
        resultado.Value.ValidaAte.Should().BeNull("quem define o fim da vigência é o gestor");
    }

    [Fact(DisplayName = "Sugerir fim de vigência para perfil de governança é recusado")]
    public void Criar_ComSugestaoParaGovernanca_Recusa() =>
        CriarComSugestao(PerfilDeGovernanca(), Agora.AddMonths(6)).Errors.Should().ContainSingle()
            .Which.Should().Match<FieldError>(e => e.Field == "validaAteSugerida" && e.Error.Code == PedidoErrorCodes.ValidadeInvalida);

    [Fact(DisplayName = "Sugerir fim de vigência que não é posterior ao instante é recusado")]
    public void Criar_ComSugestaoNoPassado_Recusa() =>
        CriarComSugestao(PerfilComum(), Agora).Errors.Should().ContainSingle()
            .Which.Should().Match<FieldError>(e => e.Field == "validaAteSugerida" && e.Error.Code == PedidoErrorCodes.ValidadeInvalida);

    [Fact(DisplayName = "O gestor define o fim de vigência ao aprovar, e a proteção de dados conclui sem alterá-lo")]
    public void Aprovar_GestorComValidade_Grava()
    {
        PedidoDeConcessao pedido = PedidoAguardandoGestor();

        pedido.Aprovar(Diogo.Identidade, Sensivel, Contexto, Agora, Agora.AddMonths(3)).IsSuccess.Should().BeTrue();
        pedido.ValidaAte.Should().Be(Agora.AddMonths(3));

        pedido.Aprovar(Marlene.Identidade, Sensivel, Contexto, Agora.AddHours(1)).IsSuccess.Should().BeTrue();
        pedido.ValidaAte.Should().Be(Agora.AddMonths(3));
    }

    [Fact(DisplayName = "O gestor não define fim de vigência para perfil de governança")]
    public void Aprovar_GestorComValidadeParaGovernanca_Recusa()
    {
        PedidoDeConcessao pedido = PedidoAguardandoGestor();

        Result<DesfechoDaAprovacao> resultado = pedido.Aprovar(Diogo.Identidade, DeGestor, Contexto, Agora, Agora.AddMonths(3));

        resultado.Errors.Should().ContainSingle()
            .Which.Should().Match<FieldError>(e => e.Field == "validaAte" && e.Error.Code == PedidoErrorCodes.ValidadeInvalida);
        pedido.Decisoes.Should().BeEmpty();
    }

    [Fact(DisplayName = "O gestor não define fim de vigência que não é posterior ao instante da aprovação")]
    public void Aprovar_GestorComValidadeNoPassado_Recusa()
    {
        PedidoDeConcessao pedido = PedidoAguardandoGestor();

        Codigos(pedido.Aprovar(Diogo.Identidade, Interno, Contexto, Agora, Agora)).Should().Equal(PedidoErrorCodes.ValidadeInvalida);
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoGestor);
    }

    [Fact(DisplayName = "Prazo do gestor vencido antes da proteção de dados devolve o pedido ao gestor, com a tentativa registrada e sem prazo")]
    public void Aprovar_ProtecaoDeDadosComPrazoVencido_VoltaAoGestor()
    {
        PedidoDeConcessao pedido = PedidoAguardandoGestor();
        pedido.Aprovar(Diogo.Identidade, Sensivel, Contexto, Agora, Agora.AddDays(1));

        Result<DesfechoDaAprovacao> resultado = pedido.Aprovar(Marlene.Identidade, Sensivel, Contexto, Agora.AddDays(2));

        Codigos(resultado).Should().Equal(PedidoErrorCodes.PrazoVencido);
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoGestor);
        pedido.ValidaAte.Should().BeNull();
        pedido.Decisoes.Select(d => d.Resultado).Should().Equal(ResultadoDaDecisao.Aprovado, ResultadoDaDecisao.PrazoVencido);
        DecisaoDoPedido tentativa = pedido.Decisoes[1];
        tentativa.Etapa.Should().Be(EtapaDoPedido.ProtecaoDeDados);
        tentativa.Decisor.Should().Be(Marlene.Identidade);
    }

    [Fact(DisplayName = "Perfil alterado entre as etapas devolve o pedido ao gestor e zera o prazo que ele definira")]
    public void Aprovar_PerfilAlterado_ZeraOPrazo()
    {
        PedidoDeConcessao pedido = PedidoAguardandoGestor();
        pedido.Aprovar(Diogo.Identidade, Sensivel, Contexto, Agora, Agora.AddMonths(3));
        PerfilAvaliado ampliado = new(["selecao:atendimento:analisar"], "sensivel", true, null);

        Codigos(pedido.Aprovar(Marlene.Identidade, ampliado, Contexto, Agora.AddHours(1))).Should().Equal(PedidoErrorCodes.PerfilAlterado);

        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoGestor);
        pedido.ValidaAte.Should().BeNull();
    }

    [Fact(DisplayName = "A proteção de dados não informa fim de vigência, que é do gestor")]
    public void Aprovar_ProtecaoDeDadosComValidade_Recusa()
    {
        PedidoDeConcessao pedido = PedidoAguardandoGestor();
        pedido.Aprovar(Diogo.Identidade, Sensivel, Contexto, Agora, Agora.AddMonths(3));

        Result<DesfechoDaAprovacao> resultado = pedido.Aprovar(Marlene.Identidade, Sensivel, Contexto, Agora.AddHours(1), Agora.AddMonths(9));

        resultado.Errors.Should().ContainSingle()
            .Which.Should().Match<FieldError>(e => e.Field == "validaAte" && e.Error.Code == PedidoErrorCodes.ValidadeInvalida);
        pedido.ValidaAte.Should().Be(Agora.AddMonths(3));
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoProtecaoDeDados);
    }

    [Fact(DisplayName = "Pedido que não aguarda aprovador não se aprova nem se recusa")]
    public void Decidir_SemEtapa_Recusa()
    {
        PedidoDeConcessao pedido = Criar(Betina, Leandro.Email);

        Codigos(pedido.Aprovar(Diogo.Identidade, Interno, Contexto, Agora)).Should().Equal(PedidoErrorCodes.SituacaoInvalida);
        Codigos(pedido.Recusar(Diogo.Identidade, "Motivo.", Interno, Agora)).Should().Equal(PedidoErrorCodes.SituacaoInvalida);
    }

    [Theory(DisplayName = "Solicitante e beneficiário não decidem o pedido em que são parte")]
    [InlineData("solicitante")]
    [InlineData("beneficiario")]
    public void Decidir_ComoParte_Recusa(string parte)
    {
        PedidoDeConcessao pedido = PedidoAguardandoGestor();
        Identidade decisor = parte == "solicitante" ? Betina.Identidade : Leandro.Identidade;

        Codigos(pedido.Aprovar(decisor, Interno, Contexto, Agora)).Should().Equal(PedidoErrorCodes.DecisorEParte);
        Codigos(pedido.Recusar(decisor, "Motivo.", Interno, Agora)).Should().Equal(PedidoErrorCodes.DecisorEParte);
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoGestor);
        pedido.Decisoes.Should().BeEmpty();
    }

    [Fact(DisplayName = "Quem aprovou como gestor não decide na etapa de proteção de dados, nem aprovando nem recusando")]
    public void Decidir_MesmaPessoaNasDuasEtapas_Recusa()
    {
        PedidoDeConcessao pedido = PedidoAguardandoGestor();
        pedido.Aprovar(Isabella.Identidade, Sensivel, Contexto, Agora);

        Codigos(pedido.Aprovar(Isabella.Identidade, Sensivel, Contexto, Agora)).Should().Equal(PedidoErrorCodes.MesmoDecisorNasDuasEtapas);
        Codigos(pedido.Recusar(Isabella.Identidade, "Motivo.", Sensivel, Agora)).Should().Equal(PedidoErrorCodes.MesmoDecisorNasDuasEtapas);
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoProtecaoDeDados);
    }

    [Fact(DisplayName = "Quem aprovou como gestor em qualquer ciclo não decide na etapa de proteção de dados, mesmo que outro gestor tenha aprovado depois")]
    public void Decidir_GestorDeCicloAnterior_NaoDecideNaEtapaDeProtecaoDeDados()
    {
        PedidoDeConcessao pedido = PedidoAguardandoGestor();
        pedido.Aprovar(Isabella.Identidade, Sensivel, Contexto, Agora);
        PerfilAvaliado ampliado = new([.. Sensivel.Permissoes, "selecao:inscricoes:exportar"], "sensivel", true, null);
        Codigos(pedido.Aprovar(Marlene.Identidade, ampliado, Contexto, Agora)).Should().Equal(PedidoErrorCodes.PerfilAlterado);
        pedido.Aprovar(Diogo.Identidade, ampliado, Contexto, Agora.AddHours(2)).IsSuccess.Should().BeTrue();

        Codigos(pedido.Aprovar(Isabella.Identidade, ampliado, Contexto, Agora)).Should().Equal(PedidoErrorCodes.MesmoDecisorNasDuasEtapas);
        Codigos(pedido.Recusar(Isabella.Identidade, "Motivo.", ampliado, Agora)).Should().Equal(PedidoErrorCodes.MesmoDecisorNasDuasEtapas);
        pedido.Aprovar(Marlene.Identidade, ampliado, Contexto, Agora.AddHours(3)).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Perfil que mesmo com as mesmas permissões deixou de exigir a proteção de dados também volta ao gestor")]
    public void Aprovar_ExigenciaDeProtecaoDeDadosMudou_VoltaAoGestor()
    {
        PedidoDeConcessao pedido = PedidoAguardandoGestor();
        pedido.Aprovar(Diogo.Identidade, Sensivel, Contexto, Agora);
        PerfilAvaliado menosSensivel = new(Sensivel.Permissoes, "interno", false, null);

        Codigos(pedido.Aprovar(Marlene.Identidade, menosSensivel, Contexto, Agora)).Should().Equal(PedidoErrorCodes.PerfilAlterado);
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoGestor);
    }
}
