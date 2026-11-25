namespace Unifesspa.UniPlus.Regras.Formularios;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>Um valor do domínio de um fato no catálogo; o desativado não aceita vínculo novo.</summary>
public sealed record ValorDoCatalogo(string Codigo, bool Ativo);

/// <summary>
/// O fato do catálogo como as regras do formulário o conferem, pelos tokens do catálogo. O processo
/// o lê pelo contrato da Configuração, e o modelo, pela própria entidade.
/// </summary>
/// <param name="Valores">Os valores de um categórico de fonte global; vazio nos demais.</param>
/// <param name="ProtecaoAprovada">Se a proteção de dados aprovou a proteção do fato; sem a aprovação, o fato não aceita vínculo novo.</param>
public sealed record FatoDoCatalogo(
    string Codigo,
    TipoDoDado Dominio,
    QuantidadeDeValores Cardinalidade,
    OrigemFato Origem,
    string Binding,
    FonteValoresFato? FonteValores,
    string Escopo,
    bool Ativo,
    IReadOnlyList<ValorDoCatalogo> Valores,
    bool ProtecaoAprovada);

/// <summary>
/// A conferência das regras do formulário contra o catálogo de fatos, no processo e no modelo: o
/// fato que o item coleta, os fatos que as regras citam, as restrições de valor e o vínculo novo a
/// fato ou valor desativado.
/// </summary>
public static class ConferenciaNoCatalogo
{
    private const string EscopoCandidato = "CANDIDATO";
    private const string EscopoMembroGrupo = "MEMBRO_GRUPO";

    /// <summary>
    /// Coletável é o fato declarado, respondido num campo de formulário, do próprio candidato. O
    /// derivado e o calculado não se respondem; o de membro de grupo só existe dentro do grupo.
    /// </summary>
    public static bool EhColetavel(FatoDoCatalogo fato) => EhRespondidoNoEscopo(fato, EscopoCandidato);

    /// <summary>
    /// Coletável num campo de grupo repetível é o fato declarado, respondido num campo de formulário,
    /// de membro do grupo: cada ocorrência responde o seu.
    /// </summary>
    public static bool EhColetavelEmGrupo(FatoDoCatalogo fato) => EhRespondidoNoEscopo(fato, EscopoMembroGrupo);

    /// <summary>O fato que o campo do grupo coleta: existe no catálogo e é coletável em grupo.</summary>
    public static DomainError? FatoDoSubitem(string fatoCodigo, IReadOnlyDictionary<string, FatoDoCatalogo> catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        if (!catalogo.TryGetValue(fatoCodigo, out FatoDoCatalogo? fato))
        {
            return FatoDesconhecido(fatoCodigo);
        }

        return EhColetavelEmGrupo(fato)
            ? null
            : new DomainError(
                ItemFormularioErrorCodes.FatoNaoColetavel,
                $"O fato '{fatoCodigo}' não é coletável num campo do grupo: só o fato declarado de membro de grupo é respondido "
                + "em cada ocorrência — fatos do candidato, derivados e calculados não.");
    }

    private static bool EhRespondidoNoEscopo(FatoDoCatalogo fato, string escopo)
    {
        ArgumentNullException.ThrowIfNull(fato);
        return fato.Origem == OrigemFato.Declarado
            && string.Equals(fato.Escopo, escopo, StringComparison.Ordinal)
            && VinculoDeFato.Usa(fato.Binding, MecanismoDoVinculo.CampoFormulario);
    }

    private static DomainError FatoDesconhecido(string fatoCodigo) =>
        new(ItemFormularioErrorCodes.FatoDesconhecido, $"O fato '{fatoCodigo}' não pertence ao catálogo de fatos do candidato.");

    /// <summary>O fato que o item coleta: existe no catálogo e é coletável.</summary>
    public static DomainError? FatoDoItem(string fatoCodigo, IReadOnlyDictionary<string, FatoDoCatalogo> catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        if (!catalogo.TryGetValue(fatoCodigo, out FatoDoCatalogo? fato))
        {
            return FatoDesconhecido(fatoCodigo);
        }

        return EhColetavel(fato)
            ? null
            : new DomainError(
                ItemFormularioErrorCodes.FatoNaoColetavel,
                $"O fato '{fatoCodigo}' não é coletável num campo: só o fato declarado do próprio candidato é respondido no "
                + "formulário — derivados, calculados e fatos de membro de grupo não.");
    }

