namespace Unifesspa.UniPlus.Autorizacao.API.Errors;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IDomainErrorRegistration, AutorizacaoDomainErrorRegistration>().")]
internal sealed class AutorizacaoDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
    [
        .. ErrosDoPerfil.Mapeamentos(),
        .. ErrosDaConcessao.Mapeamentos(),
        .. ErrosDaInstalacao.Mapeamentos(),
        .. ErrosDoPedido.Mapeamentos(),
        .. ErrosDoTermo.Mapeamentos(),
        .. ErrosDaRevisao.Mapeamentos(),
        .. ErrosDaMigracao.Mapeamentos(),
        .. ErrosDaReclassificacao.Mapeamentos(),
    ];
}
