namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O nome de cada tipo de ato que o processo cita, como as mensagens o mostram à unidade: o do
/// catálogo vigente para o tipo declarado pela primeira vez e o da cópia guardada na versão
/// publicada para o tipo que ela já congelou.
/// </summary>
/// <remarks>
/// Não há instância padrão: quem monta as mensagens exige o valor, para que nenhuma recusa
/// volte a citar o código do tipo por não ter consultado o catálogo.
/// </remarks>
public sealed class NomesDosTiposDeAto
{
    private readonly Dictionary<string, string> _nomePorCodigo;

    public NomesDosTiposDeAto(IReadOnlyDictionary<string, string> nomePorCodigo)
    {
        ArgumentNullException.ThrowIfNull(nomePorCodigo);
        _nomePorCodigo = new Dictionary<string, string>(nomePorCodigo, StringComparer.Ordinal);
    }

    /// <summary>
    /// O nome do tipo, ou nulo quando não há versão vigente dele no catálogo nem cópia na versão
    /// publicada — quem recusa o tipo desconhecido é a conferência do tipo declarado.
    /// </summary>
    public string? NomeDe(string codigo) => _nomePorCodigo.GetValueOrDefault(codigo);
}
