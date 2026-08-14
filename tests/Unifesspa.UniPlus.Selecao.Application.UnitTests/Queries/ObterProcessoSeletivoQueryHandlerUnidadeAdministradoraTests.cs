namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Reflection;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de <see cref="ObterProcessoSeletivoQueryHandler"/> para a Unidade
/// administradora (issue #849, CA-03/CA-04 da Feature #40).
/// </summary>
public sealed class ObterProcessoSeletivoQueryHandlerUnidadeAdministradoraTests
{
    [Fact(DisplayName = "Handle nunca injeta IUnidadeReader — prova estrutural de que o GET nunca releva o cadastro vivo (CA-04)")]
    public void Handle_NaoInjetaIUnidadeReader()
    {
        MethodInfo handle = typeof(ObterProcessoSeletivoQueryHandler).GetMethod(
            "Handle", BindingFlags.Public | BindingFlags.Static)!;

        handle.GetParameters().Select(p => p.ParameterType).Should().NotContain(typeof(IUnidadeReader));
    }

    [Fact(DisplayName = "Handle persiste e retorna o processo com a Unidade administradora resolvida (round-trip contra o repositório)")]
    public async Task Handle_RetornaProcessoComUnidadeAdministradora()
    {
        Guid unidadeId = Guid.NewGuid();
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Query", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, unidadeId,
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ProcessoSeletivoDto? dto = await ObterProcessoSeletivoQueryHandler.Handle(
            new ObterProcessoSeletivoQuery(processo.Id), repository, CancellationToken.None);

        dto.Should().NotBeNull();
        dto!.UnidadeAdministradora.OrigemId.Should().Be(unidadeId);
        dto.UnidadeAdministradora.Sigla.Should().Be("CEPS");
        dto.UnidadeAdministradora.Slug.Should().Be("ceps");
        dto.UnidadeAdministradora.Nome.Should().Be("Centro de Processos Seletivos");
        dto.UnidadeAdministradora.Tipo.Should().Be("ADMINISTRATIVA");
    }
}
