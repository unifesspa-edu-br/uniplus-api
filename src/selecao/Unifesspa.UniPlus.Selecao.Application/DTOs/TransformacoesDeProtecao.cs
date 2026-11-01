namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// As transformações que autorizam um valor derivado de uma fonte mais protegida a valer pela classificação própria
/// (ADR-0153).
/// </summary>
public static class TransformacoesDeProtecao
{
    /// <summary>O CPF no padrão <c>***.999.999-**</c> (parecer DPO 002/2026).</summary>
    public const string MascaraDoCpf = "mascara-cpf";
}
