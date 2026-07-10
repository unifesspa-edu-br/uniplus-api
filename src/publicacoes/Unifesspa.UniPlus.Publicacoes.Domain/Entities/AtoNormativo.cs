namespace Unifesspa.UniPlus.Publicacoes.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>
/// Ato publicado — o registro central e append-only de que algo foi publicado
/// (ADR-0105). Guarda a essência documental do ato (órgão, série, ano, número,
/// tipo, data de publicação, hash do documento, assinante) e, por valor, os
/// atributos de consequência do catálogo e a versão de configuração que o
/// governou. É a <b>prova</b> do publicado: o passado documental não se muta.
/// </summary>
/// <remarks>
/// <para>
/// Implementa <see cref="IForensicEntity"/> (ADR-0063): append-only, não herda
/// <see cref="Kernel.Domain.Entities.EntityBase"/> e não carrega soft-delete.
/// Só recebe <c>INSERT</c> — <c>UPDATE</c>/<c>DELETE</c> são bloqueados no banco
/// por trigger (a factory não expõe mutadores; o trigger fecha a brecha de um
/// <c>UPDATE</c>/<c>DELETE</c> cru fora do agregado).
/// </para>
/// <para>
/// <b>Número declarado, não gerado.</b> O sistema não opina sobre a política de
/// numeração de cada órgão: <see cref="Numero"/> é opcional e não tem unicidade
/// imposta — dois atos com o mesmo <c>(orgao, serie, ano, numero)</c> são aceitos.
/// A colisão de número gera um <i>aviso</i> consultável (computado na leitura),
/// jamais recusa.
/// </para>
/// <para>
/// <b>Data documental ≠ ordem de vigência.</b> <see cref="DataPublicacao"/> é o
/// que o documento declara e nunca ordena vigência; a ordem pertence à versão de
/// configuração, no relógio do sistema. <see cref="RegistradoEm"/> é o instante
/// forense em que o registro entrou no sistema — distinto da data documental.
/// </para>
/// <para>
/// <b>Cópia por valor.</b> <see cref="CongelaConfiguracao"/> e
/// <see cref="EfeitoIrreversivel"/> são copiados do <c>TipoAtoPublicado</c>
/// vigente na data de publicação, no instante do registro — nunca consultados de
/// volta para decidir fluxo (ADR-0103). Editar o catálogo depois não reescreve
/// ato nenhum.
/// </para>
/// <para>
/// Retificação (relação entre atos) e o vínculo genérico ato↔entidade não
/// pertencem a este agregado ainda — nascem em stories próprias (#800 e #801).
/// </para>
/// </remarks>
public sealed class AtoNormativo : IForensicEntity
{
    private const int OrgaoMaxLength = 200;
    private const int SerieMaxLength = 100;
    private const int NumeroMaxLength = 60;
    private const int TipoCodigoMaxLength = 60;
    private const int AssinanteMaxLength = 200;

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    /// <summary>Órgão publicador que emitiu o ato (ex.: CEPS, CRCA).</summary>
    public string Orgao { get; private init; } = null!;

    /// <summary>Série de numeração do ato (ex.: EDITAL, AVISO) — atravessa certames.</summary>
    public string Serie { get; private init; } = null!;

    /// <summary>Ano da numeração declarada pelo órgão.</summary>
    public int Ano { get; private init; }

    /// <summary>Número que o órgão deu ao ato. Opcional — há atos sem número (ex.: comunicado).</summary>
    public string? Numero { get; private init; }

    /// <summary>Código do tipo de ato no catálogo (<c>TipoAtoPublicado</c>) vigente na data de publicação.</summary>
    public string TipoCodigo { get; private init; } = null!;

    /// <summary>Se o ato produz nova versão congelada da configuração — copiado por valor do catálogo (RN08).</summary>
    public bool CongelaConfiguracao { get; private init; }

