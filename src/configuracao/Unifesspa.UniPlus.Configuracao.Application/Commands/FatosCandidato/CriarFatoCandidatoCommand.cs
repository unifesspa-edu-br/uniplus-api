namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cadastro de fato declarado pelo administrador (ADR-0136). Os eixos chegam como token canônico
/// UPPER_SNAKE; o vínculo ao campo de formulário é gerado a partir do código.
/// </summary>
public sealed record CriarFatoCandidatoCommand(
    string Codigo,
    string Nome,
    string? Descricao,
    string Dominio,
    string Cardinalidade,
    string? FonteValores,
    string? Formato,
    string PontoResolucao,
    string Escopo,
    string ClassificacaoProtecao,
    string FinalidadeTratamento,
    string HipoteseLegal) : ICommand<Result<Guid>>;
