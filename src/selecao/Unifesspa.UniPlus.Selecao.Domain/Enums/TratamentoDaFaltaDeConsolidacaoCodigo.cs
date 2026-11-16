namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="TratamentoDaFaltaDeConsolidacao"/> e o token textual que a regra de consolidação
/// declara no <c>esquema_args</c> — fonte única da leitura.
/// </summary>
public static class TratamentoDaFaltaDeConsolidacaoCodigo
{
    public const string NovoCorretor = "novo_corretor";
    public const string DecisaoDaBanca = "decisao_da_banca";

    /// <summary>
    /// Converte o token para o enum. Texto ausente, em branco ou desconhecido é recusado: um erro de digitação
    /// não pode passar por tratamento declarado.
    /// </summary>
    public static bool TentarConverter(string? codigo, out TratamentoDaFaltaDeConsolidacao tratamento)
    {
        switch (codigo)
        {
            case NovoCorretor:
                tratamento = TratamentoDaFaltaDeConsolidacao.NovoCorretor;
                return true;
            case DecisaoDaBanca:
                tratamento = TratamentoDaFaltaDeConsolidacao.DecisaoDaBanca;
                return true;
            default:
                tratamento = default;
                return false;
        }
    }

    /// <summary>Converte o enum de volta ao token.</summary>
    /// <exception cref="ArgumentOutOfRangeException">O valor não é declarável.</exception>
    public static string ToCodigo(this TratamentoDaFaltaDeConsolidacao tratamento) => tratamento switch
    {
        TratamentoDaFaltaDeConsolidacao.NovoCorretor => NovoCorretor,
        TratamentoDaFaltaDeConsolidacao.DecisaoDaBanca => DecisaoDaBanca,
        _ => throw new ArgumentOutOfRangeException(nameof(tratamento), tratamento, "TratamentoDaFaltaDeConsolidacao desconhecido."),
    };
}
