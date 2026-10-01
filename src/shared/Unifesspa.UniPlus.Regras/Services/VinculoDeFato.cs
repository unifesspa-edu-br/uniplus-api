namespace Unifesspa.UniPlus.Regras.Services;

/// <summary>
/// O vínculo de um fato do catálogo diz como o valor dele é produzido (ADR-0116, ADR-0136): o
/// prefixo nomeia o mecanismo, e o sufixo, o campo, a regra ou o atributo. Um lugar só para os
/// prefixos, lidos pela Configuração, que os grava, e pela Seleção, que decide por eles.
/// </summary>
public static class VinculoDeFato
{
    /// <summary>Respondido pelo candidato num campo de formulário — o único vínculo coletável.</summary>
    public const string CampoDoFormulario = "CAMPO_INSCRICAO";

    /// <summary>Calculado pelo sistema de atributos do candidato, como a faixa etária.</summary>
    public const string AtributoDoCandidato = "ATRIBUTO_CANDIDATO";

    /// <summary>Derivado pela regra declarada no processo, como a modalidade de concorrência.</summary>
    public const string RegraDeDerivacao = "REGRA_DERIVACAO";

    /// <summary>Produzido pela classificação, como o grupo em que o candidato foi convocado.</summary>
    public const string Classificacao = "CLASSIFICACAO";

    /// <summary>Recebido de integração externa.</summary>
    public const string Integracao = "INTEGRACAO";

    /// <summary>O vínculo do mecanismo para o código dado.</summary>
    public static string De(string prefixo, string codigo) => $"{prefixo}:{codigo}";

    /// <summary>Se o vínculo é do mecanismo dado e nomeia o que o produz.</summary>
    public static bool Usa(string binding, string prefixo)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(prefixo);
        return binding.Length > prefixo.Length + 1
            && binding.StartsWith(prefixo, StringComparison.Ordinal)
            && binding[prefixo.Length] == ':';
    }
}
