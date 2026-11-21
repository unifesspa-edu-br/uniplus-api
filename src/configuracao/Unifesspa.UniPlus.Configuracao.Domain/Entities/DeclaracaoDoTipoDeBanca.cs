namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

/// <summary>
/// O que a gestão declara de um tipo de banca, em criação e em edição, ainda como chega: o papel e os tipos de decisão
/// como códigos soltos. O código do tipo não faz parte da declaração porque nunca muda depois da criação.
/// </summary>
/// <param name="Nome">Nome de apresentação, obrigatório.</param>
/// <param name="FaseTipica">Fase em que o tipo costuma atuar, rótulo orientativo e opcional.</param>
/// <param name="Descricao">Descrição livre, opcional.</param>
/// <param name="Papel">Código do papel do tipo, obrigatório.</param>
/// <param name="ExigeDuasOuMaisBancas">O tipo só vale com duas ou mais bancas no mesmo lugar.</param>
/// <param name="EncerraRecursoNaPrimeiraInstancia">O recurso do que o tipo avalia termina na primeira instância.</param>
/// <param name="TiposDeDecisaoJulgados">Códigos dos tipos de decisão que o tipo julga; só o papel que avalia o mérito os declara.</param>
public sealed record DeclaracaoDoTipoDeBanca(
    string? Nome,
    string? FaseTipica = null,
    string? Descricao = null,
    string? Papel = null,
    bool ExigeDuasOuMaisBancas = false,
    bool EncerraRecursoNaPrimeiraInstancia = false,
    IReadOnlyList<string?>? TiposDeDecisaoJulgados = null);
