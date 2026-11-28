namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class RegrasDasExigencias
{
    private const string CampoDoReenvio = "indeferimento.abrePrazoParaReenvio";
    private const string CampoDaRepeticao = "grupoDoFormularioParaRepeticao";

    internal static Result<MudancaDaConfiguracao> Planejar(ConfiguracaoDeclarada configuracao, IReadOnlyList<RegraDeComprovacao> regrasSemGrupo)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        List<RegraDeComprovacao> todasAsRegras = [.. regrasSemGrupo.SelectMany(static regra => regra.EnumerarRegraEItens())];
        List<DocumentoExigido> documentos = [.. todasAsRegras
            .Where(static regra => regra.Forma == FormaDeComprovacao.Documento)
            .Select(static regra => regra.DocumentoExigido!)];

        IReadOnlyList<FieldError> recusas = Recusas(configuracao, todasAsRegras, documentos, CaminhosDasExigencias.DasRegras(regrasSemGrupo, "regrasDeComprovacao"));
        return recusas.Count == 0
            ? Result<MudancaDaConfiguracao>.Success(MudancaDaTroca(configuracao, todasAsRegras, documentos))
            : Result<MudancaDaConfiguracao>.ValidationFailure(recusas);
    }

    // Na ordem em que orientam: o vínculo com a fase e a etapa, o formulário, a repetição, as datas de referência (todas as recusas juntas) e o reenvio.
    private static IReadOnlyList<FieldError> Recusas(
        ConfiguracaoDeclarada configuracao, IReadOnlyList<RegraDeComprovacao> regras, IReadOnlyList<DocumentoExigido> documentos, IReadOnlyList<string> caminhosDasRegras)
    {
        IReadOnlyList<string> caminhos = CaminhosDasExigencias.DosDocumentos(regras, caminhosDasRegras);
        Func<IReadOnlyList<FieldError>>[] conferencias =
        [
            () => ValidadorVinculoDaExigencia.RecusasDosVinculos(documentos, caminhos, configuracao.Cronograma.CronogramaFases, configuracao.Avaliacao.Etapas),
            () => FormularioDasExigencias(configuracao, documentos, caminhos),
            () => RecusasDaRepeticaoPorGrupo(configuracao, regras, caminhosDasRegras),
            () => DatasDeReferenciaNoCronograma.ConferirConjunto(configuracao.Cronograma.CronogramaFases, documentos, [.. caminhos.Select(static caminho => caminho + ".requisitos")]),
            () => RecusasDaPendenciaDeReenvio(configuracao, regras, caminhosDasRegras),
        ];

        return conferencias.Select(static conferir => conferir()).FirstOrDefault(static recusas => recusas.Count > 0) ?? [];
    }

    /// <summary>
    /// A exigência cita um formulário que o processo tem, ligado à fase dela (ADR-0171). Todas as recusas saem
    /// juntas, apontando o formulário de cada exigência pelo caminho dela em <paramref name="caminhosDasExigencias"/>.
    /// </summary>
    internal static IReadOnlyList<FieldError> FormularioDasExigencias(
        ConfiguracaoDeclarada configuracao, IEnumerable<DocumentoExigido> documentos, IReadOnlyList<string> caminhosDasExigencias) =>
        ConferenciaDasExigenciasDoFormulario.Conferir(
            configuracao.Coleta.FormulariosDeclarados,
            [.. documentos.Select(ExigenciaDeDocumentoDoFormulario.De)],
            caminhosDasExigencias);

    private static MudancaDaConfiguracao MudancaDaTroca(
        ConfiguracaoDeclarada configuracao, IReadOnlyList<RegraDeComprovacao> todasAsRegras, IReadOnlyList<DocumentoExigido> documentos) => new()
        {
            DocumentosExigidos = Troca.Substituindo(configuracao.Exigencias.DocumentosExigidos, documentos),
            RegrasDeComprovacao = Troca.Substituindo(configuracao.Exigencias.RegrasDeComprovacao, todasAsRegras),
        };

    // Consequência de reenvio exige que a fase permita complementação, para regra de documento e para grupo; o
    // sentido inverso é conferido ao redefinir o cronograma.
    private static IReadOnlyList<FieldError> RecusasDaPendenciaDeReenvio(
        ConfiguracaoDeclarada configuracao, IReadOnlyList<RegraDeComprovacao> regras, IReadOnlyList<string> caminhosDasRegras) =>
        [.. regras.Select((regra, posicao) => RecusaDoReenvio(configuracao, regra, caminhosDasRegras[posicao])).OfType<FieldError>()];

    private static FieldError? RecusaDoReenvio(ConfiguracaoDeclarada configuracao, RegraDeComprovacao regra, string caminho)
    {
        bool ehDocumento = regra.Forma == FormaDeComprovacao.Documento;
        bool abrePrazo = ehDocumento ? regra.DocumentoExigido!.AbrePrazoParaReenvio : regra.AbrePrazoParaReenvio;
        return abrePrazo && regra.FaseComum() is { } faseId && ConferirConsequenciaPendenciaReenvio(configuracao, faseId) is { } erro
            ? new FieldError($"{(ehDocumento ? CaminhosDasExigencias.DoDocumento(caminho) : caminho)}.{CampoDoReenvio}", erro)
            : null;
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
                $"A fase {fase.Citacao} (ordem {fase.Ordem}) não permite complementação — a consequência que abre prazo para reenvio exige que a fase permita complementação.");
    }

    /// <summary>Verifica se uma fase possui consequência de reenvio declarada.</summary>
    internal static bool ExisteConsequenciaPendenciaReenvioNaFase(ConfiguracaoDeclarada configuracao, Guid faseId) =>
        configuracao.Exigencias.DocumentosExigidos.Any(d => d.ExigidoNaFaseId == faseId && d.AbrePrazoParaReenvio)
        || configuracao.Exigencias.RegrasDeComprovacao.Any(n => n.AbrePrazoParaReenvio && n.FaseComum() == faseId);

    /// <summary>
    /// A repetição nomeia um grupo do formulário do processo (<see cref="Services.ValidadorRepeticaoPorGrupo"/>)
    /// conhecido até a fase das regras repetidas: a fase do formulário do grupo, pela mesma regra
    /// de fase e de formulário dos gatilhos, inclusive a do grupo coletado só na isenção. A fase que o
    /// catálogo dá a cada campo é conferida com os gatilhos, por quem lê o catálogo. Saem primeiro as repetições
    /// de grupo inexistente, depois as dos campos de grupo que existe, cada uma pela posição da regra.
    /// </summary>
    internal static IReadOnlyList<FieldError> RecusasDaRepeticaoPorGrupo(
        ConfiguracaoDeclarada configuracao, IReadOnlyList<RegraDeComprovacao> regras, IReadOnlyList<string> caminhosDasRegras)
    {
        HashSet<string> existentes = new(configuracao.Coleta.GruposColetados.Select(static grupo => grupo.Codigo), StringComparer.Ordinal);
        IEnumerable<FieldError?> inexistentes = regras.Select((regra, posicao) =>
            NaRepeticao(caminhosDasRegras[posicao], Services.ValidadorRepeticaoPorGrupo.RecusarGrupoInexistente(regra.GrupoDoFormularioParaRepeticao, existentes)));
        IEnumerable<FieldError?> dosCampos = regras.Select((regra, posicao) =>
            NaRepeticao(caminhosDasRegras[posicao], RecusaDosFatosDaRepeticao(configuracao, regra)));

        return [.. inexistentes.Concat(dosCampos).OfType<FieldError>()];
    }

    private static FieldError? NaRepeticao(string caminho, DomainError? recusa) =>
        recusa is null ? null : new FieldError($"{caminho}.{CampoDaRepeticao}", recusa);

    private static DomainError? RecusaDosFatosDaRepeticao(ConfiguracaoDeclarada configuracao, RegraDeComprovacao regra)
    {
        if (regra.GrupoDoFormularioParaRepeticao is null || regra.FaseComum() is not { } fase
            || !regra.TentarFormularioComum(out CodigoDoFormulario? formulario)
            || configuracao.Coleta.GruposColetados.FirstOrDefault(g => string.Equals(g.Codigo, regra.GrupoDoFormularioParaRepeticao, StringComparison.Ordinal)) is not { } grupo)
        {
            return null;
        }

        return grupo.Subitens
            .Select(campo => FaseEfetivaDoFato.RecusaDeFaseDoGatilho(configuracao, campo.FatoCodigo, fase, formulario, CitacoesDosFormularios.SemCatalogo, CitacoesDosFormularios.SemCatalogo))
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
