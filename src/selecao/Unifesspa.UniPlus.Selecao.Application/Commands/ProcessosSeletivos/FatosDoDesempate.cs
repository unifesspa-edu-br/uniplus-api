namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Os fatos do candidato que um critério de desempate por predicado pode citar: o vocabulário fechado de fatos
/// (ADR-0111), contra o qual a condição é conferida, e o catálogo de onde ele veio, para a conferência dos vínculos novos.
/// </summary>
/// <param name="Vocabulario">Os descritores dos fatos de domínio conhecido no catálogo.</param>
/// <param name="Catalogo">Os fatos do catálogo, por código.</param>
internal sealed record FatosDoDesempate(
    IReadOnlyDictionary<string, DescritorFatoCandidato> Vocabulario,
    IReadOnlyDictionary<string, FatoCandidatoView> Catalogo)
{
    public static async Task<FatosDoDesempate> LerAsync(IFatoCandidatoReader fatoCandidatoReader, CancellationToken cancellationToken)
    {
        IReadOnlyList<FatoCandidatoView> fatos = await fatoCandidatoReader
            .ListarAsync(cancellationToken)
            .ConfigureAwait(false);

        return new(VocabularioDe(fatos), fatos.ToDictionary(static f => f.Codigo, StringComparer.Ordinal));
    }

    private static Dictionary<string, DescritorFatoCandidato> VocabularioDe(IReadOnlyList<FatoCandidatoView> fatos) =>
        fatos
            .Select(static fato => DescritorDe(fato))
            .OfType<DescritorFatoCandidato>()
            .ToDictionary(static descritor => descritor.Codigo, StringComparer.Ordinal);

    /// <summary>
    /// Um fato categórico de escopo-processo, com valores que só o processo conhece, não é representável no
    /// vocabulário fechado (ADR-0111): um predicado que o cite reprova como fato desconhecido.
    /// </summary>
    private static DescritorFatoCandidato? DescritorDe(FatoCandidatoView fato)
    {
        if (VocabularioDeFatos.Classificar(fato) is not { } tipo || tipo == TipoDominioFato.CategoricoDinamico)
        {
            return null;
        }

        Result<DescritorFatoCandidato> descritor = DescritorFatoCandidato.Criar(fato.Codigo, tipo, fato.ValoresDominio);
        return descritor.IsSuccess ? descritor.Value : null;
    }
}
