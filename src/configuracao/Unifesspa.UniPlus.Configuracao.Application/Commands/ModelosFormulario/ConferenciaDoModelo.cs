namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using System.Text.Json;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O catálogo de fatos na forma em que o modelo é conferido: os fatos, o vocabulário dos
/// predicados, o domínio dos categóricos que o catálogo não enumera, as dependências dos derivados
/// por regra e o formato dos fatos de texto.
/// </summary>
internal sealed record CatalogoDoModelo(
    IReadOnlyDictionary<string, FatoDoCatalogo> Fatos,
    IReadOnlyDictionary<string, DescritorFatoCandidato> Vocabulario,
    IReadOnlyDictionary<string, DominioDeValores> DominiosDinamicos,
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> Derivacoes,
    IReadOnlyDictionary<string, string> Formatos)
{
    public static CatalogoDoModelo De(IReadOnlyList<FatoCandidato> fatos) => new(
        VocabularioDoCatalogo.ParaRegras(fatos),
        VocabularioDoCatalogo.DescritoresDoFormulario(fatos),
        VocabularioDoCatalogo.DominiosDinamicos(fatos),
        VocabularioDoCatalogo.Derivacoes(fatos),
        VocabularioDoCatalogo.Formatos(fatos));
}

/// <summary>O que o modelo gravado já vincula no catálogo: o vínculo existente não é recusado ao ser desativado.</summary>
internal sealed record VinculosDoModelo(VinculosDeFatos Fatos, IReadOnlySet<(Guid TermoId, Guid VersaoId)> Termos)
{
    public static VinculosDoModelo Nenhum { get; } = new(
        new VinculosDeFatos(new HashSet<string>(), new HashSet<(string, string)>()), new HashSet<(Guid, Guid)>());
}

