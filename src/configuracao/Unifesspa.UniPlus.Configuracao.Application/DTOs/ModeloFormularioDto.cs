namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

/// <summary>
/// O modelo de formulário para manutenção. O conteúdo volta no mesmo formato da escrita, para que a
/// edição leia, altere e regrave sem conversão.
/// </summary>
public sealed record ModeloFormularioDto(
    Guid Id,
    string Codigo,
    string Nome,
    string? Descricao,
    string Finalidade,
    string? TipoProcessoCodigo,
    bool Ativo,
    ConteudoDoModeloInput Conteudo);
