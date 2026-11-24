namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using System.Globalization;
using System.Text;
using System.Text.Json;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Cryptography;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

/// <summary>
/// O valor de uma resposta como a linha o guarda: na coluna do tipo do fato ou, quando a definição manda cifrar, no JSON cifrado
/// com a chave da categoria do fato. A cifra é assíncrona e acontece na fronteira do repositório (ADR-0121).
/// </summary>
internal sealed class ValorGravadoDaResposta
{
    private static readonly Dictionary<TipoDoValorDeResposta, Action<RespostaDaInscricaoRecord, JsonElement>> GravadoresPorTipo = new()
    {
        [TipoDoValorDeResposta.SimOuNao] = static (linha, valor) => linha.ValorBooleano = valor.GetBoolean(),
        [TipoDoValorDeResposta.Numero] = static (linha, valor) => linha.ValorNumero = valor.GetDecimal(),
        [TipoDoValorDeResposta.Opcao] = static (linha, valor) => linha.ValorCodigo = valor.GetString(),
        [TipoDoValorDeResposta.Texto] = static (linha, valor) => linha.ValorTexto = valor.GetString(),
        [TipoDoValorDeResposta.Data] = static (linha, valor) =>
            linha.ValorData = DateOnly.ParseExact(valor.GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
    };

    private readonly IUniPlusEncryptionService _encryption;

    public ValorGravadoDaResposta(IUniPlusEncryptionService encryption)
    {
        ArgumentNullException.ThrowIfNull(encryption);

        _encryption = encryption;
    }

    public async Task<JsonElement> LerAsync(RespostaDaInscricaoRecord linha, CancellationToken cancellationToken)
    {
        if (linha.ValorCifrado is { } cifrado)
        {
            return await DecifrarAsync(ChaveDeCifraDoFato(linha.FatoCodigo), cifrado, cancellationToken).ConfigureAwait(false);
        }

        return LerDaColuna(linha);
    }

    public async Task GravarAsync(
        RespostaDaInscricaoRecord linha,
        JsonElement valor,
        DefinicaoDaResposta definicao,
        (string Codigo, string? Rotulo)? municipio,
        CancellationToken cancellationToken)
    {
        if (definicao.Cifrada)
        {
            linha.ValorCifrado = await _encryption
                .EncryptAsync(ChaveDeCifraDoFato(linha.FatoCodigo), Encoding.UTF8.GetBytes(valor.GetRawText()), cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        GravarNaColuna(linha, valor, definicao.Tipo, municipio);
    }

    /// <summary>O valor decifrado com a chave da categoria informada; a autorização de quem pode ler é de quem chama.</summary>
    public async Task<JsonElement> DecifrarAsync(string chave, byte[] cifrado, CancellationToken cancellationToken)
    {
        byte[] plano = await _encryption.DecryptAsync(chave, cifrado, cancellationToken).ConfigureAwait(false);

        return JsonSerializer.Deserialize<JsonElement>(plano);
    }

    /// <summary>Leva o valor que a linha guarda — coluna e cifra — para a linha que a substitui na mesma chave natural.</summary>
    public static void CopiarConteudo(RespostaDaInscricaoRecord de, RespostaDaInscricaoRecord para)
    {
        para.Origem = de.Origem;
        para.Tipo = de.Tipo;
        para.ValorBooleano = de.ValorBooleano;
        para.ValorNumero = de.ValorNumero;
        para.ValorCodigo = de.ValorCodigo;
        para.ValorRotulo = de.ValorRotulo;
        para.ValorTexto = de.ValorTexto;
        para.ValorData = de.ValorData;
        para.ValorCifrado = de.ValorCifrado;
    }

    /// <summary>A chave da categoria da resposta cifrada: documento ou dado sensível de cifra individual.</summary>
    private static string ChaveDeCifraDoFato(string fatoCodigo) =>
        DefinicaoDaResposta.EhFatoComCifraIndividual(fatoCodigo) ? SelecaoEncryptionKeys.SensiveisAesGcm : SelecaoEncryptionKeys.IdentificadoresAesGcm;

    private static JsonElement LerDaColuna(RespostaDaInscricaoRecord linha) =>
        linha.Tipo switch
        {
            TipoDoValorDeResposta.SimOuNao => JsonSerializer.SerializeToElement(linha.ValorBooleano!.Value),
            TipoDoValorDeResposta.Numero => JsonSerializer.SerializeToElement(linha.ValorNumero!.Value),
            TipoDoValorDeResposta.Opcao => JsonSerializer.SerializeToElement(linha.ValorCodigo!),
            TipoDoValorDeResposta.Municipio => linha.ValorRotulo is { } rotulo
                ? ValorDeMunicipio.Gravado(linha.ValorCodigo!, rotulo)
                : JsonSerializer.SerializeToElement(linha.ValorCodigo!),
            TipoDoValorDeResposta.Texto => JsonSerializer.SerializeToElement(linha.ValorTexto!),
            TipoDoValorDeResposta.Data => JsonSerializer.SerializeToElement(linha.ValorData!.Value),
            _ => throw new InvalidOperationException($"A resposta {linha.Id} tem um tipo sem coluna de valor ({linha.Tipo})."),
        };

    private static void GravarNaColuna(
        RespostaDaInscricaoRecord linha, JsonElement valor, TipoDoValorDeResposta tipo, (string Codigo, string? Rotulo)? municipio)
    {
        if (tipo == TipoDoValorDeResposta.Municipio)
        {
            (linha.ValorCodigo, linha.ValorRotulo) = municipio!.Value;
            return;
        }

        if (!GravadoresPorTipo.TryGetValue(tipo, out Action<RespostaDaInscricaoRecord, JsonElement>? gravar))
        {
            throw new InvalidOperationException($"O tipo {tipo} não tem coluna de valor.");
        }

        gravar(linha, valor);
    }
}
