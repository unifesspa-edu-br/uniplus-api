namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.ApoioDosTestesDeFaseCanonica;

public sealed class FaseCanonicaOrganizadaEmChamadasTests
{
    [Fact(DisplayName = "A fase nasce sem a marca de organizada em chamadas quando ela não é informada")]
    public void Criar_SemAMarca_NaoOrganizaEmChamadas() =>
        Criar().Value!.Sinalizadores.OrganizadaEmChamadas.Should().BeFalse();

    [Theory(DisplayName = "A marca de organizada em chamadas é aceita em qualquer fase e guardada como foi informada")]
    [InlineData("HABILITACAO")]
    [InlineData("INSCRICAO")]
    [InlineData("HOMOLOGACAO_INSCRICOES")]
    public void Criar_ComAMarca_GuardaAMarca(string codigo)
    {
        Result<FaseCanonica> resultado = Criar(codigo: codigo, sinalizadores: Sinalizadores(organizadaEmChamadas: true));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Sinalizadores.OrganizadaEmChamadas.Should().BeTrue();
    }

    [Fact(DisplayName = "A marca de organizada em chamadas não interfere na coleta de inscrição nem de isenção")]
    public void Criar_ComAMarcaEColetaDeInscricao_Aceita()
    {
        FaseCanonica fase = Criar(sinalizadores: Sinalizadores(coletaInscricao: true, organizadaEmChamadas: true)).Value!;

        fase.Sinalizadores.ColetaInscricao.Should().BeTrue();
        fase.Sinalizadores.OrganizadaEmChamadas.Should().BeTrue();
    }

    [Fact(DisplayName = "Atualizar liga e desliga a marca de organizada em chamadas")]
    public void Atualizar_AlteraAMarca()
    {
        FaseCanonica fase = Criar(codigo: "HABILITACAO", nome: "Habilitação").Value!;

        fase.Atualizar("Habilitação", null, null, "NO_EDITAL", Sinalizadores(organizadaEmChamadas: true)).IsSuccess.Should().BeTrue();
        fase.Sinalizadores.OrganizadaEmChamadas.Should().BeTrue();

        fase.Atualizar("Habilitação", null, null, "NO_EDITAL", Sinalizadores()).IsSuccess.Should().BeTrue();
        fase.Sinalizadores.OrganizadaEmChamadas.Should().BeFalse();
    }
}
