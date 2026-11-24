namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.UnitTests.Mappings;

using AwesomeAssertions;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Mappings;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.UnitTests.Commands;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;

public sealed class UnidadeMappingTests
{
    [Theory]
    [InlineData(TipoUnidade.Reitoria, "REITORIA")]
    [InlineData(TipoUnidade.ProReitoria, "PRO_REITORIA")]
    public void Mapear_Unidade_DevolveCodigoDeContrato(TipoUnidade tipo, string codigo)
    {
        Unidade unidade = ComandosDeInstituicaoDeTeste.UnidadeDoTipo(tipo);

        unidade.ToDto().Tipo.Should().Be(codigo);
        unidade.ToView().Tipo.Should().Be(codigo);
    }
}
