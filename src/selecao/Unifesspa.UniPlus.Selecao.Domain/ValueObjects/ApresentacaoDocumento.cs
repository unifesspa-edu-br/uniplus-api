namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Identidade mínima de um documento apresentado por um candidato, para efeito de
/// correlação com uma <see cref="Entities.DocumentoExigido"/> congelada (Story #554,
/// PR #903). O runtime de coleta em si — upload, análise, homologação de cada apresentação
/// — é <b>fora de escopo</b> desta Story (issue #548 §2: "o resolvedor opera depois,
/// sobre o snapshot já congelado, quando o runtime de coleta tiver apresentações para
/// avaliar"); este VO é só o suficiente para <see cref="Services.ResolvedorExigenciasDocumentais"/>
/// apontar, no resultado, QUAL apresentação satisfez uma exigência.
/// </summary>
/// <param name="Id">Identidade da apresentação — não confundir com <c>DocumentoExigido.Id</c> (a exigência que ela satisfaz é a chave do dicionário que o resolvedor recebe, não um campo deste VO).</param>
/// <param name="ChaveDistincao">
/// Story #921 — o SLOT desta apresentação específica quando a folha qualifica cardinalidade
/// (ex.: <c>"2026-03"</c> para <see cref="Enums.ChaveDistincao.CompetenciaMensal"/>, <c>"2026"</c>
/// para <see cref="Enums.ChaveDistincao.ExercicioAnual"/>, o id da ocorrência para
/// <see cref="Enums.ChaveDistincao.Ocorrencia"/>). <see langword="null"/> quando a folha não
/// qualifica cardinalidade (contagem bruta) — quem produz a tag é o runtime de coleta, fora de
/// escopo; este VO só carrega o valor já resolvido.
/// </param>
public sealed record ApresentacaoDocumento(Guid Id, string? ChaveDistincao = null);
