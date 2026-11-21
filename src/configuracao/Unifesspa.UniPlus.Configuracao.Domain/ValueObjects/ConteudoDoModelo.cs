namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using System.Text.Json;

using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O conteúdo do modelo, lido e gravado inteiro: o título, as etapas, os itens, os termos, os fatos
/// pressupostos — coletados pela inscrição, que o modelo de outra finalidade pode citar (UNI-REQ-0144)
/// — e os grupos repetíveis.
/// </summary>
public sealed record ConteudoDoModelo(
    string? Titulo,
    IReadOnlyList<EtapaDoModelo> Etapas,
    IReadOnlyList<ItemDoModelo> Itens,
    IReadOnlyList<TermoDoModelo> Termos,
    IReadOnlyList<string> Pressupostos,
    IReadOnlyList<GrupoDoModelo> Grupos,
    IReadOnlyList<DocumentoExigidoDoModelo>? DocumentosExigidos = null)
{
    /// <summary>O conjunto padrão de documentos exigidos que a aplicação do modelo copia para o processo; ausente é nenhum.</summary>
    public IReadOnlyList<DocumentoExigidoDoModelo> Documentos => DocumentosExigidos ?? [];

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
        ILookup<string?, ItemDoModelo> itensPorEtapa = Itens.ToLookup(static i => i.EtapaCodigo, StringComparer.Ordinal);
        ILookup<string?, GrupoDoModelo> gruposPorEtapa = Grupos.ToLookup(static g => g.EtapaCodigo, StringComparer.Ordinal);
        return new DefinicaoFormulario(
            [.. Etapas.Where(static e => e.Tipo == TipoEtapaFormulario.Secao).OrderBy(static e => e.Ordem).Select(e => new DefinicaoEtapa(
                e.Codigo,
                e.Exibicao,
                [.. itensPorEtapa[e.Codigo].OrderBy(static i => i.Ordem).Select(Item)],
                [.. gruposPorEtapa[e.Codigo].OrderBy(static g => g.Ordem).Select(static g => new DefinicaoGrupo(
                    g.Codigo, g.Exibicao, g.Obrigatoriedade, g.Minimo, g.Maximo, [.. g.Subitens.OrderBy(static s => s.Ordem).Select(Item)], g.IncluiCandidato))]))],
            [.. Termos.OrderBy(static t => t.Ordem).Select(static t => new DefinicaoTermo(t.Codigo, t.Exibicao, t.Obrigatoriedade))],
            derivacoes,
            agregados);
    }

    private static DefinicaoItem Item(ItemDoModelo item) =>
        new(item.FatoCodigo, item.Regras.Exibicao, item.Regras.Obrigatoriedade, item.Regras.Restricoes, item.Regras.Impedimento, item.Formato);

    /// <summary>Os campos do modelo, itens e campos de grupo.</summary>
    public IEnumerable<ItemDoModelo> Campos() => Itens.Concat(Grupos.SelectMany(static g => g.Subitens));

    /// <summary>Os fatos que as regras do modelo citam — dos campos, dos grupos, das etapas, dos termos e dos documentos.</summary>
    public IEnumerable<string> FatosCitados() =>
        Campos().SelectMany(static i => i.FatosCitados)
            .Concat(Grupos.SelectMany(static g => g.FatosCitados))
            .Concat(Etapas.SelectMany(static e => e.FatosCitados))
            .Concat(Termos.SelectMany(static t => t.FatosCitados))
            .Concat(Documentos.SelectMany(static d => d.FatosCitados));

    /// <summary>As condições de todas as regras do modelo, com fato, operador e valor.</summary>
    public IEnumerable<CondicaoDnf> CondicoesDasRegras() =>
        PredicadosDasRegras().SelectMany(static p => p.Clausulas).SelectMany(static c => c.Condicoes);

    private IEnumerable<PredicadoDnf> PredicadosDasRegras() =>
        Campos().SelectMany(static i => SemCaminho(i.Predicados()).Append(i.Regras.Impedimento?.Quando).Concat(PredicadosDasOpcoesPermitidas(i)))
            .Concat(Grupos.SelectMany(static g => SemCaminho(g.Predicados())))
            .Concat(Etapas.SelectMany(static e => SemCaminho(e.Predicados())))
            .Concat(Termos.SelectMany(static t => SemCaminho(t.Predicados())))
            .Concat(Documentos.SelectMany(static d => SemCaminho(d.Predicados())))
            .OfType<PredicadoDnf>();

    private static IEnumerable<PredicadoDnf?> SemCaminho(IEnumerable<PredicadoNaEntrada> predicados) => predicados.Select(static p => p.Predicado);

    private static IEnumerable<PredicadoDnf?> PredicadosDasOpcoesPermitidas(ItemDoModelo campo) =>
        campo.Regras.Restricoes.OfType<OpcoesPermitidas>().SelectMany(static o => o.Entradas.Select(static e => e.Quando));

    /// <summary>
    /// O conteúdo na forma em que é conferido e gravado: textos aparados, códigos em NFC, ausente como
    /// vazio — a forma do item recusa o vazio. Mantém as posições, inclusive das restrições, para que
    /// cada recusa aponte o índice que veio.
    /// </summary>
    public ConteudoDoModelo NaFormaGravada() => new(
        FormaDoItem.TextoOpcional(Titulo),
        [.. (Etapas ?? []).Select(EtapaNaFormaGravada)],
        [.. (Itens ?? []).Select(CampoNaFormaGravada)],
        [.. (Termos ?? []).Select(static t => t with { Codigo = TextoGravavel.CodigoNaFormaGravada(t.Codigo) })],
        [.. (Pressupostos ?? []).Select(static p => TextoGravavel.CodigoNaFormaGravada(p))],
        [.. (Grupos ?? []).Select(GrupoNaFormaGravada)],
        [.. (DocumentosExigidos ?? []).Select(DocumentoNaFormaGravada)]);

    /// <summary>
    /// O conteúdo na ordem em que é gravado: etapas, itens, grupos, campos de grupo e termos na ordem
    /// declarada, e as restrições de cada campo na ordem do tipo.
    /// </summary>
    public ConteudoDoModelo EmOrdem() => this with
    {
        Etapas = [.. Etapas.OrderBy(static e => e.Ordem)],
        Itens = [.. Itens.OrderBy(static i => i.Ordem).Select(ComRestricoesEmOrdem)],
        Termos = [.. Termos.OrderBy(static t => t.Ordem)],
        Grupos = [.. Grupos.OrderBy(static g => g.Ordem)
            .Select(static g => g with { Subitens = [.. g.Subitens.OrderBy(static s => s.Ordem).Select(ComRestricoesEmOrdem)] })],
        DocumentosExigidos = Documentos,
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

    private static EtapaDoModelo EtapaNaFormaGravada(EtapaDoModelo etapa) => etapa with
    {
        Codigo = TextoGravavel.CodigoNaFormaGravada(etapa.Codigo),
        Titulo = (etapa.Titulo ?? string.Empty).Trim(),
        Descricao = FormaDoItem.TextoOpcional(etapa.Descricao),
        Aviso = FormaDoItem.TextoOpcional(etapa.Aviso),
    };

    private static ItemDoModelo CampoNaFormaGravada(ItemDoModelo campo) => campo with
    {
        FatoCodigo = TextoGravavel.CodigoNaFormaGravada(campo.FatoCodigo),
        EtapaCodigo = campo.EtapaCodigo is null ? null : TextoGravavel.CodigoNaFormaGravada(campo.EtapaCodigo),
        Apresentacao = campo.Apresentacao.NaFormaGravada(),
        Formato = FormaDoItem.TextoOpcional(campo.Formato),
    };

    private static GrupoDoModelo GrupoNaFormaGravada(GrupoDoModelo grupo) => grupo with
    {
        Codigo = TextoGravavel.CodigoNaFormaGravada(grupo.Codigo),
        EtapaCodigo = grupo.EtapaCodigo is null ? null : TextoGravavel.CodigoNaFormaGravada(grupo.EtapaCodigo),
        Rotulo = (grupo.Rotulo ?? string.Empty).Trim(),
        Subitens = [.. (grupo.Subitens ?? []).Select(CampoNaFormaGravada)],
    };

    private static DocumentoExigidoDoModelo DocumentoNaFormaGravada(DocumentoExigidoDoModelo documento) => documento with
    {
        FaseCodigo = TextoGravavel.CodigoNaFormaGravada(documento.FaseCodigo),
        TipoDocumentoCodigo = TextoGravavel.CodigoNaFormaGravada(documento.TipoDocumentoCodigo),
        PorOpcao = documento.PorOpcao is { } porOpcao
            ? new ExigenciaPorOpcao(
                TextoGravavel.CodigoNaFormaGravada(porOpcao.FatoCodigo), [.. porOpcao.Exceto.Select(static v => TextoGravavel.CodigoNaFormaGravada(v))])
            : null,
    };

    private IEnumerable<(string Campo, string? Texto)> TextosDoConteudo() =>
        new (string, string?)[] { ("titulo", Titulo) }
            .Concat(Etapas.SelectMany(TextosDaEtapa))
            .Concat(Itens.SelectMany(static (item, i) => TextosDoCampo($"itens[{i}]", item)))
            .Concat(Grupos.SelectMany(TextosDoGrupo))
            .Concat(Termos.SelectMany(TextosDoTermo))
            .Concat(Documentos.SelectMany(TextosDoDocumento))
            .Concat(Pressupostos.Select(static (p, i) => ($"pressupostos[{i}]", (string?)p)));

    private static IEnumerable<(string Campo, string? Texto)> TextosDaEtapa(EtapaDoModelo etapa, int indice) =>
        new (string, string?)[]
        {
            ($"etapas[{indice}].codigo", etapa.Codigo), ($"etapas[{indice}].titulo", etapa.Titulo),
            ($"etapas[{indice}].descricao", etapa.Descricao), ($"etapas[{indice}].aviso", etapa.Aviso),
        }
        .Concat(TextosDosPredicados($"etapas[{indice}]", etapa.Predicados()));

    private static IEnumerable<(string Campo, string? Texto)> TextosDoGrupo(GrupoDoModelo grupo, int indice) =>
        new (string, string?)[]
        {
            ($"grupos[{indice}].codigo", grupo.Codigo), ($"grupos[{indice}].etapaCodigo", grupo.EtapaCodigo), ($"grupos[{indice}].rotulo", grupo.Rotulo),
        }
        .Concat(TextosDosPredicados($"grupos[{indice}]", grupo.Predicados()))
        .Concat(grupo.Subitens.SelectMany((s, j) => TextosDoCampo($"grupos[{indice}].subitens[{j}]", s)));

    private static IEnumerable<(string Campo, string? Texto)> TextosDoTermo(TermoDoModelo termo, int indice) =>
        new (string, string?)[] { ($"termos[{indice}].codigo", termo.Codigo) }
            .Concat(TextosDosPredicados($"termos[{indice}]", termo.Predicados()));

    private static IEnumerable<(string Campo, string? Texto)> TextosDoDocumento(DocumentoExigidoDoModelo documento, int indice)
    {
        string caminho = $"documentosExigidos[{indice}]";
        return new (string, string?)[]
            {
                ($"{caminho}.faseCodigo", documento.FaseCodigo), ($"{caminho}.tipoDocumentoCodigo", documento.TipoDocumentoCodigo),
                ($"{caminho}.consequenciaIndeferimento", documento.ConsequenciaIndeferimento),
            }
            .Concat(TextosDosPredicados(caminho, documento.Predicados()))
            .Concat(TextosDaExigenciaPorOpcao($"{caminho}.porOpcao", documento.PorOpcao))
            .Concat(documento.BasesLegais.SelectMany((b, j) => TextosDaBaseLegal($"{caminho}.basesLegais[{j}]", b)));
    }

    private static IEnumerable<(string Campo, string? Texto)> TextosDaExigenciaPorOpcao(string caminho, ExigenciaPorOpcao? porOpcao)
    {
        if (porOpcao is null)
        {
            return [];
        }

        return porOpcao.Exceto.Select((v, j) => ($"{caminho}.exceto[{j}]", (string?)v))
            .Prepend(($"{caminho}.fatoCodigo", porOpcao.FatoCodigo));
    }

    private static (string Campo, string? Texto)[] TextosDaBaseLegal(string caminho, BaseLegalDoDocumentoDoModelo baseLegal) =>
        new (string, string?)[]
        {
            ($"{caminho}.referencia", baseLegal.Referencia), ($"{caminho}.abrangencia", baseLegal.Abrangencia),
            ($"{caminho}.status", baseLegal.Status), ($"{caminho}.observacao", baseLegal.Observacao),
        };

    private static IEnumerable<(string Campo, string? Texto)> TextosDoCampo(string caminho, ItemDoModelo campo) =>
        new (string, string?)[]
        {
            ($"{caminho}.fatoCodigo", campo.FatoCodigo), ($"{caminho}.etapaCodigo", campo.EtapaCodigo),
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
