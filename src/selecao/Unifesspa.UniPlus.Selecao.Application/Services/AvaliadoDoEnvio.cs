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
/// O que o envio avaliou: o endereço conferido no Geo, os derivados do sistema, a avaliação da definição inteira da versão fixada e
/// os municípios dos campos que a avaliação deu como aplicáveis.
/// </summary>
public sealed record AvaliadoDoEnvio(
    ConferenciaDosEnderecos DoEndereco,
    DerivadosCalculados Derivados,
    EnvioDasRespostas Envio,
    ArquivosDaInscricao Arquivos,
    ResultadoDaAvaliacaoDaInscricao Avaliacao,
    ResultadoDaConferenciaDosMunicipios DosMunicipios);
