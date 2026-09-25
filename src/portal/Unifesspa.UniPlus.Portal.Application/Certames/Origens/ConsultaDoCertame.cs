namespace Unifesspa.UniPlus.Portal.Application.Certames.Origens;

/// <summary>
/// Desfecho da consulta ao detalhe público do certame em Seleção.
/// </summary>
/// <remarks>
/// Os três desfechos têm tratamentos opostos na página, e por isso não se reduzem a "achou ou
/// não achou": "não encontrado" é resposta da origem e se repassa como tal; "indisponível" é a
/// origem sem responder, e a Portal não o encobre com conteúdo vencido.
/// </remarks>
public abstract record ConsultaDoCertame
{
    private ConsultaDoCertame()
    {
    }

    /// <summary>A origem devolveu o certame.</summary>
    public sealed record Encontrado(CertameDaOrigem Certame) : ConsultaDoCertame;

    /// <summary>
    /// A origem respondeu que não há certame divulgado com esse identificador. Inexistente,
    /// rascunho e abertura sem ato registrado chegam aqui com a mesma resposta.
    /// </summary>
    public sealed record NaoEncontrado : ConsultaDoCertame;

    /// <summary>A origem não respondeu de forma utilizável.</summary>
    public sealed record Indisponivel : ConsultaDoCertame;
}
