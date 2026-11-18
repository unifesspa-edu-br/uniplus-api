namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.ValueObjects;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O que a versão congela dos fatos coletados: o metadado dos fatos citados, os valores que o candidato pode escolher, a
/// operação dos agregados e a definição avaliável. As conferências acumulam, na ordem: a coletabilidade, o
/// metadado, os valores, o eixo trocado e a definição. Cada fato coletado que o catálogo não deixa coletar é uma causa
/// própria, e as demais conferências seguem sobre os fatos que ainda se coletam. O eixo trocado se compara sobre o que
/// resolveu: o fato que o catálogo não tem ou não deixa coletar já é causa própria e sai da comparação, e os demais seguem
/// comparados. A definição, as conferências de gatilho e as de elegibilidade só se avaliam com todos os fatos coletáveis e os
/// valores resolvidos, porque pressupõem isso e repetiriam a causa.
/// </summary>
internal sealed record FatosConferidos(
    IReadOnlyDictionary<string, MetadadoFatoCongelado>? Metadados,
    IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>? Valores,
    IReadOnlyList<DefinicaoAgregado>? Agregados,
    DefinicaoAvaliavel? Avaliavel,
    IReadOnlyList<FieldError> Causas);

public static class ConferenciaDosFatosDaVersao
{
    internal static FatosConferidos Conferir(ProcessoSeletivo processo, LeiturasDoCongelamento lidos)
    {
        IReadOnlyList<ItemNoDocumento> naoColetaveis = ConferenciaDeColetabilidadeDeFatos.NaoColetaveis(processo, lidos.Catalogo.PorCodigo);
        HashSet<string> excluidos = [.. naoColetaveis.Select(static n => n.Item.FatoCodigo)];
        Resolucao resolucao = new(
            naoColetaveis.Count == 0
                ? ConferenciaDosFatosAoCongelar.Conferir(processo, lidos.Catalogo.PorCodigo, lidos.Recopiado.Publicadas)
                : ResolvedorMetadadosFatosCongelados.Resolver(processo, lidos.Catalogo.PorCodigo, excluidos),
            ResolvedorValoresSelecionaveisCongelados.Resolver(processo, lidos.Catalogo.PorCodigo, excluidos),
            excluidos);

        return Reunir(processo, lidos, naoColetaveis, resolucao);
    }

    private static FatosConferidos Reunir(
        ProcessoSeletivo processo, LeiturasDoCongelamento lidos, IReadOnlyList<ItemNoDocumento> naoColetaveis, Resolucao resolucao)
    {
        IReadOnlyList<DefinicaoAgregado> agregados = AgregadosDosFatosColetaveis(processo, lidos, resolucao.Excluidos);
        Result<DefinicaoAvaliavel>? avaliavel = naoColetaveis.Count == 0 && resolucao.Valores.IsSuccess
            ? DefinicaoAvaliavelDoProcesso.DaConfiguracaoViva(processo, lidos.Catalogo.Fatos)
            : null;
        IEnumerable<FieldError> eixo = EixoTrocado(processo, lidos, resolucao, agregados);

        return new FatosConferidos(
            resolucao.Metadados.Value, resolucao.Valores.Value, agregados, avaliavel?.Value, Causas(naoColetaveis, resolucao, eixo, avaliavel));
    }

    private static IReadOnlyList<FieldError> Causas(
        IReadOnlyList<ItemNoDocumento> naoColetaveis, Resolucao resolucao, IEnumerable<FieldError> eixo, Result<DefinicaoAvaliavel>? avaliavel) =>
    [
        .. ConferenciaDeColetabilidadeDeFatos.Recusas(naoColetaveis),
        .. resolucao.Metadados.Errors,
        .. resolucao.Valores.Errors,
        .. eixo,
        .. avaliavel?.Errors ?? [],
    ];

    private static IReadOnlyList<DefinicaoAgregado> AgregadosDosFatosColetaveis(
        ProcessoSeletivo processo, LeiturasDoCongelamento lidos, IReadOnlySet<string> excluidos) =>
        [.. DefinicaoAvaliavelDoProcesso.AgregadosDosGrupos(processo.GruposColetados, lidos.Catalogo.PorCodigo).Where(a => !excluidos.Contains(a.FatoDeMembro))];

    private sealed record Resolucao(
        Result<IReadOnlyDictionary<string, MetadadoFatoCongelado>?> Metadados,
        Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>> Valores,
        IReadOnlySet<string> Excluidos);

    // Compara os itens já recopiados, o metadado dos gatilhos e a operação dos agregados que a versão congelaria:
    // o fato já coletado não os troca (UNI-REQ-0144). Cada comparação roda sobre o que se pôde resolver: o
    // fato que o catálogo não tem ou não deixa coletar já é causa própria e fica fora, e os demais continuam comparados.
    private static IEnumerable<FieldError> EixoTrocado(
        ProcessoSeletivo processo,
        LeiturasDoCongelamento lidos,
        Resolucao resolucao,
        IReadOnlyList<DefinicaoAgregado> agregados) =>
        DaVersaoPublicada(lidos, ItensComparaveis(processo, resolucao), MetadadosComparaveis(processo, lidos, resolucao), agregados);

    private static IReadOnlyDictionary<string, MetadadoFatoCongelado>? MetadadosComparaveis(
        ProcessoSeletivo processo, LeiturasDoCongelamento lidos, Resolucao resolucao)
    {
        if (resolucao.Metadados.IsSuccess)
        {
            return resolucao.Metadados.Value;
        }

        HashSet<string> semOsAusentes = [.. resolucao.Excluidos, .. FatosCitadosAusentesDoCatalogo(processo, lidos)];

        return ResolvedorMetadadosFatosCongelados.Resolver(processo, lidos.Catalogo.PorCodigo, semOsAusentes).Value;
    }

    private static IEnumerable<string> FatosCitadosAusentesDoCatalogo(ProcessoSeletivo processo, LeiturasDoCongelamento lidos) =>
        processo.DocumentosExigidos
            .SelectMany(static d => d.Condicoes)
            .Select(static c => c.Fato)
            .Where(fato => !lidos.Catalogo.PorCodigo.ContainsKey(fato));

    // A declaração tirada da raiz tem origem identidade: a posição canônica é a posição do documento atual.
    private static IEnumerable<ItemNoDocumento> ItensComparaveis(ProcessoSeletivo processo, Resolucao resolucao) =>
        ItensDoFormularioNoDocumento.EmOrdemDoDocumento(processo).Where(item => !resolucao.Excluidos.Contains(item.Item.FatoCodigo));

    private static IReadOnlyList<FieldError> DaVersaoPublicada(
        LeiturasDoCongelamento lidos, IEnumerable<ItemNoDocumento> itens, IReadOnlyDictionary<string, MetadadoFatoCongelado>? metadados, IReadOnlyList<DefinicaoAgregado> agregados) =>
        lidos.Recopiado.Publicadas is { } publicadas
            ? ConferenciaDosEixosDosFatosColetados.Conferir(publicadas, itens, metadados, agregados)
            : [];
}
