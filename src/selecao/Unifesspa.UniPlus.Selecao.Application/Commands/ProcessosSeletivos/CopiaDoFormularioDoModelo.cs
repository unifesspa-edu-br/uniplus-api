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
internal sealed class CopiaDoFormularioDoModelo(
    ProcessoSeletivo processo,
    FinalidadeFormulario finalidade,
    ContextoDoCatalogo contexto,
    RelatorioDaAplicacaoDoModelo relatorio)
{
    private readonly IReadOnlySet<string> _vinculados = ReferenciasVivas.Vinculos(EstadoDaConfiguracao.De(processo)).Fatos;

    /// <summary>Copia as partes do formulário; as recusas entram em <paramref name="erros"/>, já na posição do modelo.</summary>
    public FormularioCopiado Copiar(ConteudoDoModeloInput conteudo, List<FieldError> erros)
    {
        ConteudoDoModeloInput conteudoFinal = finalidade == FinalidadeFormulario.Inscricao ? ComConjuntoBasico(conteudo, erros) : conteudo;
        ItensCopiados itens = CopiarItens(conteudoFinal.Itens ?? [], erros);
        GruposCopiados grupos = CopiarGrupos(conteudoFinal.Grupos ?? [], erros);
        EtapasLidas etapas = EscritaDasEtapas.Ler(conteudoFinal.Etapas ?? []);
        erros.AddRange(etapas.Erros.Concat(EscritaDasEtapas.ConferirExibicoes(etapas, contexto)).Select(static e => PosicaoNoModelo.NoModelo(e)));
        return new FormularioCopiado(itens, grupos, etapas);
    }

    // O formulário de inscrição coleta o conjunto básico na seção reservada: o básico que o modelo não traz
    // entra como o conjunto básico o define.
    private static ConteudoDoModeloInput ComConjuntoBasico(ConteudoDoModeloInput conteudo, List<FieldError> erros)
    {
        IReadOnlyList<FatoColetadoInput> itens = conteudo.Itens ?? [];
        IReadOnlyList<EtapaFormularioInput> etapas = conteudo.Etapas ?? [];
        (IReadOnlyList<FatoColetadoInput> itensMesclados, IReadOnlyList<GrupoColetadoInput> gruposMesclados, List<FieldError> mescla) =
            ConjuntoBasicoDaInscricao.MesclarItens(itens, conteudo.Grupos ?? [], ConjuntoBasicoDaInscricao.Referencia(itens));
        (IReadOnlyList<EtapaFormularioInput> etapasMescladas, List<FieldError> mesclaDasEtapas) =
            ConjuntoBasicoDaInscricao.MesclarEtapas(etapas, ConjuntoBasicoDaInscricao.SecaoDe(etapas));
        erros.AddRange(mescla.Concat(mesclaDasEtapas).Select(static e => PosicaoNoModelo.NoModelo(e)));
        return conteudo with { Itens = itensMesclados, Grupos = gruposMesclados, Etapas = etapasMescladas };
    }

    private ItensCopiados CopiarItens(IReadOnlyList<FatoColetadoInput> itensDoModelo, List<FieldError> erros)
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
        erros.AddRange(errosDosItens.Select(e => PosicaoNoModelo.NoModelo(e, "itens", indices)));
        return new ItensCopiados(itens, indices, aCopiar);
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
    private GruposCopiados CopiarGrupos(IReadOnlyList<GrupoColetadoInput> gruposDoModelo, List<FieldError> erros)
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
        erros.AddRange(errosDosGrupos.Select(e => PosicaoNoModelo.NoModelo(e, "grupos", indices)));
        return new GruposCopiados(grupos, indices, aCopiar);
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

/// <summary>Os itens que entram na cópia, o índice de cada um no modelo e a entrada que os originou.</summary>
internal sealed record ItensCopiados(List<FatoColetado> Itens, List<int> IndicesNoModelo, List<FatoColetadoInput> Entradas);

/// <summary>Os grupos que entram na cópia, o índice de cada um no modelo e a entrada que os originou.</summary>
internal sealed record GruposCopiados(List<GrupoColetado> Grupos, List<int> IndicesNoModelo, List<GrupoColetadoInput> Entradas);

/// <summary>As partes do formulário copiadas do modelo.</summary>
internal sealed record FormularioCopiado(ItensCopiados Itens, GruposCopiados Grupos, EtapasLidas Etapas);
