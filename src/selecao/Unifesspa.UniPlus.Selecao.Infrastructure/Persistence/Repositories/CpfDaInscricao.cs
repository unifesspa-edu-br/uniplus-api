namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Cryptography;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

/// <summary>
/// A leitura única do CPF de uma inscrição: ele só existe como resposta do fato <c>CPF</c>, na ocorrência raiz e
/// informada pelo candidato — nenhuma coluna da inscrição o repete. Quem precisa do CPF lê por aqui, em lote, sem
/// hidratar a inscrição.
/// </summary>
/// <remarks>
/// A resposta do CPF é sempre cifrada, e o valor decifrado é o JSON do texto, como o de toda resposta cifrada. Uma resposta
/// sem o valor cifrado é dado fora do desenho e falha alto, em vez de devolver o CPF em claro.
/// </remarks>
internal static class CpfDaInscricao
{
    /// <summary>As respostas do CPF entre as de <paramref name="respostas"/>, que o chamador já restringiu às inscrições que lhe interessam.</summary>
    public static async Task<Dictionary<Guid, string>> LerAsync(
        IQueryable<RespostaDaInscricaoRecord> respostas, IUniPlusEncryptionService encryption, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(respostas);
        ArgumentNullException.ThrowIfNull(encryption);

        List<RespostaDoCpf> linhas = await ConsultarRespostasDoCpfAsync(respostas, cancellationToken).ConfigureAwait(false);
        ExigirRespostasCifradas(linhas);
        return await DecifrarRespostasDoCpfAsync(linhas, encryption, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<List<RespostaDoCpf>> ConsultarRespostasDoCpfAsync(
        IQueryable<RespostaDaInscricaoRecord> respostas, CancellationToken cancellationToken) =>
        await respostas
            .AsNoTracking()
            .Where(r => r.FatoCodigo == ConjuntoBasicoDaInscricao.Cpf
                && r.OcorrenciaId == null
                && r.Origem == OrigemDaResposta.Informada)
            .Select(static r => new RespostaDoCpf(r.InscricaoId, r.ValorCifrado))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private static void ExigirRespostasCifradas(List<RespostaDoCpf> linhas)
    {
        RespostaDoCpf? semCifra = linhas.FirstOrDefault(static linha => linha.ValorCifrado is null);
        if (semCifra is not null)
        {
            throw new InvalidOperationException($"A resposta do CPF da inscrição {semCifra.InscricaoId} não está cifrada.");
        }
    }

    private static async Task<Dictionary<Guid, string>> DecifrarRespostasDoCpfAsync(
        List<RespostaDoCpf> linhas, IUniPlusEncryptionService encryption, CancellationToken cancellationToken)
    {
        string[] decifradas = await DecifradorDeCpfsEmParalelo
            .DecifrarAsync(encryption, [.. linhas.Select(static linha => linha.ValorCifrado!)], cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, string> cpfs = [];
        for (int i = 0; i < linhas.Count; i++)
        {
            cpfs[linhas[i].InscricaoId] = JsonSerializer.Deserialize<string>(decifradas[i])
                ?? throw new InvalidOperationException($"A resposta do CPF da inscrição {linhas[i].InscricaoId} decifrada não é um texto.");
        }

        return cpfs;
    }

    /// <summary>O CPF da inscrição; falha alto se a resposta não existe, porque toda inscrição nasce com ela.</summary>
    public static string Exigir(IReadOnlyDictionary<Guid, string> cpfs, Guid inscricaoId) =>
        cpfs.TryGetValue(inscricaoId, out string? cpf)
            ? cpf
            : throw new InvalidOperationException($"A inscrição {inscricaoId} não tem a resposta do CPF.");

    /// <summary>O CPF entre as respostas informadas já lidas da inscrição; falha alto se a resposta não existe ou não é texto.</summary>
    public static Cpf Lido(Guid inscricaoId, IReadOnlyDictionary<string, JsonElement> informadas)
    {
        ArgumentNullException.ThrowIfNull(informadas);

        return informadas.TryGetValue(ConjuntoBasicoDaInscricao.Cpf, out JsonElement resposta) && resposta.ValueKind == JsonValueKind.String
            ? Revalidar(resposta.GetString()!)
            : throw new InvalidOperationException($"A inscrição {inscricaoId} não tem a resposta do CPF.");
    }

    /// <summary>
    /// Revalida o CPF decifrado contra a regra de domínio — dado corrompido no banco ou chave de
    /// cifragem incorreta falha alto, em vez de produzir um <see cref="Cpf"/> inválido em silêncio
    /// (mesmo padrão de <c>VinculoDiscenteRepository</c>).
    /// </summary>
    public static Cpf Revalidar(string valor) =>
        Cpf.Criar(valor).Match(
            cpf => cpf,
            erro => throw new InvalidOperationException($"CPF da resposta da inscrição falhou a revalidação de domínio ({erro.Code})."));

    private sealed record RespostaDoCpf(Guid InscricaoId, byte[]? ValorCifrado);
}
