namespace Unifesspa.UniPlus.Configuracao.Contracts;

using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// DTO read-only de <c>TipoDocumento</c> para consumo cross-módulo via
/// <see cref="ITipoDocumentoReader"/> (ADR-0056). Expõe o tipo vivo
/// (código + nome + categoria + proteção) que o Módulo Seleção lê ao montar a relação de
/// exigências documentais de um edital, antes de congelar por valor a identidade
/// na exigência (snapshot-copy, ADR-0061).
/// </summary>
/// <param name="Id">Identificador único (Guid v7 — ADR-0032).</param>
/// <param name="Codigo">Código classificatório, chave natural do tipo (ex.: "LAUDO_MEDICO").</param>
/// <param name="Nome">Rótulo legível do tipo.</param>
/// <param name="Categoria">Categoria classificatória (token canônico UPPER_SNAKE; ex.: "SAUDE").</param>
/// <param name="ClassificacaoProtecao">Proteção que o documento exige, na escala única; a exigência a congela.</param>
/// <param name="FinalidadeTratamento">Finalidade do tratamento do dado que o documento contém.</param>
/// <param name="HipoteseLegal">Hipótese legal do tratamento (token canônico UPPER_SNAKE; ex.: "CUMPRIMENTO_OBRIGACAO_LEGAL").</param>
public sealed record TipoDocumentoView(
    Guid Id,
    string Codigo,
    string Nome,
    string Categoria,
    ClassificacaoDeProtecao ClassificacaoProtecao,
    string FinalidadeTratamento,
    string HipoteseLegal);
