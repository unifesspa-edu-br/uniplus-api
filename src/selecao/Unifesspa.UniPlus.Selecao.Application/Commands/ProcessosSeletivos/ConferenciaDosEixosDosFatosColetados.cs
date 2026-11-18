namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Compara o que a versão nova vai congelar de um fato já coletado com o que as versões publicadas
/// congelaram (UNI-REQ-0144): os itens e campos de grupo, o metadado dos fatos citados por condição de documento exigido e a
/// operação de cada agregado, que o handler resolve do catálogo na hora de congelar. A comparação é sempre contra o que a versão nova
/// congelaria, nunca contra o catálogo vivo: edição do catálogo que a versão nova não congela não
/// recusa a retificação.
/// </summary>
internal static class ConferenciaDosEixosDosFatosColetados
{
    /// <summary>O que a versão reidratada congelou, no formato que a comparação usa.</summary>
    public static EixosDeVersaoPublicada DaVersao(EnvelopeReidratado versao)
    {
        ArgumentNullException.ThrowIfNull(versao);
        return Congelado(versao.MetadadosFatosCongelados, versao.AgregadosDosGrupos);
    }

    /// <summary>Uma causa por item trocado, na ordem recebida, depois por metadado e por operação de agregado.</summary>
    public static IReadOnlyList<FieldError> Conferir(
        FatosDasVersoesPublicadas publicadas,
        IEnumerable<ItemNoDocumento> itens,
        IReadOnlyDictionary<string, MetadadoFatoCongelado>? metadados,
        IReadOnlyList<DefinicaoAgregado> agregados)
    {
        ArgumentNullException.ThrowIfNull(publicadas);
        ArgumentNullException.ThrowIfNull(itens);
        ArgumentNullException.ThrowIfNull(agregados);

        EixosDeVersaoPublicada novo = Congelado(metadados, agregados);
        return
        [
            .. RecusasDosItens(publicadas.EixosDosItens, itens),
            .. publicadas.EixosCongeladosTrocados(novo.Metadados, novo.OperacaoDosAgregados).Select(static troca => new FieldError(null, troca.ParaErro())),
        ];
    }

    private static IEnumerable<FieldError> RecusasDosItens(
        IReadOnlyDictionary<string, IReadOnlyCollection<EixosDoItemCongelado>> publicados, IEnumerable<ItemNoDocumento> itens)
    {
        foreach (ItemNoDocumento atual in itens)
        {
            if (publicados.TryGetValue(atual.Item.FatoCodigo, out IReadOnlyCollection<EixosDoItemCongelado>? eixos)
                && eixos.Select(publicado => publicado.PrimeiraTroca(atual.Item.FatoCodigo, EixosDoItemCongelado.De(atual.Item)))
                    .OfType<TrocaDeEixo>().FirstOrDefault() is { } troca)
            {
                yield return new FieldError(atual.Caminho, troca.ParaErro());
            }
        }
    }

    private static EixosDeVersaoPublicada Congelado(
        IReadOnlyDictionary<string, MetadadoFatoCongelado>? metadados, IReadOnlyList<DefinicaoAgregado> agregados) =>
        new(
            (metadados ?? new Dictionary<string, MetadadoFatoCongelado>()).ToDictionary(
                static par => par.Key,
                static par => new EixosDoMetadadoCongelado(par.Value.Dominio, par.Value.Cardinalidade, par.Value.PontoResolucao),
                StringComparer.Ordinal),
            agregados.ToDictionary(static a => a.Codigo, static a => AgregadoDeGrupo.ParaToken(a.Operacao), StringComparer.Ordinal));
}
