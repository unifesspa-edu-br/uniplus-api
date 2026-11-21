namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Domain.Enums;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;

/// <summary>Projeção de leitura de <c>ProdutoDaFase</c> — o que a fase publica e o papel de cada publicação.</summary>
/// <param name="Papel">Token canônico UPPER_SNAKE (<c>PRELIMINAR</c>/<c>DEFINITIVO</c>), ou nulo quando o ato não é resultado.</param>
public sealed record ProdutoDaFaseDto(Guid Id, string AtoCodigo, string? Papel);

/// <summary>Projeção de leitura de <c>CategoriaJulgada</c> — uma categoria de documento do recorte de competência da banca.</summary>
public sealed record CategoriaJulgadaDto(Guid Id, Guid CategoriaDocumentoOrigemId, string Codigo);

/// <summary>Projeção de leitura de <c>BancaRequerida</c> (Story #851).</summary>
/// <param name="Nome">Nome do tipo de banca congelado na declaração: o cliente o exibe como veio.</param>
/// <param name="RegraDeDeliberacao">Nula enquanto o rascunho não a declara; a publicação a exige.</param>
/// <param name="RecorteDeCompetencia">
/// As categorias de documento que a banca julga — o que a distingue de outra banca do
/// mesmo tipo na mesma fase. Vazio quando o tipo já identifica a banca sozinho.
/// </param>
public sealed record BancaRequeridaDto(
    Guid Id,
    Guid TipoBancaOrigemId,
    string Codigo,
    string Nome,
    RegraDeDeliberacaoDto? RegraDeDeliberacao,
    IReadOnlyList<CategoriaJulgadaDto> RecorteDeCompetencia);

/// <summary>Projeção de leitura de <c>ParametrosRegraPrazoRecurso</c> (Story #851).</summary>
/// <param name="PrazoValor">Valor do prazo de interposição do recurso na 1ª instância.</param>
/// <param name="PrazoUnidade">Unidade do prazo de interposição.</param>
/// <param name="AdmiteInstanciaSuperior">Se o recurso admite instância superior.</param>
/// <param name="PrazoSegundaInstanciaValor">Valor do prazo na 2ª instância; nulo quando o recurso não admite instância superior.</param>
/// <param name="PrazoSegundaInstanciaUnidade">Unidade do prazo na 2ª instância; nula quando o recurso não admite instância superior.</param>
/// <param name="EfeitoSuspensivo">Efeito suspensivo do recurso na 1ª instância.</param>
/// <param name="GuardaDaVaga">Se a vaga em disputa fica guardada enquanto o recurso corre.</param>
public sealed record ParametrosRegraPrazoRecursoDto(
    decimal PrazoValor,
    CodigoComRotulo<UnidadePrazo> PrazoUnidade,
    bool AdmiteInstanciaSuperior,
    decimal? PrazoSegundaInstanciaValor,
    CodigoComRotulo<UnidadePrazo>? PrazoSegundaInstanciaUnidade,
    CodigoComRotulo<EfeitoSuspensivo> EfeitoSuspensivo,
    CodigoComRotulo<GuardaDaVaga> GuardaDaVaga);

/// <summary>Projeção de leitura de <c>RegraRecursoFase</c> (0..1, Story #851) — presença = a fase admite recurso.</summary>
/// <param name="ProdutoAncoraId">
/// O <c>ProdutoDaFase</c> desta mesma fase de cujo instante de publicação o prazo de
/// interposição conta. Derivado do produto preliminar, não escrito por quem configura.
/// </param>
public sealed record RegraRecursoFaseDto(
    Guid Id,
    Guid ProdutoAncoraId,
    ReferenciaRegraDto Regra,
    ParametrosRegraPrazoRecursoDto Parametros,
    IReadOnlyList<ProcedimentoDeRecursoDto> Procedimentos);

/// <summary>Projeção de leitura da unidade que responde por uma fase — a cópia congelada ao definir o cronograma.</summary>
public sealed record UnidadeResponsavelDto(Guid OrigemId, string Sigla, string Nome);

