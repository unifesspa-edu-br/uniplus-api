namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cadastro do agregado sobre grupo repetível pelo administrador (ADR-0138, UNI-REQ-0146): o fato do
/// candidato que resume o que os membros de um grupo responderam no fato de membro informado —
/// "existe membro que…", sobre fato booleano, ou "valores presentes", sobre fato categórico.
/// </summary>
public sealed record CriarFatoAgregadoCommand(
    string Codigo,
    string Nome,
    string? Descricao,
    string FatoDeMembro,
    string PontoResolucao,
    string ClassificacaoProtecao,
    string FinalidadeTratamento,
    string HipoteseLegal) : ICommand<Result<Guid>>;
