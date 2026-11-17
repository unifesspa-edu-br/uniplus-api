namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O percurso do documento da configuração sobre os formulários do processo: por finalidade, os itens e depois os grupos
/// repetíveis com os seus campos, cada um com o caminho que o documento lhe dá (<c>formularios[{finalidade}].itens[i]</c>,
/// <c>formularios[{finalidade}].grupos[g].subitens[c]</c>). A posição é a da ordem dentro da finalidade, a mesma que o
/// documento congela, de modo que toda conferência que aponta um item usa o mesmo caminho.
/// </summary>
internal static class ItensDoFormularioNoDocumento
{
    public static IReadOnlyList<FormularioComOsSeusItens> De(ProcessoSeletivo processo)
    {
        ArgumentNullException.ThrowIfNull(processo);

        return [.. processo.Campos.Select(static c => c.Finalidade).Distinct().Order().Select(finalidade => DaFinalidade(processo, finalidade))];
    }

    /// <summary>Os campos coletados do processo, itens e campos de grupo, na ordem do documento: por finalidade, os itens e depois os grupos.</summary>
    public static IEnumerable<ItemNoDocumento> EmOrdemDoDocumento(ProcessoSeletivo processo) =>
        De(processo).SelectMany(static f => f.Itens.Concat(f.Grupos.SelectMany(static g => g.Campos)));

    /// <summary>O caminho no documento de cada fato coletado do processo, pelo código do fato: cada fato é coletado por um só formulário.</summary>
    public static IReadOnlyDictionary<string, string> CaminhosPorFato(ProcessoSeletivo processo)
    {
        Dictionary<string, string> caminhos = new(StringComparer.Ordinal);
        foreach (ItemNoDocumento campo in EmOrdemDoDocumento(processo))
        {
            caminhos.TryAdd(campo.Item.FatoCodigo, campo.Caminho);
        }

        return caminhos;
    }

    private static FormularioComOsSeusItens DaFinalidade(ProcessoSeletivo processo, FinalidadeFormulario finalidade)
    {
        string formulario = $"formularios[{EstruturaFormulario.ParaToken(finalidade)}]";
        FatoColetado[] itens = [.. processo.FatosColetados.Where(i => i.Finalidade == finalidade).OrderBy(static i => i.Ordem)];
        GrupoColetado[] grupos = [.. processo.GruposColetados.Where(g => g.Finalidade == finalidade).OrderBy(static g => g.Ordem)];

        return new(
            [.. itens.Select((item, posicao) => new ItemNoDocumento(item, $"{formulario}.itens[{posicao}]"))],
            [.. grupos.Select((grupo, posicao) => DoGrupo(grupo, $"{formulario}.grupos[{posicao}]"))]);
    }

    private static GrupoNoDocumento DoGrupo(GrupoColetado grupo, string caminho) => new(
        grupo,
        caminho,
        [.. grupo.Subitens.OrderBy(static s => s.Ordem).Select((campo, posicao) => new ItemNoDocumento(campo, $"{caminho}.subitens[{posicao}]"))]);
}

/// <summary>Os itens e os grupos de uma finalidade, na ordem do documento: os itens primeiro.</summary>
internal sealed record FormularioComOsSeusItens(IReadOnlyList<ItemNoDocumento> Itens, IReadOnlyList<GrupoNoDocumento> Grupos);

/// <summary>O campo coletado e o caminho dele no documento.</summary>
internal sealed record ItemNoDocumento(FatoColetado Item, string Caminho);

/// <summary>O grupo repetível, o caminho dele e os campos que coleta, cada um com o seu caminho.</summary>
internal sealed record GrupoNoDocumento(GrupoColetado Grupo, string Caminho, IReadOnlyList<ItemNoDocumento> Campos);
