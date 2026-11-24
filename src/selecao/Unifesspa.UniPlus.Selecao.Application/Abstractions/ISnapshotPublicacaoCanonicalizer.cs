namespace Unifesspa.UniPlus.Selecao.Application.Abstractions;

using Domain.Entities;
using Domain.ValueObjects;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Bytes canônicos + metadados de um snapshot de publicação, prontos para
/// <c>ProcessoSeletivo.Publicar</c> congelar (ADR-0100). Não carrega hash —
/// <c>VersaoConfiguracao.Abrir</c> deriva o hash dos bytes internamente
/// (revisão de plano, evita divergência entre bytes e hash persistidos).
/// </summary>
public sealed record SnapshotCanonico(byte[] Bytes, string SchemaVersion, string AlgoritmoHash);

/// <summary>
/// Informação do ato de retificação (ADR-0103) acrescentada ao envelope como um
/// bloco adicional (<c>retificacao</c>) além dos 33 blocos canônicos.
/// <see langword="null"/> na publicação de abertura — o envelope de abertura
/// mantém exatamente os 33 blocos, sem o bloco de retificação.
/// </summary>
public sealed record RetificacaoDoEdital(Guid EditalRetificadoId, string Motivo);

/// <summary>
/// Um valor do conjunto fechado de um fato categórico estático, congelado junto do
/// metadado do fato (ADR-0116) — a descrição que orienta a escolha do candidato para
/// aquele código, e a ordem de apresentação que UNI-REQ-0072 exige preservar. Serve aos
/// DOIS blocos que congelam vocabulário de fato: <c>documentosExigidos.metadadosFatos</c>
/// (evidência do gatilho) e <c>fatosColetados[].valoresSelecionaveis</c> (as opções do
/// seletor do formulário público) — um segundo record quase idêntico produziria duas
/// verdades sobre o mesmo dado.
/// </summary>
/// <param name="Codigo">Código do valor (ex.: "PRETA").</param>
/// <param name="Descricao">Descrição do valor — obrigatória quando o fato pai é DECLARADO.</param>
/// <param name="Ordem">
/// Ordem de apresentação (<see cref="Domain.Entities.FatoValorDominio.Ordem"/> quando o valor
/// vem do catálogo). <c>Ativo</c> continua fora daqui: o congelamento já filtra os valores
/// ativos mais os desativados que o processo cita.
/// </param>
/// <param name="Orientacao">
/// O que o candidato precisa saber sobre a opção, mostrado abaixo dela. Congela só nas opções do
/// seletor do formulário; a evidência do gatilho não a usa.
/// </param>
public sealed record ValorDominioDeclaradoCongelado(string Codigo, string? Descricao, int Ordem, string? Orientacao = null);

/// <summary>
/// Metadado de um fato do candidato (ADR-0111) congelado no envelope de publicação
/// (congelamento dos parâmetros na publicação, UNI-REQ-0019) — domínio, origem, cardinalidade, ponto de resolução, binding e
/// o(s) conjunto(s) de valores, no instante em que o fato foi citado numa condição de
/// gatilho de <see cref="Domain.Entities.DocumentoExigido"/>. Mapeado pelo HANDLER a
/// partir de <c>Unifesspa.UniPlus.Configuracao.Contracts.FatoCandidatoView</c> —
/// nunca reaproveita <see cref="Regras.ValueObjects.DescritorFatoCandidato"/> (VO mínimo
/// do validador de predicado, propósito distinto: validação de forma, não congelamento
/// de evidência).
/// </summary>
/// <remarks>
/// Sem <c>Nome</c> deliberadamente: é rótulo de apresentação, não dado que o congelamento dos parâmetros na publicação (UNI-REQ-0019) exige
/// congelar para o gatilho — o mesmo raciocínio que exclui <c>Ativo</c> de
/// <see cref="ValorDominioDeclaradoCongelado"/>.
/// </remarks>
public sealed record MetadadoFatoCongelado(
    string Codigo,
    TipoDoDado Dominio,
    OrigemFato Origem,
    QuantidadeDeValores Cardinalidade,
    string PontoResolucao,
    string Binding,
    IReadOnlyList<string>? ValoresDominio,
    IReadOnlyList<ValorDominioDeclaradoCongelado>? ValoresDominioDeclarados);

