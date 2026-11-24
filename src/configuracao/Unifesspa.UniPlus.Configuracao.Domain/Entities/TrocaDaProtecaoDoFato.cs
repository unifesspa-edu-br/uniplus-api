namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

using static Unifesspa.UniPlus.Configuracao.Domain.Entities.ConferenciaDaDefinicaoDoFato;

/// <summary>
/// A troca da proteção de dados do fato e o que a decisão da proteção de dados faz com ela: a
/// recomendação de ajuste, que respeita o mínimo do domínio, e o aceite da recomendação. O fato só
/// muda por <see cref="FatoCandidato.AplicarProtecao"/>, e volta ao que era quando há recusa.
/// </summary>
internal static class TrocaDaProtecaoDoFato
{
    /// <summary>
    /// Troca a proteção do fato conferindo o que ele próprio exige — o mínimo do domínio e a coerência da
    /// hipótese com a classificação — e o que depende dele: só a recusa que a troca causa conta (ADR-0125).
    /// O fato não muda quando há recusa. Quem chama decide o que a troca faz com a aprovação.
    /// </summary>
    internal static Result Trocar(
        FatoCandidato fato, ClassificacaoDeProtecao? classificacao, string finalidade, HipoteseLegalTratamento? hipotese, CatalogoDeFatos catalogo)
    {
        Result<ProtecaoDeDados> protecao = ValidarProtecao(fato.Dominio, classificacao, finalidade, hipotese);
        if (protecao.IsFailure)
        {
            return Result.ValidationFailure(protecao.Errors);
        }

        ProtecaoDeDados anterior = fato.Protecao;
        List<FieldError> novas = ConferenciaDasDependenciasDoFato.NovasRecusas(fato, catalogo, () => fato.AplicarProtecao(protecao.Value));
        fato.AplicarProtecao(novas.Count == 0 ? protecao.Value : anterior);
        return novas.Count == 0 ? Result.Success() : Result.ValidationFailure(novas);
    }

    internal static Result Recomendar(
        FatoCandidato fato, ClassificacaoDeProtecao? recomendada, string? justificativa, string ator, DateTimeOffset em)
    {
        Result recusa = AprovacaoDaProtecao.ConferirRecomendacao(recomendada, fato.ClassificacaoProtecao, fato.HipoteseLegal);
        if (recusa.IsFailure)
        {
            return recusa;
        }

        Result<ProtecaoDeDados> noMinimoDoDominio = ValidarProtecao(
            fato.Dominio, recomendada, fato.FinalidadeTratamento, fato.HipoteseLegal);
        if (noMinimoDoDominio.Errors.FirstOrDefault(static e => e.Field == "classificacaoProtecao") is { } abaixo)
        {
            return Result.ValidationFailure([abaixo with { Field = "classificacaoRecomendada" }]);
        }

        return fato.Aprovacao.RecomendarAjuste(recomendada, justificativa, ator, em);
    }

    internal static Result Aceitar(FatoCandidato fato, string ator, DateTimeOffset em, CatalogoDeFatos catalogo)
    {
        Result<ClassificacaoDeProtecao> recomendada = fato.Aprovacao.RecomendacaoAAceitar();
        if (recomendada.IsFailure)
        {
            return Result.Failure(recomendada.Error!);
        }

        Result troca = Trocar(fato, recomendada.Value, fato.FinalidadeTratamento, fato.HipoteseLegal, catalogo);
        if (troca.IsFailure)
        {
            return troca;
        }

        return fato.Aprovacao.AceitarRecomendacao(recomendada.Value, ator, em);
    }
}
