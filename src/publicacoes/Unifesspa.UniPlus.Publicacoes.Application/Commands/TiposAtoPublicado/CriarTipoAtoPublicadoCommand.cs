namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria uma versão de tipo de ato: código (UPPER_SNAKE), nome, os três atributos de
/// consequência, a janela semiaberta de vigência e a base legal opcional. O ator de
/// auditoria (<c>created_by</c>) é carimbado server-side via <c>IUserContext</c>,
/// não no payload.
/// </summary>
/// <remarks>
/// <c>Codigo</c>/<c>Nome</c> são <c>string?</c>, não <c>string</c> (ADR-0125): sem
/// valor default, para o schema OpenAPI continuar listando-os como obrigatórios;
/// nulos, para o campo ausente escapar do <c>[ApiController]</c> e chegar à
/// validação de domínio, que acumula toda violação no mesmo lote.
/// </remarks>
public sealed record CriarTipoAtoPublicadoCommand(
    string? Codigo,
    string? Nome,
    bool CongelaConfiguracao,
    bool UnicoPorObjeto,
    bool EfeitoIrreversivel,
    DateOnly VigenciaInicio,
    DateOnly? VigenciaFim = null,
    string? BaseLegal = null) : ICommand<Result<Guid>>;
