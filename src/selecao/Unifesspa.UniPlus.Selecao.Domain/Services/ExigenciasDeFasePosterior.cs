namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Entities;

using Enums;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

using ValueObjects;

/// <summary>
/// As exigências que, na fase da inscrição, ainda não se decidem porque dependem de um fato que só se
/// conhece depois dela (UNI-REQ-0077, UNI-REQ-0147). Um exemplo é a modalidade da convocação, que só
/// existe depois do resultado. O resolvedor da árvore as recebe como bloqueadas: não são exigidas nem
/// dispensadas.
/// </summary>
/// <remarks>
/// Na fase da inscrição se conhece o que os formulários das fases até ela perguntam, o que se deriva
/// só disso e o que chega de fora como fato conhecido. O resto é de fase posterior: o campo de um
/// formulário de fase posterior, o grupo dele e o fato que o processo não pergunta nem deriva.
/// </remarks>
public static class ExigenciasDeFasePosterior
{
    /// <param name="fatosConhecidos">Os fatos que a avaliação já recebe conhecidos, como os pressupostos.</param>
    public static IReadOnlySet<Guid> Na(ConfiguracaoAvaliavel configuracao, IReadOnlyDictionary<string, FatoResolvido> fatosConhecidos)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(fatosConhecidos);

        if (FinalidadesAteAInscricao(configuracao) is not { } finalidades)
        {
            return new HashSet<Guid>();
        }

        ProducaoDasEtapas ateAInscricao = ProducaoDasEtapas.De(
            configuracao.Definicao,
            configuracao.Definicao.Etapas.Where(e => finalidades.Any(f => DefinicaoDoProcesso.EhDaFinalidade(e.Codigo, f))));

        bool Conhecido(string fato) =>
            ateAInscricao.Fechamento([fato]).Pressupostos.All(p =>
                fatosConhecidos.ContainsKey(p)
                || (DerivadosDoSistema.Dependencias.TryGetValue(p, out IReadOnlyList<string>? dependencias)
                    && dependencias.All(ateAInscricao.Produzidos.Contains)));

        HashSet<Guid> bloqueadas = [];
        foreach (NoExigencia raiz in configuracao.Arvore.Raizes)
        {
            Coletar(raiz, grupoRepetido: null);
        }

        return bloqueadas;

        void Coletar(NoExigencia no, string? grupoRepetido)
        {
            string? repetido = no.RepetePorEntidade ?? grupoRepetido;
            if (no.Tipo != TipoNo.Folha)
            {
                foreach (NoExigencia filho in no.Filhos)
                {
                    Coletar(filho, repetido);
                }

                return;
            }

            DocumentoExigido documento = no.DocumentoExigido!;
            bool doGrupoConhecido = repetido is null || ateAInscricao.Grupos.Contains(repetido);
            if (!doGrupoConhecido || documento.Condicoes.Select(static c => c.Fato).Any(f => !Conhecido(f)))
            {
                bloqueadas.Add(documento.Id);
            }
        }
    }

    /// <summary>
    /// As finalidades cujo formulário pergunta numa fase até a da inscrição; nulo quando o processo não
    /// tem formulário de inscrição com fase, e então não há fase da inscrição de que partir.
    /// </summary>
    private static List<FinalidadeFormulario>? FinalidadesAteAInscricao(ConfiguracaoAvaliavel configuracao)
    {
        Dictionary<Guid, int> ordemDaFase = configuracao.Fases.ToDictionary(static f => f.Id, static f => f.Ordem);
        if (configuracao.Formularios.FirstOrDefault(static f => f.Finalidade == FinalidadeFormulario.Inscricao) is not { FaseId: { } faseDaInscricao }
            || !ordemDaFase.TryGetValue(faseDaInscricao, out int ordemDaInscricao))
        {
            return null;
        }

        return [.. configuracao.Formularios
            .Where(f => f.FaseId is { } fase && ordemDaFase.TryGetValue(fase, out int ordem) && ordem <= ordemDaInscricao)
            .Select(static f => f.Finalidade)];
    }
}
