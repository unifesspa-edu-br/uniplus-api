namespace Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// O ato que um domínio acaba de publicar, como Publicações o registra: tudo o que o registro central guarda e confere,
/// já decidido por quem publica. Só trafega em chamada de processo, na transação do domínio que publica: o assinante,
/// único dado pessoal do ato, nunca viaja em mensagem.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="AtoId"/> é decidido por quem publica, e é o que lhe permite referenciar o ato por valor, sem chave
/// estrangeira, antes mesmo de ele existir fisicamente (ADR-0061). <see cref="DataDoAto"/> é a data que o documento
/// declara; <see cref="PublicadoEm"/> é o instante da divulgação, lido uma só vez por quem publica. Publicações guarda os
/// dois e não define nenhum.
/// </para>
/// <para>
/// <see cref="TipoConferido"/> são os atributos do tipo como o catálogo os declarava quando o domínio os conferiu: a
/// decisão tomada na publicação não é refeita contra o cadastro, que é editável (ADR-0061).
/// </para>
/// </remarks>
public sealed record AtoNormativoPublicado
{
    /// <summary>O identificador do ato, decidido por quem publica.</summary>
    public required Guid AtoId { get; init; }

    /// <summary>A sigla do órgão que numera o ato.</summary>
    public required string Orgao { get; init; }

    /// <summary>A série de numeração do órgão.</summary>
    public required string Serie { get; init; }

    /// <summary>O ano da numeração oficial.</summary>
    public required int Ano { get; init; }

    /// <summary>O número que o documento declara; nulo quando o tipo não o tem.</summary>
    public required string? Numero { get; init; }

    /// <summary>O código do tipo do ato no catálogo de Publicações.</summary>
    public required string TipoCodigo { get; init; }

    /// <summary>Os atributos do tipo como o domínio os conferiu.</summary>
    public required AtributosDoTipoAto TipoConferido { get; init; }

    /// <summary>A data que o documento declara; não conta para prazo, vigência nem escolha do tipo.</summary>
    public required DateOnly DataDoAto { get; init; }

    /// <summary>O instante da divulgação, lido uma só vez por quem publica.</summary>
    public required DateTimeOffset PublicadoEm { get; init; }

    /// <summary>O hash SHA-256 (hexadecimal minúsculo) do documento principal.</summary>
    public required string DocumentoHash { get; init; }

    /// <summary>Quem assina o ato.</summary>
    public required string Assinante { get; init; }

    /// <summary>A versão de configuração que o ato invoca; nula quando não invoca nenhuma.</summary>
    public VersaoInvocadaDoAto? VersaoInvocada { get; init; }

    /// <summary>O ato que este corrige; nulo quando o ato não retifica nenhum.</summary>
    public CorrecaoDoAto? Correcao { get; init; }

    /// <summary>As entidades de outros domínios de que o ato trata.</summary>
    public required IReadOnlyList<VinculoDoAtoPublicado> Vinculos { get; init; }

    /// <summary>Os arquivos que acompanham o documento principal, na ordem em que o ato os apresenta.</summary>
    public IReadOnlyList<AnexoDoAtoPublicado> Anexos { get; init; } = [];
}

/// <summary>A versão de configuração que o ato invoca: o identificador e o hash dela, recebidos por valor.</summary>
public sealed record VersaoInvocadaDoAto(Guid Id, string Hash);

/// <summary>O ato que o novo ato retifica e o motivo da retificação (ADR-0103).</summary>
public sealed record CorrecaoDoAto(Guid AtoRetificadoId, string Motivo);

/// <summary>
/// Arquivo anexo ao documento principal do ato: o nome com que foi publicado, o tipo de mídia e o hash SHA-256
/// (hexadecimal minúsculo) do conteúdo. Publicações guarda o trio e não conhece o arquivo nem o domínio que o produziu
/// (ADR-0105).
/// </summary>
public sealed record AnexoDoAtoPublicado(string Nome, string TipoMidia, string Hash);

/// <summary>
/// Entidade de outro domínio a que o ato se liga (ADR-0105). O par é opaco para Publicações: sem lista de valores
/// permitidos e sem chave estrangeira.
/// </summary>
public sealed record VinculoDoAtoPublicado(string EntidadeTipo, Guid EntidadeId);

/// <summary>
/// O que o catálogo dizia sobre o tipo do ato no momento em que o domínio publicou, copiado por valor e não relido
/// depois (ADR-0061).
/// </summary>
/// <remarks>
/// O catálogo é editável: reler o tipo no registro faria um edital publicado como congelante ser registrado com um tipo
/// que já não congela. O <see cref="Nome"/> segue a mesma regra, porque compõe o título público do ato: renomear o tipo
/// no cadastro não reescreve o título de atos já registrados.
/// </remarks>
public sealed record AtributosDoTipoAto(
    bool CongelaConfiguracao,
    bool UnicoPorObjeto,
    bool EfeitoIrreversivel,
    string? Nome);
