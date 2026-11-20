namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json.Nodes;

using DTOs;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que o certame oferta: as ofertas de curso, quantas opções de curso o candidato escolhe, as
/// modalidades ofertadas e o quadro de vagas, como o envelope congelado os guarda.
/// </summary>
internal sealed record OfertaDoCertame(
    List<OfertaDeCursoCertameDto> Ofertas,
    OpcoesDeCursoCertameDto OpcoesDeCurso,
    List<string> Modalidades,
    List<QuadroDeVagasCertameDto> Vagas)
{
    /// <summary>Lê a oferta do envelope, parando na primeira parte que não se deixa ler.</summary>
    internal static Result<OfertaDoCertame> Ler(JsonObject envelope) =>
        LerOfertas(envelope).Encadear(ofertas =>
            LerOpcoesDeCurso(envelope).Encadear(opcoesDeCurso =>
            LerModalidades(envelope).Encadear(modalidades =>
            LerVagas(envelope).Compor(vagas => new OfertaDoCertame(ofertas, opcoesDeCurso, modalidades, vagas)))));

    private static Result<List<OfertaDeCursoCertameDto>> LerOfertas(JsonObject envelope) =>
        LeituraDoCertameVagas.TentarOfertas(envelope, out List<OfertaDeCursoCertameDto>? ofertas)
            ? Result<List<OfertaDeCursoCertameDto>>.Success(ofertas)
            : ProjecaoDoCertamePublicado.Recusar<List<OfertaDeCursoCertameDto>>("ofertas");

    private static Result<OpcoesDeCursoCertameDto> LerOpcoesDeCurso(JsonObject envelope) =>
        LeituraDoCertameVagas.TentarOpcoesDeCurso(envelope, out OpcoesDeCursoCertameDto? opcoesDeCurso)
            ? Result<OpcoesDeCursoCertameDto>.Success(opcoesDeCurso)
            : ProjecaoDoCertamePublicado.Recusar<OpcoesDeCursoCertameDto>("opções de curso");

    private static Result<List<string>> LerModalidades(JsonObject envelope) =>
        PrimitivasDaProjecao.TentarTextos(envelope, ProjecaoDoCertamePublicado.BlocoPublico("modalidadesOfertadas"), out List<string>? modalidades)
            ? Result<List<string>>.Success(modalidades)
            : ProjecaoDoCertamePublicado.Recusar<List<string>>("modalidades ofertadas");

    private static Result<List<QuadroDeVagasCertameDto>> LerVagas(JsonObject envelope) =>
        LeituraDoCertameVagas.TentarVagas(envelope, out List<QuadroDeVagasCertameDto>? vagas)
            ? Result<List<QuadroDeVagasCertameDto>>.Success(vagas)
            : ProjecaoDoCertamePublicado.Recusar<List<QuadroDeVagasCertameDto>>("quadro de vagas");
}
