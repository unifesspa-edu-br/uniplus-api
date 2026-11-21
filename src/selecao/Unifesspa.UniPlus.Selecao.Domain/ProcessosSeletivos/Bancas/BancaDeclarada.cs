namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Uma banca da fase ou da etapa como foi declarada: a posição dela na lista, a posição entre as do mesmo tipo, a cópia do tipo
/// e a regra de deliberação. As regras decidem pela cópia, e as mensagens citam a banca pelo rótulo do tipo e pela posição.
/// </summary>
/// <param name="Posicao">A posição da banca na lista da fase ou da etapa, a que o campo da recusa aponta.</param>
/// <param name="PosicaoNoTipo">A posição da banca entre as do mesmo tipo, a partir de 1.</param>
/// <param name="Tipo">A cópia do tipo de banca que a banca guarda.</param>
/// <param name="Regra">Nula enquanto o rascunho não a declara.</param>
public sealed record BancaDeclarada(int Posicao, int PosicaoNoTipo, TipoDeBancaCopiado Tipo, RegraDeDeliberacao? Regra)
{
    /// <summary>Como as mensagens citam a banca: o rótulo fixo do tipo e a posição entre as do mesmo tipo.</summary>
    public string Citacao => $"{Tipo.Codigo.Rotulo()} {PosicaoNoTipo}";

    /// <summary>Se a banca decide por ato individual e julga recurso sem declarar a votação dele.</summary>
    public bool JulgaRecursoSemVotacao => Tipo.JulgaRecurso && Regra is { AtoIndividualSemVotacao: true };

    internal static IReadOnlyList<BancaDeclarada> De(IReadOnlyList<(TipoDeBancaCopiado Tipo, RegraDeDeliberacao? Regra)> bancas)
    {
        IReadOnlyList<int> posicoes = PosicaoDasBancasNoTipo.Calcular(bancas.Select(static banca => banca.Tipo.Codigo));

        return [.. bancas.Select((banca, posicao) => new BancaDeclarada(posicao, posicoes[posicao], banca.Tipo, banca.Regra))];
    }
}
