namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Domain.Enums;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Projeção de leitura de <c>EtapaProcesso</c>.
/// </summary>
/// <param name="Identificacao">Quem a etapa é no processo: o nome, o caráter, o tipo, a ordem e a fase onde acontece.</param>
/// <param name="Nota">O peso da etapa na nota final e a escala em que a nota é lançada.</param>
/// <param name="Avaliacao">Como a banca chega à nota: a forma de avaliação, a consolidação, os itens e a escala de atendimento.</param>
/// <param name="Janela">A janela própria da etapa.</param>
/// <param name="Publicacoes">O que a etapa publica e decide: os produtos, o parecer, os recursos e os tipos de decisão.</param>
public sealed record EtapaProcessoDto(
    Guid Id,
    IdentificacaoDaEtapaDto Identificacao,
    NotaDaEtapaDto Nota,
    AvaliacaoDaEtapaDto Avaliacao,
    JanelaDaEtapaDto Janela,
    PublicacoesDaEtapaDto Publicacoes,
    IReadOnlyList<BancaDaEtapaDto> Bancas)
{
    /// <summary>Chave local permanente da etapa no processo.</summary>
    public required string Chave { get; init; }
}

/// <summary>Quem a etapa é no processo.</summary>
/// <param name="Nome">O nome editorial da etapa.</param>
/// <param name="Carater">O caráter declarado da etapa, com código e rótulo.</param>
/// <param name="TipoEtapa">
/// Snapshot congelado do tipo — fecha o round-trip GET→PUT: um cliente que
/// reler a etapa precisa de <c>OrigemId</c> para reenviar o mesmo vínculo em
/// <c>EtapaProcessoInput.TipoEtapaOrigemId</c>.
/// </param>
/// <param name="Ordem">A posição da etapa na sequência.</param>
/// <param name="FaseCodigo">
/// O código da fase do cronograma onde a etapa acontece; é por código, e não por id, porque o id da fase
/// não identifica a fase fora do processo.
/// </param>
public sealed record IdentificacaoDaEtapaDto(
    string Nome,
    CodigoComRotulo<CaraterEtapa> Carater,
    TipoEtapaSnapshotDto TipoEtapa,
    int Ordem,
    string? FaseCodigo);

/// <summary>O peso da etapa na nota final e a escala em que a nota é lançada.</summary>
/// <param name="Peso">O peso da etapa na média, ou nulo quando ela não compõe a nota final.</param>
/// <param name="NotaMinima">A nota mínima exigida na etapa, ou nulo.</param>
/// <param name="NotaMaxima">O teto da escala do lançamento; nulo na etapa sem nota e na de nota do ENEM.</param>
/// <param name="PassoDoLancamento">De quanto em quanto a nota pode ser lançada; vale só para a nota digitada.</param>
public sealed record NotaDaEtapaDto(
    decimal? Peso,
    decimal? NotaMinima,
    decimal? NotaMaxima,
    decimal? PassoDoLancamento);

/// <summary>Como a banca chega à nota da etapa.</summary>
/// <param name="FormaDeAvaliacao">
/// Código (<c>OBJETIVA</c>/<c>HUMANA</c>) e rótulo da forma de avaliação; nulo enquanto a etapa não declara.
/// </param>
/// <param name="RegraConsolidacao">
/// A regra do catálogo que combina as notas dos avaliadores; só existe na forma humana.
/// </param>
/// <param name="ItensDeAvaliacao">
/// Os critérios ou blocos em que a nota se divide, com o id que o lançamento e o recurso usam;
/// o cliente devolve o id para manter o mesmo item.
/// </param>
/// <param name="EscalaDeAtendimento">
/// Os níveis entre os quais a banca escolhe em cada item, do que mais vale ao que menos vale; nulo
/// quando a etapa lança nota digitada.
/// </param>
public sealed record AvaliacaoDaEtapaDto(
    CodigoComRotulo<FormaDeAvaliacao>? FormaDeAvaliacao,
    ReferenciaRegraDto? RegraConsolidacao,
    IReadOnlyList<ItemDeAvaliacaoDto> ItensDeAvaliacao,
    IReadOnlyList<NivelDeAtendimentoDto>? EscalaDeAtendimento);

/// <summary>A janela própria da etapa, em UTC.</summary>
/// <param name="Inicio">O início da janela própria; nulo quando a etapa herda a da fase.</param>
/// <param name="Fim">O fim da janela própria; nulo quando a etapa herda a da fase.</param>
public sealed record JanelaDaEtapaDto(DateTimeOffset? Inicio, DateTimeOffset? Fim);

