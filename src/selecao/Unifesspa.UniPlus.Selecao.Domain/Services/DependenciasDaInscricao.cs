namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using System.Text.Json;

using Entities;

using Enums;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

using ValueObjects;

internal static class DependenciasDaInscricao
{
    /// <summary>
    /// Os campos e os grupos da inscrição de que dependem os fatos citados: os do candidato pelo
    /// fechamento das derivações e das condições de exibição, os de membro pelo grupo que os repete, e o
    /// grupo pelo qual as regras de comprovação se repetem, com o que decide se ele aparece e é exigido.
    /// </summary>
    internal static DependenciasDaAvaliacao DependenciasDe(DefinicaoFormulario definicao, CitacoesDosGatilhos citacoes)
    {
        FechamentoDasDependencias fechamento = Fechamento(definicao, AlvosDoFechamento(definicao, citacoes));
        HashSet<string> camposDaInscricao = CamposDaInscricao(definicao);
        HashSet<string> gruposDaInscricao = GruposDaInscricao(definicao);
        DefinicaoAgregado[] agregados = [.. definicao.Agregados
            .Where(a => fechamento.Agregados.Contains(a.Codigo) && gruposDaInscricao.Contains(a.GrupoCodigo))];
        (string Grupo, string Fato)[] deMembro = [.. citacoes.DeMembro.Where(m => gruposDaInscricao.Contains(m.Grupo))];
        string[] repetidos = [.. citacoes.Repetidos.Where(gruposDaInscricao.Contains)];

        return new DependenciasDaAvaliacao(
            FatosDasDependencias(fechamento, camposDaInscricao, agregados, deMembro),
            GruposDasDependencias(agregados, deMembro, repetidos));
    }

    private static string[] GruposDasDependencias(
        DefinicaoAgregado[] agregados, (string Grupo, string Fato)[] deMembro, string[] repetidos) =>
        [.. agregados.Select(static a => a.GrupoCodigo).Concat(deMembro.Select(static m => m.Grupo))
            .Concat(repetidos).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    private static HashSet<string> CamposDaInscricao(DefinicaoFormulario definicao) =>
        new(AvaliacaoDaInscricao.PaginasDaInscricao(definicao).SelectMany(static e => e.Itens).Select(static i => i.FatoCodigo), StringComparer.Ordinal);

    private static HashSet<string> GruposDaInscricao(DefinicaoFormulario definicao) =>
        new(AvaliacaoDaInscricao.PaginasDaInscricao(definicao).SelectMany(static e => e.Grupos).Select(static g => g.Codigo), StringComparer.Ordinal);

    private static string[] FatosDasDependencias(
        FechamentoDasDependencias fechamento, HashSet<string> camposDaInscricao,
        DefinicaoAgregado[] agregados, (string Grupo, string Fato)[] deMembro) =>
            [.. CamposAlcancados(fechamento)
                .Where(camposDaInscricao.Contains)
                .Concat(agregados.Select(static a => $"{a.GrupoCodigo}.{a.FatoDeMembro}"))
                .Concat(deMembro.Select(static m => $"{m.Grupo}.{m.Fato}"))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)];

    /// <summary>
    /// De onde parte o conjunto de dependências das regras de documento: os fatos do candidato que os gatilhos citam
    /// e o que decide se aparecem os grupos pelos quais as regras de comprovação se repetem.
    /// </summary>
    internal static IEnumerable<string> AlvosDoFechamento(DefinicaoFormulario definicao, CitacoesDosGatilhos citacoes)
    {
        Dictionary<string, IReadOnlyCollection<string>> condicoesDoGrupo = CondicoesDosGrupos(definicao);
        return citacoes.DoCandidato.Concat(citacoes.Repetidos.SelectMany(g => condicoesDoGrupo.GetValueOrDefault(g) ?? []));
    }

    /// <summary>
    /// Dependências das regras de documento: os fatos do candidato que os gatilhos citam; os de membro — os subitens
    /// do grupo pelo qual as regras se repetem, que o gatilho lê de cada ocorrência —; e os grupos pelos
    /// quais essas regras de documento se repetem, cujas ocorrências dizem a quem se exige.
    /// </summary>
    internal sealed record CitacoesDosGatilhos(
        IReadOnlyCollection<string> DoCandidato,
        IReadOnlyCollection<(string Grupo, string Fato)> DeMembro,
        IReadOnlyCollection<string> Repetidos);

    /// <summary>
    /// O que os fatos alcançam: as dependências das derivações e, de cada campo e agregado alcançado,
    /// o que decide se ele aparece e é exigido — a resposta que esconde o campo também muda a
    /// avaliação, pois a resposta escondida não é considerada.
    /// </summary>
    internal static FechamentoDasDependencias Fechamento(DefinicaoFormulario definicao, IEnumerable<string> citados)
    {
        Dictionary<string, IReadOnlyCollection<string>> condicoes = new(StringComparer.Ordinal);
        Dictionary<string, IReadOnlyCollection<string>> condicoesDoGrupo = CondicoesDosGrupos(definicao);
        PreencherCondicoes(definicao, condicoes, condicoesDoGrupo);

        return FechamentoDasDependencias.De(
            citados,
            definicao.Paginas.SelectMany(static e => e.Itens).Select(static i => i.FatoCodigo).ToHashSet(StringComparer.Ordinal),
            definicao.Derivacoes.ToDictionary(static d => d.CodigoFato, StringComparer.Ordinal),
            definicao.Agregados.ToDictionary(static a => a.Codigo, StringComparer.Ordinal),
            condicoes);
    }

    private static void PreencherCondicoes(
        DefinicaoFormulario definicao, Dictionary<string, IReadOnlyCollection<string>> condicoes,
        Dictionary<string, IReadOnlyCollection<string>> condicoesDoGrupo)
    {
        foreach (DefinicaoPagina pagina in definicao.Paginas)
        {
            IReadOnlyCollection<string> daSecao = pagina.Exibicao?.FatosCitados ?? [];
            foreach (DefinicaoItem item in pagina.Itens)
            {
                condicoes[item.FatoCodigo] = [.. daSecao, .. item.FatosCitados];
            }
        }

        foreach (DefinicaoAgregado agregado in definicao.Agregados)
        {
            condicoes[agregado.Codigo] = condicoesDoGrupo.GetValueOrDefault(agregado.GrupoCodigo) ?? [];
        }
    }

    /// <summary>
    /// Do que cada grupo depende para aparecer e ser exigido: a exibição da seção dele e as condições de
    /// exibição e de obrigatoriedade do próprio grupo.
    /// </summary>
    private static Dictionary<string, IReadOnlyCollection<string>> CondicoesDosGrupos(DefinicaoFormulario definicao) =>
        definicao.Paginas
            .SelectMany(static e => e.Grupos.Select(g => (Grupo: g, DaSecao: e.Exibicao?.FatosCitados ?? [])))
            .ToDictionary(
                static g => g.Grupo.Codigo,
                static g => (IReadOnlyCollection<string>)[.. g.DaSecao, .. g.Grupo.FatosDoCandidatoCitados],
                StringComparer.Ordinal);

    /// <summary>Os campos perguntados que o fechamento alcança, direto ou pelo derivado do sistema que se calcula deles.</summary>
    private static IEnumerable<string> CamposAlcancados(FechamentoDasDependencias fechamento) =>
        fechamento.Perguntados.Concat(fechamento.Pressupostos
            .Where(DerivadosDoSistema.Dependencias.ContainsKey)
            .SelectMany(static p => DerivadosDoSistema.Dependencias[p]));

}
