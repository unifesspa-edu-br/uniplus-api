namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Modalidades;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;

public sealed record ObterModalidadePorIdQuery(Guid Id) : IQuery<ModalidadeDto?>;
