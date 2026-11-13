namespace Unifesspa.UniPlus.Autorizacao.API.Errors;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>O registro único dos erros de domínio do módulo Autorização, instanciado pelo contêiner como <see cref="IDomainErrorRegistration"/>.</summary>
public sealed class AutorizacaoDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
    [
        .. ErrosDoPerfil.Mapeamentos(),
        .. ErrosDaConcessao.Mapeamentos(),
        .. ErrosDaInstalacao.Mapeamentos(),
        .. ErrosDoPedido.Mapeamentos(),
        .. ErrosDoTermo.Mapeamentos(),
        .. ErrosDaRevisao.Mapeamentos(),
        .. ErrosDaProvidencia.Mapeamentos(),
        .. ErrosDaMigracao.Mapeamentos(),
        .. ErrosDaReclassificacao.Mapeamentos(),
    ];
}
