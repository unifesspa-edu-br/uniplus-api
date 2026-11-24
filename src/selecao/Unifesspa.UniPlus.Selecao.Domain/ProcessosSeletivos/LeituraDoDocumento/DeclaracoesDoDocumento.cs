namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O que o documento da configuração declara, copiado do JSON sem conferir regra: a identificação e as fases do cronograma, com a
/// posição de cada parte no documento enviado e os códigos de catálogo como foram escritos. Os blocos seguintes entram aqui à medida
/// que a leitura os alcança.
/// </summary>
public sealed record DeclaracoesDoDocumento(IdentificacaoNoDocumento Identificacao, IReadOnlyList<FaseNoDocumento> Fases);

/// <summary>
/// A identificação do processo como o documento a traz. O nome e o identificador legível vão como foram escritos, cada código de
/// catálogo também, e ausente é nulo.
/// </summary>
public sealed record IdentificacaoNoDocumento(
    string? Nome,
    string? TipoProcessoCodigo,
    string? UnidadeAdministradoraCodigo,
    OrigemCandidatos? OrigemCandidatos,
    string? LocalidadeCodigoIbge,
    string? IdentificadorLegivel,
    ReferenciaDeRegraNoDocumento? AlgoritmoContagemPrazo);

/// <summary>A regra do catálogo que o documento cita pelo código e pela versão; um dos dois pode faltar no rascunho.</summary>
public sealed record ReferenciaDeRegraNoDocumento(string? Codigo, string? Versao);
