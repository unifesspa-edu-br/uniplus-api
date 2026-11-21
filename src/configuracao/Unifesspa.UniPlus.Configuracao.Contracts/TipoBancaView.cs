namespace Unifesspa.UniPlus.Configuracao.Contracts;

using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// DTO read-only de <c>TipoBanca</c> para consumo cross-módulo via
/// <see cref="ITipoBancaReader"/> (ADR-0056). Expõe o tipo de banca vivo que o
/// Módulo Seleção lê ao configurar as bancas requeridas por fase, antes de congelar
/// por valor (snapshot-copy, ADR-0061).
/// </summary>
/// <param name="Id">Identificador único (Guid v7 — ADR-0032).</param>
/// <param name="Codigo">Tipo do vocabulário, chave natural da banca; o código de contrato sai de <c>Codigo()</c>.</param>
/// <param name="Nome">Rótulo de apresentação.</param>
/// <param name="FaseTipica">Fase usual da banca — rótulo de texto orientativo, não vinculante; ou null.</param>
/// <param name="Descricao">Descrição livre opcional.</param>
/// <param name="Papel">O que as bancas do tipo fazem no processo; as regras da seleção decidem por ele, e não pelo código.</param>
/// <param name="ExigeDuasOuMaisBancas">O tipo só vale com duas ou mais bancas no mesmo lugar.</param>
/// <param name="EncerraRecursoNaPrimeiraInstancia">O recurso do que o tipo avalia termina na primeira instância.</param>
/// <param name="TiposDeDecisaoJulgados">Tipos de decisão que o tipo julga, em ordem alfabética ordinal do código; vazio fora do papel que avalia o mérito.</param>
public sealed record TipoBancaView(
    Guid Id,
    TipoDeBanca Codigo,
    string Nome,
    string? FaseTipica,
    string? Descricao,
    PapelDaBanca Papel,
    bool ExigeDuasOuMaisBancas,
    bool EncerraRecursoNaPrimeiraInstancia,
    IReadOnlyList<TipoMotivoDeDecisao> TiposDeDecisaoJulgados);
