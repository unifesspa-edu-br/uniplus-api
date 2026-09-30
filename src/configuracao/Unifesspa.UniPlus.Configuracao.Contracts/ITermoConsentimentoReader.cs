namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Leitura cross-módulo das versões promovidas do catálogo de termos de consentimento (UNI-REQ-0086):
/// o processo escolhe termo e versão, e congela o conteúdo da versão.
/// </summary>
public interface ITermoConsentimentoReader
{
    /// <summary>
    /// As versões pedidas, cada uma com o termo a que pertence. Versão inexistente, ou de termo
    /// removido do catálogo, não vem.
    /// </summary>
    Task<IReadOnlyList<VersaoTermoConsentimentoView>> ListarVersoesAsync(
        IReadOnlyCollection<Guid> versaoIds, CancellationToken cancellationToken);
}

/// <summary>
/// Uma versão promovida de termo, imutável: nome do termo, texto, base legal, forma de aceite em
/// token canônico e o hash do conteúdo.
/// </summary>
public sealed record VersaoTermoConsentimentoView(
    Guid TermoId,
    Guid VersaoId,
    string Nome,
    string Texto,
    string BaseLegal,
    string FormaAceite,
    string Hash);
