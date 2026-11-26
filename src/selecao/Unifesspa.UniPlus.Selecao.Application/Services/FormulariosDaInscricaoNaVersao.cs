namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os formulários que a inscrição responde na versão publicada: os obrigatórios ligados a uma fase que coleta
/// inscrição, pelas marcas das fases que a versão congelou (ADR-0171).
/// </summary>
internal static class FormulariosDaInscricaoNaVersao
{
    /// <summary>Os formulários da inscrição, em ordem ordinal do código.</summary>
    public static IReadOnlyList<FormularioProcesso> De(ConfiguracaoPublicada configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        return [.. FormulariosDaInscricao.De(configuracao.Coleta.FormulariosDeclarados, configuracao.Cronograma.CronogramaFases)
            .Select(declarado => configuracao.Coleta.FormularioDe(declarado.Codigo)!)];
    }
}
