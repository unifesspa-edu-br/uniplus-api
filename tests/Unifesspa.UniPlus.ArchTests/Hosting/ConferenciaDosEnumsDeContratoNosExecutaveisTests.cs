namespace Unifesspa.UniPlus.ArchTests.Hosting;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Infrastructure.Core.Hosting;

/// <summary>
/// O Host e o Portal registram a conferência dos códigos de contrato dos enums como serviço hospedado (ADR-0176). O
/// início dos dois hosts, com a migration e o runtime do Wolverine removidos, já executa a conferência sobre o catálogo
/// real: um defeito de código derrubaria o início e reprovaria todo teste que depende dos hosts iniciados.
/// </summary>
public sealed class ConferenciaDosEnumsDeContratoNosExecutaveisTests
{
    public static TheoryData<string> Executaveis => new(HostsDoProdutoIniciados.Executaveis);

    [Theory(DisplayName = "O executável registra uma só conferência dos enums de contrato como serviço hospedado")]
    [MemberData(nameof(Executaveis))]
    public void Provedor_TemUmaSoConferenciaDosEnumsDeContrato(string executavel) =>
        HostsDoProdutoIniciados.Obter(executavel).ProvedorDeServicos.GetServices<IHostedService>()
            .OfType<ConferenciaDosEnumsDeContratoNaSubida>().Should().ContainSingle();
}
