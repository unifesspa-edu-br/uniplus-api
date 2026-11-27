namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Autorizacao.Domain.UnitTests.ApoioDoPedido;

/// <summary>O escopo e a origem da concessão, que a resposta expõe como código e rótulo.</summary>
public sealed class ConcessaoMappingTests
{
    [Fact(DisplayName = "A concessão de um pedido, no escopo de uma unidade, mostra o escopo Unidade e a origem Pedido")]
    public void ToDto_ConcessaoDeUnidadePorPedido_MostraCodigoERotulo()
    {
        Concessao concessao = ConcessaoBuilder.DoPerfil(PerfilComum(), Catalogo).Construir();

        ConcessaoDto dto = ConcessaoMapping.ToDto(Item(concessao), Agora);

        (dto.Escopo.Tipo.Codigo, dto.Escopo.Tipo.Rotulo, dto.Origem.Tipo.Codigo, dto.Origem.Tipo.Rotulo)
            .Should().Be(("UNIDADE", "Unidade", "PEDIDO", "Pedido"));
    }

    [Fact(DisplayName = "A concessão de governança, no escopo da instituição e por instalação, mostra o escopo Instituição e a origem Instalação")]
    public void ToDto_ConcessaoDaInstituicaoPorInstalacao_MostraCodigoERotulo()
    {
        Concessao concessao = ConcessaoBuilder.DoPerfil(PerfilDeGovernanca(), Catalogo)
            .DaOrigem(new OrigemDaConcessao(TipoDeOrigem.Instalacao, Guid.NewGuid()))
            .Construir();

        ConcessaoDto dto = ConcessaoMapping.ToDto(Item(concessao), Agora);

        (dto.Escopo.Tipo.Codigo, dto.Escopo.Tipo.Rotulo, dto.Origem.Tipo.Codigo, dto.Origem.Tipo.Rotulo)
            .Should().Be(("INSTITUICAO", "Instituição", "INSTALACAO", "Instalação"));
    }

    private static ConcessaoComPerfil Item(Concessao concessao) => new(concessao, NomeDoPerfil.Reidratar("Análise de atendimento"), 1);
}
