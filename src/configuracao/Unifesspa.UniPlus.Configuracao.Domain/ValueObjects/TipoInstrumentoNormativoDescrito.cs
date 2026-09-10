namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// Um tipo de instrumento normativo do vocabulário fechado da Base Legal de Bônus Regional,
/// com o rótulo e a descrição que o cliente usa para montar o campo
/// "Tipo de instrumento" sem manter cópia própria do vocabulário.
/// </summary>
/// <param name="Codigo">
/// Código canônico UPPER_SNAKE — o mesmo valor aceito no campo de Base Legal e
/// trafegado no wire de comando.
/// </param>
/// <param name="Nome">Rótulo humano do tipo de instrumento (ex.: "Instrução Normativa").</param>
/// <param name="Descricao">
/// Definição concisa do instrumento — o que ele é e de onde emana —, suficiente para
/// que o operador escolha o tipo correto sem consultar o Diário Oficial.
/// </param>
public sealed record TipoInstrumentoNormativoDescrito(
    string Codigo,
    string Nome,
    string Descricao);
