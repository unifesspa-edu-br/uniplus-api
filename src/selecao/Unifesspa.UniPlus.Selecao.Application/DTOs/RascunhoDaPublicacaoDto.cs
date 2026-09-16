namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.Text.Json;

/// <summary>
/// O rascunho da publicação como o cliente o recebe de volta — o mesmo documento que enviou,
/// byte a byte, mais o que ele precisa para decidir o que fazer com ele.
/// </summary>
/// <param name="Versao">
/// O formato em que foi gravado. Não batendo com o que o cliente corrente entende, ele descarta
/// o rascunho em vez de hidratar um bloco meio traduzido.
/// </param>
/// <param name="Conteudo">O documento opaco, round-tripável direto de volta pelo mesmo PUT.</param>
/// <param name="SalvoEm">O instante que a tela mostra como "rascunho salvo às …".</param>
public sealed record RascunhoDaPublicacaoDto(int Versao, JsonElement Conteudo, DateTimeOffset SalvoEm);
