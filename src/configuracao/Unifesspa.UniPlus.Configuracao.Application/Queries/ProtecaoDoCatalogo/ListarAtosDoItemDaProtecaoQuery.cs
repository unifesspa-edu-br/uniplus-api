namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Todos os atos registrados da proteção de um item do catálogo, em ordem cronológica, para quem mantém o cadastro do
/// item; recusa com o erro de tipo de documento ou de dado do candidato não encontrado quando o item não existe. A permissão de consultar o cadastro é verificada na borda.
/// </summary>
public sealed record ListarAtosDoItemDaProtecaoQuery(CadastroDaProtecaoDoCatalogo Cadastro, Guid Id)
    : IQuery<Result<IReadOnlyList<AtoDaProtecaoDto>>>;
