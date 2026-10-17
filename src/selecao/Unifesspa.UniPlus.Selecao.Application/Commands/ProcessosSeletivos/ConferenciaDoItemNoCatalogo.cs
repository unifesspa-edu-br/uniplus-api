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
        FatoCandidatoView fato,
        TipoRenderizacao tipoRenderizacao,
        PredicadoDnf? precondicao,
        IEnumerable<string> fatosDaPrecondicao,
        Obrigatoriedade obrigatoriedade,
        IReadOnlyList<RestricaoValor> restricoes,
        Impedimento? impedimento,
        ContextoDoCatalogo contexto)
    {
        ArgumentNullException.ThrowIfNull(fato);
        ArgumentNullException.ThrowIfNull(fatosDaPrecondicao);
        ArgumentNullException.ThrowIfNull(obrigatoriedade);
        ArgumentNullException.ThrowIfNull(restricoes);
        ArgumentNullException.ThrowIfNull(contexto);
        List<FieldError> erros = [];
        if (precondicao is not null
            && PredicadoDnfValidador.Validar(precondicao, contexto.Vocabulario, null, contexto.DominiosDinamicos) is { IsFailure: true } semanticaDaPrecondicao)
        {
            erros.Add(new("precondicao", semanticaDaPrecondicao.Error!));
        }

        if (obrigatoriedade.Predicado is { } predicado
            && PredicadoDnfValidador.Validar(predicado, contexto.Vocabulario, null, contexto.DominiosDinamicos) is { IsFailure: true } semantica)
        {
            erros.Add(new("predicadoObrigatoriedade", semantica.Error!));
        }

        erros.AddRange(ConferenciaNoCatalogo.SemanticaDasRestricoes(
            contexto.FatosDasRegras[fato.Codigo], tipoRenderizacao, restricoes, contexto.FatosDasRegras, contexto.Vocabulario, contexto.DominiosDinamicos));

        if (impedimento is not null
            && PredicadoDnfValidador.Validar(impedimento.Quando, contexto.Vocabulario, null, contexto.DominiosDinamicos) is { IsFailure: true } semanticaDoImpedimento)
        {
            erros.Add(new("impedimento.quando", semanticaDoImpedimento.Error!));
        }

        IEnumerable<string> citados = fatosDaPrecondicao
            .Concat(obrigatoriedade.FatosCitados)
            .Concat(restricoes.SelectMany(static r => r.FatosCitados))
            .Concat(impedimento?.FatosCitados ?? []);
        if (VocabularioDeFatos.CitacaoDeAtributoDoCandidato(citados, contexto.Fatos) is { } atributo)
        {
            erros.Add(new(string.Empty, atributo));
        }

        return erros;
    }

    /// <summary>
    /// A semântica da exibição e da obrigatoriedade do grupo contra o catálogo e a recusa de citar
    /// atributo do candidato, como na gravação do grupo. O campo vazio é o grupo inteiro.
    /// </summary>
    public static List<FieldError> DoGrupo(PredicadoDnf? exibicao, Obrigatoriedade obrigatoriedade, ContextoDoCatalogo contexto)
    {
        ArgumentNullException.ThrowIfNull(obrigatoriedade);
        ArgumentNullException.ThrowIfNull(contexto);
        List<FieldError> erros = [];
        foreach ((string regra, PredicadoDnf? predicado) in new[] { ("exibicao", exibicao), ("predicadoObrigatoriedade", obrigatoriedade.Predicado) })
        {
            if (predicado is not null
                && PredicadoDnfValidador.Validar(predicado, contexto.Vocabulario, null, contexto.DominiosDinamicos) is { IsFailure: true } semantica)
            {
                erros.Add(new(regra, semantica.Error!));
            }
        }

        if (VocabularioDeFatos.CitacaoDeAtributoDoCandidato(
                (exibicao?.FatosCitados ?? []).Concat(obrigatoriedade.FatosCitados), contexto.Fatos) is { } atributo)
        {
            erros.Add(new(string.Empty, atributo));
        }

        return erros;
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

        List<FieldError> erros = [];
        erros.AddRange(Semantica(
            fato, item.TipoRenderizacao, item.Exibicao, item.Precondicoes.Select(static c => c.Fato), item.Obrigatoriedade, item.Restricoes, item.Impedimento, contexto));
        return erros;
    }
}
