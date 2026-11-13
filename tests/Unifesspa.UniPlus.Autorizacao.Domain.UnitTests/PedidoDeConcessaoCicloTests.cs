namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Domain.UnitTests.PedidoDeConcessaoBuilder;

/// <summary>O perfil que muda entre as etapas, a recusa, o cancelamento, o anexo e a concessão do pedido.</summary>
public sealed class PedidoDeConcessaoCicloTests : PedidoDeConcessaoTestBase
{
    [Fact(DisplayName = "Perfil que mudou entre as etapas devolve o pedido ao gestor e nada é concedido")]
    public void Aprovar_PerfilAlterado_VoltaAoGestor()
    {
        PedidoDeConcessao pedido = Pedido().Protegido().AguardandoGestor();
        pedido.Aprovar(Diogo.Identidade, Sensivel, Contexto, Agora);

        Result<DesfechoDaAprovacao> resultado = pedido.Aprovar(Marlene.Identidade, Ampliado, Contexto, Agora.AddHours(1));

        Codigos(resultado).Should().Equal(PedidoErrorCodes.PerfilAlterado);
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoGestor);
        pedido.EncerradoEm.Should().BeNull();
        pedido.Decisoes.Select(d => d.Resultado).Should().Equal(ResultadoDaDecisao.Aprovado, ResultadoDaDecisao.PerfilAlterado);
        DecisaoDoPedido tentativa = pedido.Decisoes[1];
        tentativa.Etapa.Should().Be(EtapaDoPedido.ProtecaoDeDados);
        tentativa.DecisorSub.Should().Be(Marlene.Identidade.Sub);
        tentativa.PermissoesAvaliadas.Should().Equal(Ampliado.Permissoes);
    }

    [Fact(DisplayName = "Devolvido pelo perfil alterado, o gestor decide de novo e a segunda etapa aprova o que ele avaliou")]
    public void Aprovar_PerfilAlterado_GestorDecideDeNovo()
    {
        PedidoDeConcessao pedido = Pedido().Protegido().AguardandoGestor();
        pedido.Aprovar(Diogo.Identidade, Sensivel, Contexto, Agora);
        pedido.Aprovar(Marlene.Identidade, Ampliado, Contexto, Agora.AddHours(1));

        pedido.Aprovar(Isabella.Identidade, Ampliado, Contexto, Agora.AddHours(2)).Value.Should().Be(DesfechoDaAprovacao.AguardaProtecaoDeDados);
        pedido.Aprovar(Marlene.Identidade, Ampliado, Contexto, Agora.AddHours(3)).Value.Should().Be(DesfechoDaAprovacao.Concluido);
    }

    [Theory(DisplayName = "Perfil desativado não aceita aprovação em nenhuma etapa")]
    [InlineData(false)]
    [InlineData(true)]
    public void Aprovar_PerfilInativo_Recusa(bool naEtapaDeProtecaoDeDados)
    {
        PedidoDeConcessao pedido = Pedido().Protegido().AguardandoGestor();
        if (naEtapaDeProtecaoDeDados)
        {
            pedido.Aprovar(Diogo.Identidade, Sensivel, Contexto, Agora);
        }

        PerfilAvaliado inativo = new(Sensivel.Permissoes, Sensivel.Sensibilidade, true, null, ativo: false);
        SituacaoDoPedido antes = pedido.Situacao;

        Codigos(pedido.Aprovar(naEtapaDeProtecaoDeDados ? Marlene.Identidade : Diogo.Identidade, inativo, Contexto, Agora))
            .Should().Equal(PedidoErrorCodes.PerfilInativo);
        pedido.Situacao.Should().Be(antes);
        pedido.Decisoes.Count.Should().Be(naEtapaDeProtecaoDeDados ? 1 : 0);
    }

    [Fact(DisplayName = "Recusa encerra o pedido com o motivo e o retrato avaliado, sem concessão")]
    public void Recusar_EncerraComMotivo()
    {
        PedidoDeConcessao pedido = Pedido().Protegido().AguardandoGestor();

        Result recusa = pedido.Recusar(Diogo.Identidade, "  Função não exige acesso a laudos.  ", Sensivel, Agora.AddHours(1));

        recusa.IsSuccess.Should().BeTrue();
        pedido.Situacao.Should().Be(SituacaoDoPedido.Recusado);
        pedido.EncerradoEm.Should().Be(Agora.AddHours(1));
        pedido.ConcessaoId.Should().BeNull();
        DecisaoDoPedido decisao = pedido.Decisoes.Single();
        decisao.Resultado.Should().Be(ResultadoDaDecisao.Recusado);
        decisao.Motivo.Should().Be("Função não exige acesso a laudos.");
        decisao.PermissoesAvaliadas.Should().Equal(Sensivel.Permissoes);
    }

    [Theory(DisplayName = "Recusa sem motivo, com motivo longo ou com caractere nulo é recusada e não encerra")]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("Mot\0ivo")]
    public void Recusar_MotivoInvalido_Recusa(string? motivo)
    {
        PedidoDeConcessao pedido = Pedido().Protegido().AguardandoGestor();

        Codigos(pedido.Recusar(Diogo.Identidade, motivo, Interno, Agora)).Should().Equal(PedidoErrorCodes.MotivoInvalido);
        Codigos(pedido.Recusar(Diogo.Identidade, new string('a', 501), Interno, Agora)).Should().Equal(PedidoErrorCodes.MotivoInvalido);
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoGestor);
    }

    [Fact(DisplayName = "O solicitante cancela antes da primeira decisão")]
    public void Cancelar_AntesDaPrimeiraDecisao_Encerra()
    {
        PedidoDeConcessao cancelavel = Pedido().Protegido().AguardandoGestor();

        cancelavel.Cancelar(Agora.AddHours(1)).IsSuccess.Should().BeTrue();

        cancelavel.Situacao.Should().Be(SituacaoDoPedido.Cancelado);
        cancelavel.EncerradoEm.Should().Be(Agora.AddHours(1));
        cancelavel.Ativo.Should().BeFalse();
    }

    [Fact(DisplayName = "Depois da primeira decisão o pedido não se cancela, nem quando volta ao gestor")]
    public void Cancelar_DepoisDaPrimeiraDecisao_Recusa()
    {
        PedidoDeConcessao decidido = Pedido().Protegido().AguardandoGestor();
        decidido.Aprovar(Diogo.Identidade, Sensivel, Contexto, Agora);
        Codigos(decidido.Cancelar(Agora)).Should().Equal(PedidoErrorCodes.SituacaoInvalida);

        PedidoDeConcessao devolvido = Pedido().Protegido().AguardandoGestor();
        devolvido.Aprovar(Diogo.Identidade, Sensivel, Contexto, Agora);
        devolvido.Aprovar(Marlene.Identidade, new PerfilAvaliado(["outra"], "sensivel", true, null), Contexto, Agora);
        devolvido.Situacao.Should().Be(SituacaoDoPedido.AguardandoGestor);
        Codigos(devolvido.Cancelar(Agora)).Should().Equal(PedidoErrorCodes.SituacaoInvalida);
    }

    [Fact(DisplayName = "O anexo só se envia e se confirma antes da primeira decisão")]
    public void Anexo_SoAntesDaPrimeiraDecisao()
    {
        PedidoDeConcessao pedido = Pedido().AguardandoGestor();
        AnexoDoPedido anexo = pedido.NovoAnexo(2048).Value!;
        anexo.ChaveDeEnvio.Should().Be($"autorizacao/pedido/{pedido.Id}/anexo/{anexo.Id}/envio");
        anexo.ChaveDefinitiva.Should().Be($"autorizacao/pedido/{pedido.Id}/anexo/{anexo.Id}/definitivo");

        pedido.Aprovar(Diogo.Identidade, Sensivel, Contexto, Agora);

        Codigos(pedido.NovoAnexo(2048)).Should().Equal(PedidoErrorCodes.SituacaoInvalida);
        Codigos(pedido.ConfirmarAnexo(anexo.Id, "\"etag\"", Agora)).Should().Equal(PedidoErrorCodes.SituacaoInvalida);
        anexo.Situacao.Should().Be(SituacaoDoAnexo.AguardandoEnvio);
    }

    [Theory(DisplayName = "O PDF do anexo tem de 1 byte a 10 MB")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10L * 1024 * 1024 + 1)]
    public void Anexo_TamanhoInvalido_Recusa(long tamanho) =>
        Codigos(Pedido().AguardandoGestor().NovoAnexo(tamanho)).Should().Equal(PedidoErrorCodes.AnexoInvalido);

    [Fact(DisplayName = "Confirmar o anexo exige o ETag e leva o anexo à verificação, uma vez só")]
    public void Anexo_Confirmar_LevaAVerificacao()
    {
        PedidoDeConcessao pedido = Pedido().AguardandoGestor();
        AnexoDoPedido anexo = pedido.NovoAnexo(2048).Value!;

        Codigos(pedido.ConfirmarAnexo(anexo.Id, "  ", Agora)).Should().Equal(PedidoErrorCodes.AnexoInvalido);
        Codigos(pedido.ConfirmarAnexo(Guid.NewGuid(), "\"etag\"", Agora)).Should().Equal(PedidoErrorCodes.NaoEncontrado);
        anexo.RegistrarVerificado(new string('a', 64), Agora).Should().BeFalse("o anexo ainda não está em verificação");

        pedido.ConfirmarAnexo(anexo.Id, " \"etag\" ", Agora).IsSuccess.Should().BeTrue();
        anexo.Situacao.Should().Be(SituacaoDoAnexo.EmVerificacao);
        anexo.ETag.Should().Be("\"etag\"");
        Codigos(pedido.ConfirmarAnexo(anexo.Id, "\"etag\"", Agora)).Should().Equal(PedidoErrorCodes.SituacaoInvalida);
    }

    [Fact(DisplayName = "O veredicto repetido do anexo não muda nada")]
    public void Anexo_VeredictoRepetido_NaoMuda()
    {
        PedidoDeConcessao pedido = Pedido().AguardandoGestor();
        AnexoDoPedido anexo = pedido.NovoAnexo(2048).Value!;
        pedido.ConfirmarAnexo(anexo.Id, "\"etag\"", Agora);

        anexo.RegistrarRecusado(Agora).Should().BeTrue();
        anexo.RegistrarVerificado(new string('a', 64), Agora).Should().BeFalse("o veredicto repetido não reabre o anexo");
        anexo.Situacao.Should().Be(SituacaoDoAnexo.Recusado);
        pedido.TemAnexoVerificado.Should().BeFalse();
    }

    [Fact(DisplayName = "As partes do pedido são o solicitante e o beneficiário, este também pelo e-mail antes do aceite")]
    public void EhParte_SolicitanteEBeneficiario()
    {
        PedidoDeConcessao pedido = Pedido().Criado();

        pedido.EhParte(Betina).Should().BeTrue();
        pedido.EhParte(Leandro).Should().BeTrue("o e-mail verificado dele é o do pedido");
        pedido.EhParte(Leandro with { EmailVerificado = false }).Should().BeFalse();
        pedido.EhParte(Diogo).Should().BeFalse();

        pedido.Aceitar(Leandro, null, Agora);
        pedido.EhParte(Leandro with { Email = "outro@uniplus-test.local" }).Should().BeTrue("já aceitou: vale a identidade");
    }

    [Fact(DisplayName = "Só o pedido aprovado registra a concessão")]
    public void RegistrarConcessao_SoNoPedidoAprovado()
    {
        PedidoDeConcessao pedido = Pedido().AguardandoGestor();
        Guid concessao = Guid.NewGuid();

        FluentActions.Invoking(() => pedido.RegistrarConcessao(concessao)).Should().Throw<InvalidOperationException>();

        pedido.Aprovar(Diogo.Identidade, Interno, Contexto, Agora);
        pedido.RegistrarConcessao(concessao);
        pedido.ConcessaoId.Should().Be(concessao);
    }

    private static PerfilAvaliado Ampliado => new([.. Sensivel.Permissoes, "selecao:inscricoes:exportar"], "sensivel", true, null);
}
