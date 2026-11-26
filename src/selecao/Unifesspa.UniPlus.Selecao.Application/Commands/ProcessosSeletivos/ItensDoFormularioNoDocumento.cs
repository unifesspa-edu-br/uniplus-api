namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.ProcessosSeletivos.Ordenacao;

/// <summary>
/// O percurso do documento da configuração sobre os formulários do processo: por formulário, os itens e depois os grupos
/// repetíveis com os seus campos, cada um com o caminho que o documento lhe dá (<c>formularios[f].itens[i]</c>,
/// <c>formularios[f].grupos[g].subitens[c]</c>). Cada posição é a do item na ordem canônica do documento
/// (<see cref="OrdemDaConfiguracao"/>): o formulário pela ordem do código e os itens, os grupos e os campos pela ordem
/// dentro dele, de modo que toda conferência que aponta um item usa o mesmo caminho.
/// </summary>
internal static class ItensDoFormularioNoDocumento
{
    public static IReadOnlyList<FormularioComOsSeusItens> De(ProcessoSeletivo processo)
    {
        ArgumentNullException.ThrowIfNull(processo);

        return [.. OrdemDaConfiguracao.Formularios(processo.Formularios).Select((formulario, posicao) => DoFormulario(processo, formulario, posicao))];
    }

    /// <summary>Os campos coletados do processo, itens e campos de grupo, na ordem do documento: por formulário, os itens e depois os grupos.</summary>
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

    private static FormularioComOsSeusItens DoFormulario(ProcessoSeletivo processo, FormularioProcesso formulario, int posicaoDoFormulario)
    {
        string caminho = $"formularios[{posicaoDoFormulario}]";
        FormularioProcesso[] doFormulario = [formulario];
        FatoColetado[] itens = [.. OrdemDaConfiguracao.Campos(processo.FatosColetados.Where(i => i.FormularioId == formulario.Id), doFormulario)];
        GrupoColetado[] grupos = [.. OrdemDaConfiguracao.Grupos(processo.GruposColetados.Where(g => g.FormularioId == formulario.Id), doFormulario)];

        return new(
            [.. itens.Select((item, posicao) => new ItemNoDocumento(item, $"{caminho}.itens[{posicao}]"))],
            [.. grupos.Select((grupo, posicao) => DoGrupo(grupo, $"{caminho}.grupos[{posicao}]"))]);
    }

    private static GrupoNoDocumento DoGrupo(GrupoColetado grupo, string caminho) => new(
        grupo,
        caminho,
        [.. OrdemDaConfiguracao.CamposDoGrupo(grupo.Subitens).Select((campo, posicao) => new ItemNoDocumento(campo, $"{caminho}.subitens[{posicao}]"))]);
}

/// <summary>Os itens e os grupos de um formulário, na ordem do documento: os itens primeiro.</summary>
internal sealed record FormularioComOsSeusItens(IReadOnlyList<ItemNoDocumento> Itens, IReadOnlyList<GrupoNoDocumento> Grupos);

/// <summary>O campo coletado e o caminho dele no documento.</summary>
internal sealed record ItemNoDocumento(FatoColetado Item, string Caminho);

/// <summary>O grupo repetível, o caminho dele e os campos que coleta, cada um com o seu caminho.</summary>
internal sealed record GrupoNoDocumento(GrupoColetado Grupo, string Caminho, IReadOnlyList<ItemNoDocumento> Campos);
