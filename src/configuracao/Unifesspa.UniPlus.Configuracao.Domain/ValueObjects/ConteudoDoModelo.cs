namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using System.Text.Json;

using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O conteúdo do modelo, lido e gravado inteiro: o título, as páginas, os itens, os termos, os fatos
/// pressupostos — coletados pela inscrição, que o modelo de outra finalidade pode citar (UNI-REQ-0144)
/// — e os grupos repetíveis.
/// </summary>
public sealed record ConteudoDoModelo(
    string? Titulo,
    IReadOnlyList<PaginaDoModelo> Paginas,
    IReadOnlyList<ItemDoModelo> Itens,
    IReadOnlyList<TermoDoModelo> Termos,
    IReadOnlyList<string> Pressupostos,
    IReadOnlyList<GrupoDoModelo> Grupos,
    ConjuntoDeDocumentosDoModelo? ConjuntoDeDocumentos = null)
{
    /// <summary>O conjunto padrão de documentos exigidos que a aplicação do modelo copia para o processo; ausente é nenhum.</summary>
    public ConjuntoDeDocumentosDoModelo Documentos => ConjuntoDeDocumentos ?? ConjuntoDeDocumentosDoModelo.Nenhum;

    /// <summary>
    /// O conteúdo como o avaliador de formulário o lê: cada seção, na ordem, com os seus itens e grupos
    /// repetíveis — os blocos que o sistema monta não têm regra, como na definição do processo —; os
    /// termos; as derivações por regra do catálogo, resolvidas com as respostas simuladas; e os agregados
    /// do catálogo sobre os grupos.
    /// </summary>
    public DefinicaoFormulario ParaAvaliacao(IReadOnlyList<DerivacaoDoFato> derivacoes, IReadOnlyList<DefinicaoAgregado> agregados)
    {
        ArgumentNullException.ThrowIfNull(derivacoes);
        ArgumentNullException.ThrowIfNull(agregados);
        ILookup<string?, ItemDoModelo> itensPorPagina = Itens.ToLookup(static i => i.PaginaCodigo, StringComparer.Ordinal);
        ILookup<string?, GrupoDoModelo> gruposPorPagina = Grupos.ToLookup(static g => g.PaginaCodigo, StringComparer.Ordinal);
        return new DefinicaoFormulario(
            [.. Paginas.Where(static e => e.Tipo == TipoPaginaFormulario.Secao).OrderBy(static e => e.Ordem).Select(e => new DefinicaoPagina(
                e.Codigo,
                e.Exibicao,
                [.. itensPorPagina[e.Codigo].OrderBy(static i => i.Ordem).Select(Item)],
                [.. gruposPorPagina[e.Codigo].OrderBy(static g => g.Ordem).Select(Grupo)]))],
            [.. Termos.OrderBy(static t => t.Ordem).Select(static t => new DefinicaoTermo(t.Codigo, t.Regras.Exibicao, t.Regras.Obrigatoriedade))],
            derivacoes,
            agregados);
    }

    private static DefinicaoGrupo Grupo(GrupoDoModelo grupo) => new(
        grupo.Codigo, grupo.Regras.Exibicao, grupo.Regras.Obrigatoriedade, grupo.Ocorrencias.Minimo, grupo.Ocorrencias.Maximo,
        [.. grupo.Subitens.OrderBy(static s => s.Ordem).Select(Item)], grupo.Ocorrencias.IncluiCandidato);

    private static DefinicaoItem Item(ItemDoModelo item) =>
        new(item.FatoCodigo, item.Regras.Exibicao, item.Regras.Obrigatoriedade, item.Regras.Restricoes, item.Regras.Impedimento, item.Formato);

    /// <summary>Os campos do modelo, itens e campos de grupo.</summary>
    public IEnumerable<ItemDoModelo> Campos() => Itens.Concat(Grupos.SelectMany(static g => g.Subitens));

    /// <summary>Os fatos que as regras do modelo citam — dos campos, dos grupos, das páginas, dos termos e dos documentos.</summary>
    public IEnumerable<string> FatosCitados() =>
        Campos().SelectMany(static i => i.FatosCitados)
            .Concat(Grupos.SelectMany(static g => g.FatosCitados))
            .Concat(Paginas.SelectMany(static e => e.FatosCitados))
            .Concat(Termos.SelectMany(static t => t.FatosCitados))
            .Concat(Documentos.FatosCitados());

    /// <summary>As condições de todas as regras do modelo, com fato, operador e valor.</summary>
    public IEnumerable<CondicaoDnf> CondicoesDasRegras() =>
        PredicadosDasRegras().SelectMany(static p => p.Clausulas).SelectMany(static c => c.Condicoes);

    private IEnumerable<PredicadoDnf> PredicadosDasRegras() =>
        Campos().SelectMany(static i => SemCaminho(i.Predicados()).Append(i.Regras.Impedimento?.Quando).Concat(PredicadosDasOpcoesPermitidas(i)))
            .Concat(Grupos.SelectMany(static g => SemCaminho(g.Predicados())))
            .Concat(Paginas.SelectMany(static e => SemCaminho(e.Predicados())))
            .Concat(Termos.SelectMany(static t => SemCaminho(t.Predicados())))
            .Concat(SemCaminho(Documentos.Predicados()))
            .OfType<PredicadoDnf>();

    private static IEnumerable<PredicadoDnf?> SemCaminho(IEnumerable<PredicadoNaEntrada> predicados) => predicados.Select(static p => p.Predicado);

    private static IEnumerable<PredicadoDnf?> PredicadosDasOpcoesPermitidas(ItemDoModelo campo) =>
        campo.Regras.Restricoes.OfType<OpcoesPermitidas>().SelectMany(static o => o.Entradas.Select(static e => e.Quando));

    /// <summary>
    /// O conteúdo na forma em que é conferido e gravado: textos aparados, ausente como
    /// vazio — a forma do item recusa o vazio. Mantém as posições, inclusive das restrições, para que
    /// cada recusa aponte o índice que veio.
    /// </summary>
    public ConteudoDoModelo NaFormaGravada() => new(
        FormaDoItem.TextoOpcional(Titulo),
        [.. (Paginas ?? []).Select(PaginaNaFormaGravada)],
        [.. (Itens ?? []).Select(CampoNaFormaGravada)],
        [.. (Termos ?? [])],
        [.. (Pressupostos ?? []).Select(static p => TextoGravavel.CodigoNaFormaGravada(p))],
        [.. (Grupos ?? []).Select(GrupoNaFormaGravada)],
        Documentos.NaFormaGravada());

    /// <summary>
    /// O conteúdo na ordem em que é gravado: páginas, itens, grupos, campos de grupo e termos na ordem
    /// declarada, e as restrições de cada campo na ordem do tipo.
    /// </summary>
    public ConteudoDoModelo EmOrdem() => this with
    {
        Paginas = [.. Paginas.OrderBy(static e => e.Ordem)],
        Itens = [.. Itens.OrderBy(static i => i.Ordem).Select(ComRestricoesEmOrdem)],
        Termos = [.. Termos.OrderBy(static t => t.Ordem)],
        Grupos = [.. Grupos.OrderBy(static g => g.Ordem)
            .Select(static g => g with { Subitens = [.. g.Subitens.OrderBy(static s => s.Ordem).Select(ComRestricoesEmOrdem)] })],
        ConjuntoDeDocumentos = Documentos,
    };

    /// <summary>
    /// Os textos do conteúdo que o banco não grava — com caractere nulo, ou que não são Unicode
    /// válido —, inclusive os valores e os fatos citados pelas regras: o conteúdo é um documento
    /// jsonb, e o Postgres o recusaria só na gravação.
    /// </summary>
    public IEnumerable<FieldError> TextosNaoGravaveis() =>
        TextosDoConteudo().SelectMany(static t => TextoGravavel.Recusar(t.Campo, t.Texto)).DistinctBy(static e => e.Field);

    private static ItemDoModelo ComRestricoesEmOrdem(ItemDoModelo campo) =>
        campo with { Regras = campo.Regras.ComRestricoesNaOrdemDoTipo() };

    private static PaginaDoModelo PaginaNaFormaGravada(PaginaDoModelo pagina) => pagina with
    {
        Titulo = (pagina.Titulo ?? string.Empty).Trim(),
        Descricao = FormaDoItem.TextoOpcional(pagina.Descricao),
        Aviso = FormaDoItem.TextoOpcional(pagina.Aviso),
    };

    private static ItemDoModelo CampoNaFormaGravada(ItemDoModelo campo) => campo with
    {
        Apresentacao = campo.Apresentacao.NaFormaGravada(),
        Formato = FormaDoItem.TextoOpcional(campo.Formato),
    };

    private static GrupoDoModelo GrupoNaFormaGravada(GrupoDoModelo grupo) => grupo with
    {
        Rotulo = (grupo.Rotulo ?? string.Empty).Trim(),
        Subitens = [.. (grupo.Subitens ?? []).Select(CampoNaFormaGravada)],
    };

    private IEnumerable<(string Campo, string? Texto)> TextosDoConteudo() =>
        new (string, string?)[] { ("titulo", Titulo) }
            .Concat(Paginas.SelectMany(TextosDaPagina))
            .Concat(Itens.SelectMany(static (item, i) => TextosDoCampo($"itens[{i}]", item)))
            .Concat(Grupos.SelectMany(TextosDoGrupo))
            .Concat(Termos.SelectMany(TextosDoTermo))
            .Concat(Documentos.ComCaminho().SelectMany(static c => TextosDoDocumento(c.Documento, c.Caminho)))
            .Concat(Documentos.GruposDeDocumentos.SelectMany(TextosDoGrupoDeDocumentos))
            .Concat(Pressupostos.Select(static (p, i) => ($"pressupostos[{i}]", (string?)p)));

    private static IEnumerable<(string Campo, string? Texto)> TextosDaPagina(PaginaDoModelo pagina, int indice) =>
        new (string, string?)[]
        {
            ($"paginas[{indice}].{PaginaDoFormularioInput.CaminhoDaApresentacao}.titulo", pagina.Titulo),
            ($"paginas[{indice}].{PaginaDoFormularioInput.CaminhoDaApresentacao}.descricao", pagina.Descricao),
            ($"paginas[{indice}].{PaginaDoFormularioInput.CaminhoDaApresentacao}.aviso", pagina.Aviso),
        }
        .Concat(TextosDosPredicados($"paginas[{indice}]", pagina.Predicados()));

    private static IEnumerable<(string Campo, string? Texto)> TextosDoGrupo(GrupoDoModelo grupo, int indice) =>
        new (string, string?)[]
        {
            ($"grupos[{indice}].paginaCodigo", grupo.PaginaCodigo), ($"grupos[{indice}].rotulo", grupo.Rotulo),
        }
        .Concat(TextosDosPredicados($"grupos[{indice}]", grupo.Predicados()))
        .Concat(grupo.Subitens.SelectMany((s, j) => TextosDoCampo($"grupos[{indice}].subitens[{j}]", s)));

    private static IEnumerable<(string Campo, string? Texto)> TextosDoTermo(TermoDoModelo termo, int indice) =>
        TextosDosPredicados($"termos[{indice}]", termo.Predicados());

    private static IEnumerable<(string Campo, string? Texto)> TextosDoGrupoDeDocumentos(GrupoDeDocumentosDoModelo grupo, int indice) =>
        grupo.Indeferimento is { } indeferimento
            ? TextosDoIndeferimentoDoGrupo($"{ConjuntoDeDocumentosDoModelo.CaminhoDoGrupo(indice)}.indeferimento", indeferimento)
            : [];

    private static IEnumerable<(string Campo, string? Texto)> TextosDoIndeferimentoDoGrupo(string caminho, IndeferimentoDoGrupoDeDocumentosDoModelo indeferimento) =>
        indeferimento.BasesLegais.SelectMany((b, j) => TextosDaBaseLegal($"{caminho}.basesLegais[{j}]", b))
            .Prepend(($"{caminho}.consequencia", (string?)indeferimento.Consequencia));

    private static IEnumerable<(string Campo, string? Texto)> TextosDoDocumento(DocumentoExigidoDoModelo documento, string caminho) =>
        new (string, string?)[]
        {
            ($"{caminho}.faseCodigo", documento.FaseCodigo), ($"{caminho}.tipoDocumentoCodigo", documento.TipoDocumentoCodigo),
            ($"{caminho}.indeferimento.consequencia", documento.Indeferimento.Consequencia),
        }
        .Concat(TextosDosPredicados(caminho, documento.Predicados()))
        .Concat(documento.Requisitos.TextosDasReferencias(caminho))
        .Concat(TextosDaExigenciaPorOpcao($"{caminho}.porOpcao", documento.PorOpcao))
        .Concat(documento.Indeferimento.BasesLegais.SelectMany((b, j) => TextosDaBaseLegal($"{caminho}.indeferimento.basesLegais[{j}]", b)));

    private static IEnumerable<(string Campo, string? Texto)> TextosDaExigenciaPorOpcao(string caminho, ExigenciaPorOpcao? porOpcao)
    {
        if (porOpcao is null)
        {
            return [];
        }

        return porOpcao.Exceto.Select((v, j) => ($"{caminho}.exceto[{j}]", (string?)v))
            .Prepend(($"{caminho}.fatoCodigo", porOpcao.FatoCodigo));
    }

    private static (string Campo, string? Texto)[] TextosDaBaseLegal(string caminho, BaseLegalDoModelo baseLegal) =>
        new (string, string?)[]
        {
            ($"{caminho}.referencia", baseLegal.Referencia), ($"{caminho}.abrangencia", baseLegal.Abrangencia),
            ($"{caminho}.situacao", baseLegal.Situacao), ($"{caminho}.observacao", baseLegal.Observacao),
        };

    private static IEnumerable<(string Campo, string? Texto)> TextosDoCampo(string caminho, ItemDoModelo campo) =>
        new (string, string?)[]
        {
            ($"{caminho}.fatoCodigo", campo.FatoCodigo), ($"{caminho}.paginaCodigo", campo.PaginaCodigo),
            ($"{caminho}.{FatoColetadoInput.CaminhoDaApresentacao}.rotulo", campo.Apresentacao.Rotulo), ($"{caminho}.formato", campo.Formato),
            ($"{caminho}.{FatoColetadoInput.CaminhoDaApresentacao}.ajuda", campo.Apresentacao.Ajuda),
        }
        .Concat(TextosDosPredicados(caminho, campo.Predicados()))
        .Concat(TextosDasRegrasSemPredicados($"{caminho}.{FatoColetadoInput.CaminhoDasRegras}", campo.Regras));

    private static IEnumerable<(string Campo, string? Texto)> TextosDasRegrasSemPredicados(string caminho, RegrasDoCampo regras) =>
        regras.Restricoes.SelectMany((r, j) => TextosDaRestricao($"{caminho}.restricoes[{j}]", r))
            .Concat(TextosDoImpedimento(caminho, regras.Impedimento));

    private static IEnumerable<(string Campo, string? Texto)> TextosDoImpedimento(string caminho, Impedimento? impedimento) =>
        impedimento is null
            ? []
            : TextosDoPredicado($"{caminho}.impedimento.quando", impedimento.Quando).Append(($"{caminho}.impedimento.mensagem", impedimento.Mensagem));

    private static IEnumerable<(string Campo, string? Texto)> TextosDaRestricao(string campo, RestricaoValor restricao) => restricao switch
    {
        OpcoesPermitidas opcoes => opcoes.Entradas.SelectMany((e, k) => e.Valores.Select(v => ($"{campo}.entradas[{k}].valores", (string?)v))
            .Concat(TextosDoPredicado($"{campo}.entradas[{k}].quando", e.Quando))),
        OpcoesDasRespostas respostas => respostas.Fatos.Select(f => ($"{campo}.fatos", (string?)f)),
        MunicipiosDaUf daUf => [($"{campo}.fatos", daUf.FatoUf)],
        _ => [],
    };

    private static IEnumerable<(string Campo, string? Texto)> TextosDosPredicados(string caminho, IEnumerable<PredicadoNaEntrada> predicados) =>
        predicados.SelectMany(p => TextosDoPredicado($"{caminho}.{p.Caminho}", p.Predicado));

    private static IEnumerable<(string Campo, string? Texto)> TextosDoPredicado(string campo, PredicadoDnf? predicado) =>
        (predicado?.Clausulas ?? []).SelectMany(static c => c.Condicoes)
            .SelectMany(c => new[] { (campo, (string?)c.Fato) }.Concat(TextosDoValor(c.Valor).Select(v => (campo, (string?)v))));

    private static IEnumerable<string> TextosDoValor(JsonElement valor) => valor.ValueKind switch
    {
        JsonValueKind.String => [valor.GetString()!],
        JsonValueKind.Array => valor.EnumerateArray().SelectMany(TextosDoValor),
        JsonValueKind.Object => valor.EnumerateObject().SelectMany(static p => TextosDoValor(p.Value).Prepend(p.Name)),
        _ => [],
    };
}
