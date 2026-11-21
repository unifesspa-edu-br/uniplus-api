namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A banca que julga recurso atende todos os candidatos e não define candidatos atendidos. É banca que julga recurso a de
/// papel em <see cref="PapeisQueJulgamRecurso"/>, e nunca a de um código de tipo.
/// </summary>
public static class ExigenciaDeBancaDeRecurso
{
    /// <summary>O código da recusa dos candidatos atendidos na banca que julga recurso, para quem a projeta num item só.</summary>
    internal static readonly IReadOnlySet<string> CodigosDosCandidatosAtendidos = new HashSet<string>([BancaErrorCodes.CandidatosAtendidosEmBancaDeRecursos], StringComparer.Ordinal);

    /// <summary>A recusa dos candidatos atendidos na banca que julga recurso, declarada pela fase.</summary>
    public static DomainError? CandidatosAtendidosNaDeclaracao(
        string codigoDaFase, TipoDeBancaCopiado tipo, int posicaoNoTipo, int categoriasNosCandidatosAtendidos)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        return tipo.JulgaRecurso && categoriasNosCandidatosAtendidos > 0
            ? CandidatosAtendidosEmBancaDeRecursos(codigoDaFase, tipo, posicaoNoTipo)
            : null;
    }

    private static DomainError CandidatosAtendidosEmBancaDeRecursos(string codigoDaFase, TipoDeBancaCopiado tipo, int posicaoNoTipo) =>
        new(
            BancaErrorCodes.CandidatosAtendidosEmBancaDeRecursos,
            $"Na fase '{codigoDaFase}', a banca '{tipo.Codigo.Rotulo()} {posicaoNoTipo}' tem o papel '{tipo.Papel.Rotulo()}' e define candidatos atendidos, mas a banca que julga recurso atende todos os candidatos. Retire as categorias de documento dela.");
}