/// <summary>O que a etapa publica e decide: os produtos, o parecer, os recursos e os tipos de decisão.</summary>
/// <param name="Produtos">Os atos que a etapa publica, com o papel de cada publicação.</param>
/// <param name="EmiteParecerIndividual">Se a etapa promete parecer individual por candidato.</param>
/// <param name="Recursos">Os recursos que a etapa admite, cada um ancorado num produto dela.</param>
/// <param name="TiposDeDecisao">Os tipos de decisão que a etapa declara produzir, cada um com código e rótulo, em ordem alfabética ordinal do código e sem repetição.</param>
public sealed record PublicacoesDaEtapaDto(
    IReadOnlyList<ProdutoDaEtapaDto> Produtos,
    bool EmiteParecerIndividual,
    IReadOnlyList<RecursoDaEtapaDto> Recursos,
    IReadOnlyList<CodigoComRotulo<TipoMotivoDeDecisao>> TiposDeDecisao);

/// <summary>Projeção de leitura de <c>ItemDeAvaliacao</c>.</summary>
public sealed record ItemDeAvaliacaoDto(Guid Id, string Rotulo, decimal PontuacaoMaxima, int Ordem, string Chave);

/// <summary>Projeção de leitura de um nível da escala de atendimento.</summary>
public sealed record NivelDeAtendimentoDto(string Rotulo, decimal Fracao, int Ordem, string Chave);

/// <summary>Projeção de leitura de <c>BancaDaEtapa</c>.</summary>
/// <param name="Id">Identidade permanente da banca: o cliente a devolve ao regravar a etapa, para a banca continuar sendo a mesma.</param>
/// <param name="Chave">Chave local permanente da banca no documento.</param>
/// <param name="PosicaoNoTipo">Posição da banca entre as do mesmo tipo na etapa, a partir de 1.</param>
/// <param name="Codigo">O código do tipo de banca, com o rótulo que a tela mostra.</param>
/// <param name="Nome">Nome do tipo de banca congelado na declaração: o cliente o exibe como veio.</param>
/// <param name="Papel">O papel do tipo de banca congelado na declaração: o que a banca faz no processo.</param>
/// <param name="ExigeDuasOuMaisBancas">Se o tipo exige duas ou mais bancas congeladas na declaração.</param>
/// <param name="EncerraRecursoNaPrimeiraInstancia">Se o recurso julgado por essa banca termina na 1ª instância.</param>
/// <param name="TiposDeDecisaoJulgados">As decisões que a banca julga, congeladas na declaração; vazia quando o papel não avalia o mérito.</param>
/// <param name="RegraDeDeliberacao">Nula enquanto o rascunho não a declara; a publicação a exige.</param>
public sealed record BancaDaEtapaDto(
    Guid Id,
    string Chave,
    Guid TipoBancaOrigemId,
    CodigoComRotulo<TipoDeBanca> Codigo,
    int PosicaoNoTipo,
    string Nome,
    CodigoComRotulo<PapelDaBanca> Papel,
    bool ExigeDuasOuMaisBancas,
    bool EncerraRecursoNaPrimeiraInstancia,
    IReadOnlyList<CodigoComRotulo<TipoMotivoDeDecisao>> TiposDeDecisaoJulgados,
    RegraDeDeliberacaoDto? RegraDeDeliberacao, CodigoComRotulo<ConsequenciaDoDescumprimento>? ConsequenciaDaAusencia = null);

/// <summary>Projeção de leitura de <c>RecursoDaEtapa</c>.</summary>
public sealed record RecursoDaEtapaDto(
    Guid Id,
    CodigoComRotulo<EventoInicialDoPrazo> Ancora,
    ReferenciaRegraDto Regra,
    ParametrosRegraPrazoRecursoDto Parametros,
    Guid ProdutoAncoraId,
    string? AtoAncoraCodigo,
    IReadOnlyList<ProcedimentoDeRecursoDto> Procedimentos);

/// <summary>Projeção de leitura de <c>ProdutoDaEtapa</c>.</summary>
/// <param name="Papel">
/// Código (<c>PRELIMINAR</c>/<c>DEFINITIVO</c>) e rótulo do papel da publicação, o mesmo que a fase publica;
/// nulo quando o ato não é resultado.
/// </param>
public sealed record ProdutoDaEtapaDto(Guid Id, string AtoCodigo, CodigoComRotulo<PapelDaPublicacaoPrevista>? Papel);

/// <summary>Cópia por valor do tipo de etapa, projetada para leitura (ADR-0061).</summary>
/// <param name="NotaDeOrigemNoEnem">
/// Se a nota da etapa vem do ENEM, como congelado na etapa: é por ele, e não pelo cadastro
/// atual nem pelo código, que o cliente reconhece a etapa de nota do ENEM.
/// </param>
/// <param name="ExigeCorrecaoIndependente">
/// Se as etapas do tipo são corrigidas por mais de um corretor, cada um independente dos outros, como congelado na etapa.
/// </param>
public sealed record TipoEtapaSnapshotDto(Guid OrigemId, string Codigo, string Nome, bool NotaDeOrigemNoEnem, bool ExigeCorrecaoIndependente);
