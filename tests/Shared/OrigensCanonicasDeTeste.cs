namespace Unifesspa.UniPlus.Testes.Compartilhado;

using System.Globalization;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class OrigensCanonicasDeTeste
{
    private static readonly Dictionary<string, string> Nomes = new(StringComparer.Ordinal)
    {
        ["SELECAO"] = "Seleção",
        ["INSCRICAO"] = "Inscrição",
        ["SOLICITACAO_ISENCAO"] = "Solicitação de isenção",
        ["HOMOLOGACAO_INSCRICOES"] = "Homologação das inscrições",
        ["ENSALAMENTO"] = "Ensalamento",
        ["AVALIACAO"] = "Avaliação",
        ["ANALISE"] = "Análise",
        ["ANALISE_DOCUMENTAL"] = "Análise documental",
        ["PROVA_OBJETIVA"] = "Prova objetiva",
        ["CLASSIFICACAO"] = "Classificação",
        ["RESULTADO_FINAL"] = "Resultado final",
        ["HABILITACAO"] = "Habilitação",
        ["HETEROIDENTIFICACAO"] = "Heteroidentificação",
        ["AVALIACAO_BIOPSICOSSOCIAL"] = "Avaliação biopsicossocial",
        ["MATRICULA"] = "Matrícula",
        ["HOMOLOGACAO_RESULTADO_FINAL"] = "Homologação do resultado final",
        ["LISTA_ESPERA"] = "Lista de espera",
        ["CHAMADA"] = "Chamada",
    };

    internal static OrigemCanonicaDaFase ComNome(OrigemCanonicaDaFase origem) =>
        origem with { Nome = string.IsNullOrWhiteSpace(origem.Nome) ? NomeDoCodigo(origem.Codigo) : origem.Nome };

    private static string NomeDoCodigo(string codigo) =>
        string.IsNullOrWhiteSpace(codigo) ? "Fase de teste" :
        Nomes.TryGetValue(codigo, out string? nome) ? nome :
            CultureInfo.GetCultureInfo("pt-BR").TextInfo.ToTitleCase(codigo.Replace('_', ' ').ToLowerInvariant());
}
