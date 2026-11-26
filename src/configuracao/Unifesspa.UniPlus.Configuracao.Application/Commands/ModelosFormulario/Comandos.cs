namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>Cadastra um modelo de formulário, desativado até a ativação; o código e a declaração não mudam depois.</summary>
public sealed record CriarModeloFormularioCommand(
    string Codigo,
    DeclaracaoDoModeloInput? Declaracao,
    DescritivoDoModeloInput? Descritivo,
    ConteudoDoModeloInput? Conteudo) : ICommand<Result<Guid>>;

/// <summary>Substitui o descritivo e o conteúdo do modelo.</summary>
public sealed record AtualizarModeloFormularioCommand(
    Guid Id,
    DescritivoDoModeloInput? Descritivo,
    ConteudoDoModeloInput? Conteudo) : ICommand<Result>;

/// <summary>Reativa o modelo, que volta à escolha dos processos novos.</summary>
public sealed record AtivarModeloFormularioCommand(Guid Id) : ICommand<Result>;

/// <summary>Desativa o modelo; os processos que já o copiaram não mudam.</summary>
public sealed record DesativarModeloFormularioCommand(Guid Id) : ICommand<Result>;

/// <summary>
/// O que o modelo declara de si na criação, com o formato de <see cref="Application.DTOs.DeclaracaoDoModeloDto"/>: se
/// serve à fase de inscrição e se é obrigatório. A declaração com algum dos dois ausentes é recusada pelo domínio.
/// </summary>
public sealed record DeclaracaoDoModeloInput(bool? ServeAFaseDeInscricao, bool? Obrigatorio)
{
    /// <summary>A declaração completa, ou nula quando falta algum dos dois.</summary>
    public DeclaracaoDoFormulario? ParaDeclaracao() =>
        ServeAFaseDeInscricao is { } serve && Obrigatorio is { } obrigatorio ? new DeclaracaoDoFormulario(serve, obrigatorio) : null;
}

/// <summary>
/// O descritivo do modelo na escrita, com o formato de <see cref="Application.DTOs.DescritivoDoModeloDto"/>, que a
/// leitura devolve: o nome, a descrição e o tipo de processo a que o modelo se destina — nulo quando serve a
/// todos. O nome ausente é recusado pelo domínio, no caminho dele.
/// </summary>
public sealed record DescritivoDoModeloInput(string? Nome, string? Descricao, string? TipoProcessoCodigo)
{
    /// <summary>O caminho do descritivo no corpo, onde a recusa do nome, da descrição e do tipo de processo se aponta.</summary>
    public const string Caminho = ModeloFormulario.CaminhoDoDescritivo;

    /// <summary>O descritivo que não veio: sem nome, sem descrição e sem tipo de processo.</summary>
    public static DescritivoDoModeloInput Ausente { get; } = new(null, null, null);
}

/// <summary>O corpo da edição do modelo: o descritivo e o conteúdo; o código e a declaração não mudam.</summary>
public sealed record EdicaoDoModeloInput(DescritivoDoModeloInput? Descritivo, ConteudoDoModeloInput? Conteudo);