    /// <summary>
    /// A recusa de citar, numa regra do formulário, um fato que o sistema calcula de atributos do
    /// candidato sem dependências declaradas (<see cref="DerivadosDoSistema"/>), como a renda per
    /// capita: o formulário não tem como saber em que ponto ele fica conhecido. O que declara as
    /// dependências é conferido pelo grafo do formulário, como o derivado por regra.
    /// </summary>
    public static DomainError? CitacaoDeAtributoDoCandidato(IEnumerable<string> citados, IReadOnlyDictionary<string, FatoDoCatalogo> catalogo) =>
        AtributoCitado(citados, catalogo) is { } atributo
            ? new DomainError(
                GrafoFormularioErrorCodes.CitaAtributoDoCandidato,
                $"O fato '{atributo}' é calculado pelo sistema a partir de atributos do candidato sem dependências declaradas e não pode ser citado em regra do formulário.")
            : null;

    /// <summary>O primeiro fato citado que é atributo do candidato sem dependências declaradas.</summary>
    public static string? AtributoCitado(IEnumerable<string> citados, IReadOnlyDictionary<string, FatoDoCatalogo> catalogo)
    {
        ArgumentNullException.ThrowIfNull(citados);
        ArgumentNullException.ThrowIfNull(catalogo);
        return citados.FirstOrDefault(c => c is not null && catalogo.TryGetValue(c, out FatoDoCatalogo? fato)
            && VinculoDeFato.Usa(fato.Binding, MecanismoDoVinculo.AtributoCandidato)
            && !DerivadosDoSistema.Dependencias.ContainsKey(c));
    }

    /// <summary>
    /// As restrições contra o catálogo: a condição de cada grupo de opções valida como qualquer
    /// predicado; os valores permitidos são do domínio do próprio fato, conferidos como a condição
    /// <c>FATO ALGUM_DE [valores]</c>; e as respostas que formam as opções vêm de campos categóricos cujas
    /// opções são todas opções do campo, para que toda resposta anterior seja uma opção válida.
    /// A restrição que não cabe no tipo do campo fica para a recusa de coerência do item.
    /// </summary>
    public static IEnumerable<FieldError> SemanticaDasRestricoes(
        FatoDoCatalogo alvo,
        TipoDeCampo tipoDeCampo,
        IReadOnlyList<RestricaoValor> restricoes,
        IReadOnlyDictionary<string, FatoDoCatalogo> catalogo,
        IReadOnlyDictionary<string, DescritorFatoCandidato> vocabulario,
        IReadOnlyDictionary<string, DominioDeValores> dominiosDinamicos)
    {
        ArgumentNullException.ThrowIfNull(alvo);
        ArgumentNullException.ThrowIfNull(restricoes);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(vocabulario);
        ArgumentNullException.ThrowIfNull(dominiosDinamicos);
        return Semantica(alvo, tipoDeCampo, restricoes, catalogo, vocabulario, dominiosDinamicos);
    }

    /// <summary>
    /// Desativar um fato ou um valor no catálogo recusa só vínculo novo (ADR-0136): a configuração
    /// que já usava o fato, ou já citava o valor, continua com ele. O fato sem a proteção aprovada
    /// pela proteção de dados também só recusa vínculo novo (ADR-0153): quem já o vincula é barrado na
    /// publicação. Compara o que se propõe com o que já está vinculado e recusa, de uma vez, todo
    /// vínculo novo a desativado ou sem a aprovação.
    /// </summary>
    public static Result VinculoNovo(IReadOnlyDictionary<string, FatoDoCatalogo> catalogo, VinculosDeFatos existentes, VinculosDeFatos propostos)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(existentes);
        ArgumentNullException.ThrowIfNull(propostos);

