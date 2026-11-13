namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// API UniPlus com Wolverine e Postgres, com o administrador dos testes concedido em todo o módulo Configuração, o TTL da
/// chave de idempotência e o do cursor configurados com um resto de ticks abaixo do microssegundo, e o relógio
/// <see cref="RelogioControlado"/>.
/// </summary>
public sealed class ConfiguracaoComTempoEmResiduoFactory : MonolitoApiFactory
{
    public const string TtlDaChaveConfigurado = "01:00:00.0000005";
    public const string TtlDoCursorConfigurado = "00:15:00.0000005";

    internal RelogioControlado Relogio { get; } = new();

    public ConfiguracaoComTempoEmResiduoFactory(string connectionString)
        : base(connectionString, wolverineEnabled: true)
    {
    }

    protected override IEnumerable<KeyValuePair<string, string?>> OverridesAdicionais() =>
    [
        new("UniPlus:Idempotency:Ttl", TtlDaChaveConfigurado),
        new("UniPlus:Pagination:Cursor:CursorTtl", TtlDoCursorConfigurado),
    ];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.AdicionarAdministradorInstitucionalDeTeste<PermissaoConfiguracao>();
            services.AdicionarRegistroDeDecisoesDeTeste();
            services.AddSingleton(Relogio);
            services.AddSingleton<TimeProvider>(Relogio);
        });
    }
}
