namespace Unifesspa.UniPlus.Selecao.API.Errors;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IDomainErrorRegistration, SelecaoDomainErrorRegistration>().")]
internal sealed class SelecaoDomainErrorRegistration : IDomainErrorRegistration
{
    // A ordem vale: o registro global guarda o último mapeamento de um código repetido.
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
    [
        .. ErrosDoProcessoSeletivo.Mapeamentos(),
        .. ErrosDaOfertaEDaClassificacao.Mapeamentos(),
        .. ErrosDosDocumentosEAtos.Mapeamentos(),
        .. ErrosDaPublicacao.Mapeamentos(),
        .. ErrosDaColetaDeFatos.Mapeamentos(),
        .. ErrosDaLeituraDoCongelado.Mapeamentos(),
        .. ErrosDoCronograma.Mapeamentos(),
        .. ErrosDoEnvelopeEDaRetificacao.Mapeamentos(),
        .. ErrosDosDocumentosExigidos.Mapeamentos(),
        .. ErrosDaInscricao.Mapeamentos(),
        .. ErrosDaArvoreDeExigencias.Mapeamentos(),
        .. ErrosDaAdministracaoDoProcesso.Mapeamentos(),
        .. ErrosDasIntegracoesExternas.Mapeamentos(),
    ];
}
