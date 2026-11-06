namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

/// <summary>
/// O que trocar o cronograma faz além de gravar as fases novas: atualiza as que continuam e poda o
/// que existia só por causa das que saem. Tudo foi calculado antes de qualquer mutação, e
/// <see cref="Aplicar"/> não falha.
/// </summary>
internal sealed record PlanoDoCronograma(
    IReadOnlyList<(FaseCronograma Existente, FaseCronograma Nova)> Retidas,
    IReadOnlyList<FaseCronograma> Resultantes,
    IReadOnlyList<string> CodigosRemovidos,
    IReadOnlySet<Guid> IdsRemovidos)
{
    internal void Aplicar(ColecoesDoProcesso colecoes, Guid processoId)
    {
        foreach ((FaseCronograma existente, FaseCronograma nova) in Retidas)
        {
            existente.AtualizarDe(nova, [.. nova.Produtos]);
        }

        PartesDoProcesso.Substituir(colecoes.CronogramaFases, Resultantes, processoId);
        PodarDependentes(colecoes);
    }

    // Sem a poda, a etapa ficava apontando para uma fase que não existe mais e a gravação seguinte era
    // recusada por EtapaSemFaseNoCronograma: o operador removia uma fase e o passo travava.
    private void PodarDependentes(ColecoesDoProcesso colecoes)
    {
        if (CodigosRemovidos.Count == 0)
        {
            return;
        }

        colecoes.Etapas.RemoveAll(e => e.FaseCodigo is { } codigo
            && CodigosRemovidos.Contains(codigo, StringComparer.Ordinal));
        PodarExigenciasDasFasesRemovidas(colecoes);
    }

    private void PodarExigenciasDasFasesRemovidas(ColecoesDoProcesso colecoes)
    {
        // O Id da fase, e não o código, porque é por Id que a exigência a referencia.
        HashSet<Guid> exigenciasRemovidas = [.. colecoes.DocumentosExigidos
            .Where(d => IdsRemovidos.Contains(d.ExigidoNaFaseId))
            .Select(static d => d.Id)];
        if (exigenciasRemovidas.Count == 0)
        {
            return;
        }

        colecoes.DocumentosExigidos.RemoveAll(d => exigenciasRemovidas.Contains(d.Id));

        // A folha sai com o nó que a carregava; o grupo que ficou sem filho nenhum sai atrás, senão
        // sobraria um "um destes documentos" sem documento algum.
        List<NoExigencia> nos = colecoes.NosExigencia;
        nos.RemoveAll(no => no.DocumentoExigidoId is { } id && exigenciasRemovidas.Contains(id));
        while (nos.RemoveAll(no => no.Tipo != TipoNo.Folha && !nos.Exists(filho => filho.NoPaiId == no.Id)) > 0)
        {
            // Um grupo pode ter ficado vazio porque o filho dele era outro grupo que acabou de sair:
            // repete até a floresta parar de encolher.
        }
    }
}
