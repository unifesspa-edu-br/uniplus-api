namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Snapshot de uma <c>FaseCanonica</c> que o Módulo Seleção congela por valor ao
/// registrá-la no cronograma de um processo (snapshot-copy desacoplado, ADR-0061):
/// guarda a identidade de origem e os nove atributos vigentes no momento do
/// congelamento — imune a edições posteriores no cadastro vivo de Configuração.
/// </summary>
/// <remarks>
/// <b>Revisão declarada (story #851).</b> Versões anteriores deste snapshot não
/// congelavam <c>AgrupaEtapas</c> ("é atributo do cadastro vivo, não do
/// snapshot"). Essa decisão foi revista: o gate de avaliação × etapa é
/// bicondicional (uma fase que agrupa etapas existe se e somente se há etapa
/// pontuada) e o bloco congelado da versão publicada precisa ser autossuficiente
/// — ler o cadastro vivo em runtime para decidir esse gate violaria o
/// congelamento de parâmetros por edital (RN08). <c>AgrupaEtapas</c> passa a ser
/// congelado como qualquer outro atributo desta lista.
/// </remarks>
/// <param name="OrigemId">Id (Guid v7) da fase viva de origem, no momento do congelamento.</param>
/// <param name="Codigo">Código canônico congelado (ex.: "HOMOLOGACAO").</param>
/// <param name="DonoTipico">Dono típico congelado (token; ex.: "CEPS").</param>
/// <param name="AgrupaEtapas">Sinalizador de agrupamento de etapas pontuadas congelado.</param>
/// <param name="PermiteComplementacao">Sinalizador de complementação documental congelado.</param>
/// <param name="ProduzResultado">Sinalizador de produção de resultado congelado.</param>
/// <param name="ResultadoDefinitivo">Sinalizador de resultado definitivo (sem recurso) congelado.</param>
/// <param name="ColetaInscricao">Sinalizador de coleta de inscrição congelado.</param>
/// <param name="OrigemData">Origem da data congelada (token; "PROPRIA" ou "DELEGADA").</param>
public sealed record FaseCanonicaSnapshot(
    Guid OrigemId,
    string Codigo,
    string DonoTipico,
    bool AgrupaEtapas,
    bool PermiteComplementacao,
    bool ProduzResultado,
    bool ResultadoDefinitivo,
    bool ColetaInscricao,
    string OrigemData);
