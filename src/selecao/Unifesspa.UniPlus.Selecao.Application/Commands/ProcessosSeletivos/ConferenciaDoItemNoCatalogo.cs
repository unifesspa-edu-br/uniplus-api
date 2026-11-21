namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A conferência do item do formulário contra o fato do catálogo, uma só para a gravação do item e
/// para a reconferência ao congelar: a coerência do campo e a semântica da pré-condição, do
/// predicado de obrigatoriedade, das restrições e do impedimento contra o vocabulário e os
/// domínios do catálogo. Acumula toda violação independente (ADR-0125).
/// </summary>
internal static class ConferenciaDoItemNoCatalogo
{
    /// <summary>A coerência do tipo de campo com domínio, cardinalidade e fonte de valores do fato.</summary>
    public static DomainError? Coerencia(FatoCandidatoView fato, TipoRenderizacao tipoRenderizacao)
    {
        ArgumentNullException.ThrowIfNull(fato);
        return CoerenciaDoCampo.Validar(fato.Codigo, tipoRenderizacao, fato.Dominio, fato.Cardinalidade, fato.FonteValores);
    }

    /// <summary>
    /// A semântica das regras do item contra o catálogo. <paramref name="fatosDaPrecondicao"/> são os
    /// fatos que a pré-condição cita, inclusive quando a forma dela foi recusada e não há predicado.
    /// </summary>
    public static List<FieldError> Semantica(
        FatoCandidatoView fato, TipoRenderizacao tipoRenderizacao, RegrasDoCampo regras, IEnumerable<string> fatosDaPrecondicao, ContextoDoCatalogo contexto)
    {
        ArgumentNullException.ThrowIfNull(fato);
        ArgumentNullException.ThrowIfNull(regras);
        ArgumentNullException.ThrowIfNull(fatosDaPrecondicao);
        ArgumentNullException.ThrowIfNull(contexto);
        return
        [
            .. SemanticaDoPredicado("precondicao", regras.Exibicao, contexto),
            .. SemanticaDoPredicado("predicadoObrigatoriedade", regras.Obrigatoriedade.Predicado, contexto),
            .. ConferenciaNoCatalogo.SemanticaDasRestricoes(
                contexto.FatosDasRegras[fato.Codigo], tipoRenderizacao, regras.Restricoes, contexto.FatosDasRegras, contexto.Vocabulario, contexto.DominiosDinamicos),
            .. SemanticaDoPredicado("impedimento.quando", regras.Impedimento?.Quando, contexto),
            .. RecusaDeAtributoDoCandidato(fatosDaPrecondicao.Concat(FatosDasRegrasSemExibicao(regras)), contexto),
        ];
    }

    private static IEnumerable<string> FatosDasRegrasSemExibicao(RegrasDoCampo regras) =>
        regras.Obrigatoriedade.FatosCitados
            .Concat(regras.Restricoes.SelectMany(static r => r.FatosCitados))
            .Concat(regras.Impedimento?.FatosCitados ?? []);

    /// <summary>O predicado, quando há, confere com o vocabulário e os domínios do catálogo; a recusa aponta a regra.</summary>
    private static IEnumerable<FieldError> SemanticaDoPredicado(string regra, PredicadoDnf? predicado, ContextoDoCatalogo contexto) =>
        predicado is not null
        && PredicadoDnfValidador.Validar(predicado, contexto.Vocabulario, null, contexto.DominiosDinamicos) is { IsFailure: true } semantica
            ? [new(regra, semantica.Error!)]
            : [];

    /// <summary>As regras não citam atributo do candidato, que não se responde; a recusa é do campo inteiro.</summary>
    private static IEnumerable<FieldError> RecusaDeAtributoDoCandidato(IEnumerable<string> citados, ContextoDoCatalogo contexto) =>
        VocabularioDeFatos.CitacaoDeAtributoDoCandidato(citados, contexto.Fatos) is { } atributo ? [new(string.Empty, atributo)] : [];

    /// <summary>
    /// A semântica da exibição e da obrigatoriedade do grupo contra o catálogo e a recusa de citar
    /// atributo do candidato, como na gravação do grupo. O campo vazio é o grupo inteiro.
    /// </summary>
    public static List<FieldError> DoGrupo(PredicadoDnf? exibicao, Obrigatoriedade obrigatoriedade, ContextoDoCatalogo contexto)
    {
        ArgumentNullException.ThrowIfNull(obrigatoriedade);
        ArgumentNullException.ThrowIfNull(contexto);
        return
        [
            .. SemanticaDoPredicado("exibicao", exibicao, contexto),
            .. SemanticaDoPredicado("predicadoObrigatoriedade", obrigatoriedade.Predicado, contexto),
            .. RecusaDeAtributoDoCandidato((exibicao?.FatosCitados ?? []).Concat(obrigatoriedade.FatosCitados), contexto),
        ];
    }

    /// <summary>
    /// Confere o item já gravado contra o catálogo de hoje. O fato que alguma versão publicada já
    /// coletou só tem o campo conferido: os valores de domínio nunca saem do catálogo, então as
    /// regras dele não são reconferidas.
    /// </summary>
    public static List<FieldError> DoItemGravado(FatoColetado item, FatoCandidatoView fato, bool jaColetado, ContextoDoCatalogo contexto)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (Coerencia(fato, item.TipoRenderizacao) is { } incoerencia)
        {
            return [new("tipoRenderizacao", incoerencia)];
        }

        // O fato que a regra cita e o catálogo não tem é recusado adiante pelo gate dos pressupostos,
        // com a causa certa; a semântica do predicado só trocaria a causa.
        if (jaColetado || item.FatosCitados.Any(f => !contexto.Fatos.ContainsKey(f)))
        {
            return [];
        }

        return Semantica(fato, item.TipoRenderizacao, item.Regras, item.Precondicoes.Select(static c => c.Fato), contexto);
    }
}
