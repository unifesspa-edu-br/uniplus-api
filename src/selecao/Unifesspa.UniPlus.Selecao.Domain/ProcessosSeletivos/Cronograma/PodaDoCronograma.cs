namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que existia só por causa das fases que saem do cronograma. Sem a poda, a etapa ficava apontando
/// para uma fase que não existe mais e a gravação seguinte era recusada por
/// <c>EtapaSemFaseNoCronograma</c>: o operador removia uma fase e o passo travava.
/// </summary>
internal static class PodaDoCronograma
{
    internal static MudancaDaConfiguracao Planejar(ConfiguracaoDeclarada configuracao, IReadOnlyCollection<FaseCronograma> removidas)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        if (removidas.Count == 0)
        {
            return new MudancaDaConfiguracao();
        }

        HashSet<string> codigos = new(removidas.Select(static f => f.Codigo), StringComparer.Ordinal);
        IReadOnlyList<DocumentoExigido> exigencias = ExigenciasDasFases(configuracao, [.. removidas.Select(static f => f.Id)]);
        return new MudancaDaConfiguracao
        {
            Etapas = Troca.Retirando(configuracao.Avaliacao.Etapas.Where(e => e.FaseCodigo is { } codigo && codigos.Contains(codigo))),
            DocumentosExigidos = Troca.Retirando(exigencias),
            RegrasDeComprovacao = Troca.Retirando(RegrasDasExigencias(configuracao.Exigencias.RegrasDeComprovacao, exigencias)),
        };
    }

    // O Id da fase, e não o código, porque é por Id que a exigência a referencia.
    private static List<DocumentoExigido> ExigenciasDasFases(ConfiguracaoDeclarada configuracao, HashSet<Guid> fases) =>
        [.. configuracao.Exigencias.DocumentosExigidos.Where(d => fases.Contains(d.ExigidoNaFaseId))];

    // Um grupo sem itens não representa uma exigência válida.
    private static List<RegraDeComprovacao> RegrasDasExigencias(IReadOnlyCollection<RegraDeComprovacao> regras, IReadOnlyList<DocumentoExigido> exigencias)
    {
        if (exigencias.Count == 0)
        {
            return [];
        }

        HashSet<Guid> removidas = [.. exigencias.Select(static d => d.Id)];
        List<RegraDeComprovacao> restantes = [.. regras.Where(regra => regra.DocumentoExigidoId is not { } id || !removidas.Contains(id))];
        while (RetirarGruposSemItens(restantes) > 0)
        {
            // A remoção de um grupo pode deixar vazio o grupo que o contém.
        }

        return [.. regras.Except(restantes)];
    }

    private static int RetirarGruposSemItens(List<RegraDeComprovacao> regras)
    {
        HashSet<Guid> pais = [.. regras.Where(static regra => regra.RegraQueContemId is not null).Select(static regra => regra.RegraQueContemId!.Value)];
        return regras.RemoveAll(regra => regra.Forma != FormaDeComprovacao.Documento && !pais.Contains(regra.Id));
    }
}
