namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class RegrasDasExigencias
{
    internal static Result<MudancaDaConfiguracao> Planejar(ConfiguracaoDeclarada configuracao, IReadOnlyList<NoExigencia> raizes)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        List<NoExigencia> todosOsNos = [.. raizes.SelectMany(static raiz => raiz.AchatarComDescendentes())];
        List<DocumentoExigido> folhas = [.. todosOsNos
            .Where(static no => no.Tipo == TipoNo.Folha)
            .Select(static no => no.DocumentoExigido!)];

        // Onde cada documento é coletado (fase do cronograma e, opcionalmente, a etapa dela) é
        // conferido por um serviço, porque a configuração congelada volta por um caminho próprio que
        // monta o mesmo grafo sem passar por aqui.
        DomainError? recusa =
            ValidadorVinculoDaExigencia.PrimeiroVinculoInvalido(folhas, configuracao.Cronograma.CronogramaFases, configuracao.Avaliacao.Etapas)
            ?? ConferirRepeticaoPorGrupo(configuracao, todosOsNos)
            ?? folhas.Select(folha => RecusaDaIdadeMaximaDeEmissao(configuracao, folha)).FirstOrDefault(static r => r is not null)
            ?? RecusaDaPendenciaDeReenvio(configuracao, todosOsNos);
        return recusa is null
            ? Result<MudancaDaConfiguracao>.Success(MudancaDaTroca(configuracao, todosOsNos, folhas))
            : Result<MudancaDaConfiguracao>.Failure(recusa);
    }

    private static MudancaDaConfiguracao MudancaDaTroca(
        ConfiguracaoDeclarada configuracao, IReadOnlyList<NoExigencia> todosOsNos, IReadOnlyList<DocumentoExigido> folhas) => new()
        {
            DocumentosExigidos = Troca.Substituindo(configuracao.Exigencias.DocumentosExigidos, folhas),
            NosExigencia = Troca.Substituindo(configuracao.Exigencias.NosExigencia, todosOsNos),
        };

    // A âncora de fase da idade máxima de emissão é conferida na escrita, e não na publicação: a regra
    // vive na exigência, que está sendo escrita agora. Fase viva do mesmo processo com o extremo
    // correspondente definido, sem fallback silencioso.
    private static DomainError? RecusaDaIdadeMaximaDeEmissao(ConfiguracaoDeclarada configuracao, DocumentoExigido folha)
    {
        if (folha.IdadeMaximaEmissao is { ReferenciaFaseId: { } referenciaFaseId } idadeComFase)
        {
            return RecusaDaFaseAncora(configuracao, referenciaFaseId, idadeComFase);
        }

        // O fim da inscrição não usa ReferenciaFaseId (a âncora é a fase que coleta inscrição do
        // próprio cronograma), então a conferência acima nunca o valida. A pergunta é existencial: o
        // domínio admite mais de uma fase de coleta, e basta que alguma tenha fim.
        if (folha.IdadeMaximaEmissao is not { ReferenciaTipo: ReferenciaTipoIdadeEmissao.FimInscricao })
        {
            return null;
        }

        if (!configuracao.Cronograma.CronogramaFases.Any(f => f.ColetaInscricao))
        {
            return new DomainError(
                "IdadeMaximaEmissao.FaseNaoPertenceAoProcesso",
                "FIM_INSCRICAO exige uma fase do cronograma com ColetaInscricao, e o processo não tem nenhuma.");
        }

        return configuracao.Cronograma.CronogramaFases.Any(f => f.ColetaInscricao && f.Fim is not null)
            ? null
            : new DomainError(
                "IdadeMaximaEmissao.FaseExtremoAusente",
                "Nenhuma fase que coleta inscrição tem Fim definido — FIM_INSCRICAO não pode ser resolvido, sem fallback silencioso.");
    }

    private static DomainError? RecusaDaFaseAncora(ConfiguracaoDeclarada configuracao, Guid referenciaFaseId, IdadeMaximaEmissao idade)
    {
        FaseCronograma? faseAncora = configuracao.Cronograma.CronogramaFases.FirstOrDefault(f => f.Id == referenciaFaseId);
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
    private static DomainError? RecusaDaPendenciaDeReenvio(ConfiguracaoDeclarada configuracao, List<NoExigencia> todosOsNos)
    {
        foreach (NoExigencia no in todosOsNos)
        {
            string? consequenciaDoNo = no.Tipo == TipoNo.Folha
                ? no.DocumentoExigido!.ConsequenciaIndeferimento
                : no.Consequencia;
            if (consequenciaDoNo == "PENDENCIA_REENVIO"
                && no.FaseComum() is { } faseId
                && ConferirConsequenciaPendenciaReenvio(configuracao, faseId) is { } erro)
            {
                return erro;
            }
        }

        return null;
    }

    /// <summary>
    /// Gate <c>PENDENCIA_REENVIO</c>×<c>PermiteComplementacao</c> — mesmo gate
    /// para folha e grupo, forward (aqui, na escrita da exigência) e reverso (em
    /// <see cref="ProcessoSeletivo.DefinirCronogramaFases"/>, via <see cref="ExisteConsequenciaPendenciaReenvioNaFase"/>).
    /// </summary>
    internal static DomainError? ConferirConsequenciaPendenciaReenvio(ConfiguracaoDeclarada configuracao, Guid faseId)
    {
        FaseCronograma? fase = configuracao.Cronograma.CronogramaFases.FirstOrDefault(f => f.Id == faseId);
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

    /// <summary>Lado reverso do gate acima — usado por <see cref="ProcessoSeletivo.DefinirCronogramaFases"/> ao verificar se uma fase pode perder <c>PermiteComplementacao</c>, para folha OU grupo <c>OU</c>/<c>N-de</c>.</summary>
    internal static bool ExisteConsequenciaPendenciaReenvioNaFase(ConfiguracaoDeclarada configuracao, Guid faseId) =>
        configuracao.Exigencias.DocumentosExigidos.Any(d => d.ExigidoNaFaseId == faseId && d.ConsequenciaIndeferimento == "PENDENCIA_REENVIO")
        || configuracao.Exigencias.NosExigencia.Any(n => n.Tipo == TipoNo.GrupoOu && n.Consequencia == "PENDENCIA_REENVIO" && n.FaseComum() == faseId);

    /// <summary>
    /// A repetição por entidade nomeia um grupo do processo (<see cref="Services.ValidadorRepeticaoPorGrupo"/>)
    /// conhecido até a fase da subárvore que repete: a fase do formulário do grupo, pela mesma regra
    /// de fase e de formulário dos gatilhos, inclusive a do grupo coletado só na isenção. A fase que o
    /// catálogo dá a cada campo é conferida com os gatilhos, por quem lê o catálogo.
    /// </summary>
    internal static DomainError? ConferirRepeticaoPorGrupo(ConfiguracaoDeclarada configuracao, IReadOnlyCollection<NoExigencia> nos)
    {
        if (Services.ValidadorRepeticaoPorGrupo.PrimeiraSemGrupo(nos, configuracao.Coleta.GruposColetados) is { } semGrupo)
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

            GrupoColetado grupo = configuracao.Coleta.GruposColetados.Single(g => string.Equals(g.Codigo, no.RepetePorEntidade, StringComparison.Ordinal));
            if (grupo.Subitens
                    .Select(campo => FaseEfetivaDoFato.RecusaDeFaseDoGatilho(configuracao, campo.FatoCodigo, fase, finalidade, CitacoesDosFormularios.SemCatalogo, CitacoesDosFormularios.SemCatalogo))
                    .FirstOrDefault(static r => r is not null) is { } recusa)
            {
                return recusa;
            }
        }

        return null;
    }

    /// <summary>
    /// Os campos do grupo pelo qual a folha do documento se repete, subindo pelos ancestrais; vazio
    /// quando a folha não está numa subárvore repetida. São os fatos que o gatilho da folha cita além
    /// dos do candidato.
    /// </summary>
    public static IReadOnlySet<string> CamposDaRepeticao(ConfiguracaoDeclarada configuracao, Guid documentoExigidoId)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        NoExigencia? no = configuracao.Exigencias.NosExigencia.FirstOrDefault(n => n.DocumentoExigidoId == documentoExigidoId);
        while (no is { RepetePorEntidade: null, NoPaiId: { } paiId })
        {
            no = configuracao.Exigencias.NosExigencia.FirstOrDefault(n => n.Id == paiId);
        }

        return CamposDoGrupo(configuracao, no?.RepetePorEntidade);
    }

    /// <summary>Os fatos dos campos do grupo repetível de código dado; vazio quando o processo não o tem.</summary>
    public static IReadOnlySet<string> CamposDoGrupo(ConfiguracaoDeclarada configuracao, string? codigoDoGrupo)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        return new HashSet<string>(
                configuracao.Coleta.GruposColetados.Where(g => string.Equals(g.Codigo, codigoDoGrupo, StringComparison.Ordinal))
                    .SelectMany(static g => g.Subitens).Select(static s => s.FatoCodigo),
                StringComparer.Ordinal);
    }
}