/// <summary>
/// A conferência do conteúdo do modelo contra o catálogo, pelas mesmas regras do formulário do
/// processo (<see cref="ConferenciaNoCatalogo"/>, <see cref="CoerenciaDoCampo"/>,
/// <see cref="PredicadoDnfValidador"/>). Cobre o fato que cada item e cada campo de grupo coleta, o tipo do campo, as
/// regras, as restrições, os pressupostos, a versão dos termos e o vínculo novo a fato ou valor
/// desativado. O item de forma inválida não é conferido: a recusa dele é a de forma, do modelo.
/// </summary>
/// <remarks>
/// Os valores das fontes do processo — opções declaradas, modalidades e municípios do bônus — só
/// existem no processo. Aqui são aceitos pela forma, e a cópia do modelo no processo os confere de
/// novo.
/// </remarks>
internal static class ConferenciaDoModelo
{
    public static List<FieldError> Conferir(
        ConteudoLido conteudo,
        CatalogoDoModelo catalogo,
        IReadOnlyDictionary<Guid, VersaoTermoConsentimentoView> versoes,
        VinculosDoModelo existentes)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(versoes);
        ArgumentNullException.ThrowIfNull(existentes);
        return [
            .. ConferirItens(conteudo.Itens, catalogo),
            .. ConferirGrupos(conteudo.Grupos, catalogo),
            .. ConferirDocumentos(conteudo.Documentos.ComCaminho(), catalogo),
            .. ConferirEtapas(conteudo.Etapas, catalogo),
            .. ConferirTermos(conteudo.Termos, catalogo, versoes, existentes.Termos),
            .. ConferirPressupostos(conteudo.Pressupostos, catalogo),
            .. ConferirVinculos(conteudo, catalogo, existentes),
        ];
    }

    /// <summary>O que o modelo gravado já vincula: os fatos e valores, e a versão de cada termo.</summary>
    public static VinculosDoModelo VinculosGravados(ConteudoDoModelo conteudo)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        return new(Vinculos(conteudo), conteudo.Termos.Select(static t => (t.TermoId, t.VersaoId)).ToHashSet());
    }

    /// <summary>
    /// O que o conteúdo vincula no catálogo: os fatos dos campos — itens e campos de grupo — e dos
    /// pressupostos, os fatos e valores citados pelas regras, os fatos cujas respostas formam opções
    /// e os valores das opções permitidas de cada campo.
    /// </summary>
    public static VinculosDeFatos Vinculos(ConteudoDoModelo conteudo)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        ItemDoModelo[] campos = [.. conteudo.Campos()];
        IEnumerable<(string Fato, JsonElement Valor)> condicoes = conteudo.CondicoesDasRegras().Select(static c => (c.Fato, c.Valor));
        IEnumerable<(string Fato, string Valor)> opcoes = campos
            .SelectMany(static i => i.Regras.Restricoes.OfType<OpcoesPermitidas>().SelectMany(static o => o.Entradas)
                .SelectMany(e => e.Valores.Select(v => (i.FatoCodigo, v))));
        IEnumerable<string> fatos = campos.Select(static i => i.FatoCodigo)
            .Concat(conteudo.Pressupostos)
            .Concat(campos.SelectMany(static i => i.Regras.Restricoes.SelectMany(static r => r.FatosCitados)))
            .Concat(conteudo.Documentos.ComCaminho().Select(static c => c.Documento.PorOpcao?.FatoCodigo).OfType<string>().Select(TextoGravavel.CodigoNaFormaGravada));
        return VinculosDeFatos.De(fatos, condicoes, opcoes);
    }

    private static IEnumerable<FieldError> ConferirItens(IReadOnlyList<(int Indice, ItemDoModelo Item)> itens, CatalogoDoModelo catalogo) =>
        itens.SelectMany(item => ConferirCampo(item.Item, $"itens[{item.Indice}]", catalogo, ConferenciaNoCatalogo.FatoDoItem));

    private static IEnumerable<FieldError> ConferirGrupos(IReadOnlyList<(int Indice, GrupoDoModelo Grupo)> grupos, CatalogoDoModelo catalogo) =>
        grupos.SelectMany(grupo => ConferirGrupo(grupo.Grupo, $"grupos[{grupo.Indice}]", catalogo));

    private static IEnumerable<FieldError> ConferirGrupo(GrupoDoModelo grupo, string caminho, CatalogoDoModelo catalogo) =>
    [
        .. ConferirPredicados(caminho, grupo.Predicados(), catalogo),
        .. grupo.Subitens.SelectMany((subitem, posicao) =>
            ConferirCampo(subitem, $"{caminho}.subitens[{posicao}]", catalogo, ConferenciaNoCatalogo.FatoDoSubitem)),
    ];

    private static IEnumerable<FieldError> ConferirDocumentos(
        IEnumerable<(string Caminho, DocumentoExigidoDoModelo Documento)> documentos, CatalogoDoModelo catalogo) =>
        documentos.SelectMany(documento => ConferirDocumento(documento.Documento, documento.Caminho, catalogo));

    private static IEnumerable<FieldError> ConferirDocumento(DocumentoExigidoDoModelo documento, string caminho, CatalogoDoModelo catalogo) =>
    [
        .. ConferirPredicados(caminho, documento.Predicados(), catalogo),
        .. ConferirExigenciaPorOpcao(documento.PorOpcao, caminho, catalogo),
    ];

    private static IEnumerable<FieldError> ConferirEtapas(IReadOnlyList<(int Indice, EtapaDoModelo Etapa)> etapas, CatalogoDoModelo catalogo) =>
        etapas.SelectMany(etapa => ConferirPredicados($"etapas[{etapa.Indice}]", etapa.Etapa.Predicados(), catalogo));

    private static IEnumerable<FieldError> ConferirTermos(
        IReadOnlyList<(int Indice, TermoDoModelo Termo)> termos,
        CatalogoDoModelo catalogo,
        IReadOnlyDictionary<Guid, VersaoTermoConsentimentoView> versoes,
        IReadOnlySet<(Guid TermoId, Guid VersaoId)> termosExistentes) =>
        termos.SelectMany(termo => ConferirTermo(termo.Termo, $"termos[{termo.Indice}]", catalogo, versoes, termosExistentes));

    private static IEnumerable<FieldError> ConferirTermo(
        TermoDoModelo termo,
        string caminho,
        CatalogoDoModelo catalogo,
        IReadOnlyDictionary<Guid, VersaoTermoConsentimentoView> versoes,
        IReadOnlySet<(Guid TermoId, Guid VersaoId)> termosExistentes) =>
    [
        .. ConferirPredicados(caminho, termo.Predicados(), catalogo),
        .. ConferirVersaoDoTermo(termo, caminho, versoes, termosExistentes),
    ];

    private static IEnumerable<FieldError> ConferirPressupostos(IReadOnlyList<string> pressupostos, CatalogoDoModelo catalogo) =>
        pressupostos.SelectMany((pressuposto, posicao) => ConferirPressuposto(pressuposto, $"pressupostos[{posicao}]", catalogo));

    private static IEnumerable<FieldError> ConferirPressuposto(string pressuposto, string caminho, CatalogoDoModelo catalogo)
    {
        string codigo = TextoGravavel.CodigoNaFormaGravada(pressuposto);
        return !string.IsNullOrWhiteSpace(pressuposto) && ConferenciaNoCatalogo.FatoDoItem(codigo, catalogo.Fatos) is { } recusa
            ? [new FieldError(caminho, recusa, codigo)]
            : [];
    }

    private static IEnumerable<FieldError> ConferirVinculos(ConteudoLido conteudo, CatalogoDoModelo catalogo, VinculosDoModelo existentes)
    {
        // Os vínculos são os do conteúdo como será gravado: um código com espaço é o mesmo fato.
        VinculosDeFatos propostos = Vinculos(conteudo.ParaConteudo().NaFormaGravada());
        return ConferenciaNoCatalogo.VinculoNovo(catalogo.Fatos, existentes.Fatos, propostos) is { IsFailure: true } vinculo ? vinculo.Errors : [];
    }

    /// <summary>
    /// O campo do modelo contra o catálogo: o fato coletável — como item ou como campo de grupo,
    /// conforme <paramref name="recusaDeColetavel"/> —, o tipo do campo, as regras e as restrições.
    /// O campo de forma inválida não é conferido: a recusa dele é a de forma, do modelo.
    /// </summary>
    private static IEnumerable<FieldError> ConferirCampo(
        ItemDoModelo item,
        string caminho,
        CatalogoDoModelo catalogo,
        Func<string, IReadOnlyDictionary<string, FatoDoCatalogo>, DomainError?> recusaDeColetavel)
    {
        if (FormaDoItem.ValidarFormaBasica(item.FatoCodigo, item.Ordem, item.Apresentacao.Rotulo).Count > 0)
        {
            return [];
        }

        string codigo = TextoGravavel.CodigoNaFormaGravada(item.FatoCodigo);
        return recusaDeColetavel(codigo, catalogo.Fatos) is { } naoColetavel
            ? [new FieldError($"{caminho}.fatoCodigo", naoColetavel, codigo)]
            : ConferirCampoDeFatoColetavel(item, codigo, caminho, catalogo);
    }

    private static IEnumerable<FieldError> ConferirCampoDeFatoColetavel(ItemDoModelo item, string codigo, string caminho, CatalogoDoModelo catalogo)
    {
        FatoDoCatalogo fato = catalogo.Fatos[codigo];
        return
        [
            .. ConferirTipoDoCampo(item, fato, codigo, caminho),
            .. ConferirPredicados(caminho, item.Predicados(), catalogo),
            .. ConferenciaNoCatalogo.SemanticaDasRestricoes(
                fato, item.Apresentacao.TipoRenderizacao, item.Regras.Restricoes, catalogo.Fatos, catalogo.Vocabulario, catalogo.DominiosDinamicos)
                .Select(restricao => restricao.NoCaminhoDe($"{caminho}.{FatoColetadoInput.CaminhoDasRegras}")),
            .. ConferirImpedimentoDoItem(item, caminho, catalogo),
        ];
    }

    private static IEnumerable<FieldError> ConferirImpedimentoDoItem(ItemDoModelo item, string caminho, CatalogoDoModelo catalogo) =>
        ConferirPredicado($"{caminho}.{FatoColetadoInput.CaminhoDasRegras}.impedimento.quando", item.Regras.Impedimento?.Quando, catalogo) is { } recusa ? [recusa] : [];

    private static IEnumerable<FieldError> ConferirTipoDoCampo(ItemDoModelo item, FatoDoCatalogo fato, string codigo, string caminho) =>
        CoerenciaDoCampo.Validar(codigo, item.Apresentacao.TipoRenderizacao, fato.Dominio, fato.Cardinalidade, fato.FonteValores) is { } incoerencia
            ? [new FieldError($"{caminho}.{FatoColetadoInput.CaminhoDaApresentacao}.tipoRenderizacao", incoerencia, codigo)]
            : [];

    private static IEnumerable<FieldError> ConferirVersaoDoTermo(
        TermoDoModelo termo,
        string caminho,
        IReadOnlyDictionary<Guid, VersaoTermoConsentimentoView> versoes,
        IReadOnlySet<(Guid TermoId, Guid VersaoId)> termosExistentes)
    {
        // A versão já escolhida pelo modelo continua com ele, mesmo que o catálogo tenha excluído o
        // termo depois: a recusa é só do vínculo novo (ADR-0136).
        if (termosExistentes.Contains((termo.TermoId, termo.VersaoId))
            || (versoes.TryGetValue(termo.VersaoId, out VersaoTermoConsentimentoView? versao) && versao.TermoId == termo.TermoId))
        {
            return [];
        }

        return [new FieldError($"{caminho}.{TermoExigidoInput.CaminhoDoTermo}.versaoId", new DomainError(
            ModeloFormularioErrorCodes.TermoVersaoNaoEncontrada,
            "A versão informada não existe no catálogo de termos, ou pertence a outro termo."),
            $"{termo.TermoId}/{termo.VersaoId}")];
    }

    private static IEnumerable<FieldError> ConferirPredicados(
        string caminhoDaParte, IEnumerable<PredicadoNaEntrada> predicados, CatalogoDoModelo catalogo) =>
        predicados
            .Select(predicado => ConferirPredicado($"{caminhoDaParte}.{predicado.Caminho}", predicado.Predicado, catalogo))
            .OfType<FieldError>();

    /// <summary>
    /// A regra que cita fato calculado de atributos do candidato é recusada antes da semântica; a
    /// citação de fato fora do formulário é do grafo do modelo. A recusa leva o fato (e o valor, quando
    /// é do valor) que a causa.
    /// </summary>
    private static FieldError? ConferirPredicado(string campo, PredicadoDnf? predicado, CatalogoDoModelo catalogo)
    {
        if (predicado is null)
        {
            return null;
        }

        if (ConferenciaNoCatalogo.AtributoCitado(predicado.FatosCitados, catalogo.Fatos) is { } atributo)
        {
            return new(campo, ConferenciaNoCatalogo.CitacaoDeAtributoDoCandidato([atributo], catalogo.Fatos)!, atributo);
        }

        return PredicadoDnfValidador.ValidarComCausa(predicado, catalogo.Vocabulario, null, catalogo.DominiosDinamicos)
            is ({ IsFailure: true } recusa, { } causa, _)
            ? new(campo, recusa.Error!, causa)
            : null;
    }

    private static IEnumerable<FieldError> ConferirExigenciaPorOpcao(ExigenciaPorOpcao? porOpcao, string caminho, CatalogoDoModelo catalogo) =>
        porOpcao is not null && RecusaDoFatoPorOpcao(porOpcao.FatoCodigo, catalogo) is { } recusa
            ? [new FieldError($"{caminho}.porOpcao.fatoCodigo", recusa, porOpcao.FatoCodigo)]
            : [];

    /// <summary>
    /// O documento só se multiplica pelas opções que o processo declara para o fato: são elas que a
    /// aplicação do modelo enumera. Fato de valores globais ou de outra fonte não tem essa lista.
    /// </summary>
    private static DomainError? RecusaDoFatoPorOpcao(string fatoCodigoInformado, CatalogoDoModelo catalogo)
    {
        string fatoCodigo = TextoGravavel.CodigoNaFormaGravada(fatoCodigoInformado);
        if (!catalogo.Fatos.TryGetValue(fatoCodigo, out FatoDoCatalogo? fato))
        {
            return new(ItemFormularioErrorCodes.FatoDesconhecido, "O fato informado não pertence ao catálogo de fatos do candidato.");
        }

        return string.Equals(fato.Dominio, DominiosFato.ParaTokenCanonico(DominioFato.Categorico), StringComparison.Ordinal)
            && string.Equals(fato.FonteValores, FontesValoresFato.ParaTokenCanonico(FonteValoresFato.Processo), StringComparison.Ordinal)
            ? null
            : new(ModeloFormularioErrorCodes.ExigenciaPorOpcaoInvalida,
                "O documento só pode ser exigido uma vez por opção de fato cujas opções o processo declara. Informe um fato que atenda a essa condição.");
    }
}
