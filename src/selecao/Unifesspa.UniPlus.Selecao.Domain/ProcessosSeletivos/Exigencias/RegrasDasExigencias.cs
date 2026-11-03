namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class RegrasDasExigencias
{
    internal static Result<PlanoDasExigencias> Planejar(EstadoDaConfiguracao estado, IReadOnlyList<NoExigencia> raizes)
    {
        ArgumentNullException.ThrowIfNull(estado);

        List<NoExigencia> todosOsNos = [.. raizes.SelectMany(static raiz => raiz.AchatarComDescendentes())];
        List<DocumentoExigido> folhas = [.. todosOsNos
            .Where(static no => no.Tipo == TipoNo.Folha)
            .Select(static no => no.DocumentoExigido!)];

        // Onde cada documento é coletado (fase do cronograma e, opcionalmente, a etapa dela) é
        // conferido por um serviço, porque a configuração congelada volta por um caminho próprio que
        // monta o mesmo grafo sem passar por aqui.
        DomainError? recusa =
            ValidadorVinculoDaExigencia.PrimeiroVinculoInvalido(folhas, estado.CronogramaFases, estado.Etapas)
            ?? RecusaDaRepeticaoPorGrupo(estado, todosOsNos)
            ?? folhas.Select(folha => RecusaDaIdadeMaximaDeEmissao(estado, folha)).FirstOrDefault(static r => r is not null)
            ?? RecusaDaPendenciaDeReenvio(estado, todosOsNos);
        return recusa is null
            ? Result<PlanoDasExigencias>.Success(new PlanoDasExigencias(raizes, todosOsNos, folhas))
            : Result<PlanoDasExigencias>.Failure(recusa);
    }

    // A âncora de fase da idade máxima de emissão é conferida na escrita, e não na publicação: a regra
    // vive na exigência, que está sendo escrita agora. Fase viva do mesmo processo com o extremo
    // correspondente definido, sem fallback silencioso.
    private static DomainError? RecusaDaIdadeMaximaDeEmissao(EstadoDaConfiguracao estado, DocumentoExigido folha)
    {
        if (folha.IdadeMaximaEmissao is { ReferenciaFaseId: { } referenciaFaseId } idadeComFase)
        {
            return RecusaDaFaseAncora(estado, referenciaFaseId, idadeComFase);
        }

        // O fim da inscrição não usa ReferenciaFaseId (a âncora é a fase que coleta inscrição do
        // próprio cronograma), então a conferência acima nunca o valida. A pergunta é existencial: o
        // domínio admite mais de uma fase de coleta, e basta que alguma tenha fim.
        if (folha.IdadeMaximaEmissao is not { ReferenciaTipo: ReferenciaTipoIdadeEmissao.FimInscricao })
        {
            return null;
        }

        if (!estado.CronogramaFases.Any(f => f.ColetaInscricao))
        {
            return new DomainError(
                "IdadeMaximaEmissao.FaseNaoPertenceAoProcesso",
                "FIM_INSCRICAO exige uma fase do cronograma com ColetaInscricao, e o processo não tem nenhuma.");
        }

        return estado.CronogramaFases.Any(f => f.ColetaInscricao && f.Fim is not null)
            ? null
            : new DomainError(
                "IdadeMaximaEmissao.FaseExtremoAusente",
                "Nenhuma fase que coleta inscrição tem Fim definido — FIM_INSCRICAO não pode ser resolvido, sem fallback silencioso.");
    }

    private static DomainError? RecusaDaFaseAncora(EstadoDaConfiguracao estado, Guid referenciaFaseId, IdadeMaximaEmissao idade)
    {
        FaseCronograma? faseAncora = estado.CronogramaFases.FirstOrDefault(f => f.Id == referenciaFaseId);
        if (faseAncora is null)
        {
            return new DomainError(
                "IdadeMaximaEmissao.FaseNaoPertenceAoProcesso",
                $"A fase âncora {referenciaFaseId} da idade máxima de emissão não pertence ao cronograma deste processo.");
        }

        DateTimeOffset? extremo = idade.ReferenciaTipo == ReferenciaTipoIdadeEmissao.InicioFase ? faseAncora.Inicio : faseAncora.Fim;
        return extremo is null
            ? new DomainError(
                "IdadeMaximaEmissao.FaseExtremoAusente",
                $"A fase âncora da idade máxima de emissão não tem {(idade.ReferenciaTipo == ReferenciaTipoIdadeEmissao.InicioFase ? "Início" : "Fim")} definido — sem fallback silencioso.")
            : null;
    }

    // Consequência de reenvio exige que a fase permita complementação, para folha e para grupo; o
    // sentido inverso é conferido ao redefinir o cronograma.
    private static DomainError? RecusaDaPendenciaDeReenvio(EstadoDaConfiguracao estado, List<NoExigencia> todosOsNos)
    {
        foreach (NoExigencia no in todosOsNos)
        {
            string? consequenciaDoNo = no.Tipo == TipoNo.Folha
                ? no.DocumentoExigido!.ConsequenciaIndeferimento
                : no.Consequencia;
            if (consequenciaDoNo == "PENDENCIA_REENVIO"
                && no.FaseComum() is { } faseId
                && ValidarConsequenciaPendenciaReenvio(estado, faseId) is { } erro)
            {
                return erro;
            }
        }

        return null;
    }

    /// <summary>
    /// Gate <c>PENDENCIA_REENVIO</c>×<c>PermiteComplementacao</c> (Story #920) — mesmo gate
    /// para folha e grupo, forward (aqui, na escrita da exigência) e reverso (em
    /// <see cref="DefinirCronogramaFases"/>, via <see cref="ExisteConsequenciaPendenciaReenvioNaFase"/>).
    /// </summary>
    internal static DomainError? ValidarConsequenciaPendenciaReenvio(EstadoDaConfiguracao estado, Guid faseId)
    {
        FaseCronograma? fase = estado.CronogramaFases.FirstOrDefault(f => f.Id == faseId);
        if (fase is null)
        {
            // FaseNaoPertenceAoProcesso já cobre a ausência — defesa em profundidade, não
            // deveria ocorrer (a fase já foi validada acima nesta mesma chamada).
            return null;
        }

        return fase.PermiteComplementacao
            ? null
            : new DomainError(
                "DocumentoExigido.PendenciaReenvioExigeComplementacao",
                $"A fase '{fase.Codigo}' (ordem {fase.Ordem}) não permite complementação — consequência PENDENCIA_REENVIO exige PermiteComplementacao.");
    }

    /// <summary>Lado reverso do gate acima — usado por <see cref="DefinirCronogramaFases"/> ao verificar se uma fase pode perder <c>PermiteComplementacao</c>, para folha OU grupo <c>OU</c>/<c>N-de</c>.</summary>
    internal static bool ExisteConsequenciaPendenciaReenvioNaFase(EstadoDaConfiguracao estado, Guid faseId) =>
        estado.DocumentosExigidos.Any(d => d.ExigidoNaFaseId == faseId && d.ConsequenciaIndeferimento == "PENDENCIA_REENVIO")
        || estado.NosExigencia.Any(n => n.Tipo == TipoNo.GrupoOu && n.Consequencia == "PENDENCIA_REENVIO" && n.FaseComum() == faseId);

    /// <summary>
    /// A repetição por entidade nomeia um grupo do processo (<see cref="Services.ValidadorRepeticaoPorGrupo"/>)
    /// conhecido até a fase da subárvore que repete: a fase do formulário do grupo, pela mesma regra
    /// de fase e de formulário dos gatilhos, inclusive a do grupo coletado só na isenção. A fase que o
    /// catálogo dá a cada campo é conferida com os gatilhos, por quem lê o catálogo.
    /// </summary>
    internal static DomainError? RecusaDaRepeticaoPorGrupo(EstadoDaConfiguracao estado, IReadOnlyCollection<NoExigencia> nos)
    {
        if (Services.ValidadorRepeticaoPorGrupo.PrimeiraSemGrupo(nos, estado.GruposColetados) is { } semGrupo)
        {
            return semGrupo;
        }

        foreach (NoExigencia no in nos)
        {
            if (no.RepetePorEntidade is null
                || no.FaseComum() is not { } fase
                || !no.TentarFinalidadeComum(out FinalidadeFormulario? finalidade))
            {
                continue;
            }

            GrupoColetado grupo = estado.GruposColetados.Single(g => string.Equals(g.Codigo, no.RepetePorEntidade, StringComparison.Ordinal));
            if (grupo.Subitens
                    .Select(campo => FaseEfetivaDoFato.RecusaDeFaseDoGatilho(estado, campo.FatoCodigo, fase, finalidade, CitacoesDosFormularios.SemCatalogo, CitacoesDosFormularios.SemCatalogo))
                    .FirstOrDefault(static r => r is not null) is { } recusa)
            {
                return recusa;
            }
        }

        return null;
    }

    /// <summary>A repetição por entidade de exigência viva que deixou de valer: o grupo saiu ou mudou de formulário.</summary>
    internal static DomainError? PendenciaDaRepeticaoPorGrupo(EstadoDaConfiguracao estado) => RecusaDaRepeticaoPorGrupo(estado, estado.NosExigencia);

    /// <summary>
    /// Os campos do grupo pelo qual a folha do documento se repete, subindo pelos ancestrais; vazio
    /// quando a folha não está numa subárvore repetida. São os fatos que o gatilho da folha cita além
    /// dos do candidato.
    /// </summary>
    public static IReadOnlySet<string> CamposDaRepeticao(EstadoDaConfiguracao estado, Guid documentoExigidoId)
    {
        ArgumentNullException.ThrowIfNull(estado);

        NoExigencia? no = estado.NosExigencia.FirstOrDefault(n => n.DocumentoExigidoId == documentoExigidoId);
        while (no is { RepetePorEntidade: null, NoPaiId: { } paiId })
        {
            no = estado.NosExigencia.FirstOrDefault(n => n.Id == paiId);
        }

        return CamposDoGrupo(estado, no?.RepetePorEntidade);
    }

    /// <summary>Os fatos dos campos do grupo repetível de código dado; vazio quando o processo não o tem.</summary>
    public static IReadOnlySet<string> CamposDoGrupo(EstadoDaConfiguracao estado, string? codigoDoGrupo)
    {
        ArgumentNullException.ThrowIfNull(estado);

        return new HashSet<string>(
                estado.GruposColetados.Where(g => string.Equals(g.Codigo, codigoDoGrupo, StringComparison.Ordinal))
                    .SelectMany(static g => g.Subitens).Select(static s => s.FatoCodigo),
                StringComparer.Ordinal);
    }
}

/// <summary>As duas coleções que a troca da árvore de exigências reescreve: as folhas e os nós.</summary>
internal sealed record PlanoDasExigencias(
    IReadOnlyList<NoExigencia> Raizes,
    IReadOnlyList<NoExigencia> TodosOsNos,
    IReadOnlyList<DocumentoExigido> Folhas)
{
    internal void Aplicar(ColecoesDoProcesso colecoes, Guid processoId)
    {
        PartesDoProcesso.Substituir(colecoes.DocumentosExigidos, Folhas, processoId);

        colecoes.NosExigencia.Clear();
        foreach (NoExigencia raiz in Raizes)
        {
            ((IParteDoProcesso)raiz).VincularProcesso(processoId);
        }

        colecoes.NosExigencia.AddRange(TodosOsNos);
    }
}
