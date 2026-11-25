namespace Unifesspa.UniPlus.Configuracao.Application.Commands.OfertasCurso;

using Unifesspa.UniPlus.Application.Abstractions.Persistence;
using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A recusa de um código de oferta que outra oferta viva já tem, e o reconhecimento da violação do índice único que a
/// corrida entre dois cadastros produz: o handler confere antes e o índice decide quando as duas conferências passam juntas.
/// </summary>
internal static class CodigoDaOfertaEmUso
{
    public static DomainError Recusa() => new(
        OfertaCursoErrorCodes.CodigoJaEmUso,
        "Já existe uma oferta de curso viva com este código.");

    public static bool ViolaOIndice(Exception exception) =>
        ConstraintVioladaNaGravacao.NomeDaConstraintViolada(exception) == RestricoesDoBanco.OfertaDeCursoCodigoAtivos;
}
