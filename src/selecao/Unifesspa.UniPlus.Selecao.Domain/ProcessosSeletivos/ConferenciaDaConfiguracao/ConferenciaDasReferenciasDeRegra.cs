namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Confere cada referência guardada contra a regra vigente no catálogo.</summary>
internal static class ConferenciaDasReferenciasDeRegra
{
    internal static IReadOnlyList<FieldError> Conferir(
        IReadOnlyList<(string Campo, ReferenciaRegra Regra)> referencias,
        IReadOnlyDictionary<(string Codigo, string Versao), RegraDoCatalogoCitada> catalogo)
    {
        ArgumentNullException.ThrowIfNull(referencias);
        ArgumentNullException.ThrowIfNull(catalogo);

        return [.. referencias.Where(item => Diverge(item.Regra, catalogo))
            .Select(item => new FieldError(item.Campo, new DomainError(
                ProcessoSeletivoErrorCodes.RegraCitadaNaoCorrespondeAoCatalogo,
                "A regra citada não corresponde à regra atual do catálogo. Cite a regra novamente.")))];
    }

    internal static IReadOnlyList<FieldError> Conferir(ConfiguracaoDeclarada configuracao, CadastrosDaConfiguracao cadastros) =>
        [.. Conferir(ReferenciasDeRegraDaConfiguracao.De(configuracao), cadastros.Regras)
            .Select(erro => erro with { Error = new DomainError(erro.Error.Code, Mensagem(erro, configuracao, cadastros)) })];

    private static string Mensagem(FieldError erro, ConfiguracaoDeclarada configuracao, CadastrosDaConfiguracao cadastros) =>
        $"A regra citada {Localizacao(erro.Field!, configuracao, cadastros)} não corresponde à regra atual do catálogo. Cite a regra novamente.";

    private static string Localizacao(string campo, ConfiguracaoDeclarada configuracao, CadastrosDaConfiguracao cadastros)
    {
        string ponteiro = PonteiroNoDocumento.Converter(campo, configuracao.Origem)!;
        string[] partes = ponteiro.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (partes[0] == "etapas")
        {
            return LocalizacaoNaEtapa(campo, partes, configuracao);
        }

        if (partes[0] == "cronograma")
        {
            return LocalizacaoNaFase(campo, partes, configuracao, cadastros);
        }

        return LocalizacaoNosDemaisBlocos(partes);
    }

    private static string LocalizacaoNaFase(string campo, string[] partes, ConfiguracaoDeclarada configuracao, CadastrosDaConfiguracao cadastros)
    {
        int canonica = int.Parse(campo.AsSpan(6, campo.IndexOf(']', StringComparison.Ordinal) - 6), System.Globalization.CultureInfo.InvariantCulture);
        Guid id = configuracao.Cronograma.CronogramaFases[canonica].FaseCanonicaOrigemId;
        string nome = cadastros.NomesDasFases.TryGetValue(id, out string? valor) ? $" ({valor})" : string.Empty;
        return $"na {int.Parse(partes[2], System.Globalization.CultureInfo.InvariantCulture) + 1}ª fase{nome}, {Recurso(partes, 3)}";
    }

    private static string LocalizacaoNosDemaisBlocos(string[] partes) => partes[0] switch
    {
        "distribuicaoVagas" => $"na {int.Parse(partes[1], System.Globalization.CultureInfo.InvariantCulture) + 1}ª oferta de vagas, na regra de {(partes[2] == "regraAjuste" ? "ajuste" : "distribuição")}",
        "cascataRemanejamento" => "na cascata de remanejamento",
        "bonusRegional" => "no bônus regional",
        "classificacao" => LocalizacaoNaClassificacao(partes),
        _ => "na contagem de prazo",
    };

    private static string LocalizacaoNaEtapa(string campo, string[] partes, ConfiguracaoDeclarada configuracao)
    {
        int canonica = int.Parse(campo.AsSpan(7, campo.IndexOf(']', StringComparison.Ordinal) - 7), System.Globalization.CultureInfo.InvariantCulture);
        string etapa = $"na {int.Parse(partes[1], System.Globalization.CultureInfo.InvariantCulture) + 1}ª etapa ({configuracao.Avaliacao.Etapas[canonica].Nome})";
        return partes.Length == 3 ? $"{etapa}, na regra de consolidação" : $"{etapa}, {Recurso(partes, 2)}";
    }

    private static string Recurso(string[] partes, int inicio)
    {
        int procedimento = Array.IndexOf(partes, "procedimentosDeRecurso", inicio);
        string recurso = partes[inicio] == "recursos"
            ? $"no {int.Parse(partes[inicio + 1], System.Globalization.CultureInfo.InvariantCulture) + 1}º recurso"
            : "no recurso";
        return procedimento < 0 ? $"{recurso}, na regra de prazo" :
            $"{recurso}, no {int.Parse(partes[procedimento + 1], System.Globalization.CultureInfo.InvariantCulture) + 1}º procedimento";
    }

    private static string LocalizacaoNaClassificacao(string[] partes) => partes[1] switch
    {
        "regraCalculo" => "na regra de cálculo da classificação",
        "regraArredondamento" => "na regra de arredondamento da classificação",
        "regraOrdemAlocacao" => "na regra de ordem de alocação da classificação",
        "regrasEliminacao" => $"na {int.Parse(partes[2], System.Globalization.CultureInfo.InvariantCulture) + 1}ª regra de eliminação",
        "criteriosDeDesempate" => $"no {int.Parse(partes[2], System.Globalization.CultureInfo.InvariantCulture) + 1}º critério de desempate",
        _ => "na classificação",
    };

    private static bool Diverge(ReferenciaRegra referencia, IReadOnlyDictionary<(string Codigo, string Versao), RegraDoCatalogoCitada> catalogo) =>
        !catalogo.TryGetValue((referencia.Codigo, referencia.Versao), out RegraDoCatalogoCitada? regra)
        || !string.Equals(referencia.Hash, regra.Referencia.Hash, StringComparison.Ordinal);
}
