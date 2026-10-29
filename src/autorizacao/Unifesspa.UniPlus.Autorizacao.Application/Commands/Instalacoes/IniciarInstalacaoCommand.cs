namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Passo 1 da instalação: quem tem o papel de instalação designa os dois gestores e o documento. Cancela a
/// pendência anterior dele e devolve a URL de PUT assinada para o PDF.
/// </summary>
public sealed record IniciarInstalacaoCommand(
    IReadOnlyList<IdentidadeInformada> Gestores,
    string? NumeroDoDocumento,
    long TamanhoDoPdf) : ICommand<Result<InstalacaoIniciadaDto>>;