/// <summary>
/// A classificação de um tipo de ato que o cronograma declara, como o catálogo de Publicações a
/// dizia quando a configuração foi publicada, congelada na versão.
/// </summary>
/// <remarks>
/// O catálogo é editável, e a Seleção trabalha sobre a configuração congelada: o que a publicação e a
/// correção de um item do edital perguntam sobre o tipo — se sai uma só vez, se é resultado, se congela
/// a configuração — responde-se por esta cópia, e não pelo catálogo de hoje.
/// </remarks>
/// <param name="Codigo">Código do tipo de ato.</param>
/// <param name="UnicoPorObjeto">Se o tipo sai uma só vez no processo seletivo.</param>
/// <param name="EhResultado">Se o tipo é classificado como resultado.</param>
/// <param name="CongelaConfiguracao">Se publicar o tipo cria uma versão da configuração.</param>
public sealed record TipoDeAtoCongelado(string Codigo, string Nome, bool UnicoPorObjeto, bool EhResultado, bool CongelaConfiguracao)
{
    /// <summary>O que decide se o tipo pode ser item de resultado.</summary>
    public ClassificacaoDoTipoDeAto Classificacao => new(EhResultado, CongelaConfiguracao);
}

/// <summary>
/// O fato derivado do catálogo como a publicação o congela (ADR-0154): o nome, a descrição e o componente
/// de exibição, que dizem como mostrar o valor a quem analisa a inscrição, o domínio e a cardinalidade,
/// que dizem como ler o valor que a inscrição grava, e a proteção — classificação, hipótese legal e
/// finalidade de tratamento —, copiadas do catálogo como o fato coletado copia.
/// </summary>
/// <remarks>
/// O derivado não é coletado, então o processo não tem item que o congele. Sem esta cópia, a consulta
/// do que a inscrição grava e a conferência da proteção das exigências leriam o catálogo vivo.
/// </remarks>
/// <param name="Codigo">Código do fato derivado.</param>
/// <param name="Nome">Nome do fato, o rótulo do item que o mostra.</param>
/// <param name="Descricao">O que o dado significa, para quem analisa a inscrição.</param>
/// <param name="ComponenteExibicao">Como a tela exibe o valor — token canônico (MOEDA, SITUACAO) —, ou <see langword="null"/> quando não declara.</param>
/// <param name="Dominio">Tipo do dado: sim ou não, opção, número, texto, data ou endereço.</param>
/// <param name="Cardinalidade">Quantidade de valores: um valor ou vários valores.</param>
/// <param name="ClassificacaoProtecao">Classificação de proteção — token canônico (PUBLICO, INTERNO, PESSOAL, IDENTIFICADOR, SENSIVEL).</param>
/// <param name="HipoteseLegal">Hipótese legal de tratamento — token canônico (UPPER_SNAKE).</param>
/// <param name="FinalidadeTratamento">Para que o dado é tratado.</param>
public sealed record FatoDerivadoCongelado(
    string Codigo,
    string Nome,
    string Descricao,
    string? ComponenteExibicao,
    TipoDoDado Dominio,
    QuantidadeDeValores Cardinalidade,
    string ClassificacaoProtecao,
    string HipoteseLegal,
    string FinalidadeTratamento);

