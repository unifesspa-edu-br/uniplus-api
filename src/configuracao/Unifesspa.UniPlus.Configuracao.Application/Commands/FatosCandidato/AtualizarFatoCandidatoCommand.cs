namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Edita o nome, a descrição e o componente de exibição, que valem também no fato de sistema; os eixos têm a alteração
/// própria, <see cref="AlterarDefinicaoDoFatoCandidatoCommand"/>.
/// </summary>
public sealed record AtualizarFatoCandidatoCommand(Guid Id, string Nome, string? Descricao, string? ComponenteExibicao) : ICommand<Result>;
