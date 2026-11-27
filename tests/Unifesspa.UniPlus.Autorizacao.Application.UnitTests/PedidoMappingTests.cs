namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Autorizacao.Domain.UnitTests.ApoioDoPedido;

/// <summary>Os campos de situação, etapa, escopo e decisão do pedido, que a resposta expõe como código e rótulo.</summary>
public sealed class PedidoMappingTests
{
    [Fact(DisplayName = "O pedido que aguarda o gestor mostra a situação e a etapa como código e rótulo")]
    public void ToDto_AguardandoGestor_MostraSituacaoEEtapa()
    {
        PedidoDeConcessao pedido = PedidoDeConcessaoBuilder.Pedido().AguardandoGestor();

        PedidoDeConcessaoDto dto = PedidoMapping.ToDto(pedido, null);

        (dto.Situacao.Codigo, dto.Situacao.Rotulo, dto.EtapaAtual?.Codigo, dto.EtapaAtual?.Rotulo)
            .Should().Be(("AGUARDANDO_GESTOR", "Aguardando gestor", "GESTOR", "Gestor"));
        (dto.Escopo.Tipo.Codigo, dto.Escopo.Tipo.Rotulo).Should().Be(("UNIDADE", "Unidade"));
    }

    [Fact(DisplayName = "O pedido que não aguarda aprovador não tem etapa atual")]
    public void ToDto_SemAprovadorAguardado_EtapaAtualNula()
    {
        PedidoDeConcessao pedido = PedidoDeConcessaoBuilder.Pedido().DeclarandoDocumento().Criado();

        PedidoDeConcessaoDto dto = PedidoMapping.ToDto(pedido, null);

        (dto.Situacao.Codigo, dto.EtapaAtual).Should().Be(("AGUARDANDO_DOCUMENTO", null));
    }

    [Fact(DisplayName = "O item da fila mostra a situação, a etapa e o escopo como código e rótulo")]
    public void ToResumo_AguardandoGestor_MostraSituacaoEtapaEEscopo()
    {
        PedidoDeConcessao pedido = PedidoDeConcessaoBuilder.Pedido().AguardandoGestor();

        PedidoResumoDto resumo = PedidoMapping.ToResumo(new PedidoComPerfil(pedido, NomeDoPerfil.Reidratar("Análise de atendimento"), 1));

        (resumo.Situacao.Codigo, resumo.EtapaAtual?.Codigo, resumo.Escopo.Tipo.Codigo, resumo.Escopo.Tipo.Rotulo)
            .Should().Be(("AGUARDANDO_GESTOR", "GESTOR", "UNIDADE", "Unidade"));
    }

    [Fact(DisplayName = "O anexo mostra a situação como código e rótulo")]
    public void ToDto_Anexo_MostraSituacao()
    {
        PedidoDeConcessao pedido = PedidoDeConcessaoBuilder.Pedido().DeclarandoDocumento().Criado();
        AnexoDoPedido anexo = pedido.NovoAnexo(1024).Value!;

        AnexoDoPedidoDto dto = PedidoMapping.ToDto(anexo);

        (dto.Situacao.Codigo, dto.Situacao.Rotulo).Should().Be(("AGUARDANDO_ENVIO", "Aguardando envio"));
    }

    [Fact(DisplayName = "A decisão do gestor mostra a etapa, o resultado e a sensibilidade avaliada como código e rótulo")]
    public void ToDto_DecisaoDoGestor_MostraEtapaResultadoESensibilidade()
    {
        PedidoDeConcessao pedido = PedidoDeConcessaoBuilder.Pedido().AguardandoGestor();
        pedido.Aprovar(Diogo.Identidade, Interno, Contexto, Agora);

        DecisaoDoPedidoDto decisao = PedidoMapping.ToDto(pedido, null).Decisoes.Single();

        (decisao.Etapa.Codigo, decisao.Etapa.Rotulo, decisao.Resultado.Codigo, decisao.Resultado.Rotulo, decisao.SensibilidadeAvaliada.Codigo, decisao.SensibilidadeAvaliada.Rotulo)
            .Should().Be(("GESTOR", "Gestor", "APROVADO", "Aprovado", "INTERNO", "Interno"));
    }

    [Fact(DisplayName = "A decisão sobre um perfil sensível mostra a sensibilidade Sensível")]
    public void ToDto_DecisaoSobrePerfilSensivel_MostraSensibilidadeSensivel()
    {
        PedidoDeConcessao pedido = PedidoDeConcessaoBuilder.Pedido().Protegido().AguardandoGestor();
        pedido.Aprovar(Diogo.Identidade, Sensivel, Contexto, Agora);

        DecisaoDoPedidoDto decisao = PedidoMapping.ToDto(pedido, null).Decisoes.Single();

        (decisao.SensibilidadeAvaliada.Codigo, decisao.SensibilidadeAvaliada.Rotulo).Should().Be(("SENSIVEL", "Sensível"));
    }

    [Fact(DisplayName = "A troca de versão que só atualiza o pedido mostra o desfecho como código e rótulo")]
    public void ToDto_TrocaDeVersaoSemAcrescimo_MostraDesfecho()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Análise de atendimento", "Analisa os pedidos.", ["selecao:processos:consultar", "selecao:processos:exportar"], Catalogo);
        PedidoDeConcessao pedido = PedidoDeConcessaoBuilder.Pedido().DoPerfil(perfil).AguardandoGestor();
        TrocaDeVigencia troca = PerfisDeTeste.Trocar(perfil, ["selecao:processos:consultar"], Catalogo)[^1];
        pedido.AoEntrarEmVigor(troca, Agora);

        TrocaDeVersaoDoPedidoDto dto = PedidoMapping.ToDto(pedido, perfil).TrocasDeVersao.Single();

        (dto.Desfecho.Codigo, dto.Desfecho.Rotulo).Should().Be(("VERSAO_ATUALIZADA", "Versão atualizada"));
    }
}
