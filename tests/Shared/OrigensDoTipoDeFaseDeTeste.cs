namespace Unifesspa.UniPlus.Testes.Compartilhado;

using System.Globalization;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class OrigensDoTipoDeFaseDeTeste
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

    /// <summary>
    /// A origem do tipo de fase de uma fase de teste. O nome é o que o cadastro daria ao código (por exemplo, "Inscrição"
    /// para INSCRICAO), sempre diferente do código; <see cref="ComNome"/> o troca.
    /// </summary>
    internal static OrigemCanonicaDaFase Da(
        Guid origemId,
        string codigo,
        OrigemDataFase origemData,
        bool agrupaEtapas,
        bool permiteComplementacao,
        bool coletaInscricao,
        bool coletaSolicitacaoIsencao) =>
        new(origemId, codigo, origemData, agrupaEtapas, permiteComplementacao, coletaInscricao, coletaSolicitacaoIsencao)
        {
            Nome = NomeDoCodigo(codigo),
        };

    internal static OrigemCanonicaDaFase ComNome(this OrigemCanonicaDaFase origem, string nome) =>
        origem with { Nome = nome };

    private static string NomeDoCodigo(string codigo) =>
        string.IsNullOrWhiteSpace(codigo) ? "Fase de teste" :
        Nomes.TryGetValue(codigo, out string? nome) ? nome :
            CultureInfo.GetCultureInfo("pt-BR").TextInfo.ToTitleCase(codigo.Replace('_', ' ').ToLowerInvariant());
}
