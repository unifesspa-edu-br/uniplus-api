namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>As bancas que a fase requer, como valores: nome, regra de deliberação e se é a banca de análise de recursos, na posição declarada.</summary>
public sealed record BancasDaFase
{
    internal BancasDaFase(IReadOnlyList<BancaRequerida> bancas)
    {
        Declaradas =
        [
            .. bancas.Select(static (banca, posicao) =>
                (posicao, banca.Nome, banca.RegraDeDeliberacao, JulgamentoDasBancas.EhDeRecursos(banca.Codigo))),
        ];
    }

    public bool TemBancaDeRecursos => Declaradas.Any(static banca => banca.DeRecursos);

    public IReadOnlyList<(int Posicao, string Nome, RegraDeDeliberacao? Regra, bool DeRecursos)> Declaradas { get; }
}