/// <summary>
/// Entrada <b>única e explícita</b> da canonicalização (ADR-0109 D6).
/// </summary>
/// <remarks>
/// <para>
/// O canonicalizador é uma <b>projeção pura</b>: função total desta entrada
/// para bytes. Não lê repositório, não lê relógio, não é assíncrono — o que
/// não é alcançável a partir do agregado <b>entra por aqui</b>, montado pelo
/// handler (Application), que é quem tem os repositórios.
/// </para>
/// <para>
/// É esta a extensão que os blocos ainda não construídos vão usar: o catálogo
/// de obrigatoriedades legais e o quadro de vagas não pertencem ao agregado
/// <see cref="ProcessoSeletivo"/>. Injetar um repositório no canonicalizador —
/// ou no domínio — inverteria a dependência (ADR-0042). Acrescentar um campo a
/// este record, não.
/// </para>
/// </remarks>
/// <param name="Processo">Agregado com a configuração viva a congelar.</param>
/// <param name="Dados">Dados documentais do ato (número, período, documento).</param>
/// <param name="HashDocumento">SHA-256 do PDF confirmado do ato.</param>
/// <param name="Retificacao">Presente apenas quando o ato é de retificação.</param>
/// <param name="Conformidade">
/// Veredicto da conformidade legal, montado pelo handler a partir do
/// catálogo <c>ObrigatoriedadeLegal</c> — o canonicalizador não injeta repositório
/// (ADR-0042). Só regras aprovadas chegam aqui: se qualquer uma reprovasse, o handler já
/// teria recusado a transição antes de canonicalizar.
/// </param>
/// <param name="DadosDeCatalogo">Dados dos cadastros copiados para a versão publicada.</param>
/// <remarks>
/// Os valores que o candidato pode escolher para cada <see cref="Domain.Entities.FatoColetado"/>
/// do processo, por <c>FatoCodigo</c> — montado pelo handler via <c>IFatoCandidatoReader</c> e
/// pelo atendimento especializado do próprio processo (o canonicalizador não injeta reader nem lê
/// I/O, ADR-0042). Toda entrada é <c>IReadOnlyList</c> quando o fato é de seleção
/// (<c>ESCOLHA_UNICA</c>/<c>ESCOLHA_MULTIPLA</c>) — array possivelmente vazio, nunca ausente — e
/// <see langword="null"/> quando não é; o handler garante essa totalidade ANTES de
/// canonicalizar, e o canonicalizador LANÇA se um fato de seleção não tiver entrada (defesa em
/// profundidade contra um handler que esqueceu de passar o valor, nunca publicando um seletor
/// vazio por omissão).
/// </remarks>
public sealed record EntradaCanonicalizacao(
    ProcessoSeletivo Processo,
    DadosEdital Dados,
    string HashDocumento,
    /// <summary>
    /// Identificador IANA do fuso institucional aplicado nesta canonicalização. Obrigatório de
    /// propósito: é dado forense que entra no envelope congelado, e um default silencioso deixaria
    /// caller novo congelar fuso que ninguém escolheu. Na recodificação de uma versão já publicada,
    /// é o fuso <b>congelado nela</b> que volta aqui — nunca o vigente —, senão a prova byte a byte
    /// mudaria de resultado quando o padrão institucional mudasse.
    /// </summary>
    string FusoHorario,
    RetificacaoDoEdital? Retificacao,
    ResultadoConformidade? Conformidade,
    DadosDeCatalogoDaConfiguracaoPublicada DadosDeCatalogo)
{
    public ConfiguracaoDeclarada? Configuracao { get; init; }

    public ConfiguracaoPublicada? ConfiguracaoPublicada { get; init; }
}

/// <summary>
/// Porta da projeção canônica do envelope de congelamento (ADR-0100, ADR-0109).
/// Projeta a configuração viva do <see cref="ProcessoSeletivo"/> num payload de
/// <b>33 blocos reais</b> e devolve os bytes que
/// <c>VersaoConfiguracao.Abrir</c> persiste como base do hash. Quando a entrada
/// carrega <see cref="EntradaCanonicalizacao.Retificacao"/>, acrescenta o 34º
/// bloco <c>retificacao</c> preservando os 33 anteriores intactos (ADR-0103).
/// </summary>
/// <remarks>
/// A assinatura recebe <b>um único</b> parâmetro por decisão (ADR-0109 D6):
/// acrescentar dado ao envelope não pode significar mudar a assinatura da porta
/// a cada story. O contrato de pureza é travado por ArchTest.
/// </remarks>
public interface ISnapshotPublicacaoCanonicalizer
{
    SnapshotCanonico Canonicalizar(EntradaCanonicalizacao entrada);
}
