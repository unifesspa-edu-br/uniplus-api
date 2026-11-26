namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// De quem é cada campo, grupo e termo do processo, e quais formulários são os da inscrição (ADR-0171). O campo,
/// o grupo e o termo pertencem ao formulário que os liga a si pelo identificador; o formulário da inscrição é o
/// obrigatório ligado à fase do cronograma que coleta a inscrição, e a configuração não limita a quantidade deles.
/// </summary>
public static class PartesDoFormulario
{
    internal static IEnumerable<FatoColetado> CamposDo(this ColetaDeclarada coleta, FormularioProcesso formulario) =>
        coleta.Campos.Where(campo => campo.FormularioId == formulario.Id);

    /// <summary>Os campos de resposta única do formulário, sem os campos dos grupos repetíveis.</summary>
    internal static IEnumerable<FatoColetado> ItensDo(this ColetaDeclarada coleta, FormularioProcesso formulario) =>
        coleta.Itens.Where(item => item.FormularioId == formulario.Id);

    internal static IEnumerable<GrupoColetado> GruposDo(this ColetaDeclarada coleta, FormularioProcesso formulario) =>
        coleta.GruposColetados.Where(grupo => grupo.FormularioId == formulario.Id);

    internal static IEnumerable<TermoExigidoFormulario> TermosDo(this ColetaDeclarada coleta, FormularioProcesso formulario) =>
        coleta.TermosExigidos.Where(termo => termo.FormularioId == formulario.Id);

    /// <summary>O código do formulário que coleta o campo; nulo quando o campo ainda não está ligado a formulário do processo.</summary>
    internal static string? CodigoDoDono(this ColetaDeclarada coleta, FatoColetado campo) =>
        coleta.Formularios.FirstOrDefault(formulario => formulario.Id == campo.FormularioId)?.Codigo.Valor;

    /// <summary>As fases do cronograma, lidas como o autor as declarou.</summary>
    internal static CronogramaDoProcesso CronogramaLido(this ConfiguracaoDeclarada configuracao) =>
        CronogramaDoProcesso.Do(configuracao.Cronograma.CronogramaFases, configuracao.Origem);

    /// <summary>O que a estrutura das páginas confere do formulário: se ele serve à fase de inscrição e se é obrigatório.</summary>
    internal static DeclaracaoDoFormulario Estrutura(this ConfiguracaoDeclarada configuracao, FormularioDeclarado declarado) =>
        declarado.Estrutura(configuracao.CronogramaLido());

    /// <summary>Os formulários da inscrição que o processo tem, em ordem ordinal do código.</summary>
    public static IReadOnlyList<FormularioProcesso> DaInscricao(this ConfiguracaoDeclarada configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        return
        [
            .. FormulariosDaInscricao.De(configuracao.Coleta.FormulariosDeclarados, configuracao.Cronograma.CronogramaFases)
                .Select(declarado => configuracao.Coleta.FormularioDe(declarado.Codigo)!),
        ];
    }

    /// <summary>O formulário declarado é da inscrição: obrigatório e ligado à fase do cronograma que coleta a inscrição.</summary>
    public static bool EhDaInscricao(this ConfiguracaoDeclarada configuracao, FormularioDeclarado declarado)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(declarado);

        return FormulariosDaInscricao.De([declarado], configuracao.Cronograma.CronogramaFases).Count > 0;
    }

    internal static IReadOnlySet<Guid> IdsDaInscricao(this ConfiguracaoDeclarada configuracao) =>
        configuracao.DaInscricao().Select(static formulario => formulario.Id).ToHashSet();

    /// <summary>Os fatos que os itens dos formulários da inscrição coletam, que os demais formulários podem citar.</summary>
    internal static HashSet<string> FatosDaInscricao(this ConfiguracaoDeclarada configuracao) =>
        FatosDosFormularios(configuracao.Coleta.Itens, configuracao.IdsDaInscricao());

    /// <summary>Os fatos dos formulários da inscrição, sem os do formulário dado, que está sendo trocado.</summary>
    internal static HashSet<string> FatosDaInscricaoSem(this ConfiguracaoDeclarada configuracao, FormularioProcesso formulario) =>
        FatosDosFormularios(configuracao.Coleta.Itens, configuracao.IdsDaInscricao().Where(id => id != formulario.Id).ToHashSet());

    internal static HashSet<string> FatosDosFormularios(IEnumerable<FatoColetado> itens, IReadOnlySet<Guid> formularios) =>
        new(itens.Where(item => item.FormularioId is { } id && formularios.Contains(id)).Select(static item => item.FatoCodigo), StringComparer.Ordinal);
}
