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
public sealed record FatoDoCatalogo(
    string Codigo,
    string Dominio,
    string Cardinalidade,
    string Origem,
    string Binding,
    string? FonteValores,
    string Escopo,
    bool Ativo,
    IReadOnlyList<ValorDoCatalogo> Valores);

/// <summary>
/// A conferência das regras do formulário contra o catálogo de fatos, no processo e no modelo: o
/// fato que o item coleta, os fatos que as regras citam, as restrições de valor e o vínculo novo a
/// fato ou valor desativado.
/// </summary>
public static class ConferenciaNoCatalogo
{
    private const string OrigemDeclarado = "DECLARADO";
    private const string EscopoCandidato = "CANDIDATO";

    /// <summary>
    /// Coletável é o fato declarado, respondido num campo de formulário, do próprio candidato. O
    /// derivado e o calculado não se respondem; o de membro de grupo só existe dentro do grupo.
    /// </summary>
    public static bool EhColetavel(FatoDoCatalogo fato)
    {
        ArgumentNullException.ThrowIfNull(fato);
        return string.Equals(fato.Origem, OrigemDeclarado, StringComparison.Ordinal)
            && string.Equals(fato.Escopo, EscopoCandidato, StringComparison.Ordinal)
            && VinculoDeFato.Usa(fato.Binding, VinculoDeFato.CampoDoFormulario);
    }

