namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.ProcessosSeletivos.Edicao;
using Domain.ProcessosSeletivos.Referencias;
using Domain.Services;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Copia do modelo os itens, os grupos e as etapas do formulário pelos mesmos resolvedores da escrita
/// direta. O que o catálogo mudou depois da composição do modelo sai da cópia e volta no relatório; o fato
/// desativado que o processo já vincula não é vínculo novo e continua (ADR-0136). O fato que outra
/// finalidade já coleta segue o par de finalidades, porque só a inscrição é citável pelas outras
/// (UNI-REQ-0144): na inscrição ele passa para a cópia, e nas outras o que está na inscrição sai da cópia.
/// </summary>
internal sealed class SelecaoDoFormularioDoModelo(
    ProcessoSeletivo processo,
    FinalidadeFormulario finalidade,
    ContextoDoCatalogo contexto,
    RelatorioDaAplicacaoDoModelo relatorio)
{
    private readonly IReadOnlySet<string> _vinculados = ReferenciasVivas.Vinculos(EstadoDaConfiguracao.De(processo)).Fatos;

    /// <summary>Copia as partes do formulário; as recusas vêm já na posição do modelo.</summary>
    public FormularioSelecionado Copiar(ConteudoDoModeloInput conteudo)
    {
        (ConteudoDoModeloInput conteudoFinal, List<FieldError> recusasDaMescla) =
            finalidade == FinalidadeFormulario.Inscricao ? ComConjuntoBasico(conteudo) : (conteudo, []);
        ItensSelecionados itens = CopiarItens(conteudoFinal.Itens ?? []);
        GruposSelecionados grupos = CopiarGrupos(conteudoFinal.Grupos ?? []);
        EtapasDoFormularioLidas etapas = EscritaDasEtapasDoFormulario.Ler(conteudoFinal.Etapas ?? []);
        IEnumerable<FieldError> recusasDasEtapas = etapas.Erros
            .Concat(EscritaDasEtapasDoFormulario.ConferirExibicoes(etapas, contexto))
            .Select(static e => PosicaoNoModelo.NoModelo(e));
        return new FormularioSelecionado(itens, grupos, etapas, [.. recusasDaMescla, .. itens.Recusas, .. grupos.Recusas, .. recusasDasEtapas]);
    }

    // O formulário de inscrição coleta o conjunto básico na seção reservada: o básico que o modelo não traz
    // entra como o conjunto básico o define.
    private static (ConteudoDoModeloInput Conteudo, List<FieldError> Recusas) ComConjuntoBasico(ConteudoDoModeloInput conteudo)
    {
        IReadOnlyList<FatoColetadoInput> itens = conteudo.Itens ?? [];
        IReadOnlyList<EtapaFormularioInput> etapas = conteudo.Etapas ?? [];
        (IReadOnlyList<FatoColetadoInput> itensMesclados, IReadOnlyList<GrupoColetadoInput> gruposMesclados, List<FieldError> mescla) =
            ConjuntoBasicoDaInscricao.MesclarItens(itens, conteudo.Grupos ?? [], ConjuntoBasicoDaInscricao.Referencia(itens));
        (IReadOnlyList<EtapaFormularioInput> etapasMescladas, List<FieldError> mesclaDasEtapas) =
            ConjuntoBasicoDaInscricao.MesclarEtapas(etapas, ConjuntoBasicoDaInscricao.SecaoDe(etapas));
        return (
            conteudo with { Itens = itensMesclados, Grupos = gruposMesclados, Etapas = etapasMescladas },
            [.. mescla.Concat(mesclaDasEtapas).Select(static e => PosicaoNoModelo.NoModelo(e))]);
    }

    private ItensSelecionados CopiarItens(IReadOnlyList<FatoColetadoInput> itensDoModelo)
    {
        List<int> indices = [];
        List<FatoColetadoInput> aCopiar = [];
        for (int i = 0; i < itensDoModelo.Count; i++)
        {
            if (ItemEntraNaCopia(itensDoModelo[i]))
            {
                indices.Add(i);
                aCopiar.Add(itensDoModelo[i]);
            }
        }

        (List<FatoColetado> itens, List<FieldError> errosDosItens) = EscritaDosItens.Resolver(EscritaDosItens.Ler(aCopiar), contexto);
        return new ItensSelecionados(itens, indices, aCopiar, [.. errosDosItens.Select(e => PosicaoNoModelo.NoModelo(e, "itens", indices))]);
    }

    private bool ItemEntraNaCopia(FatoColetadoInput item)
    {
        if (DescarteDoCampo(item.FatoCodigo, ConferenciaNoCatalogo.EhColetavel) is { } motivo)
        {
            relatorio.Descartados.Add(new ParteDescartadaDto(ParteDoModelo.Item, item.FatoCodigo, motivo));
            return false;
        }

        FatoColetado? emOutra = processo.FatosColetados.FirstOrDefault(f => f.FatoCodigo == item.FatoCodigo && f.Finalidade != finalidade);
        if (emOutra is not null && finalidade == FinalidadeFormulario.Inscricao)
        {
            relatorio.Trazidos.Add(item.FatoCodigo);
            return true;
        }

        if (emOutra?.Finalidade != FinalidadeFormulario.Inscricao)
        {
            return true;
        }

        relatorio.Mantidos.Add(item.FatoCodigo);
        return false;
    }

    // O grupo com campo que o catálogo mudou sai da cópia inteiro: a ocorrência sem o campo seria outra
    // lista que o modelo não compôs.
    private GruposSelecionados CopiarGrupos(IReadOnlyList<GrupoColetadoInput> gruposDoModelo)
    {
        List<int> indices = [];
        List<GrupoColetadoInput> aCopiar = [];
        for (int i = 0; i < gruposDoModelo.Count; i++)
        {
            if (GrupoEntraNaCopia(gruposDoModelo[i]))
            {
                indices.Add(i);
                aCopiar.Add(gruposDoModelo[i]);
            }
        }

        (List<GrupoColetado> grupos, List<FieldError> errosDosGrupos) = EscritaDosItens.ResolverGrupos(EscritaDosItens.LerGrupos(aCopiar), contexto);
        return new GruposSelecionados(grupos, indices, aCopiar, [.. errosDosGrupos.Select(e => PosicaoNoModelo.NoModelo(e, "grupos", indices))]);
    }

    private bool GrupoEntraNaCopia(GrupoColetadoInput grupo)
    {
        string? motivo = (grupo.Subitens ?? [])
            .Select(s => DescarteDoCampo(s?.FatoCodigo, ConferenciaNoCatalogo.EhColetavelEmGrupo))
            .FirstOrDefault(static m => m is not null);
        if (motivo is null)
        {
            return true;
        }

        relatorio.Descartados.Add(new ParteDescartadaDto(ParteDoModelo.Grupo, grupo.Codigo, motivo));
        return false;
    }

    // O motivo de o campo — item ou campo de grupo, conforme o critério de coleta — sair da cópia. O fato
    // desativado que o processo já vincula não é vínculo novo e fica.
    private string? DescarteDoCampo(string? fatoCodigo, Func<FatoDoCatalogo, bool> coletavel)
    {
        if (fatoCodigo is null || !contexto.FatosDasRegras.TryGetValue(fatoCodigo, out FatoDoCatalogo? fato) || !coletavel(fato))
        {
            return MotivoDoDescarte.FatoNaoColetavel;
        }

        return fato.Ativo || _vinculados.Contains(fatoCodigo) ? null : MotivoDoDescarte.FatoDesativado;
    }
}

/// <summary>Os itens que entram na cópia, o índice de cada um no modelo, a entrada que os originou e as recusas.</summary>
internal sealed record ItensSelecionados(
    List<FatoColetado> Itens, List<int> IndicesNoModelo, List<FatoColetadoInput> Entradas, List<FieldError> Recusas);

/// <summary>Os grupos que entram na cópia, o índice de cada um no modelo, a entrada que os originou e as recusas.</summary>
internal sealed record GruposSelecionados(
    List<GrupoColetado> Grupos, List<int> IndicesNoModelo, List<GrupoColetadoInput> Entradas, List<FieldError> Recusas);

/// <summary>As partes do formulário copiadas do modelo e as recusas de todas elas, na ordem de avaliação.</summary>
internal sealed record FormularioSelecionado(ItensSelecionados Itens, GruposSelecionados Grupos, EtapasDoFormularioLidas Etapas, List<FieldError> Recusas);
