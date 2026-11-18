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
            NosExigencia = Troca.Retirando(NosDasExigencias(configuracao.Exigencias.NosExigencia, exigencias)),
        };
    }

    // O Id da fase, e não o código, porque é por Id que a exigência a referencia.
    private static List<DocumentoExigido> ExigenciasDasFases(ConfiguracaoDeclarada configuracao, HashSet<Guid> fases) =>
        [.. configuracao.Exigencias.DocumentosExigidos.Where(d => fases.Contains(d.ExigidoNaFaseId))];

    // A folha sai com o nó que a carregava; o grupo que ficou sem filho nenhum sai atrás, senão
    // sobraria um "um destes documentos" sem documento algum.
    private static List<NoExigencia> NosDasExigencias(IReadOnlyCollection<NoExigencia> nos, IReadOnlyList<DocumentoExigido> exigencias)
    {
        if (exigencias.Count == 0)
        {
            return [];
        }

        HashSet<Guid> removidas = [.. exigencias.Select(static d => d.Id)];
        List<NoExigencia> restantes = [.. nos.Where(no => no.DocumentoExigidoId is not { } id || !removidas.Contains(id))];
        while (RetirarGruposSemFilho(restantes) > 0)
        {
            // O grupo cujo filho era outro grupo que acabou de sair também fica vazio: repete até parar de encolher.
        }

        return [.. nos.Except(restantes)];
    }

    private static int RetirarGruposSemFilho(List<NoExigencia> nos)
    {
        HashSet<Guid> pais = [.. nos.Where(static no => no.NoPaiId is not null).Select(static no => no.NoPaiId!.Value)];
        return nos.RemoveAll(no => no.Tipo != TipoNo.Folha && !pais.Contains(no.Id));
    }
}
