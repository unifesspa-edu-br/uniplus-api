namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Cadastra um modelo de formulário, ativo; o código e a finalidade não mudam depois.</summary>
public sealed record CriarModeloFormularioCommand(
    string Codigo,
    string Nome,
    string? Descricao,
    string Finalidade,
    string? TipoProcessoCodigo,
    ConteudoDoModeloInput? Conteudo) : ICommand<Result<Guid>>;

/// <summary>Substitui o descritivo, o tipo de processo e o conteúdo do modelo.</summary>
public sealed record AtualizarModeloFormularioCommand(
    Guid Id,
    string Nome,
    string? Descricao,
    string? TipoProcessoCodigo,
    ConteudoDoModeloInput? Conteudo) : ICommand<Result>;

/// <summary>Reativa o modelo, que volta à escolha dos processos novos.</summary>
public sealed record AtivarModeloFormularioCommand(Guid Id) : ICommand<Result>;

/// <summary>Desativa o modelo; os processos que já o copiaram não mudam.</summary>
public sealed record DesativarModeloFormularioCommand(Guid Id) : ICommand<Result>;

/// <summary>O corpo da edição do modelo: tudo menos o código e a finalidade, que não mudam.</summary>
public sealed record EdicaoDoModeloInput(string Nome, string? Descricao, string? TipoProcessoCodigo, ConteudoDoModeloInput? Conteudo);
