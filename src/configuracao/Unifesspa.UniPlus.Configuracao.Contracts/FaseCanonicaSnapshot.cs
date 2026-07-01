namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Snapshot mínimo de uma <c>FaseCanonica</c> que o Módulo Seleção congela por
/// valor ao registrá-la no cronograma de um processo (snapshot-copy desacoplado,
/// ADR-0061): guarda a identidade de origem, o código canônico e o sinalizador de
/// complementação vigente no momento do congelamento — imune a edições posteriores
/// no cadastro vivo de Configuração. O <c>agrupa_etapas</c> <b>não</b> é congelado:
/// é atributo do cadastro vivo, não do snapshot.
/// </summary>
/// <param name="OrigemId">Id (Guid v7) da fase viva de origem, no momento do congelamento.</param>
/// <param name="Codigo">Código canônico congelado (ex.: "HOMOLOGACAO").</param>
/// <param name="PermiteComplementacao">Sinalizador de complementação documental congelado.</param>
public sealed record FaseCanonicaSnapshot(
    Guid OrigemId,
    string Codigo,
    bool PermiteComplementacao);
