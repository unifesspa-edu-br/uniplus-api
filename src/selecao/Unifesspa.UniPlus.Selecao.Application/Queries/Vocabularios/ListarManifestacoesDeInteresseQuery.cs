namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using System.Collections.Generic;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Manifestacao;

/// <summary>
/// Lê as formas de o edital prever a manifestação de interesse depois da 1ª chamada, com o código, o rótulo e o que cada uma significa.
/// </summary>
public sealed record ListarManifestacoesDeInteresseQuery : IQuery<IReadOnlyList<CodigoComDescricao<ManifestacaoDeInteresse>>>;
