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
    internal static Result<MudancaDaConfiguracao> Planejar(ConfiguracaoDeclarada configuracao, IReadOnlyList<RegraDeComprovacao> regrasSemGrupo)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        List<RegraDeComprovacao> todasAsRegras = [.. regrasSemGrupo.SelectMany(static regra => regra.EnumerarRegraEItens())];
        List<DocumentoExigido> documentos = [.. todasAsRegras
            .Where(static regra => regra.Forma == FormaDeComprovacao.Documento)
            .Select(static regra => regra.DocumentoExigido!)];

        IReadOnlyList<FieldError> recusas = Recusas(configuracao, todasAsRegras, documentos);
        return recusas.Count == 0
            ? Result<MudancaDaConfiguracao>.Success(MudancaDaTroca(configuracao, todasAsRegras, documentos))
            : Result<MudancaDaConfiguracao>.ValidationFailure(recusas);
    }

    // Na ordem em que orientam: o vínculo e a repetição, as datas de referência (todas as recusas juntas) e o reenvio.
    private static IReadOnlyList<FieldError> Recusas(
        ConfiguracaoDeclarada configuracao, IReadOnlyList<RegraDeComprovacao> regras, IReadOnlyList<DocumentoExigido> documentos)
    {
        DomainError? vinculo = ValidadorVinculoDaExigencia.PrimeiroVinculoInvalido(documentos, configuracao.Cronograma.CronogramaFases, configuracao.Avaliacao.Etapas)
            ?? ConferirRepeticaoPorGrupo(configuracao, regras);
        if (vinculo is not null)
        {
            return [new FieldError(null, vinculo)];
        }

        IReadOnlyList<FieldError> datas = DatasDeReferenciaNoCronograma.ConferirConjunto(configuracao.Cronograma.CronogramaFases, documentos);
        if (datas.Count > 0)
        {
            return datas;
        }

        return RecusaDaPendenciaDeReenvio(configuracao, regras) is { } reenvio ? [new FieldError(null, reenvio)] : [];
    }

    private static MudancaDaConfiguracao MudancaDaTroca(
        ConfiguracaoDeclarada configuracao, IReadOnlyList<RegraDeComprovacao> todasAsRegras, IReadOnlyList<DocumentoExigido> documentos) => new()
        {
            DocumentosExigidos = Troca.Substituindo(configuracao.Exigencias.DocumentosExigidos, documentos),
            RegrasDeComprovacao = Troca.Substituindo(configuracao.Exigencias.RegrasDeComprovacao, todasAsRegras),
        };

    // Consequência de reenvio exige que a fase permita complementação, para regra de documento e para grupo; o
    // sentido inverso é conferido ao redefinir o cronograma.
    private static DomainError? RecusaDaPendenciaDeReenvio(ConfiguracaoDeclarada configuracao, IReadOnlyList<RegraDeComprovacao> todasAsRegras)
    {
        foreach (RegraDeComprovacao regra in todasAsRegras)
        {
            string? consequenciaDaRegra = regra.Forma == FormaDeComprovacao.Documento
                ? regra.DocumentoExigido!.ConsequenciaIndeferimento
                : regra.Consequencia;
            if (consequenciaDaRegra == "PENDENCIA_REENVIO"
                && regra.FaseComum() is { } faseId
                && ConferirConsequenciaPendenciaReenvio(configuracao, faseId) is { } erro)
            {
                return erro;
            }
        }

        return null;
    }

    /// <summary>
    /// A consequência de reenvio exige uma fase que permita complementação.
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

    /// <summary>Verifica se uma fase possui consequência de reenvio declarada.</summary>
    internal static bool ExisteConsequenciaPendenciaReenvioNaFase(ConfiguracaoDeclarada configuracao, Guid faseId) =>
        configuracao.Exigencias.DocumentosExigidos.Any(d => d.ExigidoNaFaseId == faseId && d.ConsequenciaIndeferimento == "PENDENCIA_REENVIO")
        || configuracao.Exigencias.RegrasDeComprovacao.Any(n => n.Forma == FormaDeComprovacao.QuantidadeMinimaDeItens && n.Consequencia == "PENDENCIA_REENVIO" && n.FaseComum() == faseId);

    /// <summary>
    /// A repetição nomeia um grupo do formulário do processo (<see cref="Services.ValidadorRepeticaoPorGrupo"/>)
    /// conhecido até a fase das regras repetidas: a fase do formulário do grupo, pela mesma regra
    /// de fase e de formulário dos gatilhos, inclusive a do grupo coletado só na isenção. A fase que o
    /// catálogo dá a cada campo é conferida com os gatilhos, por quem lê o catálogo.
    /// </summary>
    internal static DomainError? ConferirRepeticaoPorGrupo(ConfiguracaoDeclarada configuracao, IReadOnlyCollection<RegraDeComprovacao> regras)
    {
        if (Services.ValidadorRepeticaoPorGrupo.PrimeiraSemGrupo(regras, configuracao.Coleta.GruposColetados) is { } semGrupo)
        {
            return semGrupo;
        }

        foreach (RegraDeComprovacao regra in regras)
        {
            if (RecusaDosFatosDaRepeticao(configuracao, regra) is { } recusa)
            {
                return recusa;
            }
        }

        return null;
    }

    private static DomainError? RecusaDosFatosDaRepeticao(ConfiguracaoDeclarada configuracao, RegraDeComprovacao regra)
    {
        if (regra.GrupoDoFormularioParaRepeticao is null || regra.FaseComum() is not { } fase
            || !regra.TentarFinalidadeComum(out FinalidadeFormulario? finalidade))
        {
            return null;
        }

        GrupoColetado grupo = configuracao.Coleta.GruposColetados.Single(g => string.Equals(g.Codigo, regra.GrupoDoFormularioParaRepeticao, StringComparison.Ordinal));
        return grupo.Subitens
            .Select(campo => FaseEfetivaDoFato.RecusaDeFaseDoGatilho(configuracao, campo.FatoCodigo, fase, finalidade, CitacoesDosFormularios.SemCatalogo, CitacoesDosFormularios.SemCatalogo))
            .FirstOrDefault(static recusa => recusa is not null);
    }

    /// <summary>
    /// Os campos do grupo pelo qual a regra de documento se repete; vazio
    /// quando não há repetição. São os fatos que o gatilho da regra cita além
    /// dos do candidato.
    /// </summary>
    public static IReadOnlySet<string> CamposDaRepeticao(ConfiguracaoDeclarada configuracao, Guid documentoExigidoId)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        RegraDeComprovacao? regra = configuracao.Exigencias.RegrasDeComprovacao.FirstOrDefault(n => n.DocumentoExigidoId == documentoExigidoId);
        while (regra is { GrupoDoFormularioParaRepeticao: null, RegraQueContemId: { } paiId })
        {
            regra = configuracao.Exigencias.RegrasDeComprovacao.FirstOrDefault(n => n.Id == paiId);
        }

        return CamposDoGrupo(configuracao, regra?.GrupoDoFormularioParaRepeticao);
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
