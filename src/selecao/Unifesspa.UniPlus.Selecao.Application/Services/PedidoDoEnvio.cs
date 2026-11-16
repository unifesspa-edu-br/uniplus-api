namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Text.Json;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;
using DTOs.Comprovantes;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O que o candidato pediu ao enviar a inscrição, ligado à versão do formulário em que o rascunho está: o corpo lido, as respostas
/// em que o envio se confere e o que a conta do token diz do nome.
/// </summary>
public sealed record PedidoDoEnvio(
    Inscricao Inscricao,
    InscricaoRequest Request,
    CorpoDoEnvio Corpo,
    ContextoDaVersaoFixada Contexto,
    RespostasDoEnvio Respostas,
    RespostasAdmitidas AdmitidasDoCandidato);
