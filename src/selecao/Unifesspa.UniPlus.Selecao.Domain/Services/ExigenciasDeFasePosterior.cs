namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Entities;

using Enums;


using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

using ValueObjects;

/// <summary>
/// As exigências que, na fase da inscrição, ainda não se decidem porque dependem de um fato que só se
/// conhece depois dela (UNI-REQ-0077, UNI-REQ-0147). Um exemplo é a modalidade da convocação, que só
/// existe depois do resultado. A avaliação da comprovação as recebe como bloqueadas: não são exigidas nem
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

        ConhecimentoAteAInscricao conhecimento = ConhecimentoAteAInscricao.De(configuracao, finalidades, fatosConhecidos);

        return configuracao.RegrasDeComprovacao.RegrasSemGrupo.SelectMany(regra => Bloqueadas(regra, grupoRepetido: null, conhecimento)).ToHashSet();
    }

    // Uma exigência depende da fase posterior se seu grupo ou condição ainda não são conhecidos.
    private static IEnumerable<Guid> Bloqueadas(RegraDeComprovacao regra, string? grupoRepetido, ConhecimentoAteAInscricao conhecimento)
    {
        string? repetido = regra.GrupoDoFormularioParaRepeticao ?? grupoRepetido;
        if (regra.Forma != FormaDeComprovacao.Documento)
        {
            return regra.Itens.SelectMany(item => Bloqueadas(item, repetido, conhecimento));
        }

        DocumentoExigido documento = regra.DocumentoExigido!;
        bool bloqueada = !conhecimento.ConheceOGrupo(repetido) || documento.Condicoes.Any(c => !conhecimento.ConheceOFato(c.Fato));

        return bloqueada ? [documento.Id] : [];
    }

    /// <summary>
    /// As finalidades cujo formulário pergunta numa fase até a da inscrição; nulo quando o processo não
    /// tem formulário de inscrição com fase, e então não há fase da inscrição de que partir.
    /// </summary>
    private static List<FinalidadeFormulario>? FinalidadesAteAInscricao(ConfiguracaoAvaliavel configuracao)
    {
        Dictionary<Guid, FaseCronograma> fasesPorId = configuracao.Fases.ToDictionary(static f => f.Id);
        if (FaseDaInscricao(configuracao, fasesPorId) is not { } faseDaInscricao)
        {
            return null;
        }

        OrdemDoCronograma ordem = OrdemDoCronograma.Do(configuracao.Fases);

        return [.. configuracao.Formularios
            .Where(f => f.FaseId is { } fase && fasesPorId.TryGetValue(fase, out FaseCronograma? faseDoFormulario) && !ordem.Posterior(faseDoFormulario, faseDaInscricao))
            .Select(static f => f.Finalidade)];
    }

    private static FaseCronograma? FaseDaInscricao(ConfiguracaoAvaliavel configuracao, Dictionary<Guid, FaseCronograma> fasesPorId) =>
        configuracao.Formularios.FirstOrDefault(static f => f.Finalidade == FinalidadeFormulario.Inscricao) is { FaseId: { } faseId }
            && fasesPorId.TryGetValue(faseId, out FaseCronograma? fase)
                ? fase
                : null;
}