    /// <summary>O fato que o item coleta: existe no catálogo e é coletável.</summary>
    public static DomainError? FatoDoItem(string fatoCodigo, IReadOnlyDictionary<string, FatoDoCatalogo> catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        if (!catalogo.TryGetValue(fatoCodigo, out FatoDoCatalogo? fato))
        {
            return new DomainError(
                ItemFormularioErrorCodes.FatoDesconhecido, $"O fato '{fatoCodigo}' não pertence ao catálogo de fatos do candidato.");
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
    /// candidato, como a faixa etária: as dependências dele não são declaradas, e o formulário não
    /// tem como saber em que ponto ele fica conhecido.
    /// </summary>
    public static DomainError? CitacaoDeAtributoDoCandidato(IEnumerable<string> citados, IReadOnlyDictionary<string, FatoDoCatalogo> catalogo)
    {
        ArgumentNullException.ThrowIfNull(citados);
        ArgumentNullException.ThrowIfNull(catalogo);
        return citados.FirstOrDefault(c => c is not null && catalogo.TryGetValue(c, out FatoDoCatalogo? fato)
                && VinculoDeFato.Usa(fato.Binding, VinculoDeFato.AtributoDoCandidato)) is { } atributo
            ? new DomainError(
                GrafoFormularioErrorCodes.CitaAtributoDoCandidato,
                $"O fato '{atributo}' é calculado pelo sistema a partir de atributos do candidato e ainda não pode ser citado em regra do formulário.")
            : null;
    }

    /// <summary>
    /// As restrições contra o catálogo: a condição de cada grupo de opções valida como qualquer
    /// predicado; os valores permitidos são do domínio do próprio fato, conferidos como a condição
    /// <c>FATO EM [valores]</c>; e as respostas que formam as opções vêm de campos categóricos cujas
    /// opções são todas opções do campo, para que toda resposta anterior seja uma opção válida.
    /// A restrição que não cabe no tipo do campo fica para a recusa de coerência do item.
    /// </summary>
    public static IEnumerable<FieldError> SemanticaDasRestricoes(
        FatoDoCatalogo alvo,
        TipoRenderizacao tipoRenderizacao,
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
        return Semantica(alvo, tipoRenderizacao, restricoes, catalogo, vocabulario, dominiosDinamicos);
    }

    /// <summary>
    /// Desativar um fato ou um valor no catálogo recusa só vínculo novo (ADR-0136): a configuração
    /// que já usava o fato, ou já citava o valor, continua com ele. Compara o que se propõe com o
    /// que já está vinculado e recusa, de uma vez, todo vínculo novo a desativado.
    /// </summary>
    public static Result VinculoNovo(IReadOnlyDictionary<string, FatoDoCatalogo> catalogo, VinculosDeFatos existentes, VinculosDeFatos propostos)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(existentes);
        ArgumentNullException.ThrowIfNull(propostos);

        // Todos os vínculos novos a desativado saem juntos (ADR-0125), em ordem estável: fatos, depois valores.
        List<FieldError> erros = [.. propostos.Fatos
            .Where(fato => !existentes.Fatos.Contains(fato) && catalogo.TryGetValue(fato, out FatoDoCatalogo? doCatalogo) && !doCatalogo.Ativo)
            .Order(StringComparer.Ordinal)
            .Select(static fato => new FieldError(string.Empty, new DomainError(
                VinculoCatalogoErrorCodes.FatoDesativado, $"O fato '{fato}' está desativado no catálogo e não aceita vínculo novo.")))];
        erros.AddRange(propostos.Valores
            .Where(valor => !existentes.Valores.Contains(valor)
                && catalogo.TryGetValue(valor.Fato, out FatoDoCatalogo? doCatalogo) && doCatalogo.Valores.Any(v => v.Codigo == valor.Valor && !v.Ativo))
            .OrderBy(static v => v.Fato, StringComparer.Ordinal)
            .ThenBy(static v => v.Valor, StringComparer.Ordinal)
            .Select(static v => new FieldError(string.Empty, new DomainError(
                VinculoCatalogoErrorCodes.ValorDesativado, $"O valor '{v.Valor}' do fato '{v.Fato}' está desativado no catálogo e não aceita vínculo novo."))));
        return erros.Count > 0 ? Result.ValidationFailure(erros) : Result.Success();
    }

    private static IEnumerable<FieldError> Semantica(
        FatoDoCatalogo alvo,
        TipoRenderizacao tipoRenderizacao,
        IReadOnlyList<RestricaoValor> restricoes,
        IReadOnlyDictionary<string, FatoDoCatalogo> catalogo,
        IReadOnlyDictionary<string, DescritorFatoCandidato> vocabulario,
        IReadOnlyDictionary<string, DominioDeValores> dominiosDinamicos)
    {
        for (int indice = 0; indice < restricoes.Count; indice++)
        {
            if (!FormaDoItem.RestricaoCabeNoCampo(restricoes[indice].Tipo, tipoRenderizacao))
            {
                continue;
            }

            string campo = $"restricoes[{indice}]";
            switch (restricoes[indice])
            {
                case OpcoesPermitidas opcoes:
                    for (int entrada = 0; entrada < opcoes.Entradas.Count; entrada++)
                    {
                        OpcoesCondicionadas grupo = opcoes.Entradas[entrada];
                        if (grupo.Quando is { } quando
                            && PredicadoDnfValidador.Validar(quando, vocabulario, null, dominiosDinamicos) is { IsFailure: true } condicao)
                        {
                            yield return new($"{campo}.entradas[{entrada}].quando", condicao.Error!);
                        }

                        PredicadoDnf pertinencia = PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(
                            alvo.Codigo, Operador.Em, JsonSerializer.SerializeToElement(grupo.Valores.Order(StringComparer.Ordinal))).Value!)]).Value!;
                        if (PredicadoDnfValidador.Validar(pertinencia, vocabulario, null, dominiosDinamicos) is { IsFailure: true } valores)
                        {
                            yield return new($"{campo}.entradas[{entrada}].valores", valores.Error!);
                        }
                    }

                    break;
                case OpcoesDasRespostas respostas
                    when respostas.Fatos.Any(f => !catalogo.TryGetValue(f, out FatoDoCatalogo? fonte) || !OpcoesDaFonteCabemNoAlvo(alvo, fonte, dominiosDinamicos)):
                    yield return new($"{campo}.fatos", new DomainError(
                        ItemFormularioErrorCodes.OpcoesDeOutroDominio,
                        "As opções formadas pelas respostas vêm de campos de seleção cujas opções são todas opções do campo."));
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>
    /// Toda resposta possível da fonte é opção do alvo: os valores da fonte cabem nos do alvo; no
    /// domínio por formato, que não enumera valores, a fonte dos valores é a mesma.
    /// </summary>
    private static bool OpcoesDaFonteCabemNoAlvo(
        FatoDoCatalogo alvo, FatoDoCatalogo fonte, IReadOnlyDictionary<string, DominioDeValores> dominiosDinamicos) =>
        string.Equals(fonte.Dominio, alvo.Dominio, StringComparison.Ordinal)
        && (ValoresDoDominio(fonte, dominiosDinamicos), ValoresDoDominio(alvo, dominiosDinamicos)) switch
        {
            (null, null) => string.Equals(fonte.FonteValores, alvo.FonteValores, StringComparison.Ordinal),
            ({ } daFonte, { } doAlvo) => daFonte.IsSubsetOf(doAlvo),
            _ => false,
        };

    /// <summary>Os valores enumerados do domínio do fato — do catálogo ou do processo —; nulo no domínio por formato.</summary>
    private static IReadOnlySet<string>? ValoresDoDominio(FatoDoCatalogo fato, IReadOnlyDictionary<string, DominioDeValores> dominiosDinamicos) =>
        fato.Valores is { Count: > 0 } estaticos
            ? estaticos.Select(static v => v.Codigo).ToHashSet(StringComparer.Ordinal)
            : dominiosDinamicos.TryGetValue(fato.Codigo, out DominioDeValores? dinamico) ? dinamico.Valores : null;
}
