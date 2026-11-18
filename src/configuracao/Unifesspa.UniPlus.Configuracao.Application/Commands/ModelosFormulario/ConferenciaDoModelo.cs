namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using System.Text.Json;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Results;
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

        return
        [
            .. ConferirCampos(conteudo, catalogo),
            .. ConferirPredicadosDasPartes(conteudo, catalogo),
            .. ConferirTermos(conteudo, catalogo, versoes, existentes),
            .. ConferirPressupostos(conteudo, catalogo),
            .. ConferirVinculoNovo(conteudo, catalogo, existentes),
        ];
    }

    private static IEnumerable<FieldError> ConferirCampos(ConteudoLido conteudo, CatalogoDoModelo catalogo) =>
        conteudo.Itens
            .SelectMany(item => ConferirCampo(item.Item, catalogo, ConferenciaNoCatalogo.FatoDoItem)
                .Select(e => e with { Field = Prefixar($"itens[{item.Indice}]", e.Field) }))
            .Concat(conteudo.Grupos.SelectMany(grupo => ConferirGrupo(grupo.Indice, grupo.Grupo, catalogo)));

    private static IEnumerable<FieldError> ConferirGrupo(int indice, GrupoDoModelo grupo, CatalogoDoModelo catalogo) =>
        new[]
        {
            ConferirPredicado($"grupos[{indice}].exibicao", grupo.Exibicao, catalogo),
            ConferirPredicado($"grupos[{indice}].predicadoObrigatoriedade", grupo.Obrigatoriedade.Predicado, catalogo),
        }.OfType<FieldError>()
        .Concat(grupo.Subitens.SelectMany((subitem, j) => ConferirCampo(subitem, catalogo, ConferenciaNoCatalogo.FatoDoSubitem)
            .Select(e => e with { Field = Prefixar($"grupos[{indice}].subitens[{j}]", e.Field) })));

    private static IEnumerable<FieldError> ConferirPredicadosDasPartes(ConteudoLido conteudo, CatalogoDoModelo catalogo) =>
        conteudo.Documentos.SelectMany(documento => ConferirDocumento(documento.Indice, documento.Documento, catalogo))
            .Concat(conteudo.Etapas
                .Select(etapa => ConferirPredicado($"etapas[{etapa.Indice}].exibicao", etapa.Etapa.Exibicao, catalogo))
                .OfType<FieldError>());

    private static IEnumerable<FieldError> ConferirDocumento(int indice, DocumentoExigidoDoModelo documento, CatalogoDoModelo catalogo)
    {
        FieldError? porOpcao = documento.PorOpcao is { } opcao && ConferirPorOpcao(opcao.FatoCodigo, catalogo) is { } recusa
            ? new($"documentosExigidos[{indice}].porOpcao.fatoCodigo", recusa, opcao.FatoCodigo)
            : null;
        return new[] { ConferirPredicado($"documentosExigidos[{indice}].quando", documento.Quando, catalogo), porOpcao }.OfType<FieldError>();
    }

    private static IEnumerable<FieldError> ConferirTermos(
        ConteudoLido conteudo,
        CatalogoDoModelo catalogo,
        IReadOnlyDictionary<Guid, VersaoTermoConsentimentoView> versoes,
        VinculosDoModelo existentes) =>
        conteudo.Termos.SelectMany(termo => ConferirTermo(termo.Termo, catalogo, versoes, existentes.Termos)
            .Select(e => e with { Field = Prefixar($"termos[{termo.Indice}]", e.Field) }));

    private static IEnumerable<FieldError> ConferirPressupostos(ConteudoLido conteudo, CatalogoDoModelo catalogo) =>
        conteudo.Pressupostos.Select((texto, i) => ConferirPressuposto(texto, i, catalogo)).OfType<FieldError>();

    private static FieldError? ConferirPressuposto(string texto, int indice, CatalogoDoModelo catalogo)
    {
        string pressuposto = ModeloFormulario.CodigoNaFormaGravada(texto);
        return !string.IsNullOrWhiteSpace(texto) && ConferenciaNoCatalogo.FatoDoItem(pressuposto, catalogo.Fatos) is { } recusa
            ? new($"pressupostos[{indice}]", recusa, pressuposto)
            : null;
    }

    // Os vínculos são os do conteúdo como será gravado: um código com espaço é o mesmo fato.
    private static IReadOnlyList<FieldError> ConferirVinculoNovo(ConteudoLido conteudo, CatalogoDoModelo catalogo, VinculosDoModelo existentes)
    {
        VinculosDeFatos propostos = Vinculos(ModeloFormulario.NaFormaGravada(conteudo.ParaConteudo()));
        return ConferenciaNoCatalogo.VinculoNovo(catalogo.Fatos, existentes.Fatos, propostos) is { IsFailure: true } vinculo
            ? vinculo.Errors
            : [];
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
        ItemDoModelo[] campos = [.. conteudo.Itens, .. conteudo.Grupos.SelectMany(static g => g.Subitens)];
        IEnumerable<PredicadoDnf> predicados = Predicados(conteudo, campos);
        IEnumerable<(string Fato, JsonElement Valor)> condicoes = predicados
            .SelectMany(static p => p.Clausulas).SelectMany(static c => c.Condicoes).Select(static c => (c.Fato, c.Valor));
        IEnumerable<(string Fato, string Valor)> opcoes = campos
            .SelectMany(static i => i.Restricoes.OfType<OpcoesPermitidas>().SelectMany(static o => o.Entradas)
                .SelectMany(e => e.Valores.Select(v => (i.FatoCodigo, v))));
        IEnumerable<string> fatos = campos.Select(static i => i.FatoCodigo)
            .Concat(conteudo.Pressupostos)
            .Concat(campos.SelectMany(static i => i.Restricoes.SelectMany(static r => r.FatosCitados)))
            .Concat(conteudo.Documentos.Select(static d => d.PorOpcao?.FatoCodigo).OfType<string>().Select(ModeloFormulario.CodigoNaFormaGravada));
        return VinculosDeFatos.De(fatos, condicoes, opcoes);
    }

    private static IEnumerable<PredicadoDnf> Predicados(ConteudoDoModelo conteudo, ItemDoModelo[] campos)
    {
        return campos
            .SelectMany(static i => new[] { i.Exibicao, i.Obrigatoriedade.Predicado, i.Impedimento?.Quando }
                .Concat(i.Restricoes.OfType<OpcoesPermitidas>().SelectMany(static o => o.Entradas.Select(static e => e.Quando))))
            .Concat(conteudo.Grupos.SelectMany(static g => new[] { g.Exibicao, g.Obrigatoriedade.Predicado }))
            .Concat(conteudo.Etapas.Select(static e => e.Exibicao))
            .Concat(conteudo.Termos.SelectMany(static t => new[] { t.Exibicao, t.Obrigatoriedade.Predicado }))
            .Concat(conteudo.Documentos.Select(static d => d.Quando))
            .OfType<PredicadoDnf>();
    }

    /// <summary>
    /// O campo do modelo contra o catálogo: o fato coletável — como item ou como campo de grupo,
    /// conforme <paramref name="recusaDeColetavel"/> —, o tipo do campo, as regras e as restrições.
    /// </summary>
    private static IEnumerable<FieldError> ConferirCampo(
        ItemDoModelo item, CatalogoDoModelo catalogo, Func<string, IReadOnlyDictionary<string, FatoDoCatalogo>, DomainError?> recusaDeColetavel)
    {
        if (FormaDoItem.ValidarFormaBasica(item.FatoCodigo, item.Ordem, item.Rotulo, item.TipoRenderizacao).Count > 0)
        {
            return [];
        }

        string codigo = ModeloFormulario.CodigoNaFormaGravada(item.FatoCodigo);
        if (recusaDeColetavel(codigo, catalogo.Fatos) is { } naoColetavel)
        {
            return [new("fatoCodigo", naoColetavel, codigo)];
        }

        return ConferirCampoConhecido(item, catalogo, codigo);
    }

    private static IEnumerable<FieldError> ConferirCampoConhecido(ItemDoModelo item, CatalogoDoModelo catalogo, string codigo)
    {
        FatoDoCatalogo fato = catalogo.Fatos[codigo];
        if (CoerenciaDoCampo.Validar(codigo, item.TipoRenderizacao, fato.Dominio, fato.Cardinalidade, fato.FonteValores) is { } incoerencia)
        {
            yield return new("tipoRenderizacao", incoerencia, codigo);
        }

        foreach (FieldError erro in ConferirRegrasERestricoes(item, catalogo, fato))
        {
            yield return erro;
        }
    }

    private static IEnumerable<FieldError> ConferirRegrasERestricoes(ItemDoModelo item, CatalogoDoModelo catalogo, FatoDoCatalogo fato) =>
        ConferirRegrasDoCampo(item, catalogo).Concat(ConferenciaNoCatalogo.SemanticaDasRestricoes(
            fato, item.TipoRenderizacao, item.Restricoes, catalogo.Fatos, catalogo.Vocabulario, catalogo.DominiosDinamicos));

    private static IEnumerable<FieldError> ConferirRegrasDoCampo(ItemDoModelo item, CatalogoDoModelo catalogo)
    {
        if (ConferirPredicado("precondicao", item.Exibicao, catalogo) is { } exibicao)
        {
            yield return exibicao;
        }

        if (ConferirPredicado("predicadoObrigatoriedade", item.Obrigatoriedade.Predicado, catalogo) is { } obrigatoriedade)
        {
            yield return obrigatoriedade;
        }

        FieldError? impedimento = ConferirPredicado("impedimento.quando", item.Impedimento?.Quando, catalogo);
        if (impedimento is not null) yield return impedimento;
    }

    private static IEnumerable<FieldError> ConferirTermo(
        TermoDoModelo termo,
        CatalogoDoModelo catalogo,
        IReadOnlyDictionary<Guid, VersaoTermoConsentimentoView> versoes,
        IReadOnlySet<(Guid TermoId, Guid VersaoId)> termosExistentes)
    {
        if (ConferirPredicado("exibicao", termo.Exibicao, catalogo) is { } exibicao)
        {
            yield return exibicao;
        }

        if (ConferirPredicado("predicadoObrigatoriedade", termo.Obrigatoriedade.Predicado, catalogo) is { } obrigatoriedade)
        {
            yield return obrigatoriedade;
        }

        if (ConferirVersaoDoTermo(termo, versoes, termosExistentes) is { } versao)
        {
            yield return versao;
        }
    }

    private static FieldError? ConferirVersaoDoTermo(TermoDoModelo termo,
        IReadOnlyDictionary<Guid, VersaoTermoConsentimentoView> versoes, IReadOnlySet<(Guid TermoId, Guid VersaoId)> termosExistentes)
    {
        // A versão já escolhida pelo modelo continua com ele, mesmo que o catálogo tenha excluído o
        // termo depois: a recusa é só do vínculo novo (ADR-0136).
        if (!termosExistentes.Contains((termo.TermoId, termo.VersaoId))
            && (!versoes.TryGetValue(termo.VersaoId, out VersaoTermoConsentimentoView? versao) || versao.TermoId != termo.TermoId))
        {
            return new("versaoId", new DomainError(
                ModeloFormularioErrorCodes.TermoVersaoNaoEncontrada,
                "A versão informada não existe no catálogo de termos, ou pertence a outro termo."),
                $"{termo.TermoId}/{termo.VersaoId}");
        }

        return null;
    }

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

    /// <summary>
    /// O documento só se multiplica pelas opções que o processo declara para o fato: são elas que a
    /// aplicação do modelo enumera. Fato de valores globais ou de outra fonte não tem essa lista.
    /// </summary>
    private static DomainError? ConferirPorOpcao(string fatoCodigoInformado, CatalogoDoModelo catalogo)
    {
        string fatoCodigo = ModeloFormulario.CodigoNaFormaGravada(fatoCodigoInformado);
        if (!catalogo.Fatos.TryGetValue(fatoCodigo, out FatoDoCatalogo? fato))
        {
            return new(ItemFormularioErrorCodes.FatoDesconhecido, $"O fato '{fatoCodigo}' não pertence ao catálogo de fatos do candidato.");
        }

        return string.Equals(fato.Dominio, DominiosFato.ParaTokenCanonico(DominioFato.Categorico), StringComparison.Ordinal)
            && string.Equals(fato.FonteValores, FontesValoresFato.ParaTokenCanonico(FonteValoresFato.Processo), StringComparison.Ordinal)
            ? null
            : new(ModeloFormularioErrorCodes.ExigenciaPorOpcaoInvalida,
                $"O documento só pode ser exigido uma vez por opção de fato cujas opções o processo declara; o fato '{fatoCodigo}' não é um deles.");
    }

    private static string Prefixar(string prefixo, string? campo) => string.IsNullOrEmpty(campo) ? prefixo : $"{prefixo}.{campo}";
}
