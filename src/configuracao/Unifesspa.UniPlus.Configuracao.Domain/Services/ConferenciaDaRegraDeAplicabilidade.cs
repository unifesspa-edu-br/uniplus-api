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
/// (ADR-0125), apontando a condição ou a regra inteira.
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
        void Recusar(string campo, string codigo, string mensagem, string origem) =>
            erros.Add(new(campo, new DomainError(codigo, mensagem), origem));

        bool fora = ConferirFatos(regra, catalogo, fatos, vocabulario, existentes, Recusar);
        if (!fora)
            ConferirPredicado(regra, fatos, vocabulario, Recusar);
        ConferirValores(regra, fatos, existentes, Recusar);
        ConferirCiclo(regra, fatos.Values, Recusar);
        return erros;
    }

    private static IEnumerable<(string Caminho, CondicaoDnf Condicao)> CondicoesNoPedido(PredicadoDnf regra) =>
        regra.Clausulas.SelectMany((clausula, c) => clausula.Condicoes.Select((condicao, i) => ($"{Campo}[{c}][{i}]", condicao)));

    private static void ConferirPredicado(
        PredicadoDnf regra, Dictionary<string, FatoCandidato> fatos,
        Dictionary<string, DescritorFatoCandidato> vocabulario, Action<string, string, string, string> recusar)
    {
        if (PredicadoDnfValidador.ValidarComCausa(regra, vocabulario, dominiosDinamicos: VocabularioDoCatalogo.DominiosDinamicos(fatos.Values))
            is ({ IsFailure: true } predicado, { } causa, _))
        {
            recusar(predicado.Errors[0].NoCaminhoDe(Campo).Field!, predicado.Error!.Code, predicado.Error.Message, causa);
        }
    }

    private static void ConferirCiclo(PredicadoDnf regra, IEnumerable<FatoCandidato> fatos, Action<string, string, string, string> recusar)
    {
        if (CicloComAModalidade(regra, fatos) is { } ciclo)
        {
            recusar(Campo, ModalidadeErrorCodes.RegraDependeDaModalidade,
                $"A regra depende da própria modalidade de concorrência: {string.Join(" → ", ciclo)}.", Campo);
        }
    }

    private static bool ConferirFatos(
        PredicadoDnf regra, CatalogoDeFatos catalogo, Dictionary<string, FatoCandidato> fatos,
        Dictionary<string, DescritorFatoCandidato> vocabulario, VinculosDeFatos existentes,
        Action<string, string, string, string> recusar)
    {
        bool fora = false;
        foreach ((string caminho, CondicaoDnf condicao) in CondicoesNoPedido(regra))
        {
            if (!fatos.TryGetValue(condicao.Fato, out FatoCandidato? fato))
                continue;
            bool naoCitavel = !vocabulario.ContainsKey(condicao.Fato);
            ConferirFato($"{caminho}.fato", fato, naoCitavel, existentes, catalogo.Precedencias, recusar);
            fora |= naoCitavel;
        }
        return fora;
    }

    private static void ConferirFato(
        string campo, FatoCandidato fato, bool naoCitavel, VinculosDeFatos existentes,
        IReadOnlyCollection<PrecedenciaFase> precedencias, Action<string, string, string, string> recusar)
    {
        if (naoCitavel)
            recusar(campo, ModalidadeErrorCodes.RegraCitaFatoNaoCitavel,
                "O fato não pode ser citado em regra: texto, data, endereço, fato de membro de grupo e categórico sem valores não entram em predicado.", fato.Codigo);
        if (!fato.Ativo && !existentes.Fatos.Contains(fato.Codigo))
            recusar(campo, ModalidadeErrorCodes.RegraCitaFatoDesativado, "O fato está desativado e não aceita citação nova.", fato.Codigo);
        if (!ConhecidoAteAInscricao(fato.PontoResolucao, precedencias))
            recusar(campo, ModalidadeErrorCodes.RegraCitaFatoPosteriorAInscricao,
                $"O fato só é conhecido em {fato.PontoResolucao}, que não antecede a inscrição, em que a modalidade se decide.", fato.Codigo);
    }

    private static void ConferirValores(
        PredicadoDnf regra, Dictionary<string, FatoCandidato> fatos, VinculosDeFatos existentes,
        Action<string, string, string, string> recusar)
    {
        foreach ((string caminho, CondicaoDnf condicao) in CondicoesNoPedido(regra))
        {
            string[] valores = [.. VinculosDeFatos.De([], [(condicao.Fato, condicao.Valor)]).Valores
                .Where(v => !existentes.Valores.Contains(v) && ValidadorRegrasPadrao.ValorDesativado(fatos, v.Fato, v.Valor))
                .Select(static v => v.Valor).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
            if (valores.Length > 0)
                recusar($"{caminho}.valor", ModalidadeErrorCodes.RegraCitaValorDesativado,
                    "O valor do fato está desativado e não aceita citação nova.", $"{condicao.Fato}.{string.Join(",", valores)}");
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
