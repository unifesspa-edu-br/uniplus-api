namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Uma versão promovida e imutável de um <see cref="TermoConsentimento"/>
/// (UNI-REQ-0086/RN-COL-05). Corrigir o texto exige um novo rascunho revisado
/// e uma nova promoção — nunca editar esta linha.
/// </summary>
/// <remarks>
/// Implementa <see cref="IForensicEntity"/> per ADR-0063: forensic append-only,
/// deliberadamente NÃO herda <see cref="Kernel.Domain.Entities.EntityBase"/> e NÃO
/// carrega soft-delete — qualquer <c>UPDATE</c>/<c>DELETE</c> em produção é
/// incidente operacional, não fluxo normal.
/// </remarks>
public sealed class TermoConsentimentoVersao : IForensicEntity
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid TermoConsentimentoId { get; private init; }

    public string Texto { get; private init; } = null!;

    public string BaseLegal { get; private init; } = null!;

    public FormaAceite FormaAceite { get; private init; }

    public string Hash { get; private init; } = null!;

    public DateTimeOffset PromovidaEm { get; private init; }

    public string PromovidaPor { get; private init; } = null!;

    // Construtor de materialização do EF Core.
    private TermoConsentimentoVersao()
    {
    }

    /// <summary>
    /// Promove um rascunho revisado a versão imutável. Chamado pelo agregado
    /// <see cref="TermoConsentimento"/> — pública por convenção de factory forense
    /// (ADR-0063, <c>ForensicEntityConventionsTests</c> exige factory estática
    /// pública em vez de construtor público), não porque seja uma API de uso geral.
    /// </summary>
    /// <remarks>
    /// O hash é calculado AQUI DENTRO, a partir do próprio conteúdo recebido —
    /// nunca aceito como parâmetro do chamador. Sendo a factory pública (exigência
    /// do fitness test de entidades forenses), um hash passado por fora seria um
    /// convite a gravar uma versão cujo SHA-256 anunciado não corresponde ao
    /// conteúdo real, corrompendo o registro forense sem erro nenhum.
    /// </remarks>
    public static TermoConsentimentoVersao Promover(
        Guid termoConsentimentoId,
        string texto,
        string baseLegal,
        FormaAceite formaAceite,
        DateTimeOffset promovidaEm,
        string promovidaPor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(texto);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseLegal);
        ArgumentException.ThrowIfNullOrWhiteSpace(promovidaPor);

        string formaAceiteToken = FormasAceite.ParaTokenCanonico(formaAceite);
        string hash = CalcularHash(texto, baseLegal, formaAceiteToken);

        return new TermoConsentimentoVersao
        {
            TermoConsentimentoId = termoConsentimentoId,
            Texto = texto,
            BaseLegal = baseLegal,
            FormaAceite = formaAceite,
            Hash = hash,
            PromovidaEm = promovidaEm,
            PromovidaPor = promovidaPor,
        };
    }

    /// <summary>
    /// SHA-256 hex do conteúdo semântico da versão (texto + base legal + forma de
    /// aceite). Cada campo entra prefixado pelo próprio tamanho em bytes UTF-8,
    /// em ordem de bytes big-endian explícita — codificação sem delimitador
    /// reservado e sem dependência da endianness do host, então nenhum conteúdo de
    /// entrada consegue produzir uma colisão de concatenação ambígua nem um hash
    /// diferente conforme a arquitetura de quem promoveu ou verificou depois.
    /// Determinístico: o mesmo conteúdo produz o mesmo hash em qualquer runtime.
    /// </summary>
    private static string CalcularHash(string texto, string baseLegal, string formaAceiteToken)
    {
        using MemoryStream buffer = new();
        EscreverComPrefixoDeTamanho(buffer, texto);
        EscreverComPrefixoDeTamanho(buffer, baseLegal);
        EscreverComPrefixoDeTamanho(buffer, formaAceiteToken);

        byte[] hash = System.Security.Cryptography.SHA256.HashData(buffer.ToArray());
        return Convert.ToHexStringLower(hash);
    }

    private static void EscreverComPrefixoDeTamanho(MemoryStream buffer, string valor)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(valor);

        Span<byte> tamanho = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(tamanho, bytes.Length);
        buffer.Write(tamanho);
        buffer.Write(bytes);
    }
}
