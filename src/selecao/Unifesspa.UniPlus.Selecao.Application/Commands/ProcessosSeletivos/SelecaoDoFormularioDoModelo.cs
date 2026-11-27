namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.ProcessosSeletivos.Coleta;
using Domain.ProcessosSeletivos.Edicao;
using Domain.ProcessosSeletivos.Formularios;
using Domain.ProcessosSeletivos.Referencias;
using Domain.Services;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Copia do modelo os itens, os grupos e as páginas do formulário pelos mesmos resolvedores da escrita
/// direta. O que o catálogo mudou depois da composição do modelo sai da cópia e volta no relatório; o fato
/// desativado que o processo já vincula não é vínculo novo e continua (ADR-0136). O fato que outro
/// formulário já coleta segue o par de formulários, porque só os da inscrição são citáveis pelos outros
/// (UNI-REQ-0144): o formulário da inscrição só puxa o fato de formulário que não é da inscrição, e o que já está
/// num formulário da inscrição sai da cópia, em qualquer formulário.
/// </summary>
internal sealed class SelecaoDoFormularioDoModelo
{
    private readonly ProcessoSeletivo _processo;
    private readonly FormularioProcesso _formulario;
    private readonly ContextoDoCatalogo _contexto;
    private readonly RelatorioDaAplicacaoDoModelo _relatorio;
    private readonly IReadOnlySet<string> _vinculados;
    private readonly bool _guardaOConjuntoBasico;
    private readonly bool _coletaAInscricao;
    private readonly HashSet<Guid> _formulariosDaInscricao;

    public SelecaoDoFormularioDoModelo(
        ProcessoSeletivo processo, FormularioProcesso formulario, ContextoDoCatalogo contexto, RelatorioDaAplicacaoDoModelo relatorio)
    {
        ConfiguracaoDeclarada configuracao = processo.Declaracao();
        _processo = processo;
        _formulario = formulario;
        _contexto = contexto;
        _relatorio = relatorio;
        _vinculados = ReferenciasVivas.Vinculos(configuracao).Fatos;
        _guardaOConjuntoBasico = ConjuntoBasicoNaInscricao.Guarda(configuracao, formulario.Declarado);
        _coletaAInscricao = configuracao.EhDaInscricao(formulario.Declarado);
        _formulariosDaInscricao = [.. configuracao.DaInscricao().Select(static f => f.Id)];
    }

    /// <summary>Copia as partes do formulário; as recusas vêm já na posição do modelo.</summary>
    public FormularioSelecionado Copiar(ConteudoDoModeloInput conteudo)
    {
        (ConteudoDoModeloInput conteudoFinal, List<FieldError> recusasDaMescla) =
            _guardaOConjuntoBasico ? ComConjuntoBasico(conteudo) : (conteudo, []);
        ItensSelecionados itens = CopiarItens(conteudoFinal.Itens ?? []);
        GruposSelecionados grupos = CopiarGrupos(conteudoFinal.Grupos ?? []);
        PaginasDoFormularioLidas paginas = EscritaDasPaginasDoFormulario.Ler(conteudoFinal.Paginas ?? []);
        IEnumerable<FieldError> recusasDasPaginas = paginas.Erros
            .Concat(EscritaDasPaginasDoFormulario.ConferirExibicoes(paginas, _contexto))
            .Select(static e => PosicaoNoModelo.NoModelo(e));
        return new FormularioSelecionado(itens, grupos, paginas, [.. recusasDaMescla, .. itens.Recusas, .. grupos.Recusas, .. recusasDasPaginas]);
    }

    // O formulário de inscrição coleta o conjunto básico na seção reservada: o básico que o modelo não traz
    // entra como o conjunto básico o define.
    private static (ConteudoDoModeloInput Conteudo, List<FieldError> Recusas) ComConjuntoBasico(ConteudoDoModeloInput conteudo)
    {
        IReadOnlyList<FatoColetadoInput> itens = conteudo.Itens ?? [];
        IReadOnlyList<PaginaDoFormularioInput> paginas = conteudo.Paginas ?? [];
        (IReadOnlyList<FatoColetadoInput> itensMesclados, IReadOnlyList<GrupoColetadoInput> gruposMesclados, List<FieldError> mescla) =
            ConjuntoBasicoDaInscricao.MesclarItens(itens, conteudo.Grupos ?? [], ConjuntoBasicoDaInscricao.Referencia(itens));
        (IReadOnlyList<PaginaDoFormularioInput> paginasMescladas, List<FieldError> mesclaDasPaginas) =
            ConjuntoBasicoDaInscricao.MesclarPaginas(paginas, ConjuntoBasicoDaInscricao.SecaoDe(paginas));
        return (
            conteudo with { Itens = itensMesclados, Grupos = gruposMesclados, Paginas = paginasMescladas },
            [.. mescla.Concat(mesclaDasPaginas).Select(static e => PosicaoNoModelo.NoModelo(e))]);
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

        (List<FatoColetado> itens, List<FieldError> errosDosItens) = EscritaDosItens.Resolver(LeituraDosItensDoFormulario.Ler(aCopiar, _contexto), _contexto);
        return new ItensSelecionados(itens, indices, aCopiar, [.. errosDosItens.Select(e => PosicaoNoModelo.NoModelo(e, "itens", indices))]);
    }

    private bool ItemEntraNaCopia(FatoColetadoInput item)
    {
        if (DescarteDoCampo(item.FatoCodigo, ConferenciaNoCatalogo.EhColetavel) is { } motivo)
        {
            _relatorio.Descartados.Add(new ParteDescartadaDto(ParteDoModelo.Item, item.FatoCodigo, motivo));
            return false;
        }

        return DecidirCopiaEntreFormularios(item);
    }

    private bool DecidirCopiaEntreFormularios(FatoColetadoInput item)
    {
        FatoColetado? emOutro = _processo.FatosColetados.FirstOrDefault(f => f.FatoCodigo == item.FatoCodigo && f.FormularioId != _formulario.Id);
        return emOutro is null || CopiaOFatoDeOutroFormulario(item.FatoCodigo, emOutro);
    }

    private bool CopiaOFatoDeOutroFormulario(string fatoCodigo, FatoColetado emOutro)
    {
        if (emOutro.FormularioId is { } formularioDoFato && _formulariosDaInscricao.Contains(formularioDoFato))
        {
            _relatorio.Mantidos.Add(fatoCodigo);
            return false;
        }

        if (_coletaAInscricao)
        {
            _relatorio.Trazidos.Add(fatoCodigo);
        }

        return true;
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

        (List<GrupoColetado> grupos, List<FieldError> errosDosGrupos) = EscritaDosItens.ResolverGrupos(LeituraDosItensDoFormulario.LerGrupos(aCopiar, _contexto), _contexto);
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

        _relatorio.Descartados.Add(new ParteDescartadaDto(ParteDoModelo.Grupo, grupo.Codigo, motivo));
        return false;
    }

    // O motivo de o campo — item ou campo de grupo, conforme o critério de coleta — sair da cópia. O fato
    // desativado que o processo já vincula não é vínculo novo e fica.
    private string? DescarteDoCampo(string? fatoCodigo, Func<FatoDoCatalogo, bool> coletavel)
    {
        if (fatoCodigo is null || !_contexto.FatosDasRegras.TryGetValue(fatoCodigo, out FatoDoCatalogo? fato) || !coletavel(fato))
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
internal sealed record FormularioSelecionado(ItensSelecionados Itens, GruposSelecionados Grupos, PaginasDoFormularioLidas Paginas, List<FieldError> Recusas);
