namespace Unifesspa.UniPlus.Configuracao.Domain.Services;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O catálogo contra o qual as regras padrão de um derivado são conferidas: os fatos, com os seus
/// valores e as regras padrão dos outros derivados, e as precedências entre fases.
/// </summary>
public sealed record CatalogoDeFatos(IReadOnlyCollection<FatoCandidato> Fatos, IReadOnlyCollection<PrecedenciaFase> Precedencias);

/// <summary>
/// Confere as regras padrão de um derivado por regra contra o catálogo (ADR-0136). Cada regra cita
/// só fato que o predicado sabe avaliar, com operador e valor do domínio dele; o derivado não resolve
/// antes das suas dependências nem protege menos que elas; o catálogo não fecha ciclo entre
/// derivados; e desativar um fato ou um valor recusa só a citação nova. As recusas se acumulam
/// (ADR-0125), cada uma no índice da regra que a causou.
/// </summary>
public static class ValidadorRegrasPadrao
{
    public static Result Validar(FatoCandidato derivado, IReadOnlyList<RegraDerivacao> regras, CatalogoDeFatos catalogo)
    {
        ArgumentNullException.ThrowIfNull(derivado);
        ArgumentNullException.ThrowIfNull(regras);
        ArgumentNullException.ThrowIfNull(catalogo);

        Dictionary<string, FatoCandidato> fatos = catalogo.Fatos.ToDictionary(static f => f.Codigo, StringComparer.Ordinal);
        Dictionary<string, DescritorFatoCandidato> vocabulario = VocabularioDoCatalogo.Descritores(fatos.Values);
        Dictionary<string, DominioDeValores> dominiosDinamicos = VocabularioDoCatalogo.DominiosDinamicos(fatos.Values);

        List<FieldError> erros = [];
        void Recusar(string campo, string codigo, string mensagem) => erros.Add(new(campo, new DomainError(codigo, mensagem)));

        for (int i = 0; i < regras.Count; i++)
        {
            string campo = $"regras[{i}]";
            bool citaForaDoVocabulario = false;
            foreach (string citado in regras[i].FatosCitados)
            {
                // O fato desconhecido é recusado pelo validador do predicado, e o próprio derivado,
                // pela recusa de autorreferência.
                if (!fatos.TryGetValue(citado, out FatoCandidato? dependencia)
                    || string.Equals(citado, derivado.Codigo, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!vocabulario.ContainsKey(citado))
                {
                    citaForaDoVocabulario = true;
                    Recusar(campo, FatoCandidatoErrorCodes.RegraCitaFatoNaoCitavel,
                        $"O fato '{citado}' não pode ser citado em regra: texto, data, endereço, fato de membro de grupo e categórico sem valores não entram em predicado.");
                }

                if (derivado.ClassificacaoProtecao < dependencia.ClassificacaoProtecao)
                {
                    Recusar(campo, FatoCandidatoErrorCodes.ClassificacaoAbaixoDaDependencia,
                        $"O derivado revela o que se sabe de '{citado}' e não pode ter proteção de dados mais fraca que a dele.");
                }

                if (Precede(derivado.PontoResolucao, dependencia.PontoResolucao, catalogo.Precedencias))
                {
                    Recusar(campo, FatoCandidatoErrorCodes.PontoResolucaoAnteriorADependencia,
                        $"O derivado resolve em {derivado.PontoResolucao}, antes de '{citado}', que só é conhecido em {dependencia.PontoResolucao}.");
                }
            }

            if (!citaForaDoVocabulario
                && PredicadoDnfValidador.Validar(regras[i].Quando, vocabulario, dominiosDinamicos: dominiosDinamicos) is { IsFailure: true } predicado)
            {
                erros.Add(new(campo, predicado.Error!));
            }
        }

        ConferirVinculosNovos(derivado, regras, fatos, Recusar);
        ConferirEstrutura(derivado, regras, Recusar);
        ConferirCiclo(derivado, regras, fatos.Values, Recusar);

        return erros.Count > 0 ? Result.ValidationFailure(erros) : Result.Success();
    }

    /// <summary>
    /// Desativar recusa só vínculo novo: a regra que já citava o fato ou o valor desativado, ou já
    /// contribuía o valor, continua aceita quando as regras são regravadas.
    /// </summary>
    private static void ConferirVinculosNovos(
        FatoCandidato derivado,
        IReadOnlyList<RegraDerivacao> regras,
        Dictionary<string, FatoCandidato> fatos,
        Action<string, string, string> recusar)
    {
        VinculosDeFatos existentes = Vinculos(derivado.Codigo, derivado.RegrasPadrao);

        for (int i = 0; i < regras.Count; i++)
        {
            string campo = $"regras[{i}]";
            VinculosDeFatos propostos = Vinculos(derivado.Codigo, [regras[i]]);

            IEnumerable<string> fatosDesativados = propostos.Fatos
                .Where(f => !existentes.Fatos.Contains(f) && fatos.TryGetValue(f, out FatoCandidato? citado) && !citado.Ativo)
                .Order(StringComparer.Ordinal);
            foreach (string fato in fatosDesativados)
            {
                recusar(campo, FatoCandidatoErrorCodes.RegraCitaFatoDesativado,
                    $"O fato '{fato}' está desativado e não aceita citação nova.");
            }

            IEnumerable<(string Fato, string Valor)> valoresDesativados = propostos.Valores
                .Where(v => !existentes.Valores.Contains(v) && ValorDesativado(fatos, v.Fato, v.Valor))
                .OrderBy(static v => v.Fato, StringComparer.Ordinal).ThenBy(static v => v.Valor, StringComparer.Ordinal);
            foreach ((string fato, string valor) in valoresDesativados)
            {
                recusar(campo, FatoCandidatoErrorCodes.RegraCitaValorDesativado,
                    $"O valor '{valor}' do fato '{fato}' está desativado e não aceita citação nova.");
            }
        }
    }

    private static bool ValorDesativado(Dictionary<string, FatoCandidato> fatos, string fato, string valor) =>
        fatos.TryGetValue(fato, out FatoCandidato? citado)
        && VocabularioDoCatalogo.ValoresDe(citado, fatos).Any(v => v.Codigo == valor && !v.Ativo);

    /// <summary>
    /// A forma de cada regra: a do booleano não contribui código, a do categórico contribui só os
    /// valores dele, e nenhuma cita o derivado.
    /// </summary>
    private static void ConferirEstrutura(
        FatoCandidato derivado, IReadOnlyList<RegraDerivacao> regras, Action<string, string, string> recusar)
    {
        string[] dominio = [.. derivado.ValoresDominioDeclarados.Select(static v => v.Codigo)];
        for (int i = 0; i < regras.Count; i++)
        {
            string[] citados = [.. regras[i].FatosCitados.Distinct(StringComparer.Ordinal)];
            Result<RegrasDerivacaoFato> estrutura = derivado.Dominio == DominioFato.Booleano
                ? RegrasDerivacaoFato.CriarBooleana(derivado.Codigo, [regras[i]], citados)
                : RegrasDerivacaoFato.Criar(derivado.Codigo, [regras[i]], citados, dominio);
            if (estrutura.IsFailure)
            {
                recusar($"regras[{i}]", estrutura.Error!.Code, estrutura.Error.Message);
            }
        }
    }

    /// <summary>
    /// O grafo das regras padrão do catálogo, com as novas no lugar das do derivado, não pode ter
    /// ciclo: um derivado nunca resolveria esperando por outro que espera por ele.
    /// </summary>
    private static void ConferirCiclo(
        FatoCandidato derivado,
        IReadOnlyList<RegraDerivacao> regras,
        IEnumerable<FatoCandidato> fatos,
        Action<string, string, string> recusar)
    {
        Dictionary<string, IReadOnlyCollection<string>> citacoes = new(StringComparer.Ordinal);
        foreach (FatoCandidato fato in fatos.Where(f => f.RegrasPadrao.Count > 0 && f.Codigo != derivado.Codigo))
        {
            citacoes[fato.Codigo] = Citados(fato.RegrasPadrao);
        }

        // A autorreferência é recusada pela estrutura; aqui fica o ciclo entre derivados distintos.
        citacoes[derivado.Codigo] = [.. Citados(regras).Where(c => c != derivado.Codigo)];

        if (GrafoDeFatos.DetectarCiclo(citacoes) is { } ciclo)
        {
            recusar("regras", FatoCandidatoErrorCodes.CicloEntreDerivados,
                $"As regras fecham um ciclo entre derivados: {string.Join(" → ", ciclo)}.");
        }
    }

    private static HashSet<string> Citados(IEnumerable<RegraDerivacao> regras) =>
        new(regras.SelectMany(static r => r.FatosCitados), StringComparer.Ordinal);

    private static VinculosDeFatos Vinculos(string derivado, IEnumerable<RegraDerivacao> regras)
    {
        RegraDerivacao[] lista = [.. regras];
        return VinculosDeFatos.De(
            [],
            lista.SelectMany(static r => r.Quando.Clausulas.SelectMany(static c => c.Condicoes)).Select(static c => (c.Fato, c.Valor)),
            lista.Where(static r => r.Contribui is not null).Select(r => (derivado, r.Contribui!)));
    }

    /// <summary>
    /// A fase <paramref name="fase"/> vem antes de <paramref name="outra"/> quando há caminho de
    /// precedência de uma à outra.
    /// </summary>
    internal static bool Precede(string fase, string outra, IReadOnlyCollection<PrecedenciaFase> precedencias)
    {
        if (string.Equals(fase, outra, StringComparison.Ordinal))
        {
            return false;
        }

        ILookup<string, string> sucessoras = precedencias.ToLookup(
            static p => p.AntecessoraCodigo, static p => p.SucessoraCodigo, StringComparer.Ordinal);
        HashSet<string> visitadas = new(StringComparer.Ordinal) { fase };
        Queue<string> fila = new([fase]);
        while (fila.TryDequeue(out string? atual))
        {
            foreach (string proxima in sucessoras[atual])
            {
                if (string.Equals(proxima, outra, StringComparison.Ordinal))
                {
                    return true;
                }

                if (visitadas.Add(proxima))
                {
                    fila.Enqueue(proxima);
                }
            }
        }

        return false;
    }
}
