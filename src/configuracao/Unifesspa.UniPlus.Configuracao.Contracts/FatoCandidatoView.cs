namespace Unifesspa.UniPlus.Configuracao.Contracts;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// DTO read-only de <c>FatoCandidato</c> para consumo cross-módulo via
/// <see cref="IFatoCandidatoReader"/> (ADR-0056, ADR-0111, refinada pela ADR-0116).
/// Expõe o metadado do fato — domínio, origem, cardinalidade, ponto de resolução,
/// binding e o(s) conjunto(s) de valores — que o Módulo Seleção lê ao validar um
/// predicado de gatilho ou de desempate. Não carrega valor de candidato algum.
/// </summary>
/// <param name="Id">Identificador único (Guid v7).</param>
/// <param name="Codigo">Código do fato, chave natural (ex.: "COR_RACA", "MODALIDADE").</param>
/// <param name="Nome">Rótulo legível do fato.</param>
/// <param name="Descricao">O que o dado significa, para quem analisa a inscrição; obrigatória no catálogo e congelada pela Seleção no fato coletado e no derivado.</param>
/// <param name="Dominio">Tipo do dado: sim ou não, opção, número, texto, data ou endereço.</param>
/// <param name="Origem">Origem do dado: calculado, declarado ou de integração.</param>
/// <param name="Cardinalidade">Quantidade de valores: um valor ou vários valores.</param>
/// <param name="ValoresDominio">
/// Os códigos dos valores de um categórico de fonte global ou de tipos de deficiência, na ordem de
/// <paramref name="ValoresDominioDeclarados"/>, ou <see langword="null"/> quando o fato não tem
/// valores no catálogo.
/// </param>
/// <param name="PontoResolucao">Código canônico da fase (<c>FaseCanonicaCatalogo</c>) em que o valor do fato fica conhecido.</param>
/// <param name="Binding">Referência de onde/como o valor é produzido, no formato <c>"{PREFIXO}:{REFERENCIA}"</c>.</param>
/// <param name="ValoresDominioDeclarados">
/// Descrição por valor de um categórico estático (ADR-0116), ou <see langword="null"/>
/// quando o fato não tem valores no catálogo — booleano/numérico, ou categórico de
/// escopo-processo. No dado de fonte TIPOS_DE_DEFICIENCIA, os valores são os tipos do cadastro de
/// tipos de deficiência, com o removido inativo. Ordenada por <c>Ordem</c> e depois por <c>Codigo</c>.
/// </param>
/// <param name="FonteValores">
/// De onde vêm os valores de um dado de opção (ADR-0136), ou <see langword="null"/> em todo dado que não é de opção. O consumidor decide por ela,
/// nunca pelo código do fato.
/// </param>
/// <param name="Ativo">
/// Fato inativo não aceita vínculo novo; o processo que já o usa continua com ele (ADR-0136).
/// </param>
/// <param name="Formato">
/// Formato da resposta do fato de texto — token canônico (LIVRE, CPF, EMAIL, TELEFONE, CEP,
/// NOME_PESSOA) —, ou <see langword="null"/> quando o fato não é de texto.
/// </param>
/// <param name="Escopo">
/// De quem é o fato — token canônico (CANDIDATO, MEMBRO_GRUPO). O de membro de grupo só é
/// respondido dentro do grupo.
/// </param>
/// <param name="ClassificacaoProtecao">
/// Classificação de proteção de dados do fato — token canônico (PUBLICO, INTERNO, PESSOAL,
/// IDENTIFICADOR, SENSIVEL), na escala da ADR-0081 (ADR-0136). A Seleção congela este valor no
/// fato coletado ao aplicar o modelo ou editar o formulário (ADR-0136).
/// </param>
/// <param name="HipoteseLegal">
/// Hipótese legal de tratamento do fato — token canônico (UPPER_SNAKE). A Seleção a congela no
/// fato coletado e no derivado, como a classificação (ADR-0154).
/// </param>
/// <param name="FinalidadeTratamento">Para que o dado é tratado; congelada junto da hipótese legal (ADR-0154).</param>
/// <param name="ComponenteExibicao">
/// Como a tela exibe o valor quando o modo não se deduz do tipo, do formato nem da classificação — token canônico
/// (MOEDA, SITUACAO) —, ou <see langword="null"/> quando o fato não declara. A Seleção o congela com a descrição.
/// </param>
/// <param name="ProtecaoAprovada">
/// Se a proteção de dados aprovou a classificação, a finalidade e a hipótese legal do fato (ADR-0153). O fato sem a
/// aprovação não aceita vínculo novo no processo e bloqueia a publicação e a retificação do processo que o usa.
/// </param>
public sealed record FatoCandidatoView(
    Guid Id,
    string Codigo,
    string Nome,
    string Descricao,
    CodigoComRotulo<TipoDoDado> Dominio,
    CodigoComRotulo<OrigemFato> Origem,
    CodigoComRotulo<QuantidadeDeValores> Cardinalidade,
    IReadOnlyList<string>? ValoresDominio,
    string PontoResolucao,
    string Binding,
    IReadOnlyList<FatoValorDominioViewItem>? ValoresDominioDeclarados,
    CodigoComRotulo<FonteValoresFato>? FonteValores,
    bool Ativo,
    string ClassificacaoProtecao,
    string HipoteseLegal,
    string FinalidadeTratamento,
    bool ProtecaoAprovada,
    string? Formato = null,
    string? Escopo = null,
    string? ComponenteExibicao = null)
{
    public string Escopo { get; init; } = Escopo ?? EscopoFato.Candidato.Codigo();
    [JsonIgnore] public EscopoFato EscopoValor => CatalogoDe<EscopoFato>.Obter(Escopo);
    [JsonIgnore] public TipoDoDado DominioValor => CatalogoDe<TipoDoDado>.Obter(Dominio.Codigo);
    [JsonIgnore] public OrigemFato OrigemValor => CatalogoDe<OrigemFato>.Obter(Origem.Codigo);
    [JsonIgnore] public QuantidadeDeValores CardinalidadeValor => CatalogoDe<QuantidadeDeValores>.Obter(Cardinalidade.Codigo);
    [JsonIgnore] public FonteValoresFato? FonteValoresValor => FonteValores is null ? null : CatalogoDe<FonteValoresFato>.Obter(FonteValores.Codigo);
}

/// <summary>
/// Um valor do conjunto fechado de um <see cref="FatoCandidatoView"/> categórico
/// estático (ADR-0116) — embutido, não um reader próprio, para evitar um segundo
/// round-trip cross-módulo.
/// </summary>
/// <param name="Codigo">Código do valor (ex.: "PRETA").</param>
/// <param name="Descricao">O nome da opção que o candidato escolhe — obrigatória quando o fato pai é DECLARADO.</param>
/// <param name="Ordem">Ordem de exibição sugerida.</param>
/// <param name="Ativo">Indica se o valor está ativo para novas seleções.</param>
/// <param name="Orientacao">O que o candidato precisa saber sobre a opção, mostrado abaixo dela.</param>
public sealed record FatoValorDominioViewItem(
    string Codigo,
    string? Descricao,
    int Ordem,
    bool Ativo,
    string? Orientacao = null);
