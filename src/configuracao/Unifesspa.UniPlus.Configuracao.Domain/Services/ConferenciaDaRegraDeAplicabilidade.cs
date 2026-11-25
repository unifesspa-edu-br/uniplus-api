namespace Unifesspa.UniPlus.Configuracao.Domain.Services;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Confere a regra de aplicabilidade de uma modalidade contra o catálogo de fatos (ADR-0144). A regra
/// cita só fato que o predicado sabe avaliar, com operador e valor do domínio dele; só fato conhecido
/// até a inscrição, porque a modalidade se decide nela; e nada que dependa da própria
/// modalidade. Desativar um fato ou um valor recusa só a citação nova. As recusas se acumulam
/// (ADR-0125), todas no campo da regra.
/// </summary>
public static class ConferenciaDaRegraDeAplicabilidade
{
    private const string Campo = "regraDeAplicabilidade";

    public static IReadOnlyList<FieldError> Conferir(PredicadoDnf regra, PredicadoDnf? vigente, CatalogoDeFatos catalogo)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentNullException.ThrowIfNull(catalogo);

        return ConferirRegra(regra, vigente, catalogo);
    }

    private static List<FieldError> ConferirRegra(PredicadoDnf regra, PredicadoDnf? vigente, CatalogoDeFatos catalogo)
    {
        Dictionary<string, FatoCandidato> fatos = catalogo.Fatos.ToDictionary(static f => f.Codigo, StringComparer.Ordinal);
        Dictionary<string, DescritorFatoCandidato> vocabulario = VocabularioDoCatalogo.Descritores(fatos.Values);
        VinculosDeFatos existentes = Vinculos(vigente);

        List<FieldError> erros = [];
        void Recusar(string codigo, string mensagem, string? citado = null) =>
            erros.Add(new(Campo, new DomainError(codigo, mensagem), citado ?? Campo));

        ConferirPredicado(regra, catalogo, fatos, vocabulario, existentes, erros, Recusar);
        ConferirValores(regra, fatos, existentes, Recusar);
        ConferirCiclo(regra, fatos.Values, Recusar);
        return erros;
    }

    private static void ConferirPredicado(
        PredicadoDnf regra, CatalogoDeFatos catalogo, Dictionary<string, FatoCandidato> fatos,
        Dictionary<string, DescritorFatoCandidato> vocabulario, VinculosDeFatos existentes,
        List<FieldError> erros, Action<string, string, string?> recusar)
    {
        if (!ConferirFatos(regra, catalogo, fatos, vocabulario, existentes, recusar)
            && PredicadoDnfValidador.ValidarComCausa(regra, vocabulario, dominiosDinamicos: VocabularioDoCatalogo.DominiosDinamicos(fatos.Values))
                is ({ IsFailure: true } predicado, { } causa, _))
        {
            erros.Add(new(Campo, predicado.Error!, causa));
        }
    }

    private static void ConferirCiclo(PredicadoDnf regra, IEnumerable<FatoCandidato> fatos, Action<string, string, string?> recusar)
    {
        if (CicloComAModalidade(regra, fatos) is { } ciclo)
        {
            recusar(ModalidadeErrorCodes.RegraDependeDaModalidade,
                $"A regra depende da própria modalidade de concorrência: {string.Join(" → ", ciclo)}.", null);
        }
    }

    private static bool ConferirFatos(
        PredicadoDnf regra, CatalogoDeFatos catalogo, Dictionary<string, FatoCandidato> fatos,
        Dictionary<string, DescritorFatoCandidato> vocabulario, VinculosDeFatos existentes,
        Action<string, string, string?> recusar)
    {
        bool citaForaDoVocabulario = false;
        foreach (string citado in regra.FatosCitados)
        {
            if (fatos.TryGetValue(citado, out FatoCandidato? fato))
            {
                bool foraDoVocabulario = !vocabulario.ContainsKey(citado);
                ConferirFato(fato, foraDoVocabulario, existentes, catalogo.Precedencias, recusar);
                citaForaDoVocabulario |= foraDoVocabulario;
            }
        }

        return citaForaDoVocabulario;
    }

    private static void ConferirFato(
        FatoCandidato fato, bool foraDoVocabulario, VinculosDeFatos existentes,
        IReadOnlyCollection<PrecedenciaFase> precedencias, Action<string, string, string?> recusar)
    {
        string citado = fato.Codigo;
        if (foraDoVocabulario)
        {
            recusar(ModalidadeErrorCodes.RegraCitaFatoNaoCitavel,
                "O fato não pode ser citado em regra: texto, data, endereço, fato de membro de grupo e categórico sem valores não entram em predicado.", citado);
        }

        ConferirDisponibilidade(fato, existentes, precedencias, recusar);
    }

    private static void ConferirDisponibilidade(
        FatoCandidato fato, VinculosDeFatos existentes, IReadOnlyCollection<PrecedenciaFase> precedencias,
        Action<string, string, string?> recusar)
    {
        string citado = fato.Codigo;
        if (!fato.Ativo && !existentes.Fatos.Contains(citado))
        {
            recusar(ModalidadeErrorCodes.RegraCitaFatoDesativado, "O fato está desativado e não aceita citação nova.", citado);
        }

        if (!ConhecidoAteAInscricao(fato.PontoResolucao, precedencias))
        {
            recusar(ModalidadeErrorCodes.RegraCitaFatoPosteriorAInscricao,
                $"O fato só é conhecido em {fato.PontoResolucao}, que não antecede a inscrição, em que a modalidade se decide.", citado);
        }
    }

    private static void ConferirValores(
        PredicadoDnf regra, Dictionary<string, FatoCandidato> fatos, VinculosDeFatos existentes,
        Action<string, string, string?> recusar)
    {
        IEnumerable<(string Fato, string Valor)> valoresDesativados = Vinculos(regra).Valores
            .Where(v => !existentes.Valores.Contains(v) && ValidadorRegrasPadrao.ValorDesativado(fatos, v.Fato, v.Valor))
            .OrderBy(static v => v.Fato, StringComparer.Ordinal).ThenBy(static v => v.Valor, StringComparer.Ordinal);
        foreach ((string fato, string valor) in valoresDesativados)
        {
            recusar(ModalidadeErrorCodes.RegraCitaValorDesativado, "O valor do fato está desativado e não aceita citação nova.", $"{fato}.{valor}");
        }
    }

    private static bool ConhecidoAteAInscricao(string ponto, IReadOnlyCollection<PrecedenciaFase> precedencias) =>
        string.Equals(ponto, FaseCanonicaCatalogo.CodigoInscricao, StringComparison.Ordinal)
        || ValidadorRegrasPadrao.Precede(ponto, FaseCanonicaCatalogo.CodigoInscricao, precedencias);

    /// <summary>
    /// A modalidade é a união das regras das modalidades ofertadas, então a regra que cita a modalidade,
    /// ou um derivado cujas regras padrão chegam a ela, nunca resolveria.
    /// </summary>
    private static IReadOnlyList<string>? CicloComAModalidade(PredicadoDnf regra, IEnumerable<FatoCandidato> fatos)
    {
        Dictionary<string, IReadOnlyCollection<string>> citacoes = new(StringComparer.Ordinal);
        foreach (FatoCandidato fato in fatos.Where(static f => f.RegrasPadrao.Count > 0 && f.Codigo != ModalidadeDoCandidato.Fato))
        {
            citacoes[fato.Codigo] = [.. fato.RegrasPadrao.SelectMany(static r => r.FatosCitados).Distinct(StringComparer.Ordinal)];
        }

        citacoes[ModalidadeDoCandidato.Fato] = regra.FatosCitados;
        return GrafoDeFatos.DetectarCiclo(citacoes);
    }

    private static VinculosDeFatos Vinculos(PredicadoDnf? predicado) =>
        VinculosDeFatos.De(
            [],
            (predicado?.Clausulas ?? []).SelectMany(static c => c.Condicoes).Select(static c => (c.Fato, c.Valor)));
}
