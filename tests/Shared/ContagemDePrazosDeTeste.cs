namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que o processo com janela de recurso precisa para a contagem dos prazos: o calendário de dias úteis vigente e a
/// convenção de contagem declarada.
/// </summary>
internal static class ContagemDePrazosDeTeste
{
    /// <summary>O ambiente com um calendário de dias úteis vigente e a zona institucional resolvida.</summary>
    internal static ContextoDeContagemDePrazos ComCalendarioVigente { get; } = new(
        CalendarioDiasUteisCongelado.Criar(
            Guid.CreateVersion7(),
            "2026",
            [DiaNaoUtilCongelado.Criar(new DateOnly(2026, 1, 1), "NACIONAL", null, null, null).Value!]).Value,
        FusoInstitucional: TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId));

    /// <summary>Declara a convenção que avança a data útil, a que a janela de recurso exige para publicar.</summary>
    internal static void DeclararConvencao(ProcessoSeletivo processo)
    {
        ArgumentNullException.ThrowIfNull(processo);

        Unifesspa.UniPlus.Kernel.Results.Result resultado = processo.DefinirAlgoritmoContagemPrazo(
            ReferenciaRegra.De(AlgoritmoContagemPrazoCodigo.AvancaDataUtil, "v1", new string('e', 64)));
        if (resultado.IsFailure)
        {
            throw new InvalidOperationException($"Fixture inválida: {resultado.Error?.Message}");
        }
    }

    /// <summary>Tira a convenção declarada, o estado de um processo cuja declaração ainda não chegou.</summary>
    internal static void RetirarConvencao(ProcessoSeletivo processo)
    {
        ArgumentNullException.ThrowIfNull(processo);

        typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.AlgoritmoContagemPrazo))!.SetValue(processo, null);
    }
}
