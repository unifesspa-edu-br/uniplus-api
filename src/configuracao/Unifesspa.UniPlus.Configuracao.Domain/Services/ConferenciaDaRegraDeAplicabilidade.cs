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

        Dictionary<string, FatoCandidato> fatos = catalogo.Fatos.ToDictionary(static f => f.Codigo, StringComparer.Ordinal);
        Dictionary<string, DescritorFatoCandidato> vocabulario = VocabularioDoCatalogo.Descritores(fatos.Values);
        VinculosDeFatos existentes = Vinculos(vigente);

        List<FieldError> erros = [];
        void Recusar(string codigo, string mensagem) => erros.Add(new(Campo, new DomainError(codigo, mensagem)));

        bool citaForaDoVocabulario = false;
        foreach (string citado in regra.FatosCitados)
        {
            // O fato desconhecido é recusado pelo validador do predicado.
            if (!fatos.TryGetValue(citado, out FatoCandidato? fato))
            {
                continue;
            }

            if (!vocabulario.ContainsKey(citado))
            {
                citaForaDoVocabulario = true;
                Recusar(ModalidadeErrorCodes.RegraCitaFatoNaoCitavel,
                    $"O fato '{citado}' não pode ser citado em regra: texto, data, endereço, fato de membro de grupo e categórico sem valores não entram em predicado.");
            }

            if (!fato.Ativo && !existentes.Fatos.Contains(citado))
            {
                Recusar(ModalidadeErrorCodes.RegraCitaFatoDesativado, $"O fato '{citado}' está desativado e não aceita citação nova.");
            }

            // Fecha por padrão: a fase sem precedência declarada até a inscrição não conta como anterior.
            if (!ConhecidoAteAInscricao(fato.PontoResolucao, catalogo.Precedencias))
            {
                Recusar(ModalidadeErrorCodes.RegraCitaFatoPosteriorAInscricao,
                    $"O fato '{citado}' só é conhecido em {fato.PontoResolucao}, que não antecede a inscrição, em que a modalidade se decide.");
            }
        }

        if (!citaForaDoVocabulario
            && PredicadoDnfValidador.Validar(regra, vocabulario, dominiosDinamicos: VocabularioDoCatalogo.DominiosDinamicos(fatos.Values))
                is { IsFailure: true } predicado)
        {
            erros.Add(new(Campo, predicado.Error!));
        }

        IEnumerable<(string Fato, string Valor)> valoresDesativados = Vinculos(regra).Valores
            .Where(v => !existentes.Valores.Contains(v) && ValidadorRegrasPadrao.ValorDesativado(fatos, v.Fato, v.Valor))
            .OrderBy(static v => v.Fato, StringComparer.Ordinal).ThenBy(static v => v.Valor, StringComparer.Ordinal);
        foreach ((string fato, string valor) in valoresDesativados)
        {
            Recusar(ModalidadeErrorCodes.RegraCitaValorDesativado, $"O valor '{valor}' do fato '{fato}' está desativado e não aceita citação nova.");
        }

        if (CicloComAModalidade(regra, fatos.Values) is { } ciclo)
        {
            Recusar(ModalidadeErrorCodes.RegraDependeDaModalidade,
                $"A regra depende da própria modalidade de concorrência: {string.Join(" → ", ciclo)}.");
        }

        return erros;
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
