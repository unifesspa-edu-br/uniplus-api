namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;

/// <summary>
/// O que a gestão declara de um local de oferta, em criação e em edição, ainda como chega: a referência de cidade vem
/// do cadastro de municípios (código IBGE, nome e UF) e pode estar ausente ou fora do formato. A proveniência e o instante
/// da cópia da cidade não fazem parte da declaração: quem os define é o local.
/// </summary>
/// <param name="Tipo">Tipo do local de oferta; <see cref="TipoLocalOferta.Nenhum"/> e valores fora do enum são recusados.</param>
/// <param name="CampusResponsavelId">Campus responsável, opcional; a existência dele é conferida pelo handler.</param>
/// <param name="CidadeCodigoIbge">Código IBGE de sete dígitos do município, obrigatório.</param>
/// <param name="CidadeNome">Nome do município, obrigatório.</param>
/// <param name="CidadeUf">Sigla da UF do município, obrigatória e coerente com o prefixo do código IBGE.</param>
/// <param name="Endereco">
/// Endereço estruturado já resolvido, opcional. Fica nulo também quando a resolução falhou, para a coerência com a
/// cidade não ser conferida e a causa raiz não ficar mascarada.
/// </param>
/// <param name="CodigoEmec">Código e-MEC do local, opcional.</param>
public sealed record DeclaracaoDoLocalDeOferta(
    TipoLocalOferta Tipo,
    Guid? CampusResponsavelId,
    string? CidadeCodigoIbge,
    string? CidadeNome,
    string? CidadeUf,
    ReferenciaEnderecoGeo? Endereco,
    string? CodigoEmec);
