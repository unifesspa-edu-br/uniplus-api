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
public sealed record ApresentacaoDocumento(Guid Id);
