namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class ConferenciaDosItensDeAvaliacao
{
    internal static List<FieldError> Recusas(IReadOnlyList<ItemDeAvaliacaoDeclarado?>? itens, decimal? passoDoLancamento)
    {
        List<FieldError> recusas = [];
        HashSet<string> rotulos = new(StringComparer.Ordinal);
        HashSet<Guid> identificadores = [];
        for (int posicao = 0; posicao < (itens?.Count ?? 0); posicao++)
        {
            if (itens![posicao] is { } item)
            {
                RecusasDoItem(item, posicao, passoDoLancamento, rotulos, identificadores, recusas);
            }
        }

        return recusas;
    }

    private static void RecusasDoItem(ItemDeAvaliacaoDeclarado item, int posicao, decimal? passo,
        HashSet<string> rotulos, HashSet<Guid> identificadores, List<FieldError> recusas)
    {
        string campo = $"itensDeAvaliacao[{posicao}]";
        if (item.RecusaDoRotulo($"{campo}.rotulo") is { } recusa)
        {
            recusas.Add(recusa);
        }
        else if (!rotulos.Add(item.RotuloNormalizado))
        {
            recusas.Add(new($"{campo}.rotulo", new DomainError(ItemDeAvaliacao.RotuloDuplicado,
                $"O item '{item.Rotulo.Trim()}' aparece mais de uma vez na etapa: o rótulo identifica o critério para a banca.")));
        }

        RecusarIdentificadorRepetido(item.Id, campo, identificadores, recusas);
        recusas.AddRange(item.RecusasDaPontuacao($"{campo}.pontuacaoMaxima", passo));
    }

    private static void RecusarIdentificadorRepetido(Guid? id, string campo, HashSet<Guid> identificadores, List<FieldError> recusas)
    {
        if (id is { } identificador && !identificadores.Add(identificador))
        {
            recusas.Add(new($"{campo}.id", new DomainError(ItemDeAvaliacao.IdDuplicado,
                "O mesmo id de item de avaliação não pode ser informado mais de uma vez na etapa.")));
        }
    }
}
