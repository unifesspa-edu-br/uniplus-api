namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A alteração da definição do fato: a do administrador propõe os eixos (<see cref="ConferenciaDaRedefinicaoDoFato"/>),
/// aplica e desfaz quando há recusa; a do fato de sistema só troca a proteção de dados. O fato só muda
/// por <see cref="FatoCandidato.AplicarEixos"/> e <see cref="FatoCandidato.AplicarProtecao"/>.
/// </summary>
internal static class RedefinicaoDoFato
{
    internal static Result DoAdministrador(FatoCandidato fato, DefinicaoDoFato definicao, CatalogoDeFatos catalogo)
    {
        ProtecaoDeDados protecaoAnterior = fato.Protecao;
        EixosDaDefinicao anteriores = fato.Eixos;
        List<FieldError> erros = ErrosDaProposta(fato, new ConferenciaDaRedefinicaoDoFato(definicao, fato).Propor(), catalogo);
        if (erros.Count > 0)
        {
            fato.AplicarEixos(anteriores);
            return Result.ValidationFailure(erros);
        }

        fato.Aprovacao.AoEditarItem(fato.Protecao != protecaoAnterior);
        return Result.Success();
    }

    internal static Result DoFatoDeSistema(FatoCandidato fato, DefinicaoDoFato definicao, CatalogoDeFatos catalogo)
    {
        if (RedefinicaoDoFatoDeSistema.Recusa(definicao, fato.Eixos) is { } recusa)
        {
            return recusa;
        }

        ProtecaoDeDados anterior = fato.Protecao;
        Result troca = TrocaDaProtecaoDoFato.Trocar(fato, definicao.ClassificacaoProtecao, definicao.FinalidadeTratamento, definicao.HipoteseLegal, catalogo);
        if (troca.IsFailure)
        {
            return troca;
        }

        fato.Aprovacao.AoEditarItem(fato.Protecao != anterior);
        return Result.Success();
    }

    private static List<FieldError> ErrosDaProposta(FatoCandidato fato, PropostaDaRedefinicao proposta, CatalogoDeFatos catalogo) =>
    [
        .. proposta.Erros,
        .. ConferenciaDasDependenciasDoFato.NovasRecusas(
            fato, catalogo, () => fato.AplicarEixos(proposta.Propostos), proposta.PontoValido, proposta.ProtecaoValida),
    ];
}
