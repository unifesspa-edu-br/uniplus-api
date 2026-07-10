namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Atualiza uma versão de tipo de ato. O <c>Codigo</c> é editável: o consumo é por
/// cópia de valor no ato publicado, então renomear o código vivo não altera nenhum
/// ato já publicado.
/// </summary>
public sealed record AtualizarTipoAtoPublicadoCommand(
    Guid Id,
    string Codigo,
    string Nome,
    bool CongelaConfiguracao,
    bool UnicoPorObjeto,
    bool EfeitoIrreversivel,
    DateOnly VigenciaInicio,
    DateOnly? VigenciaFim = null,
    string? BaseLegal = null) : ICommand<Result>;
