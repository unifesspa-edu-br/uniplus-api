namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os itens do checklist de avaliação de um conjunto de etapas, localizados pelo código.</summary>
internal static class ItensDeEtapaDeTeste
{
    internal static ItemConformidade Item(string codigo, CitacoesDeNota? citacoes, params EtapaDeclarada[] etapas)
    {
        EtapasDoProcesso conjunto = new(etapas);
        CitacoesDeNota citadas = citacoes ?? CitacoesDeNota.Nenhuma;

        return conjunto.ItensDaEscala(citadas).Concat(conjunto.ItensDaAvaliacao(citadas)).Single(item => item.Codigo == codigo);
    }

    internal static ItemConformidade Item(string codigo, params EtapaDeclarada[] etapas) => Item(codigo, null, etapas);

    internal static string[] Vermelhos(CitacoesDeNota? citacoes, params EtapaDeclarada[] etapas)
    {
        EtapasDoProcesso conjunto = new(etapas);
        CitacoesDeNota citadas = citacoes ?? CitacoesDeNota.Nenhuma;

        return [.. conjunto.ItensDaEscala(citadas).Concat(conjunto.ItensDaAvaliacao(citadas)).Where(static item => !item.Ok).Select(static item => item.Codigo)];
    }
}
