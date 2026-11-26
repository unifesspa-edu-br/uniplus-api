namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As origens do fato lidas da configuração declarada: o ponto de resolução que o catálogo dá a cada fato, o formulário que coleta o
/// campo e as dependências declaradas pelo derivado, pela regra de derivação ou pelo agregado de grupo repetível.
/// </summary>
internal sealed record OrigensDoFatoNaConfiguracaoDeclarada(
    ConfiguracaoDeclarada Configuracao,
    IReadOnlyDictionary<string, string> PontoResolucaoPorFato,
    IReadOnlyDictionary<string, string> MembroPorAgregado) : IOrigensDoFato
{
    public IReadOnlyCollection<FaseCronograma> Cronograma => Configuracao.Cronograma.CronogramaFases;

    public string? CodigoDaFaseDoCatalogo(string fato) => PontoResolucaoPorFato.GetValueOrDefault(fato);

    public IEnumerable<FaseCronograma> FasesDosFormulariosQueColetam(string fato) =>
        Configuracao.Coleta.Campos.FirstOrDefault(campo => string.Equals(campo.FatoCodigo, fato, StringComparison.Ordinal)) is { } coletado
        && Configuracao.Coleta.Formularios.FirstOrDefault(formulario => formulario.Id == coletado.FormularioId)?.FaseId is { } faseDoFormulario
        && Cronograma.FirstOrDefault(fase => fase.Id == faseDoFormulario) is { } fase
            ? [fase]
            : [];

    public IEnumerable<string> Dependencias(string fato) =>
        FaseEfetivaDoFato.DependenciasDoFato(Configuracao, fato, MembroPorAgregado);
}
