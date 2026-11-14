namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;

using Unifesspa.UniPlus.Application.Abstractions.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;

/// <summary>
/// Traduz a violação de unicidade (<c>23505</c>) que a gravação do ato levanta em recusa nomeada.
/// É a garantia dura contra a corrida check-then-act das consultas prévias: entre a consulta e o
/// <c>SaveChanges</c> cabe outra transação, e só o banco a vê.
/// </summary>
/// <remarks>
/// Este tipo é o dono dos nomes das constraints de Publicações: a linearidade da cadeia de
/// retificação (ADR-0103) e a vaga do objeto (ADR-0107). O nome da constraint violada vem de
/// <see cref="ConstraintVioladaNaGravacao"/>.
/// </remarks>
internal static class ConflitoNaGravacaoDoAto
{
    private const string IndiceDaRetificacaoUnica = "ux_ato_normativo_retificado";

    private const string IndiceDaVagaDeLinhagem = "ux_vagas_de_linhagem_objeto_tipo";

    private const string AtoJaRetificadoConcorrenteMensagem =
        "O ato retificado já foi retificado por outro ato registrado concorrentemente. "
        + "A cadeia é linear: retifique a cabeça da cadeia.";

    private const string VagaOcupadaConcorrenteMensagem =
        "A vaga do objeto vinculado foi ocupada por um ato registrado concorrentemente. "
        + "O tipo admite um único ato vivo por objeto — consulte os atos da entidade "
        + "antes de reenviar.";

    /// <summary>
    /// A recusa que corresponde à violação, ou <see langword="null"/> quando a exceção não é uma
    /// violação das constraints do ato: quem chama deve propagá-la.
    /// </summary>
    public static DomainError? Traduzir(Exception excecao)
    {
        ArgumentNullException.ThrowIfNull(excecao);

        return ConstraintVioladaNaGravacao.NomeDaConstraintViolada(excecao) switch
        {
            IndiceDaRetificacaoUnica => new DomainError(
                AtoNormativoErrorCodes.AtoJaRetificado, AtoJaRetificadoConcorrenteMensagem),
            IndiceDaVagaDeLinhagem => new DomainError(
                AtoNormativoErrorCodes.ObjetoJaTemAtoVivoDoTipo, VagaOcupadaConcorrenteMensagem),
            _ => null,
        };
    }
}
