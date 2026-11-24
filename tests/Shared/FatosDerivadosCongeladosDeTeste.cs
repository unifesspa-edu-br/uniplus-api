namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>Os fatos derivados que a publicação congela do catálogo, para os testes que não os discutem.</summary>
internal static class FatosDerivadosCongeladosDeTeste
{
    private const string Hipotese = "CUMPRIMENTO_OBRIGACAO_LEGAL";

    private const string Finalidade = "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.";

    public static IReadOnlyDictionary<string, FatoDerivadoCongelado> Padrao { get; } = new Dictionary<string, FatoDerivadoCongelado>(StringComparer.Ordinal)
    {
        ["FAIXA_ETARIA"] = new("FAIXA_ETARIA", "Faixa etária", "Faixa de idade do candidato.", null, TipoDoDado.Numerico, QuantidadeDeValores.ValorUnico, "PESSOAL", Hipotese, Finalidade),
        ["MODALIDADE"] = new("MODALIDADE", "Modalidade de concorrência", "Modalidades em que o candidato concorre.", null, TipoDoDado.Opcao, QuantidadeDeValores.VariosValores, "INTERNO", Hipotese, Finalidade),
    };
}
