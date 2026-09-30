namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cadastro de fato derivado por regra pelo administrador (ADR-0136): booleano ou categórico, com o
/// vínculo à regra de derivação gerado a partir do código. As regras padrão vêm depois, por
/// <see cref="DefinirRegrasPadraoCommand"/>.
/// </summary>
public sealed record CriarFatoDerivadoCommand(
    string Codigo,
    string Nome,
    string? Descricao,
    string Dominio,
    string PontoResolucao,
    string Escopo,
    string ClassificacaoProtecao,
    string FinalidadeTratamento,
    string HipoteseLegal) : ICommand<Result<Guid>>;
