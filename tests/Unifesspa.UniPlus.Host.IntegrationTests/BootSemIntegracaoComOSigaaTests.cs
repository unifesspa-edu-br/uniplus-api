namespace Unifesspa.UniPlus.Host.IntegrationTests;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;
using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Garante que a integração com o sistema acadêmico não é condição para o monólito subir.
/// </summary>
/// <remarks>
/// A sincronização de discentes lê de um sistema externo à aplicação. Nada do que dependa
/// dele — endereço não configurado, rede institucional indisponível, credencial ausente —
/// pode impedir o processo de iniciar: derrubaria junto todos os demais módulos, que não
/// têm relação nenhuma com essa integração, e inviabilizaria o desenvolvimento local.
///
/// O handler da sincronização é descoberto pela mensageria de qualquer forma, e o pipeline
/// dele é montado na subida. É esse caminho que este teste percorre.
/// </remarks>
[Collection(MonolitoHostCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class BootSemIntegracaoComOSigaaTests
{
    private readonly MonolitoPostgresFixture _fixture;

    public BootSemIntegracaoComOSigaaTests(MonolitoPostgresFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    [Fact(DisplayName = "O monólito sobe sem endereço do SIGAA configurado")]
    public void Host_Sobe_Sem_Configuracao_Da_Integracao()
    {
        IConfiguration configuracao = _fixture.Factory.Services.GetRequiredService<IConfiguration>();

        configuracao[$"{SigaaOptions.SectionName}:{nameof(SigaaOptions.BaseUrl)}"]
            .Should().BeNullOrWhiteSpace("o cenário deste teste é justamente a ausência de configuração");

        // Resolver qualquer serviço prova que o processo iniciou: a montagem dos pipelines
        // de mensageria acontece na subida, antes disso.
        _fixture.Factory.Services.GetRequiredService<IConfiguration>().Should().NotBeNull();
    }

    [Fact(DisplayName = "Sem configuração, o cliente do SIGAA não é registrado")]
    public void Cliente_Nao_E_Registrado_Sem_Configuracao()
    {
        using IServiceScope escopo = _fixture.Factory.Services.CreateScope();

        ISigaaVinculoDiscenteClient? cliente =
            escopo.ServiceProvider.GetService<ISigaaVinculoDiscenteClient>();

        cliente.Should().BeNull(
            "sem origem configurada não há de onde ler; quem tentasse usar o cliente falharia "
            + "ao obtê-lo, com mensagem clara, em vez de descobrir tarde que a sincronização "
            + "nunca teve como funcionar");
    }
}
