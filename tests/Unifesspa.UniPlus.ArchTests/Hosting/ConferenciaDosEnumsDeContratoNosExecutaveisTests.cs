namespace Unifesspa.UniPlus.ArchTests.Hosting;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Infrastructure.Core.Hosting;

/// <summary>
/// O Host e o Portal registram a conferência dos códigos de contrato dos enums como serviço hospedado (ADR-0176). A
/// fixture que inicia os dois hosts, com a migration e o runtime do Wolverine removidos, já executa a conferência sobre
/// o catálogo real: um defeito de código derrubaria o início e reprovaria toda a coleção.
/// </summary>
[Collection(HostsDoProdutoIniciadosEmTeste.Nome)]
public sealed class ConferenciaDosEnumsDeContratoNosExecutaveisTests
{
    private readonly MigrationOrderFixture _hosts;

    public ConferenciaDosEnumsDeContratoNosExecutaveisTests(MigrationOrderFixture hosts)
    {
        _hosts = hosts;
    }

    public static TheoryData<string> Executaveis => new(MigrationOrderFixture.RegisteredKeys);

    [Theory(DisplayName = "O executável registra uma só conferência dos enums de contrato como serviço hospedado")]
    [MemberData(nameof(Executaveis))]
    public void Provedor_TemUmaSoConferenciaDosEnumsDeContrato(string executavel) =>
        _hosts.GetCapturedServiceProvider(executavel).GetServices<IHostedService>()
            .OfType<ConferenciaDosEnumsDeContratoNaSubida>().Should().ContainSingle();
}
