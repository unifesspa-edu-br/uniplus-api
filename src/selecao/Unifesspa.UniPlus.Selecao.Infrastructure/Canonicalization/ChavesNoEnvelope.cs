namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class ChavesNoEnvelope
{
    internal static ChaveLocal Ler(LeitorEnvelope leitor, JsonObject item, string caminho)
    {
        string texto = leitor.TextoNaoVazio(item, "chave", caminho, ChaveLocal.ComprimentoMaximo);
        if (leitor.Falhou)
        {
            return default;
        }

        Result<ChaveLocal> chave = ChaveLocal.Criar(texto);
        return chave.IsSuccess ? chave.Value : leitor.Malformado<ChaveLocal>($"{caminho}.chave", chave.Error!.Message);
    }

    internal static void ExigirUnicas(LeitorEnvelope leitor, IEnumerable<ChaveLocal> chaves, string caminho)
    {
        HashSet<ChaveLocal> encontradas = [];
        foreach (ChaveLocal chave in chaves)
        {
            if (!encontradas.Add(chave))
            {
                leitor.Malformado<object>(caminho, $"a chave '{chave.Valor}' aparece mais de uma vez.");
                return;
            }
        }
    }
}