/// <summary>Projeção de leitura de <c>FaseCronograma</c> (Story #851) — o eixo temporal do certame.</summary>
/// <param name="Codigo">A identidade estável da fase no processo: a etapa aponta para a fase por este código, porque o id não sobrevive à reconciliação.</param>
/// <param name="FaseCanonica">Os traços que a fase herda da fase canônica.</param>
/// <param name="Cronograma">A posição da fase, a janela e a unidade que responde por ela.</param>
/// <param name="Publicacoes">O que a fase publica e decide, e o recurso que admite.</param>
public sealed record FaseCronogramaDto(
    Guid Id,
    string Codigo,
    FaseCanonicaCongeladaDto FaseCanonica,
    CronogramaDaFaseDto Cronograma,
    PublicacoesDaFaseDto Publicacoes,
    IReadOnlyList<BancaRequeridaDto> BancasRequeridas);

/// <summary>Os traços que a fase herda da fase canônica, congelados na definição do cronograma (ADR-0061).</summary>
/// <param name="OrigemId">O id da fase canônica de que a fase foi copiada.</param>
/// <param name="OrigemData">
/// Token canônico UPPER_SNAKE (<c>NO_EDITAL</c>/<c>NA_EXECUCAO</c>), o mesmo que o catálogo de fases
/// canônicas publica — a origem deste campo é aquele cadastro, não a escrita daqui.
/// </param>
/// <param name="AgrupaEtapas">Se a fase agrupa etapas de avaliação.</param>
/// <param name="PermiteComplementacao">Se a fase admite complementação de documentos.</param>
/// <param name="ColetaInscricao">Se a fase coleta a inscrição.</param>
/// <param name="ColetaSolicitacaoIsencao">Se a fase coleta a solicitação de isenção da taxa.</param>
public sealed record FaseCanonicaCongeladaDto(
    Guid OrigemId,
    string OrigemData,
    bool AgrupaEtapas,
    bool PermiteComplementacao,
    bool ColetaInscricao,
    bool ColetaSolicitacaoIsencao);

/// <summary>O que o edital decide sobre a fase: a posição, o período e quem responde.</summary>
/// <param name="Ordem">A posição da fase na sequência do cronograma.</param>
/// <param name="NoMesmoMomentoDaAnterior">Se a fase acontece no mesmo momento da fase que a antecede na lista.</param>
/// <param name="Inicio">O início da janela da fase, ou nulo quando o edital não o fixa.</param>
/// <param name="Fim">O fim da janela da fase, ou nulo quando o edital não o fixa.</param>
/// <param name="UnidadeResponsavel">A unidade que responde pela fase, ou nulo enquanto o rascunho ainda não a definiu.</param>
public sealed record CronogramaDaFaseDto(
    int Ordem,
    bool NoMesmoMomentoDaAnterior,
    DateTimeOffset? Inicio,
    DateTimeOffset? Fim,
    UnidadeResponsavelDto? UnidadeResponsavel);

/// <summary>O que a fase publica e decide: os produtos, o resultado, o parecer, o recurso, os tipos de decisão e a regra de homologação das inscrições.</summary>
/// <param name="Produtos">Os atos que a fase publica, com o papel de cada publicação.</param>
/// <param name="ProduzResultado">
/// Derivado dos produtos declarados — a fase produz resultado quando publica ao menos um
/// produto com papel. Projetado por conveniência de quem lê; não é campo de escrita.
/// </param>
/// <param name="EmiteParecerIndividual">Se a fase emite parecer individual.</param>
/// <param name="RegraRecurso">A regra de recurso da fase, ou nulo quando a fase não admite recurso.</param>
/// <param name="TiposDeDecisao">Os tipos de decisão que a fase declara produzir, em ordem alfabética ordinal e sem repetição.</param>
/// <param name="RegraDeHomologacao">A regra com que a fase homologa as inscrições, ou nulo quando a fase não homologa.</param>
public sealed record PublicacoesDaFaseDto(
    IReadOnlyList<ProdutoDaFaseDto> Produtos,
    bool ProduzResultado,
    bool EmiteParecerIndividual,
    RegraRecursoFaseDto? RegraRecurso,
    IReadOnlyList<string> TiposDeDecisao,
    RegraDeHomologacaoDto? RegraDeHomologacao);
