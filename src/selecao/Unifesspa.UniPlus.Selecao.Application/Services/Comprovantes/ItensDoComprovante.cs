namespace Unifesspa.UniPlus.Selecao.Application.Services.Comprovantes;

using Unifesspa.UniPlus.Selecao.Application.DTOs.Administrativos;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;

/// <summary>
/// As respostas e os derivados do comprovante na forma do titular: a mesma resolução de rótulo, tipo e valor da consulta
/// administrativa, sem a classificação, a base legal nem a revelação, que são da leitura de quem opera o processo.
/// </summary>
internal static class ItensDoComprovante
{
    public static IReadOnlyList<SecaoDoComprovanteDto> Secoes(InsumosDoComprovante insumos, FatosDaVersao fatos)
    {
        Dictionary<string, string> titulos = TitulosDasOcorrencias.Dos(fatos.Grupos, insumos.Comprovante.Grupos);
        IReadOnlyList<SecaoDaInscricaoDto> secoes = ItensDaInscricao.Secoes(
            CandidatoDoComprovante.SemOsFatosDoNome(insumos.Comprovante.TodasAsRespostas()), insumos.Comprovante.Grupos, fatos, podeRevelar: false);

        return [.. secoes.Select(s => new SecaoDoComprovanteDto(s.Codigo, s.Titulo, Itens(s.Itens), [.. s.Grupos.Select(g => Grupo(g, titulos))]))];
    }

    public static IReadOnlyList<ItemDoComprovanteDto> Derivados(InsumosDoComprovante insumos, FatosDaVersao fatos) =>
        Itens(ItensDaInscricao.Derivados(insumos.Comprovante.Derivados, fatos));

    private static GrupoDoComprovanteDto Grupo(GrupoDaInscricaoDto grupo, Dictionary<string, string> titulos) =>
        new(
            grupo.Codigo,
            grupo.Rotulo,
            [.. grupo.Ocorrencias.Select(o => new OcorrenciaDoComprovanteDto(o.Id, titulos.GetValueOrDefault(o.Id, o.Id), Itens(o.Itens)))]);

    private static List<ItemDoComprovanteDto> Itens(IEnumerable<ItemDeRespostaDto> itens) =>
        [.. itens.Select(static i => new ItemDoComprovanteDto(i.Rotulo, i.Descricao, i.Tipo, i.Componente, i.Valor))];
}