        List<FieldError> erros = [.. FatosDesativados(catalogo, existentes, propostos)];
        erros.AddRange(FatosSemProtecao(catalogo, existentes, propostos));
        erros.AddRange(ValoresDesativados(catalogo, existentes, propostos));
        return erros.Count > 0 ? Result.ValidationFailure(erros) : Result.Success();
    }

    private static IEnumerable<FieldError> FatosDesativados(
        IReadOnlyDictionary<string, FatoDoCatalogo> catalogo, VinculosDeFatos existentes, VinculosDeFatos propostos) =>
        propostos.Fatos.Where(fato => !existentes.Fatos.Contains(fato) && catalogo.TryGetValue(fato, out FatoDoCatalogo? f) && !f.Ativo)
            .Order(StringComparer.Ordinal).Select(static fato => new FieldError(null, new DomainError(
                VinculoCatalogoErrorCodes.FatoDesativado, $"O fato '{fato}' está desativado no catálogo e não aceita vínculo novo."), fato));

    private static IEnumerable<FieldError> FatosSemProtecao(
        IReadOnlyDictionary<string, FatoDoCatalogo> catalogo, VinculosDeFatos existentes, VinculosDeFatos propostos) =>
        propostos.Fatos.Where(fato => !existentes.Fatos.Contains(fato) && catalogo.TryGetValue(fato, out FatoDoCatalogo? f) && !f.ProtecaoAprovada)
            .Order(StringComparer.Ordinal).Select(static fato => new FieldError(null, new DomainError(
                VinculoCatalogoErrorCodes.FatoComProtecaoPendente, $"O fato '{fato}' aguarda a aprovação da proteção de dados e não aceita vínculo novo."), fato));

    private static IEnumerable<FieldError> ValoresDesativados(
        IReadOnlyDictionary<string, FatoDoCatalogo> catalogo, VinculosDeFatos existentes, VinculosDeFatos propostos) =>
        propostos.Valores.Where(valor => !existentes.Valores.Contains(valor)
            && catalogo.TryGetValue(valor.Fato, out FatoDoCatalogo? f) && f.Valores.Any(v => v.Codigo == valor.Valor && !v.Ativo))
            .OrderBy(static v => v.Fato, StringComparer.Ordinal).ThenBy(static v => v.Valor, StringComparer.Ordinal)
            .Select(static v => new FieldError(null, new DomainError(
                VinculoCatalogoErrorCodes.ValorDesativado, $"O valor '{v.Valor}' do fato '{v.Fato}' está desativado no catálogo e não aceita vínculo novo."), $"{v.Fato}.{v.Valor}"));

    private static IEnumerable<FieldError> Semantica(
        FatoDoCatalogo alvo,
        TipoDeCampo tipoDeCampo,
        IReadOnlyList<RestricaoValor> restricoes,
        IReadOnlyDictionary<string, FatoDoCatalogo> catalogo,
        IReadOnlyDictionary<string, DescritorFatoCandidato> vocabulario,
        IReadOnlyDictionary<string, DominioDeValores> dominiosDinamicos)
    {
        for (int indice = 0; indice < restricoes.Count; indice++)
        {
            if (!FormaDoItem.RestricaoCabeNoCampo(restricoes[indice].Tipo, tipoDeCampo))
            {
                continue;
            }

            foreach (FieldError erro in SemanticaDaRestricao(restricoes[indice], $"restricoes[{indice}]", alvo, catalogo, vocabulario, dominiosDinamicos))
            {
                yield return erro;
            }
        }
    }

    private static IEnumerable<FieldError> SemanticaDaRestricao(
        RestricaoValor restricao, string campo, FatoDoCatalogo alvo, IReadOnlyDictionary<string, FatoDoCatalogo> catalogo,
        IReadOnlyDictionary<string, DescritorFatoCandidato> vocabulario, IReadOnlyDictionary<string, DominioDeValores> dominiosDinamicos)
    {
        if (restricao is OpcoesPermitidas opcoes)
        {
            return ConferirOpcoes(opcoes, campo, alvo, vocabulario, dominiosDinamicos);
        }

        return ConferirOutrasRestricoes(restricao, campo, alvo, catalogo, dominiosDinamicos) is { } erro ? [erro] : [];
    }

    private static FieldError? ConferirOutrasRestricoes(RestricaoValor restricao, string campo, FatoDoCatalogo alvo,
        IReadOnlyDictionary<string, FatoDoCatalogo> catalogo, IReadOnlyDictionary<string, DominioDeValores> dominiosDinamicos) =>
        restricao switch
        {
            OpcoesDasRespostas respostas when respostas.Fatos.Any(f => !catalogo.TryGetValue(f, out FatoDoCatalogo? fonte) || !OpcoesDaFonteCabemNoAlvo(alvo, fonte, dominiosDinamicos)) =>
                RecusaDasRespostas(respostas, campo, alvo, catalogo, dominiosDinamicos),
            MunicipiosDaUf daUf when !catalogo.TryGetValue(daUf.FatoUf, out FatoDoCatalogo? uf)
                || uf.FonteValores != FonteValoresFato.UnidadesFederativas
                || uf.Cardinalidade != QuantidadeDeValores.ValorUnico =>
                new($"{campo}.fatos", new DomainError(ItemFormularioErrorCodes.UfDoMunicipioInvalida,
                    "Os municípios da UF citam um campo de UF de resposta única, com as UFs do Geo."), daUf.FatoUf),
            _ => null,
        };

    private static FieldError RecusaDasRespostas(OpcoesDasRespostas respostas, string campo, FatoDoCatalogo alvo,
        IReadOnlyDictionary<string, FatoDoCatalogo> catalogo, IReadOnlyDictionary<string, DominioDeValores> dominios) =>
        new($"{campo}.fatos", new DomainError(ItemFormularioErrorCodes.OpcoesDeOutroDominio,
            "As opções formadas pelas respostas vêm de campos de seleção cujas opções são todas opções do campo."),
            string.Join(',', respostas.Fatos.Where(f => !catalogo.TryGetValue(f, out FatoDoCatalogo? fonte) || !OpcoesDaFonteCabemNoAlvo(alvo, fonte, dominios)).Order(StringComparer.Ordinal)));

    private static IEnumerable<FieldError> ConferirOpcoes(
        OpcoesPermitidas opcoes, string campo, FatoDoCatalogo alvo,
        IReadOnlyDictionary<string, DescritorFatoCandidato> vocabulario, IReadOnlyDictionary<string, DominioDeValores> dominios)
    {
        for (int entrada = 0; entrada < opcoes.Entradas.Count; entrada++)
        {
            OpcoesCondicionadas grupo = opcoes.Entradas[entrada];
            if (grupo.Quando is { } quando
                && PredicadoDnfValidador.ValidarComCausa(quando, vocabulario, null, dominios) is ({ IsFailure: true } condicao, { } causa, _))
            {
                yield return new($"{campo}.entradas[{entrada}].quando", condicao.Error!, causa);
            }

            if (ConferirValores(grupo, campo, entrada, alvo, vocabulario, dominios) is { } erro)
            {
                yield return erro;
            }
        }
    }

    private static FieldError? ConferirValores(OpcoesCondicionadas grupo, string campo, int entrada, FatoDoCatalogo alvo,
        IReadOnlyDictionary<string, DescritorFatoCandidato> vocabulario, IReadOnlyDictionary<string, DominioDeValores> dominios)
    {
        PredicadoDnf pertinencia = PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(
            alvo.Codigo, Operador.AlgumDe, JsonSerializer.SerializeToElement(grupo.Valores.Order(StringComparer.Ordinal))).Value!)]).Value!;
        if (PredicadoDnfValidador.ValidarComCausa(pertinencia, vocabulario, null, dominios) is ({ IsFailure: true } valores, { } causaDosValores, _))
        {
            return new($"{campo}.entradas[{entrada}].valores", valores.Error!, causaDosValores);
        }
        return null;
    }

    /// <summary>
    /// Toda resposta possível da fonte é opção do alvo: os valores da fonte cabem nos do alvo; no
    /// domínio por formato, que não enumera valores, a fonte dos valores é a mesma.
    /// </summary>
    private static bool OpcoesDaFonteCabemNoAlvo(
        FatoDoCatalogo alvo, FatoDoCatalogo fonte, IReadOnlyDictionary<string, DominioDeValores> dominiosDinamicos) =>
        fonte.Dominio == alvo.Dominio
        && (ValoresDoDominio(fonte, dominiosDinamicos), ValoresDoDominio(alvo, dominiosDinamicos)) switch
        {
            (null, null) => fonte.FonteValores == alvo.FonteValores,
            ({ } daFonte, { } doAlvo) => daFonte.IsSubsetOf(doAlvo),
            _ => false,
        };

    /// <summary>Os valores enumerados do domínio do fato — do catálogo ou do processo —; nulo no domínio por formato.</summary>
    private static IReadOnlySet<string>? ValoresDoDominio(FatoDoCatalogo fato, IReadOnlyDictionary<string, DominioDeValores> dominiosDinamicos) =>
        fato.Valores is { Count: > 0 } estaticos
            ? estaticos.Select(static v => v.Codigo).ToHashSet(StringComparer.Ordinal)
            : dominiosDinamicos.TryGetValue(fato.Codigo, out DominioDeValores? dinamico) ? dinamico.Valores : null;
}