    /// <summary>Se a publicação do ato não pode ser desfeita — copiado por valor do catálogo.</summary>
    public bool EfeitoIrreversivel { get; private init; }

    /// <summary>Data que o documento declara. Documental — nunca ordena vigência.</summary>
    public DateOnly DataPublicacao { get; private init; }

    /// <summary>Hash SHA-256 do PDF publicado. É o que prova o publicado.</summary>
    public string DocumentoHash { get; private init; } = null!;

    /// <summary>Nome de quem assina o ato.</summary>
    public string Assinante { get; private init; } = null!;

    /// <summary>Instante forense em que o registro entrou no sistema (relógio do sistema, não o documento).</summary>
    public DateTimeOffset RegistradoEm { get; private init; }

    /// <summary>
    /// Versão de configuração que governou o ato, por valor <c>{id, hash}</c>
    /// (ADR-0075). Nula quando o ato não invoca configuração (ato autônomo).
    /// </summary>
    public ReferenciaVersaoConfiguracao? VersaoInvocada { get; private init; }

    // EF Core materialization
    private AtoNormativo()
    {
    }

    /// <summary>
    /// Registra um ato publicado. Os atributos de consequência
    /// (<paramref name="congelaConfiguracao"/>, <paramref name="efeitoIrreversivel"/>)
    /// já vêm resolvidos por valor do catálogo vigente; a validação de negócio
    /// (existência de versão vigente, formato do payload) é responsabilidade do
    /// handler e do validator. Aqui ficam as invariantes de última linha, que
    /// lançam — o agregado nunca materializa em estado inválido.
    /// </summary>
    public static AtoNormativo Registrar(
        string orgao,
        string serie,
        int ano,
        string? numero,
        string tipoCodigo,
        bool congelaConfiguracao,
        bool efeitoIrreversivel,
        DateOnly dataPublicacao,
        string documentoHash,
        string assinante,
        DateTimeOffset registradoEm,
        ReferenciaVersaoConfiguracao? versaoInvocada)
    {
        string orgaoNorm = ExigirTexto(orgao, OrgaoMaxLength, nameof(orgao));
        string serieNorm = ExigirTexto(serie, SerieMaxLength, nameof(serie));
        string tipoCodigoNorm = ExigirTexto(tipoCodigo, TipoCodigoMaxLength, nameof(tipoCodigo));
        string assinanteNorm = ExigirTexto(assinante, AssinanteMaxLength, nameof(assinante));
        string? numeroNorm = NormalizarOpcional(numero, NumeroMaxLength, nameof(numero));

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ano);

        if (!HashSha256.TemFormatoValido(documentoHash))
        {
            throw new ArgumentException(
                "Hash do documento deve ser um SHA-256 em hexadecimal minúsculo (64 caracteres).",
                nameof(documentoHash));
        }

        return new AtoNormativo
        {
            Orgao = orgaoNorm,
            Serie = serieNorm,
            Ano = ano,
            Numero = numeroNorm,
            TipoCodigo = tipoCodigoNorm,
            CongelaConfiguracao = congelaConfiguracao,
            EfeitoIrreversivel = efeitoIrreversivel,
            DataPublicacao = dataPublicacao,
            DocumentoHash = documentoHash,
            Assinante = assinanteNorm,
            RegistradoEm = registradoEm,
            VersaoInvocada = versaoInvocada,
        };
    }

    private static string ExigirTexto(string valor, int maxLength, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valor, paramName);
        string norm = valor.Trim();
        if (norm.Length > maxLength)
        {
            throw new ArgumentException(
                $"'{paramName}' deve ter no máximo {maxLength} caracteres.",
                paramName);
        }

        return norm;
    }

    private static string? NormalizarOpcional(string? valor, int maxLength, string paramName)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        string norm = valor.Trim();
        if (norm.Length > maxLength)
        {
            throw new ArgumentException(
                $"'{paramName}' deve ter no máximo {maxLength} caracteres.",
                paramName);
        }

        return norm;
    }
}
