namespace Unifesspa.UniPlus.Configuracao.Contracts;

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
/// <param name="Descricao">Descrição opcional (cosmética).</param>
/// <param name="Dominio">Tipo de dado — token canônico UPPER_SNAKE (CATEGORICO, BOOLEANO, NUMERICO, TEXTO, DATA, ENDERECO).</param>
/// <param name="Origem">Origem do dado — token canônico (DERIVADO, DECLARADO, INTEGRACAO).</param>
/// <param name="Cardinalidade">Cardinalidade — token canônico (ESCALAR, MULTIVALORADO).</param>
/// <param name="ValoresDominio">
/// Os códigos dos valores de um categórico de fonte global, na ordem de
/// <paramref name="ValoresDominioDeclarados"/>, ou <see langword="null"/> quando o fato não tem
/// valores no catálogo.
/// </param>
/// <param name="PontoResolucao">Código canônico da fase (<c>FaseCanonicaCatalogo</c>) em que o valor do fato fica conhecido.</param>
/// <param name="Binding">Referência de onde/como o valor é produzido, no formato <c>"{PREFIXO}:{REFERENCIA}"</c>.</param>
/// <param name="ValoresDominioDeclarados">
/// Descrição por valor de um categórico estático (ADR-0116), ou <see langword="null"/>
/// quando o fato não tem <c>FatoValorDominio</c> filhos — booleano/numérico, ou
/// categórico de escopo-processo. Ordenada por <c>Ordem</c> e depois por <c>Codigo</c>.
/// </param>
/// <param name="FonteValores">
/// De onde vêm os valores de um categórico (ADR-0136) — token canônico (GLOBAL, PROCESSO,
/// MODALIDADE, MUNICIPIOS_BONUS, GEO_UF, GEO_MUNICIPIO), ou <see langword="null"/> em todo domínio que não é categórico. O consumidor decide por ela,
/// nunca pelo código do fato.
/// </param>
/// <param name="Ativo">
/// Fato inativo não aceita vínculo novo; o processo que já o usa continua com ele (ADR-0136).
/// </param>
/// <param name="Formato">
/// Formato da resposta do fato de texto — token canônico (LIVRE, CPF, EMAIL, TELEFONE, CEP,
/// NOME_PESSOA) —, ou <see langword="null"/> quando o fato não é de texto.
/// </param>
public sealed record FatoCandidatoView(
    Guid Id,
    string Codigo,
    string Nome,
    string? Descricao,
    string Dominio,
    string Origem,
    string Cardinalidade,
    IReadOnlyList<string>? ValoresDominio,
    string PontoResolucao,
    string Binding,
    IReadOnlyList<FatoValorDominioViewItem>? ValoresDominioDeclarados,
    string? FonteValores,
    bool Ativo,
    string? Formato = null);

/// <summary>
/// Um valor do conjunto fechado de um <see cref="FatoCandidatoView"/> categórico
/// estático (ADR-0116) — embutido, não um reader próprio, para evitar um segundo
/// round-trip cross-módulo.
/// </summary>
/// <param name="Codigo">Código do valor (ex.: "PRETA").</param>
/// <param name="Descricao">Descrição que orienta a escolha do candidato — obrigatória quando o fato pai é DECLARADO.</param>
/// <param name="Ordem">Ordem de exibição sugerida.</param>
/// <param name="Ativo">Indica se o valor está ativo para novas seleções.</param>
public sealed record FatoValorDominioViewItem(
    string Codigo,
    string? Descricao,
    int Ordem,
    bool Ativo);
