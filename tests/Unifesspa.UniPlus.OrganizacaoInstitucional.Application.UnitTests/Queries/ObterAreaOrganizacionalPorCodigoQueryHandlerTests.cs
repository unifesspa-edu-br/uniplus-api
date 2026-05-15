namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.AreasOrganizacionais;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

public sealed class ObterAreaOrganizacionalPorCodigoQueryHandlerTests
{
    [Fact(DisplayName = "Handle com código existente retorna AreaOrganizacionalDto preenchido")]
    public async Task Handle_ComCodigoExistente_RetornaDto()
    {
        AreaOrganizacional area = AreaOrganizacional.Criar(
            AreaCodigo.From("CEPS").Value,
            "Centro de Processos Seletivos",
            TipoAreaOrganizacional.Centro,
            "Descricao.",
            "0055-organizacao-institucional-bounded-context").Value!;

        IAreaOrganizacionalRepository repo = Substitute.For<IAreaOrganizacionalRepository>();
        repo.ObterPorCodigoAsync(area.Codigo, Arg.Any<CancellationToken>()).Returns(area);

        AreaOrganizacionalDto? resultado = await ObterAreaOrganizacionalPorCodigoQueryHandler.Handle(
            new ObterAreaOrganizacionalPorCodigoQuery("CEPS"), repo, CancellationToken.None);

        resultado.Should().NotBeNull();
        resultado!.Codigo.Should().Be("CEPS");
        resultado.Nome.Should().Be("Centro de Processos Seletivos");
        resultado.Tipo.Should().Be(nameof(TipoAreaOrganizacional.Centro));
    }

    [Fact(DisplayName = "Handle com código inexistente retorna null")]
    public async Task Handle_ComCodigoInexistente_RetornaNull()
    {
        IAreaOrganizacionalRepository repo = Substitute.For<IAreaOrganizacionalRepository>();
        repo.ObterPorCodigoAsync(Arg.Any<AreaCodigo>(), Arg.Any<CancellationToken>())
            .Returns((AreaOrganizacional?)null);

        AreaOrganizacionalDto? resultado = await ObterAreaOrganizacionalPorCodigoQueryHandler.Handle(
            new ObterAreaOrganizacionalPorCodigoQuery("PROEG"), repo, CancellationToken.None);

        resultado.Should().BeNull();
    }

    [Fact(DisplayName = "Handle com código malformado retorna null (controller mapeia 404)")]
    public async Task Handle_ComCodigoMalformado_RetornaNull()
    {
        IAreaOrganizacionalRepository repo = Substitute.For<IAreaOrganizacionalRepository>();

        AreaOrganizacionalDto? resultado = await ObterAreaOrganizacionalPorCodigoQueryHandler.Handle(
            new ObterAreaOrganizacionalPorCodigoQuery("9XX"), repo, CancellationToken.None);

        resultado.Should().BeNull();
        await repo.DidNotReceive().ObterPorCodigoAsync(Arg.Any<AreaCodigo>(), Arg.Any<CancellationToken>());
    }
}
